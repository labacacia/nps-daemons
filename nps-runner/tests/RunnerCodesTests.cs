// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using NPS.Daemon.Runner;
using Xunit;

namespace NPS.Daemon.Runner.Tests;

/// <summary>Tests for the NPS-CR-0007 §6 runtime code / terminal-state mapping.</summary>
public sealed class RunnerCodesTests
{
    [Theory]
    [InlineData("idle_timeout", "NOP-RUNTIME-IDLE-TIMEOUT")]
    [InlineData("max_runtime", "NOP-RUNTIME-MAX-RUNTIME")]
    [InlineData("shutdown", null)]
    [InlineData("exception", null)]
    [InlineData(null, null)]
    public void MapKilledReason_maps_lifecycle_kills_to_nop_runtime_codes(string? reason, string? expected)
    {
        Assert.Equal(expected, RunnerCodes.MapKilledReason(reason));
    }

    [Theory]
    [InlineData(0, null, "COMPLETED")]      // clean exit
    [InlineData(1, null, "FAILED")]         // non-zero exit
    [InlineData(0, "idle_timeout", "FAILED")] // killed despite exit 0 race
    [InlineData(null, "max_runtime", "FAILED")]
    public void NodeState_maps_clean_exit_to_completed_else_failed(int? exitCode, string? reason, string expected)
    {
        Assert.Equal(expected, RunnerCodes.NodeState(exitCode, reason));
    }
}
