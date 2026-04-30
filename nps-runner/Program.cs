// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0
//
// nps-runner — NPS Daemon, Layer 1 (host-local task scheduler / FaaS runtime).
// See docs/daemons/architecture.md for the role this binary plays.
//
// Phase 1 (v1.0-alpha.3): Generic Host scaffolding only. The inbox-watch
// + spawn-spec resolver lands at L3 stage (alpha.5+) per the daemon
// architecture phasing table.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<RunnerHeartbeat>();

await builder.Build().RunAsync();

/// <summary>
/// Phase 1 placeholder: prints a heartbeat every 30 s confirming the
/// daemon is alive. Replaced with the inbox watcher in alpha.5.
/// </summary>
internal sealed class RunnerHeartbeat : BackgroundService
{
    private readonly ILogger<RunnerHeartbeat> _log;
    public RunnerHeartbeat(ILogger<RunnerHeartbeat> log) => _log = log;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _log.LogInformation(
            "nps-runner v1.0.0-alpha.3 starting (Phase 1 skeleton — inbox watcher + spawn-spec resolver land at alpha.5; see docs/daemons/architecture.md)");

        var period = TimeSpan.FromSeconds(30);
        while (!ct.IsCancellationRequested)
        {
            _log.LogInformation("nps-runner heartbeat — Phase 1 skeleton, no work to do yet");
            try
            {
                await Task.Delay(period, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _log.LogInformation("nps-runner shutting down");
    }
}
