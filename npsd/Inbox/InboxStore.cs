// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;

namespace NPS.Daemon.Npsd.Inbox;

/// <summary>
/// In-memory per-NID inbox store with long-poll semantics.
/// </summary>
/// <remarks>
/// Phase 1 (alpha.3): in-memory only. Messages are lost on restart.
/// Persistence (LMDB / SQLite) lands at alpha.4 alongside the NCP
/// native-mode preamble runtime, since both share the same delivery
/// pipeline.
/// </remarks>
public sealed class InboxStore : IDisposable
{
    private readonly ConcurrentDictionary<string, NidInbox> _byNid = new();

    /// <summary>Per-NID monotonic message-id counter.</summary>
    private long _nextMessageId;

    /// <summary>Append a message to the recipient's inbox. Returns the assigned message id.</summary>
    /// <exception cref="InboxFullException">If the inbox is at <c>maxDepth</c> already.</exception>
    public ulong Enqueue(string nid, byte[] payload, string contentType, int priority, TimeSpan ttl, int maxDepth)
    {
        var inbox = _byNid.GetOrAdd(nid, _ => new NidInbox());

        var msg = new InboxMessage
        {
            MessageId   = (ulong)Interlocked.Increment(ref _nextMessageId),
            Nid         = nid,
            EnqueuedAt  = DateTimeOffset.UtcNow,
            ExpiresAt   = DateTimeOffset.UtcNow.Add(ttl),
            Priority    = priority,
            Payload     = payload,
            ContentType = contentType,
        };

        lock (inbox.Lock)
        {
            // Drain expired tail before depth check.
            inbox.PurgeExpiredLocked();

            if (inbox.Messages.Count >= maxDepth)
                throw new InboxFullException(nid, maxDepth);

            inbox.Messages.Add(msg);
            // Keep messages ordered by (priority desc, enqueued_at asc).
            inbox.Messages.Sort((a, b) =>
            {
                var p = b.Priority.CompareTo(a.Priority);
                return p != 0 ? p : a.EnqueuedAt.CompareTo(b.EnqueuedAt);
            });
        }

        // Wake any pending pollers for this nid.
        inbox.Signal.Release();

        return msg.MessageId;
    }

    /// <summary>Drains up to <paramref name="batchSize"/> messages without removing them.
    /// Long-polls for up to <paramref name="wait"/> if the inbox is empty. Returns an
    /// empty array on timeout. Callers must call <see cref="Ack"/> after processing.</summary>
    public async Task<IReadOnlyList<InboxMessage>> PeekAsync(
        string            nid,
        int               batchSize,
        TimeSpan          wait,
        CancellationToken ct = default)
    {
        var inbox = _byNid.GetOrAdd(nid, _ => new NidInbox());

        // Fast path — already non-empty.
        var snap = SnapshotNonExpired(inbox, batchSize);
        if (snap.Count > 0) return snap;

        if (wait <= TimeSpan.Zero) return Array.Empty<InboxMessage>();

        // Long-poll: race a SemaphoreSlim wait against the wait timeout.
        try
        {
            // Drain any stale signal first.
            while (inbox.Signal.CurrentCount > 0) inbox.Signal.Wait(0);
            await inbox.Signal.WaitAsync(wait, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }

        return SnapshotNonExpired(inbox, batchSize);
    }

    /// <summary>
    /// Removes a message by id. Returns false if the message has already been
    /// acked or never existed. Idempotent on repeated calls.
    /// </summary>
    public bool Ack(string nid, ulong messageId)
    {
        if (!_byNid.TryGetValue(nid, out var inbox)) return false;
        lock (inbox.Lock)
        {
            for (int i = 0; i < inbox.Messages.Count; i++)
            {
                if (inbox.Messages[i].MessageId == messageId)
                {
                    inbox.Messages.RemoveAt(i);
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>Current number of pending (non-expired) messages for the given NID.</summary>
    public int Depth(string nid)
    {
        if (!_byNid.TryGetValue(nid, out var inbox)) return 0;
        lock (inbox.Lock)
        {
            inbox.PurgeExpiredLocked();
            return inbox.Messages.Count;
        }
    }

    private static IReadOnlyList<InboxMessage> SnapshotNonExpired(NidInbox inbox, int batchSize)
    {
        lock (inbox.Lock)
        {
            inbox.PurgeExpiredLocked();
            var n = Math.Min(batchSize, inbox.Messages.Count);
            if (n == 0) return Array.Empty<InboxMessage>();
            var arr = new InboxMessage[n];
            inbox.Messages.CopyTo(0, arr, 0, n);
            return arr;
        }
    }

    public void Dispose()
    {
        foreach (var inbox in _byNid.Values) inbox.Signal.Dispose();
    }

    private sealed class NidInbox
    {
        public readonly object        Lock     = new();
        public readonly List<InboxMessage> Messages = new();
        public readonly SemaphoreSlim Signal   = new(0);

        public void PurgeExpiredLocked()
        {
            var now = DateTimeOffset.UtcNow;
            for (int i = Messages.Count - 1; i >= 0; i--)
                if (Messages[i].ExpiresAt <= now)
                    Messages.RemoveAt(i);
        }
    }
}

/// <summary>Thrown when enqueueing into an inbox at <c>maxDepth</c> capacity.</summary>
public sealed class InboxFullException(string nid, int maxDepth) :
    Exception($"Inbox is full for {nid} (max depth {maxDepth})")
{
    public string Nid      { get; } = nid;
    public int    MaxDepth { get; } = maxDepth;
}
