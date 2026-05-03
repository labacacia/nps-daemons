// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Data.Sqlite;
using NPS.NDP.Frames;
using NPS.NDP.Registry;

namespace NPS.Daemon.Registry;

/// <summary>
/// SQLite-backed implementation of <see cref="INdpRegistry"/> for nps-registry.
///
/// <para>TTL expiry is evaluated lazily on every read (no background timer).
/// Expired entries are purged during <see cref="GetAll"/> and <see cref="Resolve"/> calls.</para>
///
/// <para>Schema:
/// <code>
/// CREATE TABLE announcements (
///   nid            TEXT PRIMARY KEY,
///   addresses_json TEXT NOT NULL,
///   caps_json      TEXT NOT NULL,
///   node_type      TEXT,
///   ttl            INTEGER NOT NULL,
///   timestamp      TEXT NOT NULL,
///   signature      TEXT NOT NULL,
///   expires_at     TEXT NOT NULL   -- ISO-8601 UTC; used for lazy eviction
/// );
/// CREATE TABLE graph_meta (
///   id  INTEGER PRIMARY KEY CHECK (id = 1),
///   seq INTEGER NOT NULL DEFAULT 0
/// );
/// </code>
/// </para>
/// </summary>
public sealed class SqliteNdpRegistry : INdpRegistry, IDisposable
{
    private readonly string            _connStr;
    private readonly SqliteConnection? _keepAlive;

    private static readonly JsonSerializerOptions _json =
        new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public SqliteNdpRegistry(string sqlitePath)
    {
        _connStr = $"Data Source={sqlitePath};Cache=Shared";
        EnsureSchema();
    }

    public static SqliteNdpRegistry CreateInMemory()
    {
        var name      = $"ndp-registry-{Guid.NewGuid():N}";
        var connStr   = $"Data Source=file:{name}?mode=memory&cache=shared";
        var keepAlive = new SqliteConnection(connStr);
        keepAlive.Open();
        return new SqliteNdpRegistry(connStr, keepAlive);
    }

    private SqliteNdpRegistry(string connStr, SqliteConnection keepAlive)
    {
        _connStr   = connStr;
        _keepAlive = keepAlive;
        EnsureSchema();
    }

    public void Dispose() => _keepAlive?.Dispose();

    // ── INdpRegistry ──────────────────────────────────────────────────────────

    public void Announce(AnnounceFrame frame)
    {
        using var conn = Open();
        if (frame.Ttl == 0)
        {
            using var del = conn.CreateCommand();
            del.CommandText = "DELETE FROM announcements WHERE nid = $nid";
            del.Parameters.AddWithValue("$nid", frame.Nid);
            del.ExecuteNonQuery();
            BumpSeq(conn);
            return;
        }

        var expiresAt = (DateTime.TryParse(frame.Timestamp, out var ts) ? ts : DateTime.UtcNow)
                        .AddSeconds(frame.Ttl).ToString("O");

        using var upsert = conn.CreateCommand();
        upsert.CommandText = """
            INSERT INTO announcements
              (nid, addresses_json, caps_json, node_type, ttl, timestamp, signature, expires_at)
            VALUES
              ($nid, $addr, $caps, $nt, $ttl, $ts, $sig, $exp)
            ON CONFLICT(nid) DO UPDATE SET
              addresses_json = excluded.addresses_json,
              caps_json      = excluded.caps_json,
              node_type      = excluded.node_type,
              ttl            = excluded.ttl,
              timestamp      = excluded.timestamp,
              signature      = excluded.signature,
              expires_at     = excluded.expires_at
            """;
        upsert.Parameters.AddWithValue("$nid",  frame.Nid);
        upsert.Parameters.AddWithValue("$addr", JsonSerializer.Serialize(frame.Addresses, _json));
        upsert.Parameters.AddWithValue("$caps", JsonSerializer.Serialize(frame.Capabilities, _json));
        upsert.Parameters.AddWithValue("$nt",   (object?)frame.NodeType ?? DBNull.Value);
        upsert.Parameters.AddWithValue("$ttl",  (long)frame.Ttl);
        upsert.Parameters.AddWithValue("$ts",   frame.Timestamp);
        upsert.Parameters.AddWithValue("$sig",  frame.Signature);
        upsert.Parameters.AddWithValue("$exp",  expiresAt);
        upsert.ExecuteNonQuery();

        BumpSeq(conn);
    }

