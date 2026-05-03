// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NPS.NIP.Crypto;
using NPS.NIP.Frames;
using NSec.Cryptography;

namespace NPS.Daemon.Npsd.SubNids;

/// <summary>
/// Mints and tracks sub-NIDs for local agents on this host.
/// </summary>
/// <remarks>
/// The host's root keypair (<see cref="RootIdentity"/>) acts as the local
/// "mini-CA". Issued IdentFrames carry <c>issued_by = host root NID</c> and
/// are signed with the root key. They are intended for **host-local trust**
/// only — anything that crosses a host boundary should be re-issued by an
/// upstream CA with broader trust scope (typically <c>nip-ca-server</c> or,
/// at NPS Cloud GA, <c>nps-cloud-ca</c>).
/// </remarks>
public sealed class SubNidService
{
    private readonly NpsdOptions  _opts;
    private readonly RootIdentity _root;
    private readonly SubNidStore  _store;

    /// <summary>The full NID of the host as the issuing authority.</summary>
    public string HostNid { get; }

    public SubNidService(NpsdOptions opts, RootIdentity root, SubNidStore store)
    {
        _opts    = opts;
        _root    = root;
        _store   = store;
        HostNid  = opts.HostNidPrefix ?? $"urn:nps:host:{root.HostFingerprint}";
    }

