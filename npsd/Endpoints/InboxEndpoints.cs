// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NPS.Daemon.Npsd.Inbox;
using NPS.Daemon.Npsd.SubNids;

namespace NPS.Daemon.Npsd.Endpoints;

/// <summary>
/// Per-NID inbox endpoints — deposit, long-poll, ack, depth.
/// </summary>
public static class InboxEndpoints
{
    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        PropertyNamingPolicy   = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static IEndpointRouteBuilder MapInboxEndpoints(this IEndpointRouteBuilder app)
    {
        // POST /v1/inbox/{nid} — deposit a message addressed to {nid}.
        // Body is the raw payload; size capped by NpsdOptions.MaxInboxMessageBytes.
        // Headers:
        //   Content-Type:           stored verbatim and returned to the consumer
        //   X-Nps-Inbox-Priority:   integer, default 0
        //   X-Nps-Inbox-Ttl-Seconds: integer, default 600 (10 min)
        app.MapPost("/v1/inbox/{nid}",
            async (string nid, HttpContext ctx, InboxStore inbox, SubNidService nids, NpsdOptions opts) =>
        {
            // Recipient must exist on this host and not be revoked.
            var rec = nids.Get(nid);
            if (rec is null) return ProblemNotFound(nid);
            if (rec.Revoked) return ProblemRevoked(nid);

            // Read with cap.
            ctx.Request.EnableBuffering();
            using var ms = new MemoryStream();
            var copyBuf = new byte[8 * 1024];
            int read;
            while ((read = await ctx.Request.Body.ReadAsync(copyBuf, ctx.RequestAborted)) > 0)
            {
                if (ms.Length + read > opts.MaxInboxMessageBytes)
                    return Results.Json(new
                    {
                        error   = "NWP-PAYLOAD-TOO-LARGE",
                        status  = "NPS-CLIENT-PAYLOAD-TOO-LARGE",
                        message = $"Payload exceeds the per-message cap of {opts.MaxInboxMessageBytes} bytes.",
                    }, s_jsonOpts, statusCode: 413);
                await ms.WriteAsync(copyBuf.AsMemory(0, read));
            }

            var payload     = ms.ToArray();
            var contentType = ctx.Request.Headers.ContentType.ToString();
            if (string.IsNullOrEmpty(contentType)) contentType = "application/octet-stream";

            var priority = 0;
            if (ctx.Request.Headers.TryGetValue("X-Nps-Inbox-Priority", out var pStr) &&
                int.TryParse(pStr.ToString(), out var pp))
                priority = pp;

            var ttlSec = 600;
            if (ctx.Request.Headers.TryGetValue("X-Nps-Inbox-Ttl-Seconds", out var tStr) &&
                int.TryParse(tStr.ToString(), out var ts) && ts > 0)
                ttlSec = ts;

            try
            {
                var id = inbox.Enqueue(
                    nid:          nid,
                    payload:      payload,
                    contentType:  contentType,
                    priority:     priority,
                    ttl:          TimeSpan.FromSeconds(ttlSec),
                    maxDepth:     opts.MaxInboxDepthPerNid);

                return Results.Json(new
                {
                    message_id = id.ToString(),
                    nid,
                    enqueued_at = DateTimeOffset.UtcNow.ToString("O"),
                    expires_at  = DateTimeOffset.UtcNow.AddSeconds(ttlSec).ToString("O"),
                }, s_jsonOpts, statusCode: 201);
            }
            catch (InboxFullException)
            {
                return Results.Json(new
                {
                    error   = "NWP-RATE-LIMIT-INBOX-FULL",
                    status  = "NPS-CLIENT-RATE-LIMITED",
                    message = $"Inbox is full for {nid} (max depth {opts.MaxInboxDepthPerNid}).",
                    nid,
                }, s_jsonOpts, statusCode: 429);
            }
        });

        // GET /v1/inbox/{nid}?wait=N&batch=B — long-poll for messages.
        app.MapGet("/v1/inbox/{nid}",
            async (string nid, int? wait, int? batch, HttpContext ctx, InboxStore inbox, SubNidService nids, NpsdOptions opts) =>
        {
            var rec = nids.Get(nid);
            if (rec is null) return ProblemNotFound(nid);
            if (rec.Revoked) return ProblemRevoked(nid);

            var waitSec = Math.Clamp(wait ?? 0, 0, opts.MaxInboxWaitSeconds);
            var batchSize = Math.Clamp(batch ?? 16, 1, 256);
            var msgs = await inbox.PeekAsync(nid, batchSize, TimeSpan.FromSeconds(waitSec), ctx.RequestAborted);

            return Results.Json(new
            {
                nid,
                count    = msgs.Count,
                messages = msgs.Select(m => new
                {
                    message_id   = m.MessageId.ToString(),
                    enqueued_at  = m.EnqueuedAt.ToString("O"),
                    expires_at   = m.ExpiresAt.ToString("O"),
                    priority     = m.Priority,
                    content_type = m.ContentType,
                    payload_b64  = Convert.ToBase64String(m.Payload),
                }).ToArray(),
            }, s_jsonOpts);
        });

        // DELETE /v1/inbox/{nid}/{messageId} — ack a message.
        app.MapDelete("/v1/inbox/{nid}/{messageId:long}",
            (string nid, long messageId, InboxStore inbox, SubNidService nids) =>
        {
            var rec = nids.Get(nid);
            if (rec is null) return ProblemNotFound(nid);

            // Cast safely — we use ulong internally; route matches long which
            // can't be negative for our generated ids.
            return inbox.Ack(nid, (ulong)messageId)
                ? Results.NoContent()
                : Results.Json(new
                {
                    error      = "NWP-INBOX-MESSAGE-NOT-FOUND",
                    status     = "NPS-CLIENT-NOT-FOUND",
                    nid,
                    message_id = messageId.ToString(),
                    message    = "Already acked, or never enqueued.",
                }, s_jsonOpts, statusCode: 404);
        });

        // GET /v1/inbox/{nid}/depth — current pending count for the NID.
        app.MapGet("/v1/inbox/{nid}/depth",
            (string nid, InboxStore inbox, SubNidService nids) =>
        {
            var rec = nids.Get(nid);
            if (rec is null) return ProblemNotFound(nid);

            return Results.Json(new { nid, depth = inbox.Depth(nid) }, s_jsonOpts);
        });

        return app;
    }

    private static IResult ProblemNotFound(string nid) =>
        Results.Json(new
        {
            error   = "NIP-NID-NOT-FOUND",
            status  = "NPS-CLIENT-NOT-FOUND",
            nid,
            message = "Sub-NID not issued on this host.",
        }, s_jsonOpts, statusCode: 404);

    private static IResult ProblemRevoked(string nid) =>
        Results.Json(new
        {
            error   = "NIP-NID-REVOKED",
            status  = "NPS-AUTH-FORBIDDEN",
            nid,
            message = "Sub-NID has been revoked.",
        }, s_jsonOpts, statusCode: 403);
}
