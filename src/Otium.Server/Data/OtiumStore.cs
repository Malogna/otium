using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Otium.Server.Data;

public sealed record DeviceRecord(
    string DeviceId,
    string SecretHash,
    string WorkerId,
    string? PoolWallet,
    string? OwnWallet,
    string Code,
    string? ReferredByCode,
    DateTimeOffset? ReferredAt,
    DateTimeOffset? FeeFreeUntil,
    bool Verified,
    DateTimeOffset? LastCheckedAt,
    DateTimeOffset CreatedAt)
{
    public bool IsBalanceAccount => OwnWallet is null;
}

public static class LedgerKinds
{
    public const string Earning = "earning";
    public const string Referral = "referral";
    public const string Cashout = "cashout";
    public const string Refund = "refund";
    public const string Adjustment = "adjustment";
}

/// <param name="Micros">Amount in millionths of a US dollar; negative for debits.</param>
/// <param name="AvailableAt">When the amount can be cashed out (credits are held for a while).</param>
public sealed record LedgerEntry(long Id, string DeviceId, string Kind, long Micros, DateTimeOffset CreatedAt, DateTimeOffset AvailableAt, string? Reference);

public sealed record CashoutRecord(
    string Id,
    string DeviceId,
    string Method,
    string Destination,
    long Micros,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ProviderReference,
    string? Note,
    int Attempts);

public sealed record BalanceTotals(long AvailableMicros, long PendingMicros, long LifetimeMicros, long ReferralMicros);

/// <summary>
/// SQLite persistence for accounts, the money ledger, cash-outs and crediting progress.
/// Money is stored as integer micro-dollars so balances never drift through floating-point rounding.
/// </summary>
public sealed class OtiumStore
{
    public const long MicrosPerDollar = 1_000_000;

    private readonly string _connectionString;

    public OtiumStore(string connectionString)
    {
        _connectionString = connectionString;
        Execute("""
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS devices (
                device_id       TEXT PRIMARY KEY,
                secret_hash     TEXT NOT NULL,
                worker_id       TEXT NOT NULL UNIQUE,
                pool_wallet     TEXT NULL,
                own_wallet      TEXT NULL,
                code            TEXT NOT NULL UNIQUE,
                referred_by     TEXT NULL,
                referred_at     TEXT NULL,
                fee_free_until  TEXT NULL,
                verified        INTEGER NOT NULL DEFAULT 0,
                last_checked_at TEXT NULL,
                created_at      TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_devices_referred_by ON devices(referred_by);
            CREATE INDEX IF NOT EXISTS ix_devices_wallet ON devices(own_wallet);
            CREATE INDEX IF NOT EXISTS ix_devices_pool ON devices(pool_wallet);

            CREATE TABLE IF NOT EXISTS pool_wallets (
                address    TEXT PRIMARY KEY,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ledger (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                device_id    TEXT NOT NULL,
                kind         TEXT NOT NULL,
                micros       INTEGER NOT NULL,
                created_at   TEXT NOT NULL,
                available_at TEXT NOT NULL,
                reference    TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_ledger_device ON ledger(device_id);

            CREATE TABLE IF NOT EXISTS cashouts (
                id                 TEXT PRIMARY KEY,
                device_id          TEXT NOT NULL,
                method             TEXT NOT NULL,
                destination        TEXT NOT NULL,
                micros             INTEGER NOT NULL,
                status             TEXT NOT NULL,
                created_at         TEXT NOT NULL,
                updated_at         TEXT NOT NULL,
                provider_reference TEXT NULL,
                note               TEXT NULL,
                attempts           INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS ix_cashouts_device ON cashouts(device_id);
            CREATE INDEX IF NOT EXISTS ix_cashouts_status ON cashouts(status);

            CREATE TABLE IF NOT EXISTS worker_progress (
                worker_id  TEXT PRIMARY KEY,
                last_ts_ms INTEGER NOT NULL,
                updated_at TEXT NOT NULL
            );
            """);
    }

