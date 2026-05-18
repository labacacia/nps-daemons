// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

namespace NPS.Daemon.Npsd;

/// <summary>
/// Configurable knobs for the <c>npsd</c> daemon. All fields have safe
/// defaults; the only thing the operator typically changes is
/// <see cref="DataDir"/> (per-host persistent state) and
/// <see cref="Port"/> / <see cref="Host"/> (bind address).
/// </summary>
public sealed record NpsdOptions
{
    /// <summary>TCP port to bind. Default: 17433 (NPS-1 §2.3 unified port).</summary>
    public int Port { get; init; } = 17433;

    /// <summary>
    /// Bind address. Default: 127.0.0.1 (loopback). Set to 0.0.0.0 only inside
    /// an isolated network namespace — never expose npsd directly to the public
    /// internet (use <c>nps-ingress</c>).
    /// </summary>
    public string Host { get; init; } = "127.0.0.1";

    /// <summary>
    /// Persistent state directory. Holds the encrypted root keypair file
    /// and the SQLite databases for sub-NIDs and (future) inbox messages.
    /// </summary>
    public string DataDir { get; init; }
        = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "npsd");

    /// <summary>
    /// NID prefix used when minting sub-NIDs on this host. Defaults to
    /// <c>urn:nps:host:&lt;HostFingerprint&gt;</c>; sub-NIDs are minted as
    /// <c>{HostNidPrefixOrComputed}:agent:{identifier}</c>.
    /// </summary>
    /// <remarks>
    /// If null, the runtime computes a deterministic prefix from the
    /// host's root public key fingerprint (first 8 bytes of SHA-256,
    /// hex-encoded). Override only if the host has been registered with
    /// an upstream CA under a different NID and you want sub-NIDs derived
    /// under it.
    /// </remarks>
    public string? HostNidPrefix { get; init; }

    /// <summary>
    /// Default validity window for issued sub-NIDs, in days. Local-host
    /// agent identities are short-lived by design; the upstream CA-issued
    /// host identity is the durable trust anchor.
    /// </summary>
    public int SubNidValidityDays { get; init; } = 7;

    /// <summary>
    /// Maximum inbox depth per NID. Producers get HTTP 429 when exceeded.
    /// Default 1024; bump only after profiling memory / per-message size.
    /// </summary>
    public int MaxInboxDepthPerNid { get; init; } = 1024;

    /// <summary>
    /// Maximum payload size of a single inbox message, in bytes. Default 64 KiB
    /// matches the NCP default frame size (NPS-1 §3.1 EXT=0).
    /// </summary>
    public int MaxInboxMessageBytes { get; init; } = 64 * 1024;

    /// <summary>
    /// Maximum long-poll wait time, in seconds. Default 30. Clients that
    /// request a longer wait are clamped to this value.
    /// </summary>
    public int MaxInboxWaitSeconds { get; init; } = 30;

    /// <summary>
    /// Read configuration from <c>NPSD_*</c> environment variables. Returns
    /// a populated options instance; missing variables fall back to the
    /// defaults declared above.
    /// </summary>
    public static NpsdOptions FromEnvironment()
    {
        return new NpsdOptions
        {
            Port    = int.TryParse(Environment.GetEnvironmentVariable("NPSD_PORT"), out var p) ? p : 17433,
            Host    = Environment.GetEnvironmentVariable("NPSD_HOST") ?? "127.0.0.1",
            DataDir = Environment.GetEnvironmentVariable("NPSD_DATA_DIR")
                      ?? Path.Combine(
                          Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                          "npsd"),
            HostNidPrefix         = Environment.GetEnvironmentVariable("NPSD_HOST_NID_PREFIX"),
            SubNidValidityDays    = int.TryParse(Environment.GetEnvironmentVariable("NPSD_SUB_NID_VALIDITY_DAYS"),    out var d) ? d : 7,
            MaxInboxDepthPerNid   = int.TryParse(Environment.GetEnvironmentVariable("NPSD_MAX_INBOX_DEPTH_PER_NID"),  out var m) ? m : 1024,
            MaxInboxMessageBytes  = int.TryParse(Environment.GetEnvironmentVariable("NPSD_MAX_INBOX_MESSAGE_BYTES"),  out var b) ? b : 64 * 1024,
            MaxInboxWaitSeconds   = int.TryParse(Environment.GetEnvironmentVariable("NPSD_MAX_INBOX_WAIT_SECONDS"),   out var w) ? w : 30,
        };
    }
}
