// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0
//
// nps-registry — NPS Daemon, Layer 2 (cross-machine NDP discovery).
// See docs/daemons/architecture.md for the role this binary plays.
//
// Phase 1 (v1.0-alpha.3): listens on the NDP optional dedicated port
// 17436; resolve/graph endpoints return NDP-REGISTRY-UNAVAILABLE so
// callers can already wire against the URL surface, but real
// registration storage lands in alpha.4 (SQLite-backed) and L2
// federation lands in alpha.5+.

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    var port = int.TryParse(Environment.GetEnvironmentVariable("NPSREGISTRY_PORT"), out var p) ? p : 17436;
    var host = Environment.GetEnvironmentVariable("NPSREGISTRY_HOST") ?? "0.0.0.0";
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
    daemon         = "nps-registry",
    version        = "1.0.0-alpha.3",
    layer          = 2,
    role           = "cross-machine NDP discovery (skeleton)",
    phase          = 1,
    spec_reference = "spec/NPS-4-NDP.md",
}));

// Phase-1 placeholder Resolve / Graph endpoints. They speak the URL
// shape but return NDP-REGISTRY-UNAVAILABLE so consumers can already
// wire against this daemon and gracefully fall back. Real registration
// + storage land in alpha.4.
IResult NdpUnavailable() => Results.Json(new
{
    error   = "NDP-REGISTRY-UNAVAILABLE",
    status  = "NPS-SERVER-UNAVAILABLE",
    message = "nps-registry alpha.3 ships as a skeleton; SQLite-backed registration + Resolve/Graph land in alpha.4. See docs/daemons/architecture.md.",
}, statusCode: 503);

app.MapGet("/v1/resolve", NdpUnavailable);
app.MapGet("/v1/graph",   NdpUnavailable);
app.MapPost("/v1/announce", NdpUnavailable);

app.Logger.LogInformation("nps-registry v1.0.0-alpha.3 starting on port 17436 (Phase 1 skeleton — SQLite registry lands at alpha.4; see docs/daemons/architecture.md)");
app.Run();
