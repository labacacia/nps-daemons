// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace NPS.Daemon.Runner;

/// <summary>
/// Spawn specification embedded in an inbox message body
/// (Content-Type: application/json).
/// </summary>
internal sealed record SpawnSpec
{
    /// <summary>Caller-supplied task ID; nps-runner generates one if absent.</summary>
    [JsonPropertyName("task_id")]
    public string TaskId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Optional NID that receives a <see cref="CompletionNotification"/> when
    /// the worker exits.  Must be a sub-NID registered on the local npsd.
    /// </summary>
    [JsonPropertyName("reply_to")]
    public string? ReplyTo { get; init; }

    /// <summary>Executable name or full path.</summary>
    [JsonPropertyName("command")]
    public required string Command { get; init; }

    /// <summary>Positional arguments passed verbatim.</summary>
    [JsonPropertyName("args")]
    public IReadOnlyList<string> Args { get; init; } = Array.Empty<string>();

    /// <summary>Working directory for the spawned process.  Defaults to CWD of nps-runner.</summary>
    [JsonPropertyName("work_dir")]
    public string? WorkDir { get; init; }

    /// <summary>Extra environment variables merged on top of the inherited environment.</summary>
    [JsonPropertyName("env")]
    public IReadOnlyDictionary<string, string> Env { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Kill the worker after this many seconds of no stdout/stderr output.
    /// Null = no idle timeout.
    /// </summary>
    [JsonPropertyName("idle_timeout_seconds")]
    public int? IdleTimeoutSeconds { get; init; }

    /// <summary>
    /// Hard wall-clock limit.  Worker is killed after this many seconds
    /// regardless of activity.  Null = 4-hour ceiling.
    /// </summary>
    [JsonPropertyName("max_runtime_seconds")]
    public int? MaxRuntimeSeconds { get; init; }
}
