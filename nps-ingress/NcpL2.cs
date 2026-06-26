// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

namespace NPS.Daemon.Ingress;

/// <summary>
/// Layer-2 native-mode constants for nps-ingress (NPS-RFC-0006 §6).
/// </summary>
internal static class NcpL2
{
    /// <summary>Suite-wide native-mode ALPN token (NPS-RFC-0006 §6.1).</summary>
    public const string Alpn = "nps/1.0";

    /// <summary>
    /// Returned when the mTLS client-certificate NID does not match the session
    /// IdentFrame NID, or a resumed session's certificate NID differs from the
    /// ticket-bound NID (NPS-RFC-0006 §6.3–§6.4; error-codes.md).
    /// </summary>
    public const string NidMismatchCode = "NCP-NID-MISMATCH";
}
