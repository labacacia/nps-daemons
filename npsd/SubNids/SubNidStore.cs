// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Data.Sqlite;

namespace NPS.Daemon.Npsd.SubNids;

/// <summary>
/// SQLite-backed store for sub-NIDs issued by this host.
/// </summary>
/// <remarks>
/// Schema (single table):
/// <code>
/// CREATE TABLE sub_nids (
///   nid             TEXT PRIMARY KEY,
///   pub_key         TEXT NOT NULL,
///   priv_key_enc    TEXT,
///   issued_by       TEXT NOT NULL,
///   issued_at       TEXT NOT NULL,
///   expires_at      TEXT NOT NULL,
///   serial          TEXT NOT NULL,
///   capabilities    TEXT NOT NULL,
///   scope_json      TEXT NOT NULL,
///   metadata_json   TEXT,
///   revoked         INTEGER NOT NULL DEFAULT 0,
///   revoked_at      TEXT,
///   revoke_reason   TEXT
/// );
/// CREATE INDEX idx_sub_nids_issued_at ON sub_nids(issued_at);
/// </code>
/// </remarks>
public sealed class SubNidStore : IDisposable
{
    private readonly string            _connectionString;
    private readonly SqliteConnection? _keepAlive;

    /// <summary>File-backed store at <paramref name="sqlitePath"/>.</summary>
    public SubNidStore(string sqlitePath)
    {
        _connectionString = $"Data Source={sqlitePath};Cache=Shared";
        _keepAlive        = null;
        EnsureSchema();
    }

    /// <summary>
    /// Test-only: in-memory database. The store keeps one connection open
    /// for its lifetime so the in-memory db survives between calls.
    /// </summary>
    public static SubNidStore CreateInMemoryForTests()
    {
        var name             = $"npsd-test-{Guid.NewGuid():N}";
        var connectionString = $"Data Source=file:{name}?mode=memory&cache=shared";
        var keepAlive        = new SqliteConnection(connectionString);
        keepAlive.Open();
        return new SubNidStore(connectionString, keepAlive);
    }

    private SubNidStore(string connectionString, SqliteConnection keepAlive)
    {
        _connectionString = connectionString;
        _keepAlive        = keepAlive;
        EnsureSchema();
    }

    public void Dispose() => _keepAlive?.Dispose();