    // ---------- devices ----------

    public DeviceRecord? FindByDevice(string deviceId) => QueryDevice("SELECT * FROM devices WHERE device_id = $p", deviceId);

    public DeviceRecord? FindByCode(string code) => QueryDevice("SELECT * FROM devices WHERE code = $p", code);

    public DeviceRecord? FindByWorker(string workerId) => QueryDevice("SELECT * FROM devices WHERE worker_id = $p", workerId);

    public bool CodeExists(string code) => FindByCode(code) is not null;

    public bool WorkerExists(string workerId) => FindByWorker(workerId) is not null;

    /// <summary>True when some other device paying out to this wallet has already been referred by anyone.</summary>
    public bool WalletAlreadyReferred(string wallet, string exceptDeviceId) =>
        Scalar<long>("SELECT COUNT(*) FROM devices WHERE own_wallet = $a AND referred_by IS NOT NULL AND device_id <> $d",
            ("$a", wallet), ("$d", exceptDeviceId)) > 0;

    public IReadOnlyList<DeviceRecord> FindReferees(string code) => QueryDevices("SELECT * FROM devices WHERE referred_by = $p", ("$p", code));

    public void Upsert(DeviceRecord r) =>
        Execute("""
            INSERT INTO devices (device_id, secret_hash, worker_id, pool_wallet, own_wallet, code, referred_by, referred_at, fee_free_until, verified, last_checked_at, created_at)
            VALUES ($id, $secret, $worker, $pool, $wallet, $code, $by, $at, $free, $verified, $checked, $created)
            ON CONFLICT(device_id) DO UPDATE SET
                pool_wallet = excluded.pool_wallet,
                own_wallet = excluded.own_wallet,
                referred_by = excluded.referred_by,
                referred_at = excluded.referred_at,
                fee_free_until = excluded.fee_free_until,
                verified = excluded.verified,
                last_checked_at = excluded.last_checked_at;
            """,
            ("$id", r.DeviceId), ("$secret", r.SecretHash), ("$worker", r.WorkerId), ("$pool", r.PoolWallet), ("$wallet", r.OwnWallet), ("$code", r.Code),
            ("$by", r.ReferredByCode), ("$at", Format(r.ReferredAt)), ("$free", Format(r.FeeFreeUntil)), ("$verified", r.Verified ? 1 : 0),
            ("$checked", Format(r.LastCheckedAt)), ("$created", Format(r.CreatedAt)));

    // ---------- ledger ----------

    public void AddLedger(string deviceId, string kind, long micros, DateTimeOffset now, DateTimeOffset availableAt, string? reference) =>
        Execute("INSERT INTO ledger (device_id, kind, micros, created_at, available_at, reference) VALUES ($d, $k, $m, $c, $a, $r)",
            ("$d", deviceId), ("$k", kind), ("$m", micros), ("$c", Format(now)), ("$a", Format(availableAt)), ("$r", reference));

    public BalanceTotals Balance(string deviceId, DateTimeOffset now)
    {
        var entries = QueryLedger("SELECT * FROM ledger WHERE device_id = $p", ("$p", deviceId));
        long available = 0, pending = 0, lifetime = 0, referral = 0;
        foreach (var e in entries)
        {
            if (e.AvailableAt <= now)
                available += e.Micros;
            else
                pending += e.Micros;
            if (e.Kind is LedgerKinds.Earning or LedgerKinds.Referral)
                lifetime += e.Micros;
            if (e.Kind == LedgerKinds.Referral)
                referral += e.Micros;
        }
        return new BalanceTotals(available, pending, lifetime, referral);
    }

    /// <summary>Gross earnings credited to an account (before any cash-outs), for referral verification.</summary>
    public long LifetimeEarningMicros(string deviceId) =>
        Scalar<long>("SELECT COALESCE(SUM(micros), 0) FROM ledger WHERE device_id = $d AND kind = 'earning'", ("$d", deviceId));

