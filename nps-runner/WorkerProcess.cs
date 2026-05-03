// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace NPS.Daemon.Runner;

/// <summary>
/// Manages the lifecycle of a single spawned worker subprocess.
/// </summary>
internal sealed class WorkerProcess(SpawnSpec spec, string logPath, ILogger log)
{
    public string TaskId => spec.TaskId;

    /// <summary>
    /// Starts the process and waits for it to exit.
    /// Returns the exit code, or null if the process was killed.
    /// Also returns the kill reason (null = clean exit).
    /// </summary>
    public async Task<(int? ExitCode, string? KilledReason)> RunAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

        var psi = new ProcessStartInfo
        {
            FileName               = spec.Command,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            RedirectStandardInput  = false,
            WorkingDirectory       = spec.WorkDir ?? Directory.GetCurrentDirectory(),
        };
        foreach (var arg in spec.Args)
            psi.ArgumentList.Add(arg);
        foreach (var (key, val) in spec.Env)
            psi.Environment[key] = val;

        await using var logWriter = new StreamWriter(logPath, append: false, System.Text.Encoding.UTF8)
        {
            AutoFlush = true,
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var exitTcs        = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var startedAt      = DateTimeOffset.UtcNow;
        // Store last-output time as UTC ticks for lock-free access across threads.
        long lastOutputTicks = startedAt.UtcTicks;
        var killReason     = (string?)null;

        process.Exited += (_, _) => exitTcs.TrySetResult(process.ExitCode);
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                logWriter.WriteLine($"[stdout] {e.Data}");
                Interlocked.Exchange(ref lastOutputTicks, DateTimeOffset.UtcNow.UtcTicks);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                logWriter.WriteLine($"[stderr] {e.Data}");
                Interlocked.Exchange(ref lastOutputTicks, DateTimeOffset.UtcNow.UtcTicks);
            }
        };

        log.LogInformation("Worker {TaskId}: spawning {Command} {Args}",
            spec.TaskId, spec.Command, string.Join(" ", spec.Args));
        logWriter.WriteLine($"[runner] spawned at {startedAt:O}  command={spec.Command}  args={string.Join(" ", spec.Args)}");

        if (!process.Start())
            throw new InvalidOperationException($"Failed to start process: {spec.Command}");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var maxRuntime = spec.MaxRuntimeSeconds.HasValue
            ? TimeSpan.FromSeconds(spec.MaxRuntimeSeconds.Value)
            : TimeSpan.FromHours(4);
        var deadline   = startedAt + maxRuntime;

        // Monitor loop: checks idle and max-runtime every 5 s.
        using var monitorCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var monitorTask = Task.Run(async () =>
        {
            while (!exitTcs.Task.IsCompleted)
            {
                try { await Task.Delay(5_000, monitorCts.Token); }
                catch (OperationCanceledException) { return; }

                if (DateTimeOffset.UtcNow >= deadline)
                {
                    logWriter.WriteLine("[runner] max_runtime_seconds exceeded — killing");
                    log.LogWarning("Worker {TaskId}: max runtime exceeded, killing", spec.TaskId);
                    killReason = "max_runtime";
                    KillSafe(process);
                    return;
                }

                if (spec.IdleTimeoutSeconds is { } idleSec)
                {
                    var lastTicks = Interlocked.Read(ref lastOutputTicks);
                    var idleFor   = (DateTimeOffset.UtcNow - new DateTimeOffset(lastTicks, TimeSpan.Zero)).TotalSeconds;
                    if (idleFor >= idleSec)
                    {
                        logWriter.WriteLine($"[runner] idle_timeout_seconds={idleSec} exceeded ({idleFor:F0}s since last output) — killing");
                        log.LogWarning("Worker {TaskId}: idle timeout ({IdleSec}s), killing", spec.TaskId, idleSec);
                        killReason = "idle_timeout";
                        KillSafe(process);
                        return;
                    }
                }
            }
        }, monitorCts.Token);

        int? exitCode;
        try
        {
            exitCode = await exitTcs.Task.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            logWriter.WriteLine("[runner] shutdown signal — killing");
            killReason = "shutdown";
            KillSafe(process);
            exitCode   = null;
        }
        finally
        {
            await monitorCts.CancelAsync();
        }

        try   { await monitorTask; }
        catch (OperationCanceledException) { /* expected on clean exit */ }

        logWriter.WriteLine($"[runner] finished at {DateTimeOffset.UtcNow:O}  exit_code={exitCode}  killed={killReason ?? "none"}");
        log.LogInformation("Worker {TaskId}: exit_code={ExitCode} killed={KilledReason}",
            spec.TaskId, exitCode, killReason ?? "none");

        return (exitCode, killReason);
    }

    private static void KillSafe(Process p)
    {
        try { p.Kill(entireProcessTree: true); }
        catch { /* already gone */ }
    }
}
