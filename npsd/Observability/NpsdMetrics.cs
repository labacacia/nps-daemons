// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NPS.Daemon.Observability.Metrics;

namespace NPS.Daemon.Npsd.Observability;

/// <summary>
/// Holds the npsd-specific counters and gauges that the metrics middleware
/// updates per request. Resolved from DI as a singleton.
/// </summary>
public sealed class NpsdMetrics
{
    public MetricsRegistry.Gauge   ActiveConnections   { get; }
    public MetricsRegistry.Counter FramesProcessed     { get; }

    public NpsdMetrics(MetricsRegistry reg)
    {
        ActiveConnections = reg.RegisterGauge(
            "nps_active_connections",
            "Currently in-flight HTTP requests handled by npsd.");
        FramesProcessed = reg.RegisterCounter(
            "nps_frames_processed_total",
            "Total NPS frame-equivalent requests processed, labelled by frame_type.",
            "frame_type");
    }

    /// <summary>
    /// Best-effort mapping from request method + path template to the
    /// canonical NPS frame type that the route represents. Returns
    /// <c>null</c> for non-frame routes (health/metrics/.nwm) which should
    /// not increment the frame counter.
    /// </summary>
    public static string? ClassifyFrameType(string method, string path)
    {
        if (path.StartsWith("/v1/agents", StringComparison.Ordinal))
        {
            if (path.EndsWith("/revoke", StringComparison.Ordinal)) return "sub_nid_revoke";
            return method == "GET" ? "sub_nid_query" : "sub_nid_issue";
        }
        if (path.StartsWith("/v1/inbox", StringComparison.Ordinal))
        {
            if (path.EndsWith("/depth", StringComparison.Ordinal)) return "inbox_depth";
            return method switch
            {
                "POST"   => "inbox_deposit",
                "GET"    => "inbox_long_poll",
                "DELETE" => "inbox_ack",
                _        => "inbox_other",
            };
        }
        return null;
    }
}

/// <summary>
/// Increments the active-connections gauge and per-request frame counter
/// for every npsd HTTP request. Health, readiness, metrics, and manifest
/// routes are excluded so that scraping doesn't pollute the frame counter.
/// </summary>
public sealed class NpsdMetricsMiddleware
{
    private static readonly HashSet<string> s_excluded = new(StringComparer.Ordinal)
    {
        "/healthz", "/readyz", "/metrics", "/health", "/.nwm",
    };

    private readonly RequestDelegate _next;
    private readonly NpsdMetrics     _metrics;

    public NpsdMetricsMiddleware(RequestDelegate next, NpsdMetrics metrics)
    {
        _next    = next;
        _metrics = metrics;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value ?? "";
        if (s_excluded.Contains(path))
        {
            await _next(ctx);
            return;
        }

        _metrics.ActiveConnections.Inc();
        try
        {
            await _next(ctx);
            var frameType = NpsdMetrics.ClassifyFrameType(ctx.Request.Method, path);
            if (frameType is not null)
                _metrics.FramesProcessed.Inc(frameType);
        }
        finally
        {
            _metrics.ActiveConnections.Dec();
        }
    }
}
