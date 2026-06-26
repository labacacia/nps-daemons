// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NPS.Core.Ncp;

namespace NPS.Daemon.Ingress;

/// <summary>
/// Layer-2 native-mode TLS terminator (NPS-RFC-0006 §6). Accepts NCP-over-TLS connections,
/// negotiating ALPN <c>nps/1.0</c> over TLS 1.3 with mutual authentication: the client
/// certificate is validated to a configured trust anchor and its NID bound to the session
/// (<see cref="NipMtlsValidator"/>). The terminated NCP byte stream is then proxied to the local
/// backend (npsd). Stays idle unless a server certificate is configured.
/// </summary>
internal sealed class NcpTlsListener(IngressOptions opts, ILogger<NcpTlsListener> log) : BackgroundService
{
    private X509Certificate2? _serverCert;
    private List<X509Certificate2> _trustAnchors = new();

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (opts.TlsPort == 0 || opts.ServerCertPath is null)
        {
            log.LogWarning(
                "nps-ingress L2 TLS listener disabled (tls_port={Port}, cert={Cert}). " +
                "Set NPSINGRESS_CERT_PATH to enable NCP-over-TLS termination.",
                opts.TlsPort, opts.ServerCertPath ?? "<none>");
            return;
        }

        try
        {
            _serverCert   = X509CertificateLoader.LoadPkcs12FromFile(opts.ServerCertPath!, opts.ServerCertPassword);
            _trustAnchors = LoadTrustAnchors();
        }
        catch (Exception ex)
        {
            log.LogError(ex, "nps-ingress L2: failed to load TLS material — listener not started.");
            return;
        }

