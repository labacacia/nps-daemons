// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0
//
// npsd — NPS Daemon, Layer 1 (host-local NCP wire + state host).
// See docs/daemons/architecture.md for the role this binary plays in
// the broader NPS deployment topology.

using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// ── Bind 127.0.0.1:17433 by default ───────────────────────────────────────
//
// Per docs/daemons/architecture.md, npsd is host-local and binds to
// loopback. Public-facing ingress is the responsibility of nps-gateway.
builder.WebHost.ConfigureKestrel(options =>
{
    var port = int.TryParse(Environment.GetEnvironmentVariable("NPSD_PORT"), out var p) ? p : 17433;
    var host = Environment.GetEnvironmentVariable("NPSD_HOST") ?? "127.0.0.1";
    if (host == "0.0.0.0")
    {
        options.ListenAnyIP(port);
    }
    else
    {
        options.ListenLocalhost(port);
    }
});

builder.Services.AddSingleton<RootKeyMaterial>(sp =>
{
    var dataDir = Environment.GetEnvironmentVariable("NPSD_DATA_DIR")
                  ?? Path.Combine(
                      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                      "npsd");
    Directory.CreateDirectory(dataDir);
    return RootKeyMaterial.LoadOrCreate(dataDir);
});

var app = builder.Build();

// Trigger root keypair load/create at startup so any failure surfaces
// immediately (rather than on first request).
var rootKey = app.Services.GetRequiredService<RootKeyMaterial>();
app.Logger.LogInformation(
    "npsd starting; root NID host fingerprint = {Fingerprint}",
    rootKey.HostFingerprint);

// ── /health ───────────────────────────────────────────────────────────────
//
// Minimal liveness endpoint suitable for Docker HEALTHCHECK and systemd
// active-state probes. Returns 200 + JSON when the root key is loaded.
app.MapGet("/health", (RootKeyMaterial key) => Results.Json(new
{
    status         = "ok",
    daemon         = "npsd",
    version        = "1.0.0-alpha.3",
    layer          = 1,
    role           = "host-local NCP wire + state host",
    port           = (Environment.GetEnvironmentVariable("NPSD_PORT") ?? "17433"),
    host_nid_fpr   = key.HostFingerprint,
    spec_reference = "docs/daemons/architecture.md",
}));

// ── /.nwm — minimal Neural Web Manifest for the daemon itself ─────────────
//
// Phase 1 (alpha.3): static manifest declaring npsd as a Memory + Action
// node hosting only the system actions every L1 daemon must answer.
// The richer manifest produced by the host's actual AnchorNode middleware
// (NPS-CR-0001) lives at the application port the operator configures
// for their own NWP nodes; this `/.nwm` describes the daemon's own
// surface, not an application's.
app.MapGet("/.nwm", (RootKeyMaterial key) => Results.Content(
    JsonSerializer.Serialize(new
    {
        nwp                 = "0.7",
        node_id             = $"urn:nps:node:{key.HostFingerprint}:npsd",
        node_type           = "memory",
        display_name        = "NPS Daemon (npsd)",
        wire_formats        = new[] { "json", "msgpack" },
        preferred_format    = "json",
        capabilities        = new { query = true, subscribe = false },
        auth                = new { required = false },
        endpoints           = new
        {
            manifest = "/.nwm",
            health   = "/health",
        },
        min_assurance_level = "anonymous",
    }, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented        = false,
    }),
    contentType: "application/nwp-manifest+json"));

app.Logger.LogInformation("npsd listening on http://127.0.0.1:17433 — see docs/daemons/architecture.md for layer-1 responsibilities");
app.Run();

// ── Root keypair material ─────────────────────────────────────────────────
//
// Phase 1 reference: a stable Ed25519 keypair persisted to disk. We do
// NOT yet use NPS.NIP's HSM-or-encrypted-file storage — that arrives in
// alpha.4 alongside RFC-0002 X.509 wiring. For now the file is plain
// PKCS#8 with chmod 0600 so an L1 daemon can satisfy
// `TC-N1-NIP-01 — Root keypair generation and permission`.
internal sealed class RootKeyMaterial
{
    private readonly byte[] _privateKey;

    public string HostFingerprint { get; }

    private RootKeyMaterial(byte[] privateKey, string fingerprint)
    {
        _privateKey     = privateKey;
        HostFingerprint = fingerprint;
    }

    public static RootKeyMaterial LoadOrCreate(string dataDir)
    {
        var keyPath = Path.Combine(dataDir, "root.ed25519.pkcs8");

        byte[] priv;
        if (File.Exists(keyPath))
        {
            priv = File.ReadAllBytes(keyPath);
        }
        else
        {
            using var ed = NSec.Cryptography.Key.Create(
                NSec.Cryptography.SignatureAlgorithm.Ed25519,
                new NSec.Cryptography.KeyCreationParameters
                {
                    ExportPolicy = NSec.Cryptography.KeyExportPolicies.AllowPlaintextExport,
                });
            priv = ed.Export(NSec.Cryptography.KeyBlobFormat.PkixPrivateKey);
            File.WriteAllBytes(keyPath, priv);
            // L1 conformance — TC-N1-NIP-01: file MUST be 0600 on POSIX.
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    keyPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }

        // Derive a deterministic host fingerprint from the public key.
        using var imported = NSec.Cryptography.Key.Import(
            NSec.Cryptography.SignatureAlgorithm.Ed25519,
            priv,
            NSec.Cryptography.KeyBlobFormat.PkixPrivateKey);
        var pub = imported.PublicKey.Export(NSec.Cryptography.KeyBlobFormat.PkixPublicKey);
        var sha = System.Security.Cryptography.SHA256.HashData(pub);
        var fp  = Convert.ToHexString(sha.AsSpan(0, 8)).ToLowerInvariant();

        return new RootKeyMaterial(priv, fp);
    }
}
