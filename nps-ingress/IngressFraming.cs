// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NPS.Core.Frames;
using NPS.Core.Ncp;

namespace NPS.Daemon.Ingress;

/// <summary>
/// Minimal native-mode framing reader for the L2 terminator. After the TLS handshake, the
/// terminated stream carries the RFC-0001 preamble followed by length-delimited NCP frames
/// (NPS-RFC-0006 §2.2). These helpers let the ingress peek the handshake frames (to extract the
/// IdentFrame NID for the §6.3 session-NID binding) without depending on a full NCP stack.
/// </summary>
internal static class IngressFraming
{
    /// <summary>Reads and validates the 8-byte native-mode preamble. False on EOF or mismatch.</summary>
    public static async Task<bool> TryReadPreambleAsync(Stream s, CancellationToken ct)
    {
        var buf = new byte[NcpPreamble.Length];
        if (!await ReadExactAsync(s, buf, ct)) return false;
        return NcpPreamble.TryValidate(buf, out _);
    }

    /// <summary>
    /// Reads exactly one length-delimited NCP frame (header + payload) and returns its raw wire
    /// bytes, or null on a clean EOF. The header's EXT flag selects the 4- or 8-byte header form
    /// (NPS-1 §3.1); the payload length is read from the header.
    /// </summary>
    /// <param name="maxPayloadBytes">
    /// Hard cap on the header-declared payload length. The length prefix is attacker-controlled
    /// (up to 4 GB in EXT mode), so an unbounded <c>new byte[PayloadLength]</c> is an OOM DoS;
    /// a frame exceeding this bound throws <see cref="InvalidDataException"/> and the caller closes
    /// the connection.
    /// </param>
    public static async Task<byte[]?> ReadFrameAsync(Stream s, int maxPayloadBytes, CancellationToken ct)
    {
        // Read the 2-byte prefix (frame type + flags) to learn the header size.
        var head2 = new byte[2];
        if (!await ReadExactAsync(s, head2, ct)) return null;

        bool ext = ((FrameFlags)head2[1] & FrameFlags.Ext) != 0;
        int headerSize = ext ? FrameHeader.ExtendedSize : FrameHeader.DefaultSize;

        var headerBytes = new byte[headerSize];
        head2.CopyTo(headerBytes, 0);
        if (!await ReadExactAsync(s, headerBytes.AsMemory(2), ct)) return null;

        var header = FrameHeader.Parse(headerBytes);
        if (header.PayloadLength > (uint)maxPayloadBytes)
            throw new InvalidDataException(
                $"NCP handshake frame payload {header.PayloadLength} exceeds the {maxPayloadBytes}-byte ingress bound.");

        var payload = new byte[header.PayloadLength];
        if (header.PayloadLength > 0 && !await ReadExactAsync(s, payload, ct)) return null;

        var frame = new byte[headerSize + payload.Length];
        headerBytes.CopyTo(frame, 0);
        payload.CopyTo(frame, headerSize);
        return frame;
    }

    /// <summary>Result of classifying a handshake frame against the IdentFrame NID extraction.</summary>
    internal enum IdentScan
    {
        /// <summary>Not an IdentFrame (e.g. HelloFrame) — keep scanning the handshake.</summary>
        NotIdent,
        /// <summary>An IdentFrame whose <c>nid</c> was extracted (see the out parameter).</summary>
        Nid,
        /// <summary>
        /// An IdentFrame was seen but its NID could not be extracted (msgpack-tier, malformed JSON,
        /// or missing <c>nid</c>). Native-mode handshake IdentFrames MUST be JSON-tier (NPS-3 §5.1),
        /// so this is a non-conformant / forged frame and MUST NOT be treated as "no Ident" — the
        /// caller fails the §6.3 binding closed rather than proxying it through unchecked.
        /// </summary>
        Unverifiable,
    }

    /// <summary>
    /// Classifies <paramref name="frame"/> for the session-NID binding (NPS-RFC-0006 §6.3).
    /// Distinguishes a non-Ident handshake frame from an IdentFrame whose NID cannot be verified,
    /// so a malformed/msgpack IdentFrame cannot silently bypass the binding (fail-open).
    /// </summary>
    public static IdentScan ClassifyIdent(byte[] frame, out string? nid)
    {
        nid = null;

        FrameHeader header;
        try { header = FrameHeader.Parse(frame); }
        catch { return IdentScan.NotIdent; } // unparseable header — not classifiable as Ident; keep scanning

        if (header.FrameType != FrameType.Ident)
            return IdentScan.NotIdent;

        // It IS an IdentFrame. Per NPS-3 §5.1 the native-mode handshake Ident MUST be JSON-tier;
        // anything else (or an unparseable / nid-less payload) is non-conformant and unverifiable.
        if (header.EncodingTier != EncodingTier.Json)
            return IdentScan.Unverifiable;

        try
        {
            var payload = frame.AsSpan(header.HeaderSize, (int)header.PayloadLength);
            using var doc = JsonDocument.Parse(payload.ToArray());
            if (doc.RootElement.TryGetProperty("nid", out var n) && n.ValueKind == JsonValueKind.String)
            {
                nid = n.GetString();
                return IdentScan.Nid;
            }
            return IdentScan.Unverifiable;
        }
        catch { return IdentScan.Unverifiable; }
    }

    /// <summary>
    /// Back-compat helper: returns the IdentFrame <c>nid</c> or null. Prefer
    /// <see cref="ClassifyIdent"/>, which distinguishes "not an Ident" from "unverifiable Ident".
    /// </summary>
    public static string? TryGetIdentNid(byte[] frame)
        => ClassifyIdent(frame, out var nid) == IdentScan.Nid ? nid : null;

    private static async Task<bool> ReadExactAsync(Stream s, Memory<byte> buffer, CancellationToken ct)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int n = await s.ReadAsync(buffer[read..], ct);
            if (n == 0) return false; // EOF before the buffer was filled
            read += n;
        }
        return true;
    }

    private static Task<bool> ReadExactAsync(Stream s, byte[] buffer, CancellationToken ct)
        => ReadExactAsync(s, buffer.AsMemory(), ct);
}