    /// <summary>
    /// Debits the whole available balance and records the cash-out in one transaction, so a double-click or
    /// two devices can never spend the same money twice. Returns null when the available balance is below the minimum.
    /// </summary>
    public CashoutRecord? DebitAndCreateCashout(string deviceId, string method, string destination, long minimumMicros, string status, DateTimeOffset now)
    {
        using var connection = Open();
        using var tx = connection.BeginTransaction(deferred: false);

        long available;
        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT COALESCE(SUM(micros), 0) FROM ledger WHERE device_id = $d AND available_at <= $now";
            cmd.Parameters.AddWithValue("$d", deviceId);
            cmd.Parameters.AddWithValue("$now", Format(now));
            available = Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        if (available < minimumMicros || available <= 0)
        {
            tx.Rollback();
            return null;
        }

        var cashout = new CashoutRecord($"co_{Guid.NewGuid():N}", deviceId, method, destination, available, status, now, now, null, null, 0);
        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO cashouts (id, device_id, method, destination, micros, status, created_at, updated_at, attempts)
                VALUES ($id, $d, $m, $dest, $micros, $s, $c, $c, 0);
                INSERT INTO ledger (device_id, kind, micros, created_at, available_at, reference)
                VALUES ($d, 'cashout', -$micros, $c, $c, $id);
                """;
            cmd.Parameters.AddWithValue("$id", cashout.Id);
            cmd.Parameters.AddWithValue("$d", deviceId);
            cmd.Parameters.AddWithValue("$m", method);
            cmd.Parameters.AddWithValue("$dest", destination);
            cmd.Parameters.AddWithValue("$micros", available);
            cmd.Parameters.AddWithValue("$s", status);
            cmd.Parameters.AddWithValue("$c", Format(now));
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
        return cashout;
    }

    // ---------- cash-outs ----------

    public CashoutRecord? FindCashout(string id) => QueryCashouts("SELECT * FROM cashouts WHERE id = $p", ("$p", id)).FirstOrDefault();

    public IReadOnlyList<CashoutRecord> CashoutsFor(string deviceId, int limit = 10) =>
        QueryCashouts($"SELECT * FROM cashouts WHERE device_id = $p ORDER BY created_at DESC LIMIT {limit}", ("$p", deviceId));

    public IReadOnlyList<CashoutRecord> CashoutsWithStatus(params string[] statuses) =>
        QueryCashouts($"SELECT * FROM cashouts WHERE status IN ({string.Join(",", statuses.Select((_, i) => $"$s{i}"))}) ORDER BY created_at",
            statuses.Select((s, i) => ($"$s{i}", (object?)s)).ToArray());

    public bool HasOpenCashout(string deviceId) =>
        Scalar<long>("SELECT COUNT(*) FROM cashouts WHERE device_id = $d AND status IN ('queued','review','sending')", ("$d", deviceId)) > 0;

    /// <summary>Other accounts that have cashed out to the same destination (a sign of one person running many accounts).</summary>
    public long OtherAccountsUsingDestination(string destination, string deviceId) =>
        Scalar<long>("SELECT COUNT(DISTINCT device_id) FROM cashouts WHERE destination = $dest AND device_id <> $d", ("$dest", destination), ("$d", deviceId));

    public long AutoPaidMicrosSince(DateTimeOffset since) =>
        Scalar<long>("SELECT COALESCE(SUM(micros), 0) FROM cashouts WHERE status IN ('queued','sending','paid') AND note IS NULL AND created_at >= $s", ("$s", Format(since)));

    public void UpdateCashout(string id, string status, DateTimeOffset now, string? providerReference = null, string? note = null, bool countAttempt = false) =>
        Execute("""
            UPDATE cashouts SET status = $s, updated_at = $u,
                provider_reference = COALESCE($ref, provider_reference),
                note = COALESCE($note, note),
                attempts = attempts + $inc
            WHERE id = $id
            """,
            ("$s", status), ("$u", Format(now)), ("$ref", providerReference), ("$note", note), ("$inc", countAttempt ? 1 : 0), ("$id", id));

    /// <summary>Marks a cash-out failed and returns its amount to the balance (immediately available).</summary>
    public void FailAndRefund(CashoutRecord cashout, string reason, DateTimeOffset now)
    {
        using var connection = Open();
        using var tx = connection.BeginTransaction(deferred: false);
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            UPDATE cashouts SET status = 'failed', updated_at = $u, note = $note WHERE id = $id AND status <> 'failed';
            INSERT INTO ledger (device_id, kind, micros, created_at, available_at, reference)
            SELECT $d, 'refund', $m, $u, $u, $id WHERE changes() > 0;
            """;
        cmd.Parameters.AddWithValue("$u", Format(now));
        cmd.Parameters.AddWithValue("$note", reason);
        cmd.Parameters.AddWithValue("$id", cashout.Id);
        cmd.Parameters.AddWithValue("$d", cashout.DeviceId);
        cmd.Parameters.AddWithValue("$m", cashout.Micros);
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    // ---------- crediting progress ----------

    /// <summary>Timestamp (Unix ms) of the last pool hashrate point already credited, per worker.</summary>
    public IReadOnlyDictionary<string, long> WorkerProgress()
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT worker_id, last_ts_ms FROM worker_progress";
        using var reader = cmd.ExecuteReader();
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        while (reader.Read())
            result[reader.GetString(0)] = reader.GetInt64(1);
        return result;
    }