    /// <summary>
    /// Issues a sub-NID for a local agent. If <paramref name="agentPubKeyEncoded"/>
    /// is supplied (in <c>ed25519:{base64url}</c> form), the caller brings their own
    /// keypair and npsd never sees the private half. If null, npsd mints a fresh
    /// keypair and returns the private key encoded as <c>{base64url(raw 32 bytes)}</c>
    /// so the caller can hand it to the worker.
    /// </summary>
    /// <param name="identifier">
    /// Short identifier appended to the NID, e.g. <c>"my-worker"</c>. Must be a
    /// non-empty URN-safe string (no <c>:</c>, no whitespace). If null, a new
    /// short UUID is generated.
    /// </param>
    /// <param name="capabilities">Capability list to embed in the IdentFrame scope.</param>
    /// <param name="scopeJson">Scope JSON object as a string. May be <c>"{}"</c>.</param>
    /// <param name="agentPubKeyEncoded">Caller-supplied public key, or null to mint one.</param>
    /// <param name="metadataJson">Optional metadata JSON.</param>
    public IssueResult Issue(
        string?               identifier,
        IReadOnlyList<string> capabilities,
        string                scopeJson,
        string?               agentPubKeyEncoded,
        string?               metadataJson)
    {
        identifier ??= Guid.NewGuid().ToString("N").Substring(0, 16);
        ValidateIdentifier(identifier);

        var nid = $"{HostNid}:agent:{identifier}";
        if (_store.Get(nid) is not null)
            throw new SubNidAlreadyExistsException(nid);

        // Materialize the agent keypair (caller-supplied or freshly minted).
        string  pubKeyEncoded;
        string? privKeyRawBase64 = null;
        string? privKeyStored    = null;
        if (agentPubKeyEncoded is not null)
        {
            pubKeyEncoded = agentPubKeyEncoded;
            // Validate format only — we don't keep the key around.
            if (NipSigner.DecodePublicKey(pubKeyEncoded) is null)
                throw new ArgumentException("agent_pub_key is not a valid ed25519:{base64url}.", nameof(agentPubKeyEncoded));
        }
        else
        {
            using var fresh = Key.Create(SignatureAlgorithm.Ed25519,
                new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
            pubKeyEncoded    = NipSigner.EncodePublicKey(fresh.PublicKey);
            privKeyRawBase64 = NipSigner.Base64Url(fresh.Export(KeyBlobFormat.RawPrivateKey));
            // alpha.3: we currently do NOT persist the minted private key.
            // It is returned ONCE in the API response and forgotten — the
            // caller is responsible for storing it on the worker side. We
            // keep the column in the schema for a future opt-in
            // ("npsd-managed agent keys") that arrives with sub-NID renewal
            // in alpha.4.
            privKeyStored    = null;
        }

        var serial    = _store.NextSerial();
        var issuedAt  = DateTimeOffset.UtcNow;
        var expiresAt = issuedAt.AddDays(_opts.SubNidValidityDays);

        // Build the IdentFrame and sign with the root key.
        var unsignedScope = ParseScope(scopeJson, capabilities);
        var unsigned = new
        {
            frame        = "0x20",
            nid          = nid,
            pub_key      = pubKeyEncoded,
            capabilities = capabilities,
            scope        = unsignedScope,
            issued_by    = HostNid,
            issued_at    = issuedAt.ToString("O"),
            expires_at   = expiresAt.ToString("O"),
            serial       = serial,
        };
        var signature = NipSigner.Sign(_root.Key, unsigned);

        var frame = new IdentFrame
        {
            Nid          = nid,
            PubKey       = pubKeyEncoded,
            Capabilities = capabilities,
            Scope        = JsonSerializer.SerializeToElement(unsignedScope),
            IssuedBy     = HostNid,
            IssuedAt     = issuedAt.ToString("O"),
            ExpiresAt    = expiresAt.ToString("O"),
            Serial       = serial,
            Signature    = signature,
            Metadata     = ParseMetadata(metadataJson),
        };

        _store.Insert(new SubNidRecord
        {
            Nid              = nid,
            PubKey           = pubKeyEncoded,
            PrivKeyEncrypted = privKeyStored,
            IssuedBy         = HostNid,
            IssuedAt         = issuedAt,
            ExpiresAt        = expiresAt,
            Serial           = serial,
            Capabilities     = string.Join(',', capabilities),
            ScopeJson        = scopeJson,
            MetadataJson     = metadataJson,
        });

        return new IssueResult(frame, privKeyRawBase64);
    }

    public SubNidRecord? Get(string nid) => _store.Get(nid);

    public IReadOnlyList<SubNidRecord> List(int limit = 100, int offset = 0) => _store.List(limit, offset);

    /// <summary>Marks the NID revoked. Returns false if the NID is unknown.</summary>
    public bool Revoke(string nid, string reason)
        => _store.MarkRevoked(nid, reason, DateTimeOffset.UtcNow);

    // ── helpers ───────────────────────────────────────────────────────────────

    private static void ValidateIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier) ||
            identifier.Any(ch => ch == ':' || char.IsWhiteSpace(ch)))
            throw new ArgumentException(
                "identifier must be a non-empty string with no `:` and no whitespace.",
                nameof(identifier));
    }

    private static object ParseScope(string scopeJson, IReadOnlyList<string> capabilities)
    {
        // Scope is an opaque JSON object the agent / verifier interpret. We
        // accept whatever the caller sends (just validate it parses).
        try
        {
            using var doc = JsonDocument.Parse(scopeJson);
            return JsonSerializer.Deserialize<object>(doc.RootElement.GetRawText())
                ?? new { };
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("scope is not valid JSON.", nameof(scopeJson), ex);
        }
    }

    private static IdentMetadata? ParseMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return null;
        try
        {
            return JsonSerializer.Deserialize<IdentMetadata>(metadataJson, s_jsonOpts);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("metadata is not valid JSON.", nameof(metadataJson), ex);
        }
    }

    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Result of a sub-NID issuance: the IdentFrame to hand to the agent,
    /// plus an optional minted private key (only when the caller did not
    /// supply their own pub_key). The minted private key is returned ONCE
    /// and is not retained.
    /// </summary>
    public sealed record IssueResult(IdentFrame Frame, string? MintedPrivateKeyBase64Url);
}

/// <summary>Thrown when issuing a NID that already exists.</summary>
public sealed class SubNidAlreadyExistsException(string nid) : Exception($"NID already exists: {nid}")
{
    public string Nid { get; } = nid;
}