    private void EnsureSchema()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS sub_nids (
              nid           TEXT PRIMARY KEY,
              pub_key       TEXT NOT NULL,
              priv_key_enc  TEXT,
              issued_by     TEXT NOT NULL,
              issued_at     TEXT NOT NULL,
              expires_at    TEXT NOT NULL,
              serial        TEXT NOT NULL,
              capabilities  TEXT NOT NULL,
              scope_json    TEXT NOT NULL,
              metadata_json TEXT,
              revoked       INTEGER NOT NULL DEFAULT 0,
              revoked_at    TEXT,
              revoke_reason TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_sub_nids_issued_at ON sub_nids(issued_at);
            CREATE TABLE IF NOT EXISTS serial_counter (
              id INTEGER PRIMARY KEY CHECK (id = 1),
              next_serial INTEGER NOT NULL DEFAULT 1
            );
            INSERT OR IGNORE INTO serial_counter (id, next_serial) VALUES (1, 1);
            """;
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    /// <summary>Reserves and returns the next monotonic serial number (hex).</summary>
    public string NextSerial()
    {
        using var conn = OpenConnection();
        using var tx   = conn.BeginTransaction();
        long next;
        using (var read = conn.CreateCommand())
        {
            read.Transaction = tx;
            read.CommandText = "SELECT next_serial FROM serial_counter WHERE id = 1";
            next = (long)(read.ExecuteScalar() ?? 1L);
        }
        using (var bump = conn.CreateCommand())
        {
            bump.Transaction = tx;
            bump.CommandText = "UPDATE serial_counter SET next_serial = next_serial + 1 WHERE id = 1";
            bump.ExecuteNonQuery();
        }
        tx.Commit();
        return "0x" + next.ToString("X");
    }

    /// <summary>Persists a new record. Throws on duplicate NID.</summary>
    public void Insert(SubNidRecord record)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sub_nids
              (nid, pub_key, priv_key_enc, issued_by, issued_at, expires_at, serial,
               capabilities, scope_json, metadata_json, revoked)
            VALUES
              ($nid, $pub_key, $priv_key_enc, $issued_by, $issued_at, $expires_at, $serial,
               $capabilities, $scope_json, $metadata_json, 0)
            """;
        cmd.Parameters.AddWithValue("$nid",           record.Nid);
        cmd.Parameters.AddWithValue("$pub_key",       record.PubKey);
        cmd.Parameters.AddWithValue("$priv_key_enc",  (object?)record.PrivKeyEncrypted ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$issued_by",     record.IssuedBy);
        cmd.Parameters.AddWithValue("$issued_at",     record.IssuedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$expires_at",    record.ExpiresAt.ToString("O"));
        cmd.Parameters.AddWithValue("$serial",        record.Serial);
        cmd.Parameters.AddWithValue("$capabilities",  record.Capabilities);
        cmd.Parameters.AddWithValue("$scope_json",    record.ScopeJson);
        cmd.Parameters.AddWithValue("$metadata_json", (object?)record.MetadataJson ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public SubNidRecord? Get(string nid)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM sub_nids WHERE nid = $nid";
        cmd.Parameters.AddWithValue("$nid", nid);
        using var rdr = cmd.ExecuteReader();
        return rdr.Read() ? Map(rdr) : null;
    }

    public IReadOnlyList<SubNidRecord> List(int limit = 100, int offset = 0)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM sub_nids ORDER BY issued_at DESC LIMIT $limit OFFSET $offset";
        cmd.Parameters.AddWithValue("$limit", limit);
        cmd.Parameters.AddWithValue("$offset", offset);
        var list = new List<SubNidRecord>();
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read()) list.Add(Map(rdr));
        return list;
    }

    /// <summary>Marks a NID as revoked. Returns false if the NID is unknown.</summary>
    public bool MarkRevoked(string nid, string reason, DateTimeOffset at)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE sub_nids
            SET revoked = 1, revoked_at = $at, revoke_reason = $reason
            WHERE nid = $nid AND revoked = 0
            """;
        cmd.Parameters.AddWithValue("$at",     at.ToString("O"));
        cmd.Parameters.AddWithValue("$reason", reason);
        cmd.Parameters.AddWithValue("$nid",    nid);
        return cmd.ExecuteNonQuery() == 1;
    }

    private static SubNidRecord Map(SqliteDataReader rdr) => new()
    {
        Nid              = rdr.GetString(rdr.GetOrdinal("nid")),
        PubKey           = rdr.GetString(rdr.GetOrdinal("pub_key")),
        PrivKeyEncrypted = rdr.IsDBNull(rdr.GetOrdinal("priv_key_enc"))
                              ? null : rdr.GetString(rdr.GetOrdinal("priv_key_enc")),
        IssuedBy         = rdr.GetString(rdr.GetOrdinal("issued_by")),
        IssuedAt         = DateTimeOffset.Parse(rdr.GetString(rdr.GetOrdinal("issued_at"))),
        ExpiresAt        = DateTimeOffset.Parse(rdr.GetString(rdr.GetOrdinal("expires_at"))),
        Serial           = rdr.GetString(rdr.GetOrdinal("serial")),
        Capabilities     = rdr.GetString(rdr.GetOrdinal("capabilities")),
        ScopeJson        = rdr.GetString(rdr.GetOrdinal("scope_json")),
        MetadataJson     = rdr.IsDBNull(rdr.GetOrdinal("metadata_json"))
                              ? null : rdr.GetString(rdr.GetOrdinal("metadata_json")),
        Revoked          = rdr.GetInt64(rdr.GetOrdinal("revoked")) != 0,
        RevokedAt        = rdr.IsDBNull(rdr.GetOrdinal("revoked_at"))
                              ? null : DateTimeOffset.Parse(rdr.GetString(rdr.GetOrdinal("revoked_at"))),
        RevokeReason     = rdr.IsDBNull(rdr.GetOrdinal("revoke_reason"))
                              ? null : rdr.GetString(rdr.GetOrdinal("revoke_reason")),
    };
}