        var listener = new TcpListener(IPAddress.Any, opts.TlsPort);
        listener.Start();
        log.LogInformation(
            "nps-ingress L2 TLS listener up on :{Port} (ALPN {Alpn}, mTLS={Mtls}, trust_anchors={Anchors}) → backend {Host}:{BackendPort}",
            opts.TlsPort, NcpL2.Alpn, opts.RequireClientCert, _trustAnchors.Count, opts.BackendHost, opts.BackendPort);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await listener.AcceptTcpClientAsync(ct); }
                catch (OperationCanceledException) { break; }
                _ = HandleConnectionAsync(client, ct);
            }
        }
        finally { listener.Stop(); }
    }

    private async Task HandleConnectionAsync(TcpClient client, CancellationToken ct)
    {
        using var _client = client;
        string? boundNid = null;
        var ssl = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
        try
        {
            var authOpts = new SslServerAuthenticationOptions
            {
                ServerCertificate         = _serverCert,
                ClientCertificateRequired = opts.RequireClientCert,
                EnabledSslProtocols       = SslProtocols.Tls13,
                ApplicationProtocols      = new List<SslApplicationProtocol> { new(NcpL2.Alpn) },
                RemoteCertificateValidationCallback = (_, cert, chain, _) =>
                {
                    if (!opts.RequireClientCert) return true;
                    if (cert is null)
                    {
                        log.LogWarning("L2: connection presented no client certificate.");
                        return false;
                    }
                    var leaf = cert as X509Certificate2 ?? X509CertificateLoader.LoadCertificate(cert.GetRawCertData());
                    var intermediates = chain?.ChainElements.Skip(1).Select(e => e.Certificate).ToList()
                                        ?? new List<X509Certificate2>();
                    var res = NipMtlsValidator.ValidateClientCert(leaf, intermediates, _trustAnchors);
                    if (!res.Ok)
                    {
                        log.LogWarning("L2: client certificate rejected ({Code}): {Msg}", res.ErrorCode, res.Message);
                        return false;
                    }
                    boundNid = res.BoundNid;
                    return true;
                },
            };

            await ssl.AuthenticateAsServerAsync(authOpts, ct);
            log.LogInformation(
                "L2: TLS handshake complete (alpn={Alpn}, bound_nid={Nid}).",
                ssl.NegotiatedApplicationProtocol.ToString(), boundNid ?? "<none>");

            // NPS-RFC-0006 §6.3 session-NID binding: peek the native-mode handshake (preamble +
            // frames up to the IdentFrame) and require its NID to match the bound certificate NID.
            using var prefix = new MemoryStream();
            if (boundNid is not null && opts.RequireClientCert)
            {
                if (!await EnforceInlineNidBindingAsync(ssl, boundNid, prefix, ct))
                    return; // NCP-NID-MISMATCH — close without forwarding
            }

            await ProxyToBackendAsync(ssl, prefix.ToArray(), ct);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "L2: connection error (bound_nid={Nid}).", boundNid ?? "<none>");
        }
        finally
        {
            await ssl.DisposeAsync();
        }
    }

    /// <summary>
    /// Reads the native-mode handshake prefix (preamble + frames up to and including the first
    /// IdentFrame) into <paramref name="prefix"/> for later replay, and enforces the §6.3 session
    /// NID binding. Returns false (caller closes the connection) on <c>NCP-NID-MISMATCH</c>.
    /// Scans at most 8 frames before giving up the inline check (the connection still proceeds).
    /// </summary>
    private async Task<bool> EnforceInlineNidBindingAsync(SslStream ssl, string boundNid, MemoryStream prefix, CancellationToken ct)
    {
        var preamble = new byte[NcpPreamble.Length];
        if (!await ReadExactInto(ssl, preamble, ct))
            return true; // EOF before preamble — nothing to bind; let the proxy handle close
        prefix.Write(preamble);

        for (int i = 0; i < 8; i++)
        {
            var frame = await IngressFraming.ReadFrameAsync(ssl, opts.MaxHandshakeFrameBytes, ct);
            if (frame is null) return true; // EOF before an IdentFrame — no binding to enforce

            // Bound the buffered prefix so a flood of small sub-Ident frames cannot grow it without
            // limit before we give up the inline scan.
            if (prefix.Length + frame.Length > opts.MaxHandshakeFrameBytes)
            {
                log.LogWarning("L2: handshake prefix exceeded {Max} bytes before an IdentFrame — closing.",
                    opts.MaxHandshakeFrameBytes);
                return false;
            }
            prefix.Write(frame);

            switch (IngressFraming.ClassifyIdent(frame, out var identNid))
            {
                case IngressFraming.IdentScan.NotIdent:
                    continue; // a pre-Ident handshake frame (e.g. HelloFrame) — keep scanning

                case IngressFraming.IdentScan.Unverifiable:
                    // An IdentFrame was presented but its NID cannot be verified (non-JSON tier or
                    // malformed). Fail the §6.3 binding closed — do NOT proxy it through unchecked.
                    log.LogWarning("L2: {Code} — IdentFrame present but NID unverifiable (non-conformant tier/payload).",
                        NcpL2.NidMismatchCode);
                    return false;

                case IngressFraming.IdentScan.Nid:
                    var bind = NipMtlsValidator.CheckSessionNidBinding(boundNid, identNid!);
                    if (!bind.Ok)
                    {
                        log.LogWarning("L2: {Code} — {Msg}", bind.ErrorCode, bind.Message);
                        return false;
                    }
                    log.LogInformation("L2: session NID binding ok (nid={Nid}).", boundNid);
                    return true;
            }
        }
        // No IdentFrame within the scan window. In the interactive handshake the Ident may not have
        // been sent yet (the client awaits the backend CapsFrame first); proceed and let the NCP
        // backend enforce its own handshake. A pipelined IdentFrame, by contrast, was already
        // checked above.
        return true;
    }

    private static async Task<bool> ReadExactInto(Stream s, byte[] buf, CancellationToken ct)
    {
        int read = 0;
        while (read < buf.Length)
        {
            int n = await s.ReadAsync(buf.AsMemory(read), ct);
            if (n == 0) return false;
            read += n;
        }
        return true;
    }

    private async Task ProxyToBackendAsync(SslStream client, byte[] prefix, CancellationToken ct)
    {
        using var backend = new TcpClient();
        await backend.ConnectAsync(opts.BackendHost, opts.BackendPort, ct);
        var bs = backend.GetStream();
        // Replay the handshake prefix consumed by the inline NID-binding check, then bidirectional
        // copy. The RFC-0001 preamble and all NCP frames travel inside TLS (RFC-0006 §6.2).
        if (prefix.Length > 0)
            await bs.WriteAsync(prefix, ct);

        var clientToBackend = client.CopyToAsync(bs, ct); // request bytes
        var backendToClient = bs.CopyToAsync(client, ct); // response bytes

        // Do NOT tear down as soon as either side finishes (Task.WhenAny truncates the in-flight
        // direction). When the client half-closes, signal the backend and drain its response in
        // full; when the backend closes first, its response is already fully relayed.
        var finished = await Task.WhenAny(clientToBackend, backendToClient);
        if (finished == clientToBackend)
        {
            try { backend.Client.Shutdown(SocketShutdown.Send); } catch { /* backend already gone */ }
            await SwallowAsync(backendToClient); // deliver the remaining response
        }
        await SwallowAsync(clientToBackend);
        await SwallowAsync(backendToClient);
    }

    /// <summary>Awaits a copy task, swallowing the teardown-race exception when a stream is closed.</summary>
    private static async Task SwallowAsync(Task copy)
    {
        try { await copy; }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    private List<X509Certificate2> LoadTrustAnchors()
    {
        var list = new List<X509Certificate2>();
        if (opts.TrustAnchorsDir is null || !Directory.Exists(opts.TrustAnchorsDir))
            return list;
        foreach (var file in Directory.EnumerateFiles(opts.TrustAnchorsDir))
        {
            try { list.Add(X509CertificateLoader.LoadCertificateFromFile(file)); }
            catch (Exception ex) { log.LogWarning(ex, "L2: skipping unreadable trust anchor {File}.", file); }
        }
        return list;
    }
}
