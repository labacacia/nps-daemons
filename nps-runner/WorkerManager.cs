// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

namespace NPS.Daemon.Runner;

/// <summary>
/// Enforces the concurrent-worker cap and fires off worker tasks asynchronously.
/// Each worker owns its inbox message until it exits, then acks and optionally notifies.
/// </summary>
internal sealed class WorkerManager(RunnerOptions opts, NpsdClient client, ILogger<WorkerManager> log)
{
    private readonly SemaphoreSlim _slots =
        new(opts.MaxConcurrentWorkers, opts.MaxConcurrentWorkers);

    public bool HasCapacity => _slots.CurrentCount > 0;

    /// <summary>
    /// Acquires a worker slot (non-blocking) and fires the worker task.
    /// Returns false if the concurrency cap is already reached
    /// (message should remain unacked for the next poll cycle).
    /// </summary>
    public bool TrySpawn(SpawnSpec spec, string runnerNid, string messageId, CancellationToken ct)
    {
        if (!_slots.Wait(0))
            return false;

        _ = RunWorkerAsync(spec, runnerNid, messageId, ct);
        return true;
    }

    private async Task RunWorkerAsync(SpawnSpec spec, string runnerNid, string messageId, CancellationToken ct)
    {
        var logPath   = Path.Combine(opts.LogDir, $"{spec.TaskId}.log");
        var startedAt = DateTimeOffset.UtcNow;
        var worker    = new WorkerProcess(spec, logPath, log);

        int?    exitCode    = null;
        string? killedReason = null;

        try
        {
            (exitCode, killedReason) = await worker.RunAsync(ct);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Worker {TaskId}: unhandled exception in RunAsync", spec.TaskId);
            killedReason = "exception";
        }
        finally
        {
            _slots.Release();

            // Ack the inbox message now that the worker has finished.
            try   { await client.AckAsync(runnerNid, messageId, CancellationToken.None); }
            catch (Exception ex) { log.LogWarning(ex, "Worker {TaskId}: ack failed (message_id={MsgId})", spec.TaskId, messageId); }

            // Best-effort completion notification.
            if (spec.ReplyTo is not null)
            {
                var note = new CompletionNotification
                {
                    TaskId       = spec.TaskId,
                    ExitCode     = exitCode,
                    KilledReason = killedReason,
                    LogPath      = logPath,
                    StartedAt    = startedAt.ToString("O"),
                    FinishedAt   = DateTimeOffset.UtcNow.ToString("O"),
                };
                try   { await client.NotifyAsync(spec.ReplyTo, note, CancellationToken.None); }
                catch (Exception ex) { log.LogWarning(ex, "Worker {TaskId}: completion notify failed (reply_to={ReplyTo})", spec.TaskId, spec.ReplyTo); }
            }
        }
    }
}
