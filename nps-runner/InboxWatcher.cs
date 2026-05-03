// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NPS.Daemon.Runner;

/// <summary>
/// Background service that:
/// 1. Self-registers with the local npsd at startup (idempotent).
/// 2. Long-polls the runner's inbox for spawn-spec messages.
/// 3. Dispatches each message to <see cref="WorkerManager"/>.
/// </summary>
internal sealed class InboxWatcher(
    RunnerOptions opts,
    NpsdClient    client,
    WorkerManager workers,
    ILogger<InboxWatcher> log) : BackgroundService
{
    private static readonly JsonSerializerOptions s_json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var runnerNid = await RegisterWithRetryAsync(ct);
        log.LogInformation(
            "nps-runner ready — NID={RunnerNid}  npsd={NpsdUrl}  max_workers={MaxWorkers}  log_dir={LogDir}",
            runnerNid, opts.NpsdUrl, opts.MaxConcurrentWorkers, opts.LogDir);

        // long-poll waits up to PollIntervalMs; minimum 1 s to avoid tight loops.
        var waitSec = Math.Max(1, opts.PollIntervalMs / 1000);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var msgs = await client.PollAsync(runnerNid, waitSec, batch: 16, ct);

                foreach (var msg in msgs)
                {
                    if (!string.Equals(msg.ContentType, "application/json",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        log.LogWarning(
                            "Inbox message {MsgId}: unsupported content_type={CT} — acking and skipping",
                            msg.MessageId, msg.ContentType);
                        await AckSafeAsync(runnerNid, msg.MessageId, ct);
                        continue;
                    }

                    SpawnSpec? spec;
                    try
                    {
                        var json = Encoding.UTF8.GetString(Convert.FromBase64String(msg.PayloadB64));
                        spec = JsonSerializer.Deserialize<SpawnSpec>(json, s_json);
                    }
                    catch (Exception ex)
                    {
                        log.LogWarning(ex,
                            "Inbox message {MsgId}: failed to parse spawn spec — acking and skipping",
                            msg.MessageId);
                        await AckSafeAsync(runnerNid, msg.MessageId, ct);
                        continue;
                    }

                    if (spec is null || string.IsNullOrWhiteSpace(spec.Command))
                    {
                        log.LogWarning(
                            "Inbox message {MsgId}: null or missing `command` — acking and skipping",
                            msg.MessageId);
                        await AckSafeAsync(runnerNid, msg.MessageId, ct);
                        continue;
                    }

                    if (!workers.TrySpawn(spec, runnerNid, msg.MessageId, ct))
                    {
                        // Concurrency cap reached; leave message unacked so it re-appears next poll.
                        log.LogDebug(
                            "Inbox message {MsgId} (task={TaskId}): concurrency cap reached, will retry",
                            msg.MessageId, spec.TaskId);
                        break;
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                log.LogError(ex, "Inbox poll error — retrying in {PollMs} ms", opts.PollIntervalMs);
                try { await Task.Delay(opts.PollIntervalMs, ct); }
                catch (OperationCanceledException) { break; }
            }
        }

        log.LogInformation("nps-runner shutting down");
    }

    private async Task<string> RegisterWithRetryAsync(CancellationToken ct)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await client.EnsureRegisteredAsync(ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (attempt < 20)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(30, attempt * 2));
                log.LogWarning(ex,
                    "Registration attempt {Attempt} failed — retrying in {Delay}",
                    attempt, delay);
                await Task.Delay(delay, ct);
            }
        }
    }

    private async Task AckSafeAsync(string nid, string messageId, CancellationToken ct)
    {
        try   { await client.AckAsync(nid, messageId, ct); }
        catch (Exception ex) { log.LogWarning(ex, "Ack failed for message {MsgId}", messageId); }
    }
}
