using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Otium.Server.Data;

/// <summary>
/// A person's account: the balance, referral code and pool worker belong here, so they survive reinstalls and
/// can be shared by several computers. <paramref name="MergedInto"/> is set when this account was folded into
/// another one (someone signed in with an email that already had an account).
/// </summary>
public sealed record AccountRecord(
    string AccountId,
    string WorkerId,
    string? PoolWallet,
    string? OwnWallet,
    string Code,
    string? Email,
    string? ReferredByCode,
    DateTimeOffset? ReferredAt,
    DateTimeOffset? FeeFreeUntil,
    bool Verified,
    DateTimeOffset? LastCheckedAt,
    string? MergedInto,
    DateTimeOffset CreatedAt)
{
    public bool IsBalanceAccount => OwnWallet is null;
}

/// <summary>One computer signed in to an account, authenticated by its own random secret.</summary>
public sealed record DeviceRecord(string DeviceId, string SecretHash, string AccountId, DateTimeOffset CreatedAt, DateTimeOffset LastSeenAt);

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
public sealed record LedgerEntry(long Id, string AccountId, string Kind, long Micros, DateTimeOffset CreatedAt, DateTimeOffset AvailableAt, string? Reference);

public sealed record CashoutRecord(
    string Id,
    string AccountId,
    string Method,
    string Destination,
    long Micros,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ProviderReference,
    string? Note,
    int Attempts);