    public NdpResolveResult? Resolve(string target)
    {
        using var conn = Open();
        Purge(conn);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT nid, addresses_json, ttl FROM announcements";
        using var rdr   = cmd.ExecuteReader();
        while (rdr.Read())
        {
            var nid      = rdr.GetString(0);
            var addrJson = rdr.GetString(1);
            var ttl      = (uint)rdr.GetInt64(2);

            if (!InMemoryNdpRegistry.NwpTargetMatchesNid(nid, target)) continue;

            var addrs = JsonSerializer.Deserialize<List<NdpAddress>>(addrJson, _json);
            var addr  = addrs?.FirstOrDefault();
            if (addr is null) continue;

            return new NdpResolveResult { Host = addr.Host, Port = addr.Port, Ttl = ttl };
        }
        return null;
    }

    public IReadOnlyList<AnnounceFrame> GetAll()
    {
        using var conn = Open();
        Purge(conn);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT nid, addresses_json, caps_json, node_type, ttl, timestamp, signature FROM announcements";
        using var rdr   = cmd.ExecuteReader();
        var list = new List<AnnounceFrame>();
        while (rdr.Read()) list.Add(MapRow(rdr));
        return list;
    }

    public AnnounceFrame? GetByNid(string nid)
    {
        using var conn = Open();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT nid, addresses_json, caps_json, node_type, ttl, timestamp, signature
            FROM   announcements
            WHERE  nid = $nid AND expires_at > $now
            """;
        cmd.Parameters.AddWithValue("$nid", nid);
        cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        using var rdr = cmd.ExecuteReader();
        return rdr.Read() ? MapRow(rdr) : null;
    }

    public NdpResolveResult? ResolveViaDns(string target, IDnsTxtLookup? dnsLookup = null)
    {
        // 1. Try the persistent registry first.
        var cached = Resolve(target);
        if (cached is not null)
            return cached;

        // 2. Extract host from nwp:// URL.
        if (!target.StartsWith("nwp://", StringComparison.OrdinalIgnoreCase))
            return null;
        var rest      = target["nwp://".Length..];
        var slashIdx  = rest.IndexOf('/');
        var host      = slashIdx < 0 ? rest : rest[..slashIdx];
        if (string.IsNullOrWhiteSpace(host))
            return null;

        // 3. DNS TXT fallback on _nps-node.{host}.
        var lookup     = dnsLookup ?? new SystemDnsTxtLookup();
        var txtRecords = lookup.Lookup($"_nps-node.{host}");
        foreach (var txt in txtRecords)
        {
            var result = InMemoryNdpRegistry.ParseNpsTxtRecord(txt, host);
            if (result is not null)
                return result;
        }

        return null;
    }

    /// <summary>Returns the current monotonic graph sequence counter.</summary>
    public ulong GetSeq()
    {
        using var conn = Open();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT seq FROM graph_meta WHERE id = 1";
        return (ulong)(long)(cmd.ExecuteScalar() ?? 0L);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void EnsureSchema()
    {
        using var conn = Open();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS announcements (
              nid            TEXT PRIMARY KEY,
              addresses_json TEXT NOT NULL,
              caps_json      TEXT NOT NULL,
              node_type      TEXT,
              ttl            INTEGER NOT NULL,
              timestamp      TEXT NOT NULL,
              signature      TEXT NOT NULL,
              expires_at     TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_expires_at ON announcements(expires_at);
            CREATE TABLE IF NOT EXISTS graph_meta (
              id  INTEGER PRIMARY KEY CHECK (id = 1),
              seq INTEGER NOT NULL DEFAULT 0
            );
            INSERT OR IGNORE INTO graph_meta (id, seq) VALUES (1, 0);
            """;
        cmd.ExecuteNonQuery();
    }

    private static void Purge(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM announcements WHERE expires_at <= $now";
        cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    private static void BumpSeq(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE graph_meta SET seq = seq + 1 WHERE id = 1";
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connStr);
        conn.Open();
        return conn;
    }

    private static AnnounceFrame MapRow(SqliteDataReader rdr)
    {
        var addrs = JsonSerializer.Deserialize<List<NdpAddress>>(rdr.GetString(1), _json)
                    ?? new List<NdpAddress>();
        var caps  = JsonSerializer.Deserialize<List<string>>(rdr.GetString(2), _json)
                    ?? new List<string>();
        return new AnnounceFrame
        {
            Nid          = rdr.GetString(0),
            Addresses    = addrs,
            Capabilities = caps,
            NodeType     = rdr.IsDBNull(3) ? null : rdr.GetString(3),
            Ttl          = (uint)rdr.GetInt64(4),
            Timestamp    = rdr.GetString(5),
            Signature    = rdr.GetString(6),
        };
    }
}
