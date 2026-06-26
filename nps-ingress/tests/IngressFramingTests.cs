// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using NPS.Core.Frames;
using NPS.Core.Ncp;
using NPS.Daemon.Ingress;
using Xunit;

namespace NPS.Daemon.Ingress.Tests;

/// <summary>Tests for the L2 native-mode framing reader (preamble + length-delimited frames).</summary>
public sealed class IngressFramingTests
{
    private static byte[] Frame(FrameType type, byte[] payload)
    {
        var header = new FrameHeader(type, (FrameFlags)0, (uint)payload.Length);
        var buf = new byte[header.HeaderSize + payload.Length];
        header.WriteTo(buf);
        payload.CopyTo(buf, header.HeaderSize);
        return buf;
    }

    [Fact]
    public async Task ReadsValidPreambleThenSplitsFramesByLength()
    {
        var f1 = Frame(FrameType.Hello, Encoding.UTF8.GetBytes("{\"a\":1}"));
        var f2 = Frame(FrameType.Ident, Encoding.UTF8.GetBytes("{\"nid\":\"urn:nps:agent:x\"}"));

        using var ms = new MemoryStream();
        ms.Write(Encoding.ASCII.GetBytes(NcpPreamble.Literal));
        ms.Write(f1);
        ms.Write(f2);
        ms.Position = 0;

        Assert.True(await IngressFraming.TryReadPreambleAsync(ms, default));

        var r1 = await IngressFraming.ReadFrameAsync(ms, 1 << 20, default);
        var r2 = await IngressFraming.ReadFrameAsync(ms, 1 << 20, default);
        var r3 = await IngressFraming.ReadFrameAsync(ms, 1 << 20, default); // EOF

        Assert.Equal(f1, r1);
        Assert.Equal(f2, r2);
        Assert.Null(r3);
    }

    [Fact]
    public void TryGetIdentNid_extracts_nid_from_json_identframe()
    {
        var ident = Frame(FrameType.Ident, Encoding.UTF8.GetBytes("{\"nid\":\"urn:nps:agent:x\",\"pub_key\":\"k\"}"));
        Assert.Equal("urn:nps:agent:x", IngressFraming.TryGetIdentNid(ident));

        // A non-Ident frame yields null.
        var hello = Frame(FrameType.Hello, Encoding.UTF8.GetBytes("{\"nid\":\"urn:nps:agent:x\"}"));
        Assert.Null(IngressFraming.TryGetIdentNid(hello));

        // A msgpack-tier Ident frame is skipped (null) — only JSON-tier is parsed here.
        var header = new FrameHeader(FrameType.Ident, (FrameFlags)0x01 /* msgpack tier */, 3);
        var mp = new byte[header.HeaderSize + 3];
        header.WriteTo(mp);
        Assert.Null(IngressFraming.TryGetIdentNid(mp));
    }

    [Fact]
    public async Task RejectsBadPreamble()
    {
        using var ms = new MemoryStream(Encoding.ASCII.GetBytes("BOGUS!!!extra"));
        Assert.False(await IngressFraming.TryReadPreambleAsync(ms, default));
    }

    [Fact]
    public async Task ReturnsNullOnTruncatedFrame()
    {
        var full = Frame(FrameType.Ident, Encoding.UTF8.GetBytes("{\"nid\":\"x\"}"));
        // Keep the header but drop part of the payload — the reader must report EOF, not hang.
        using var ms = new MemoryStream(full[..(full.Length - 3)]);
        Assert.Null(await IngressFraming.ReadFrameAsync(ms, 1 << 20, default));
    }

    [Fact]
    public async Task RejectsOversizedFramePayload()
    {
        // An EXT-mode frame whose length prefix declares a huge payload must be rejected by the
        // bound BEFORE the payload buffer is allocated (OOM DoS guard) — not read into memory.
        var header = new FrameHeader(FrameType.Hello, FrameFlags.Ext, PayloadLength: 4_000_000_000);
        var buf = new byte[header.HeaderSize];
        header.WriteTo(buf);
        using var ms = new MemoryStream(buf);
        await Assert.ThrowsAsync<InvalidDataException>(
            () => IngressFraming.ReadFrameAsync(ms, maxPayloadBytes: 1 << 20, default));
    }

    [Fact]
    public void ClassifyIdent_distinguishes_unverifiable_ident_from_non_ident()
    {
        // JSON-tier Ident with a nid → Nid.
        var ident = Frame(FrameType.Ident, Encoding.UTF8.GetBytes("{\"nid\":\"urn:nps:agent:x\"}"));
        Assert.Equal(IngressFraming.IdentScan.Nid, IngressFraming.ClassifyIdent(ident, out var nid));
        Assert.Equal("urn:nps:agent:x", nid);

        // Non-Ident handshake frame → NotIdent (keep scanning).
        var hello = Frame(FrameType.Hello, Encoding.UTF8.GetBytes("{}"));
        Assert.Equal(IngressFraming.IdentScan.NotIdent, IngressFraming.ClassifyIdent(hello, out _));

        // A msgpack-tier Ident frame is an Ident we cannot verify → Unverifiable (fail closed),
        // NOT NotIdent — this is the bypass the §6.3 binding must reject.
        var mpHeader = new FrameHeader(FrameType.Ident, (FrameFlags)0x01 /* msgpack tier */, 3);
        var mp = new byte[mpHeader.HeaderSize + 3];
        mpHeader.WriteTo(mp);
        Assert.Equal(IngressFraming.IdentScan.Unverifiable, IngressFraming.ClassifyIdent(mp, out _));

        // A JSON-tier Ident with no nid field is also unverifiable.
        var noNid = Frame(FrameType.Ident, Encoding.UTF8.GetBytes("{\"pub_key\":\"k\"}"));
        Assert.Equal(IngressFraming.IdentScan.Unverifiable, IngressFraming.ClassifyIdent(noNid, out _));
    }
}