/// <summary>A bandwidth usage report as received, with what was credited for it.</summary>
public sealed record BandwidthReportRecord(
    string DeviceId,
    long Seq,
    string AccountId,
    string Ip,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    long Bytes,
    double Seconds,
    long CreditedBytes,
    long CreditedMicros,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record BalanceTotals(long AvailableMicros, long PendingMicros, long LifetimeMicros, long ReferralMicros);

/// <param name="Salt">Random per-code salt so a leaked database doesn't reveal codes by lookup.</param>
public sealed record EmailCodeRecord(long Id, string Email, string DeviceId, string CodeHash, string Salt, DateTimeOffset ExpiresAt, int Attempts, bool Used, DateTimeOffset CreatedAt);

/// <summary>
/// SQLite persistence for accounts, devices, the money ledger, cash-outs, sign-in codes and crediting progress.
/// Money is stored as integer micro-dollars so balances never drift through floating-point rounding.
/// </summary>
public sealed class OtiumStore
{
    public const long MicrosPerDollar = 1_000_000;
    private const int SchemaVersion = 3;

    private readonly string _connectionString;

    public OtiumStore(string connectionString)
    {
        _connectionString = connectionString;
        Migrate();
    }

    // ---------- schema ----------

    private void Migrate()
    {
        Execute("PRAGMA journal_mode = WAL;");
        var version = Scalar<long>("PRAGMA user_version");
        if (version >= SchemaVersion)
            return;

        // Version 1 (before sign-in) kept one row per device with the account data inline.
        var hasV1 = version == 0 && Scalar<long>("SELECT COUNT(*) FROM pragma_table_info('devices') WHERE name = 'worker_id'") > 0;
        if (hasV1)
            Execute("ALTER TABLE devices RENAME TO devices_v1;");

        Execute("""
            CREATE TABLE IF NOT EXISTS accounts (
                account_id      TEXT PRIMARY KEY,
                worker_id       TEXT NOT NULL UNIQUE,
                pool_wallet     TEXT NULL,
                own_wallet      TEXT NULL,
                code            TEXT NOT NULL UNIQUE,
                email           TEXT NULL UNIQUE,
                referred_by     TEXT NULL,
                referred_at     TEXT NULL,
                fee_free_until  TEXT NULL,
                verified        INTEGER NOT NULL DEFAULT 0,
                last_checked_at TEXT NULL,
                merged_into     TEXT NULL,
                created_at      TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_accounts_referred_by ON accounts(referred_by);
            CREATE INDEX IF NOT EXISTS ix_accounts_wallet ON accounts(own_wallet);
            CREATE INDEX IF NOT EXISTS ix_accounts_pool ON accounts(pool_wallet);

            CREATE TABLE IF NOT EXISTS devices (
                device_id    TEXT PRIMARY KEY,
                secret_hash  TEXT NOT NULL,
                account_id   TEXT NOT NULL,
                created_at   TEXT NOT NULL,
                last_seen_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_devices_account ON devices(account_id);

            CREATE TABLE IF NOT EXISTS pool_wallets (
                address    TEXT PRIMARY KEY,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ledger (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                account_id   TEXT NOT NULL,
                kind         TEXT NOT NULL,
                micros       INTEGER NOT NULL,
                created_at   TEXT NOT NULL,
                available_at TEXT NOT NULL,
                reference    TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS cashouts (
                id                 TEXT PRIMARY KEY,
                account_id         TEXT NOT NULL,
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
            CREATE INDEX IF NOT EXISTS ix_cashouts_status ON cashouts(status);

            CREATE TABLE IF NOT EXISTS worker_progress (
                worker_id  TEXT PRIMARY KEY,
                last_ts_ms INTEGER NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS email_codes (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                email      TEXT NOT NULL,
                device_id  TEXT NOT NULL,
                code_hash  TEXT NOT NULL,
                salt       TEXT NOT NULL,
                expires_at TEXT NOT NULL,
                attempts   INTEGER NOT NULL DEFAULT 0,
                used       INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_email_codes_email ON email_codes(email);
            """);

        if (hasV1)
        {
            Execute("""
                INSERT INTO accounts (account_id, worker_id, pool_wallet, own_wallet, code, email, referred_by, referred_at,
                                      fee_free_until, verified, last_checked_at, merged_into, created_at)
                SELECT device_id, worker_id, pool_wallet, own_wallet, code, NULL, referred_by, referred_at,
                       fee_free_until, verified, last_checked_at, NULL, created_at FROM devices_v1;
                INSERT INTO devices (device_id, secret_hash, account_id, created_at, last_seen_at)
                SELECT device_id, secret_hash, device_id, created_at, created_at FROM devices_v1;
                DROP TABLE devices_v1;
                """);
            // Ledger and cash-outs were keyed by device id, which became the account id above.
            if (Scalar<long>("SELECT COUNT(*) FROM pragma_table_info('ledger') WHERE name = 'device_id'") > 0)
                Execute("ALTER TABLE ledger RENAME COLUMN device_id TO account_id;");
            if (Scalar<long>("SELECT COUNT(*) FROM pragma_table_info('cashouts') WHERE name = 'device_id'") > 0)
                Execute("ALTER TABLE cashouts RENAME COLUMN device_id TO account_id;");
        }

        Execute("""
            CREATE INDEX IF NOT EXISTS ix_ledger_account ON ledger(account_id);
            CREATE INDEX IF NOT EXISTS ix_cashouts_account ON cashouts(account_id);

            -- Version 3: bandwidth sharing. One row per usage report a computer sends (idempotent by device + sequence).
            CREATE TABLE IF NOT EXISTS bandwidth_reports (
                device_id       TEXT NOT NULL,
                seq             INTEGER NOT NULL,
                account_id      TEXT NOT NULL,
                ip              TEXT NOT NULL,
                period_start    TEXT NOT NULL,
                period_end      TEXT NOT NULL,
                bytes           INTEGER NOT NULL,
                seconds         REAL NOT NULL,
                credited_bytes  INTEGER NOT NULL,
                credited_micros INTEGER NOT NULL,
                status          TEXT NOT NULL,
                created_at      TEXT NOT NULL,
                PRIMARY KEY (device_id, seq)
            );
            CREATE INDEX IF NOT EXISTS ix_bandwidth_ip ON bandwidth_reports(ip, period_end);
            """);
        Execute($"PRAGMA user_version = {SchemaVersion};");
    }

    // ---------- accounts ----------

    public AccountRecord? FindAccount(string accountId) => QueryAccount("SELECT * FROM accounts WHERE account_id = $p", accountId);

    public AccountRecord? FindAccountByCode(string code) => QueryAccount("SELECT * FROM accounts WHERE code = $p", code);

    public AccountRecord? FindAccountByWorker(string workerId) => QueryAccount("SELECT * FROM accounts WHERE worker_id = $p", workerId);

    public AccountRecord? FindAccountByEmail(string email) => QueryAccount("SELECT * FROM accounts WHERE email = $p", email);

    /// <summary>Follows merges, so old worker ids and referral codes keep crediting the surviving account.</summary>
    public AccountRecord? Resolve(AccountRecord? account)
    {
        for (var hops = 0; account?.MergedInto is { } next && hops < 10; hops++)
            account = FindAccount(next);
        return account;
    }

    public bool CodeExists(string code) => FindAccountByCode(code) is not null;

    public bool WorkerExists(string workerId) => FindAccountByWorker(workerId) is not null;

    /// <summary>True when some other account paying out to this wallet has already been referred by anyone.</summary>
    public bool WalletAlreadyReferred(string wallet, string exceptAccountId) =>
        Scalar<long>("SELECT COUNT(*) FROM accounts WHERE own_wallet = $a AND referred_by IS NOT NULL AND account_id <> $d",
            ("$a", wallet), ("$d", exceptAccountId)) > 0;

    public IReadOnlyList<AccountRecord> FindReferees(string code) =>
        QueryAccounts("SELECT * FROM accounts WHERE referred_by = $p AND merged_into IS NULL", ("$p", code));

    public void Upsert(AccountRecord r) =>
        Execute("""
            INSERT INTO accounts (account_id, worker_id, pool_wallet, own_wallet, code, email, referred_by, referred_at,
                                  fee_free_until, verified, last_checked_at, merged_into, created_at)
            VALUES ($id, $worker, $pool, $wallet, $code, $email, $by, $at, $free, $verified, $checked, $merged, $created)
            ON CONFLICT(account_id) DO UPDATE SET
                pool_wallet = excluded.pool_wallet,
                own_wallet = excluded.own_wallet,
                email = excluded.email,
                referred_by = excluded.referred_by,
                referred_at = excluded.referred_at,
                fee_free_until = excluded.fee_free_until,
                verified = excluded.verified,
                last_checked_at = excluded.last_checked_at,
                merged_into = excluded.merged_into;
            """,
            ("$id", r.AccountId), ("$worker", r.WorkerId), ("$pool", r.PoolWallet), ("$wallet", r.OwnWallet), ("$code", r.Code),
            ("$email", r.Email), ("$by", r.ReferredByCode), ("$at", Format(r.ReferredAt)), ("$free", Format(r.FeeFreeUntil)),
            ("$verified", r.Verified ? 1 : 0), ("$checked", Format(r.LastCheckedAt)), ("$merged", r.MergedInto), ("$created", Format(r.CreatedAt)));

    /// <summary>
    /// Folds <paramref name="from"/> into <paramref name="into"/>: money, cash-outs and devices move, and the old
    /// account remembers where it went so its worker id and referral code keep crediting the right place.
    /// </summary>
    public void MergeAccount(string from, string into)
    {
        using var connection = Open();
        using var tx = connection.BeginTransaction(deferred: false);
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            UPDATE ledger   SET account_id = $into WHERE account_id = $from;
            UPDATE cashouts SET account_id = $into WHERE account_id = $from;
            UPDATE devices  SET account_id = $into WHERE account_id = $from;
            UPDATE accounts SET merged_into = $into, email = NULL WHERE account_id = $from;
            """;
        cmd.Parameters.AddWithValue("$from", from);
        cmd.Parameters.AddWithValue("$into", into);
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    // ---------- devices ----------

    public DeviceRecord? FindDevice(string deviceId)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM devices WHERE device_id = $p";
        cmd.Parameters.AddWithValue("$p", deviceId);
        using var r = cmd.ExecuteReader();
        return r.Read()
            ? new DeviceRecord(
                r.GetString(r.GetOrdinal("device_id")),
                r.GetString(r.GetOrdinal("secret_hash")),
                r.GetString(r.GetOrdinal("account_id")),
                Parse(r.GetString(r.GetOrdinal("created_at"))),
                Parse(r.GetString(r.GetOrdinal("last_seen_at"))))
            : null;
    }

    public void Upsert(DeviceRecord d) =>
        Execute("""
            INSERT INTO devices (device_id, secret_hash, account_id, created_at, last_seen_at)
            VALUES ($id, $secret, $account, $created, $seen)
            ON CONFLICT(device_id) DO UPDATE SET account_id = excluded.account_id, last_seen_at = excluded.last_seen_at;
            """,
            ("$id", d.DeviceId), ("$secret", d.SecretHash), ("$account", d.AccountId), ("$created", Format(d.CreatedAt)), ("$seen", Format(d.LastSeenAt)));

    public long DeviceCount(string accountId) => Scalar<long>("SELECT COUNT(*) FROM devices WHERE account_id = $a", ("$a", accountId));

    // ---------- email sign-in codes ----------

    public void AddEmailCode(string email, string deviceId, string codeHash, string salt, DateTimeOffset expiresAt, DateTimeOffset now) =>
        Execute("""
            INSERT INTO email_codes (email, device_id, code_hash, salt, expires_at, attempts, used, created_at)
            VALUES ($e, $d, $h, $s, $x, 0, 0, $c)
            """, ("$e", email), ("$d", deviceId), ("$h", codeHash), ("$s", salt), ("$x", Format(expiresAt)), ("$c", Format(now)));

    public long EmailCodesSince(string email, DateTimeOffset since) =>
        Scalar<long>("SELECT COUNT(*) FROM email_codes WHERE email = $e AND created_at >= $s", ("$e", email), ("$s", Format(since)));

    public long DeviceEmailCodesSince(string deviceId, DateTimeOffset since) =>
        Scalar<long>("SELECT COUNT(*) FROM email_codes WHERE device_id = $d AND created_at >= $s", ("$d", deviceId), ("$s", Format(since)));

    /// <summary>The newest unused, unexpired code for this email requested by this device.</summary>
    public EmailCodeRecord? LatestEmailCode(string email, string deviceId, DateTimeOffset now)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM email_codes
            WHERE email = $e AND device_id = $d AND used = 0 AND expires_at > $now
            ORDER BY id DESC LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$e", email);
        cmd.Parameters.AddWithValue("$d", deviceId);
        cmd.Parameters.AddWithValue("$now", Format(now));
        using var r = cmd.ExecuteReader();
        return r.Read()
            ? new EmailCodeRecord(
                r.GetInt64(r.GetOrdinal("id")),
                r.GetString(r.GetOrdinal("email")),
                r.GetString(r.GetOrdinal("device_id")),
                r.GetString(r.GetOrdinal("code_hash")),
                r.GetString(r.GetOrdinal("salt")),
                Parse(r.GetString(r.GetOrdinal("expires_at"))),
                (int)r.GetInt64(r.GetOrdinal("attempts")),
                r.GetInt64(r.GetOrdinal("used")) != 0,
                Parse(r.GetString(r.GetOrdinal("created_at"))))
            : null;
    }

    public void CountEmailCodeAttempt(long id) => Execute("UPDATE email_codes SET attempts = attempts + 1 WHERE id = $id", ("$id", id));

    public void UseEmailCode(long id) => Execute("UPDATE email_codes SET used = 1 WHERE id = $id", ("$id", id));

    // ---------- bandwidth ----------

    public BandwidthReportRecord? FindBandwidthReport(string deviceId, long seq) =>
        QueryBandwidth("SELECT * FROM bandwidth_reports WHERE device_id = $d AND seq = $s", ("$d", deviceId), ("$s", seq)).FirstOrDefault();

    public void AddBandwidthReport(BandwidthReportRecord r) =>
        Execute("""
            INSERT INTO bandwidth_reports (device_id, seq, account_id, ip, period_start, period_end, bytes, seconds,
                                           credited_bytes, credited_micros, status, created_at)
            VALUES ($d, $s, $a, $ip, $ps, $pe, $b, $sec, $cb, $cm, $st, $c)
            """,
            ("$d", r.DeviceId), ("$s", r.Seq), ("$a", r.AccountId), ("$ip", r.Ip), ("$ps", Format(r.PeriodStart)),
            ("$pe", Format(r.PeriodEnd)), ("$b", r.Bytes), ("$sec", r.Seconds), ("$cb", r.CreditedBytes),
            ("$cm", r.CreditedMicros), ("$st", r.Status), ("$c", Format(r.CreatedAt)));

    /// <summary>Bytes already credited to a computer for periods ending after <paramref name="since"/>.</summary>
    public long CreditedBandwidthBytesSince(string deviceId, DateTimeOffset since) =>
        Scalar<long>("SELECT COALESCE(SUM(credited_bytes), 0) FROM bandwidth_reports WHERE device_id = $d AND period_end > $s",
            ("$d", deviceId), ("$s", Format(since)));

    /// <summary>Another computer that was sharing (and paid for it) from this internet connection in the window, if any.</summary>
    public string? OtherSharerOnIp(string ip, string deviceId, DateTimeOffset since) =>
        QueryBandwidth("""
            SELECT * FROM bandwidth_reports WHERE ip = $ip AND device_id <> $d AND period_end > $s AND status <> 'shared-connection'
            ORDER BY period_end DESC LIMIT 1
            """, ("$ip", ip), ("$d", deviceId), ("$s", Format(since))).FirstOrDefault()?.DeviceId;

    public (long Bytes, long Micros) BandwidthTotals() =>
        QueryBandwidth("SELECT * FROM bandwidth_reports").Aggregate((0L, 0L), (t, r) => (t.Item1 + r.CreditedBytes, t.Item2 + r.CreditedMicros));

    private IReadOnlyList<BandwidthReportRecord> QueryBandwidth(string sql, params (string, object?)[] parameters)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        Bind(cmd, parameters);
        using var r = cmd.ExecuteReader();
        var list = new List<BandwidthReportRecord>();
        while (r.Read())
        {
            list.Add(new BandwidthReportRecord(
                r.GetString(r.GetOrdinal("device_id")),
                r.GetInt64(r.GetOrdinal("seq")),
                r.GetString(r.GetOrdinal("account_id")),
                r.GetString(r.GetOrdinal("ip")),
                Parse(r.GetString(r.GetOrdinal("period_start"))),
                Parse(r.GetString(r.GetOrdinal("period_end"))),
                r.GetInt64(r.GetOrdinal("bytes")),
                r.GetDouble(r.GetOrdinal("seconds")),
                r.GetInt64(r.GetOrdinal("credited_bytes")),
                r.GetInt64(r.GetOrdinal("credited_micros")),
                r.GetString(r.GetOrdinal("status")),
                Parse(r.GetString(r.GetOrdinal("created_at")))));
        }
        return list;
    }

    // ---------- ledger ----------

    public void AddLedger(string accountId, string kind, long micros, DateTimeOffset now, DateTimeOffset availableAt, string? reference) =>
        Execute("INSERT INTO ledger (account_id, kind, micros, created_at, available_at, reference) VALUES ($d, $k, $m, $c, $a, $r)",
            ("$d", accountId), ("$k", kind), ("$m", micros), ("$c", Format(now)), ("$a", Format(availableAt)), ("$r", reference));

    public BalanceTotals Balance(string accountId, DateTimeOffset now)
    {
        var entries = QueryLedger("SELECT * FROM ledger WHERE account_id = $p", ("$p", accountId));
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

    /// <summary>Earnings credited to users for a source, identified by its ledger reference prefix (e.g. "kx:").</summary>
    public long CreditedWithReferencePrefix(string prefix) =>
        Scalar<long>("SELECT COALESCE(SUM(micros), 0) FROM ledger WHERE kind = 'earning' AND reference LIKE $p", ("$p", prefix + "%"));

    /// <summary>Gross earnings credited to an account (before any cash-outs), for referral verification.</summary>
    public long LifetimeEarningMicros(string accountId) =>
        Scalar<long>("SELECT COALESCE(SUM(micros), 0) FROM ledger WHERE account_id = $d AND kind = 'earning'", ("$d", accountId));

    /// <summary>
    /// Debits the whole available balance and records the cash-out in one transaction, so a double-click or
    /// two computers can never spend the same money twice. Returns null when the available balance is below the minimum.
    /// </summary>
    public CashoutRecord? DebitAndCreateCashout(string accountId, string method, string destination, long minimumMicros, string status, DateTimeOffset now)
    {
        using var connection = Open();
        using var tx = connection.BeginTransaction(deferred: false);

        long available;
        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT COALESCE(SUM(micros), 0) FROM ledger WHERE account_id = $d AND available_at <= $now";
            cmd.Parameters.AddWithValue("$d", accountId);
            cmd.Parameters.AddWithValue("$now", Format(now));
            available = Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        if (available < minimumMicros || available <= 0)
        {
            tx.Rollback();
            return null;
        }

        var cashout = new CashoutRecord($"co_{Guid.NewGuid():N}", accountId, method, destination, available, status, now, now, null, null, 0);
        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO cashouts (id, account_id, method, destination, micros, status, created_at, updated_at, attempts)
                VALUES ($id, $d, $m, $dest, $micros, $s, $c, $c, 0);
                INSERT INTO ledger (account_id, kind, micros, created_at, available_at, reference)
                VALUES ($d, 'cashout', -$micros, $c, $c, $id);
                """;
            cmd.Parameters.AddWithValue("$id", cashout.Id);
            cmd.Parameters.AddWithValue("$d", accountId);
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

    public IReadOnlyList<CashoutRecord> CashoutsFor(string accountId, int limit = 10) =>
        QueryCashouts($"SELECT * FROM cashouts WHERE account_id = $p ORDER BY created_at DESC LIMIT {limit}", ("$p", accountId));

    public IReadOnlyList<CashoutRecord> CashoutsWithStatus(params string[] statuses) =>
        QueryCashouts($"SELECT * FROM cashouts WHERE status IN ({string.Join(",", statuses.Select((_, i) => $"$s{i}"))}) ORDER BY created_at",
            statuses.Select((s, i) => ($"$s{i}", (object?)s)).ToArray());

    /// <summary>Everything ever paid out to an account.</summary>
    public long PaidOutMicros(string accountId) =>
        Scalar<long>("SELECT COALESCE(SUM(micros), 0) FROM cashouts WHERE account_id = $d AND status = 'paid'", ("$d", accountId));

    public bool HasOpenCashout(string accountId) =>
        Scalar<long>("SELECT COUNT(*) FROM cashouts WHERE account_id = $d AND status IN ('queued','review','sending')", ("$d", accountId)) > 0;

    /// <summary>Other accounts that have cashed out to the same destination (a sign of one person running many accounts).</summary>
    public long OtherAccountsUsingDestination(string destination, string accountId) =>
        Scalar<long>("SELECT COUNT(DISTINCT account_id) FROM cashouts WHERE destination = $dest AND account_id <> $d", ("$dest", destination), ("$d", accountId));

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
            INSERT INTO ledger (account_id, kind, micros, created_at, available_at, reference)
            SELECT account_id, 'refund', micros, $u, $u, id FROM cashouts WHERE id = $id AND changes() > 0;
            """;
        cmd.Parameters.AddWithValue("$u", Format(now));
        cmd.Parameters.AddWithValue("$note", reason);
        cmd.Parameters.AddWithValue("$id", cashout.Id);
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
            SELECT w.address, (SELECT COUNT(*) FROM accounts a WHERE a.pool_wallet = w.address AND a.merged_into IS NULL) AS n
            FROM pool_wallets w ORDER BY n, w.created_at
            """;
        using var reader = cmd.ExecuteReader();
        var result = new List<(string, long)>();
        while (reader.Read())
            result.Add((reader.GetString(0), reader.GetInt64(1)));
        return result;
    }

    // ---------- tax tracking ----------

    public long PaidMicrosInYear(string accountId, int year) =>
        Scalar<long>("SELECT COALESCE(SUM(micros),0) FROM cashouts WHERE account_id = $d AND status IN ('queued','sending','paid') AND substr(created_at,1,4) = $y",
            ("$d", accountId), ("$y", year.ToString(CultureInfo.InvariantCulture)));

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

    private AccountRecord? QueryAccount(string sql, string parameter) => QueryAccounts(sql, ("$p", parameter)).FirstOrDefault();

    private IReadOnlyList<AccountRecord> QueryAccounts(string sql, params (string, object?)[] parameters)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        Bind(cmd, parameters);
        using var r = cmd.ExecuteReader();
        var list = new List<AccountRecord>();
        while (r.Read())
        {
            list.Add(new AccountRecord(
                r.GetString(r.GetOrdinal("account_id")),
                r.GetString(r.GetOrdinal("worker_id")),
                NullableString(r, "pool_wallet"),
                NullableString(r, "own_wallet"),
                r.GetString(r.GetOrdinal("code")),
                NullableString(r, "email"),
                NullableString(r, "referred_by"),
                ParseNullable(NullableString(r, "referred_at")),
                ParseNullable(NullableString(r, "fee_free_until")),
                r.GetInt64(r.GetOrdinal("verified")) != 0,
                ParseNullable(NullableString(r, "last_checked_at")),
                NullableString(r, "merged_into"),
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
                r.GetString(r.GetOrdinal("account_id")),
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
                r.GetString(r.GetOrdinal("account_id")),
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
