// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0
//
// nps-ingress — NPS Daemon, Layer 2 (Internet ingress).
// See docs/daemons/architecture.md for the role this binary plays.
//
// Phase 1 (v1.0-alpha.3): public-facing HTTP listener with /health
// only. TLS termination, rate limit, NeuronHub auth, CGN debit, and
// reputation policy lookup all land in alpha.4 onwards.
//
// Naming note: this is the *process* called "nps-ingress", distinct from
// the spec-level role of cluster control plane which is now called
// **Anchor Node** in NWP (NPS-CR-0001). This process MAY host an
// Anchor Node middleware via NPS.NWP.Anchor; that wiring lands in
// alpha.4.

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    var port = int.TryParse(Environment.GetEnvironmentVariable("NPSINGRESS_PORT"), out var p) ? p : 8080;
    var host = Environment.GetEnvironmentVariable("NPSINGRESS_HOST") ?? "0.0.0.0";
    if (host == "0.0.0.0")
    {
        options.ListenAnyIP(port);
    }
    else
    {
        options.ListenLocalhost(port);
    }
});

var app = builder.Build();

app.MapGet("/health", () => Results.Json(new
{
    status         = "ok",
    daemon         = "nps-ingress",
    version        = "1.0.0-alpha.11",
    layer          = 2,
    role           = "Internet ingress (skeleton)",
    phase          = 1,
    spec_reference = "docs/daemons/architecture.md",
    todo           = new[]
    {
        "TLS termination (alpha.4)",
        "rate limiting (alpha.4)",
        "NeuronHub-customer authentication (alpha.4)",
        "CGN debit trigger (alpha.4)",
        "NPS-RFC-0004 reputation policy lookup (alpha.5)",
        "NPS.NWP.Anchor middleware wiring (alpha.4)",
    },
}));

app.Logger.LogInformation("nps-ingress v1.0.0-alpha.11 starting on configured port (Phase 1 skeleton — TLS / auth / CGN / reputation land at alpha.11+; see docs/daemons/architecture.md)");
app.Run();