    public void SetWorkerProgress(string workerId, long lastTsMs, DateTimeOffset now) =>
        Execute("""
            INSERT INTO worker_progress (worker_id, last_ts_ms, updated_at) VALUES ($w, $t, $u)
            ON CONFLICT(worker_id) DO UPDATE SET last_ts_ms = excluded.last_ts_ms, updated_at = excluded.updated_at
            """, ("$w", workerId), ("$t", lastTsMs), ("$u", Format(now)));

    // ---------- pool addresses (sharding) ----------

    public void AddPoolWallet(string address, DateTimeOffset now) =>
        Execute("INSERT OR IGNORE INTO pool_wallets (address, created_at) VALUES ($a, $c)", ("$a", address), ("$c", Format(now)));

    /// <summary>Pool addresses with how many balance accounts each one carries, least loaded first.</summary>
    public IReadOnlyList<(string Address, long Accounts)> PoolWalletLoads()
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT w.address, (SELECT COUNT(*) FROM devices d WHERE d.pool_wallet = w.address) AS n
            FROM pool_wallets w ORDER BY n, w.created_at
            """;
        using var reader = cmd.ExecuteReader();
        var result = new List<(string, long)>();
        while (reader.Read())
            result.Add((reader.GetString(0), reader.GetInt64(1)));
        return result;
    }

    // ---------- tax tracking ----------

    public long PaidMicrosInYear(string deviceId, int year) =>
        Scalar<long>("SELECT COALESCE(SUM(micros),0) FROM cashouts WHERE device_id = $d AND status IN ('queued','sending','paid') AND substr(created_at,1,4) = $y",
            ("$d", deviceId), ("$y", year.ToString(CultureInfo.InvariantCulture)));

    // ---------- reporting ----------

    public (long Credited, long Referral, long Paid, long Liability) Totals(DateTimeOffset now) => (
        Scalar<long>("SELECT COALESCE(SUM(micros),0) FROM ledger WHERE kind = 'earning'"),
        Scalar<long>("SELECT COALESCE(SUM(micros),0) FROM ledger WHERE kind = 'referral'"),
        Scalar<long>("SELECT COALESCE(SUM(micros),0) FROM cashouts WHERE status = 'paid'"),
        Scalar<long>("SELECT COALESCE(SUM(micros),0) FROM ledger"));

    // ---------- plumbing ----------

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA busy_timeout = 5000;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    private void Execute(string sql, params (string Name, object? Value)[] parameters)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        Bind(cmd, parameters);
        cmd.ExecuteNonQuery();
    }

    private T Scalar<T>(string sql, params (string Name, object? Value)[] parameters)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        Bind(cmd, parameters);
        return (T)Convert.ChangeType(cmd.ExecuteScalar()!, typeof(T), CultureInfo.InvariantCulture);
    }

    private static void Bind(SqliteCommand cmd, (string Name, object? Value)[] parameters)
    {
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    private DeviceRecord? QueryDevice(string sql, string parameter) => QueryDevices(sql, ("$p", parameter)).FirstOrDefault();

    private IReadOnlyList<DeviceRecord> QueryDevices(string sql, params (string, object?)[] parameters)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        Bind(cmd, parameters);
        using var r = cmd.ExecuteReader();
        var list = new List<DeviceRecord>();
        while (r.Read())
        {
            list.Add(new DeviceRecord(
                r.GetString(r.GetOrdinal("device_id")),
                r.GetString(r.GetOrdinal("secret_hash")),
                r.GetString(r.GetOrdinal("worker_id")),
                NullableString(r, "pool_wallet"),
                NullableString(r, "own_wallet"),
                r.GetString(r.GetOrdinal("code")),
                NullableString(r, "referred_by"),
                ParseNullable(NullableString(r, "referred_at")),
                ParseNullable(NullableString(r, "fee_free_until")),
                r.GetInt64(r.GetOrdinal("verified")) != 0,
                ParseNullable(NullableString(r, "last_checked_at")),
                Parse(r.GetString(r.GetOrdinal("created_at")))));
        }
        return list;
    }

    private IReadOnlyList<LedgerEntry> QueryLedger(string sql, params (string, object?)[] parameters)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        Bind(cmd, parameters);
        using var r = cmd.ExecuteReader();
        var list = new List<LedgerEntry>();
        while (r.Read())
        {
            list.Add(new LedgerEntry(
                r.GetInt64(r.GetOrdinal("id")),
                r.GetString(r.GetOrdinal("device_id")),
                r.GetString(r.GetOrdinal("kind")),
                r.GetInt64(r.GetOrdinal("micros")),
                Parse(r.GetString(r.GetOrdinal("created_at"))),
                Parse(r.GetString(r.GetOrdinal("available_at"))),
                NullableString(r, "reference")));
        }
        return list;
    }

    private IReadOnlyList<CashoutRecord> QueryCashouts(string sql, params (string, object?)[] parameters)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        Bind(cmd, parameters);
        using var r = cmd.ExecuteReader();
        var list = new List<CashoutRecord>();
        while (r.Read())
        {
            list.Add(new CashoutRecord(
                r.GetString(r.GetOrdinal("id")),
                r.GetString(r.GetOrdinal("device_id")),
                r.GetString(r.GetOrdinal("method")),
                r.GetString(r.GetOrdinal("destination")),
                r.GetInt64(r.GetOrdinal("micros")),
                r.GetString(r.GetOrdinal("status")),
                Parse(r.GetString(r.GetOrdinal("created_at"))),
                Parse(r.GetString(r.GetOrdinal("updated_at"))),
                NullableString(r, "provider_reference"),
                NullableString(r, "note"),
                (int)r.GetInt64(r.GetOrdinal("attempts"))));
        }
        return list;
    }

    private static string? NullableString(SqliteDataReader r, string column)
    {
        var ordinal = r.GetOrdinal(column);
        return r.IsDBNull(ordinal) ? null : r.GetString(ordinal);
    }

    // Fixed-width UTC timestamps sort correctly as text, which the SQL comparisons above rely on.
    private static string Format(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);

    private static object Format(DateTimeOffset? value) => value is { } v ? Format(v) : DBNull.Value;

    private static DateTimeOffset Parse(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    private static DateTimeOffset? ParseNullable(string? value) => value is null ? null : Parse(value);
}
