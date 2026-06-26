// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace NPS.Daemon.Runner;

/// <summary>
/// Posted to <see cref="SpawnSpec.ReplyTo"/> when a worker process exits.
/// Content-Type: application/json.
/// </summary>
internal sealed record CompletionNotification
{
    [JsonPropertyName("task_id")]
    public required string TaskId { get; init; }

    /// <summary>Process exit code; null if the process was killed or never started.</summary>
    [JsonPropertyName("exit_code")]
    public int? ExitCode { get; init; }

    /// <summary>"idle_timeout" | "max_runtime" | "shutdown" | "exception" | null (clean exit).</summary>
    [JsonPropertyName("killed_reason")]
    public string? KilledReason { get; init; }

    /// <summary>
    /// NPS-CR-0007 §6 error code when the worker was killed by a lifecycle limit
    /// (<c>NOP-RUNTIME-IDLE-TIMEOUT</c> / <c>NOP-RUNTIME-MAX-RUNTIME</c>); null otherwise.
    /// </summary>
    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; init; }

    /// <summary>Terminal node state for the orchestrator task store: <c>COMPLETED</c> / <c>FAILED</c> (NPS-5 §5).</summary>
    [JsonPropertyName("node_state")]
    public string? NodeState { get; init; }

    /// <summary>Absolute path of the per-worker log file (stdout + stderr).</summary>
    [JsonPropertyName("log_path")]
    public string? LogPath { get; init; }

    [JsonPropertyName("started_at")]
    public required string StartedAt { get; init; }

    [JsonPropertyName("finished_at")]
    public required string FinishedAt { get; init; }
}
