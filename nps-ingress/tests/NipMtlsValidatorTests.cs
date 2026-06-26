// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NPS.Daemon.Ingress;
using Xunit;

namespace NPS.Daemon.Ingress.Tests;

/// <summary>Tests for the nps-ingress L2 mTLS NID-binding logic (NPS-RFC-0006 §6.3).</summary>
public sealed class NipMtlsValidatorTests
{
    private static X509Certificate2 SelfSignedWithCn(string cn)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest($"CN={cn}", ecdsa, HashAlgorithmName.SHA256);
        return req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));
    }

    [Fact]
    public void ExtractCertNid_reads_the_subject_cn()
    {
        using var cert = SelfSignedWithCn("urn:nps:agent:ca.example.com:writer-1");
        Assert.Equal("urn:nps:agent:ca.example.com:writer-1", NipMtlsValidator.ExtractCertNid(cert));
    }

    [Fact]
    public void Alpn_token_is_nps_1_0()
    {
        Assert.Equal("nps/1.0", NcpL2.Alpn);
        Assert.Equal("NCP-NID-MISMATCH", NcpL2.NidMismatchCode);
    }

    [Fact]
    public void CheckSessionNidBinding_ok_when_cert_and_identframe_nids_match()
    {
        var res = NipMtlsValidator.CheckSessionNidBinding(
            "urn:nps:agent:x", "urn:nps:agent:x");
        Assert.True(res.Ok);
        Assert.Equal("urn:nps:agent:x", res.BoundNid);
        Assert.Null(res.ErrorCode);
    }

    [Fact]
    public void CheckSessionNidBinding_rejects_mismatch_with_ncp_nid_mismatch()
    {
        var res = NipMtlsValidator.CheckSessionNidBinding(
            certNid: "urn:nps:agent:cert-owner",
            identFrameNid: "urn:nps:agent:impostor");
        Assert.False(res.Ok);
        Assert.Equal("NCP-NID-MISMATCH", res.ErrorCode);
        Assert.Null(res.BoundNid);
        Assert.Contains("impostor", res.Message);
    }

    [Fact]
    public void ValidateClientCert_fails_against_empty_trust_anchors()
    {
        // A self-signed leaf cannot chain to an empty trust-anchor set ⇒ rejected (not bound).
        using var cert = SelfSignedWithCn("urn:nps:agent:self");
        var res = NipMtlsValidator.ValidateClientCert(
            cert, intermediates: Array.Empty<X509Certificate2>(), trustAnchors: Array.Empty<X509Certificate2>());
        Assert.False(res.Ok);
        Assert.NotNull(res.ErrorCode);
    }
}
