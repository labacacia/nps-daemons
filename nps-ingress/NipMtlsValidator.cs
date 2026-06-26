// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography.X509Certificates;
using NPS.NIP.Ca;
using NPS.NIP.X509;

namespace NPS.Daemon.Ingress;

/// <summary>Outcome of an mTLS client-certificate validation / NID-binding check.</summary>
internal sealed record MtlsResult(bool Ok, string? BoundNid, string? ErrorCode, string? Message)
{
    public static MtlsResult Success(string nid) => new(true, nid, null, null);
    public static MtlsResult Fail(string code, string message) => new(false, null, code, message);
}

/// <summary>
/// Mutual-TLS admission logic for nps-ingress (NPS-RFC-0006 §6.3). At the TLS layer the client
/// certificate's subject NID is extracted and the chain validated to a configured trust anchor;
/// the extracted NID is then bound to the NCP session. When the post-handshake IdentFrame
/// arrives, its NID MUST match the bound certificate NID, else the session is closed with
/// <see cref="NcpL2.NidMismatchCode"/> (<c>NCP-NID-MISMATCH</c>).
/// </summary>
internal static class NipMtlsValidator
{
    /// <summary>
    /// Extracts the NID from a client certificate's subject CN (RFC-0002 X.509 NID profile;
    /// mirrors <see cref="NipX509Verifier"/>'s subject-NID convention). Returns null when absent.
    /// </summary>
    public static string? ExtractCertNid(X509Certificate2 cert)
    {
        var cn = cert.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
        return string.IsNullOrEmpty(cn) ? null : cn;
    }

    /// <summary>
    /// Validates a presented client certificate (and any supplied intermediates) against the
    /// configured trust anchors and returns the bound NID on success. Chain + EKU + subject-NID
    /// checks are delegated to <see cref="NipX509Verifier"/>.
    /// </summary>
    public static MtlsResult ValidateClientCert(
        X509Certificate2 clientCert,
        IReadOnlyList<X509Certificate2> intermediates,
        IReadOnlyList<X509Certificate2> trustAnchors)
    {
        var nid = ExtractCertNid(clientCert);
        if (nid is null)
            return MtlsResult.Fail(NipErrorCodes.CertSubjectNidMismatch,
                "client certificate has no subject CN (NID).");

        // Validity-period gate. The custom RemoteCertificateValidationCallback replaces .NET's
        // default chain policy, so expiry is NOT otherwise checked — an expired or not-yet-valid
        // NIP cert would be admitted. This matters most for the short-lived edge-mTLS profile
        // (NIP v0.10 §6.1). NipX509Verifier checks the Ed25519 chain, not the validity window.
        var nowUtc = DateTime.UtcNow;
        if (nowUtc < clientCert.NotBefore.ToUniversalTime() || nowUtc > clientCert.NotAfter.ToUniversalTime())
            return MtlsResult.Fail(NipErrorCodes.CertExpired,
                $"client certificate is outside its validity window " +
                $"[{clientCert.NotBefore.ToUniversalTime():O}, {clientCert.NotAfter.ToUniversalTime():O}].");

        var chain = new List<string>(intermediates.Count + 1) { ToBase64Url(clientCert.RawData) };
        foreach (var i in intermediates)
            chain.Add(ToBase64Url(i.RawData));

        var result = NipX509Verifier.Verify(chain, nid, assertedAssuranceLevel: null, trustAnchors);
        return result.Valid
            ? MtlsResult.Success(nid)
            : MtlsResult.Fail(result.ErrorCode ?? NipErrorCodes.CertSubjectNidMismatch,
                result.Message ?? "client certificate validation failed.");
    }

    /// <summary>
    /// Enforces the session NID binding (NPS-RFC-0006 §6.3): the IdentFrame NID MUST equal the
    /// NID bound from the mTLS certificate. Returns <c>NCP-NID-MISMATCH</c> on divergence.
    /// </summary>
    public static MtlsResult CheckSessionNidBinding(string certNid, string identFrameNid)
        => string.Equals(certNid, identFrameNid, StringComparison.Ordinal)
            ? MtlsResult.Success(certNid)
            : MtlsResult.Fail(NcpL2.NidMismatchCode,
                $"IdentFrame NID '{identFrameNid}' does not match the mTLS certificate NID '{certNid}'.");

    private static string ToBase64Url(byte[] der)
        => Convert.ToBase64String(der).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
