using System.Globalization;
using Microsoft.Data.Sqlite;
using Otium.Core.Accounts;

namespace Otium.Server.Data;

/// <summary>
/// A person's account: the balance, referral code and pool worker belong here, so they survive reinstalls and
/// can be shared by several computers. <paramref name="MergedInto"/> is set when this account was folded into
/// another one (someone signed in with an email that already had an account). <paramref name="DeletedAt"/> is set
/// when the user deleted the account: the row stays, anonymized, so its money records still add up.
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
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeletedAt = null)
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
/// <summary>Mysterium's registration fee Otium paid for one node (see MystSponsorService).</summary>
public sealed record MystSponsorshipRecord(
    string Identity,
    string ChannelAddress,
    string AccountId,
    string DeviceId,
    string Ip,
    decimal AmountMyst,
    string TransactionId,
    DateTimeOffset CreatedAt);

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
    private const int SchemaVersion = 9;

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
            CREATE INDEX IF NOT EXISTS ix_bandwidth_account ON bandwidth_reports(account_id);
            """);

        // Version 4: account deletion. A deleted account keeps its (anonymized) row and money records.
        if (Scalar<long>("SELECT COUNT(*) FROM pragma_table_info('accounts') WHERE name = 'deleted_at'") == 0)
            Execute("ALTER TABLE accounts ADD COLUMN deleted_at TEXT NULL;");

        // Version 5: Mysterium registration fees Otium paid for users' nodes. Amounts in micro-MYST.
        Execute("""
            CREATE TABLE IF NOT EXISTS myst_sponsorships (
                identity     TEXT PRIMARY KEY,
                channel      TEXT NOT NULL,
                account_id   TEXT NOT NULL,
                device_id    TEXT NOT NULL,
                ip           TEXT NOT NULL,
                amount_micro INTEGER NOT NULL,
                tx           TEXT NOT NULL,
                created_at   TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_myst_account ON myst_sponsorships(account_id);
            CREATE INDEX IF NOT EXISTS ix_myst_device ON myst_sponsorships(device_id);
            CREATE INDEX IF NOT EXISTS ix_myst_ip ON myst_sponsorships(ip, created_at);
            """);

        // Version 6: real numbers for the website. Opt-in earnings reports (one row per device, report and processor or
        // graphics card) and what the pools have paid each account's own address.
        Execute("""
            CREATE TABLE IF NOT EXISTS earnings_reports (
                device_id       TEXT NOT NULL,
                seq             INTEGER NOT NULL,
                kind            TEXT NOT NULL,
                account_id      TEXT NOT NULL,
                model           TEXT NULL,
                period_start    TEXT NOT NULL,
                period_end      TEXT NOT NULL,
                seconds         REAL NOT NULL,
                user_usd        REAL NOT NULL,
                electricity_usd REAL NOT NULL,
                created_at      TEXT NOT NULL,
                PRIMARY KEY (device_id, seq, kind)
            );
            CREATE INDEX IF NOT EXISTS ix_earnings_end ON earnings_reports(period_end);
            CREATE INDEX IF NOT EXISTS ix_earnings_account ON earnings_reports(account_id);

            CREATE TABLE IF NOT EXISTS pool_payouts (
                account_id       TEXT PRIMARY KEY,
                coin             TEXT NOT NULL,
                paid             REAL NOT NULL,
                paid_usd         REAL NOT NULL,
                rewarded_30d_usd REAL NOT NULL,
                checked_at       TEXT NOT NULL
            );
            """);

        // Version 7: problem reports users send from the app, kept so support can look them up by reference.
        Execute("""
            CREATE TABLE IF NOT EXISTS problem_reports (
                reference   TEXT PRIMARY KEY,
                account_id  TEXT NOT NULL,
                device_id   TEXT NOT NULL,
                app_version TEXT NOT NULL,
                platform    TEXT NOT NULL,
                note        TEXT NULL,
                details     TEXT NOT NULL,
                created_at  TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_problem_device ON problem_reports(device_id, created_at);
            CREATE INDEX IF NOT EXISTS ix_problem_account ON problem_reports(account_id);
            """);

        // Version 8: invite links opened on an internet connection (a keyed hash of it), so Otium installed there fills
        // in the code; and the server's own random keys.
        Execute("""
            CREATE TABLE IF NOT EXISTS invite_clicks (
                network    TEXT PRIMARY KEY,
                code       TEXT NOT NULL,
                clicked_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS server_secrets (
                name  TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """);

        // Version 9: the internet connections an account's computers used lately (keyed hashes), so nobody can invite
        // themselves from their own connection; and what referred friends' apps say they mined for their inviter.
        Execute("""
            CREATE TABLE IF NOT EXISTS account_networks (
                account_id TEXT NOT NULL,
                network    TEXT NOT NULL,
                seen_at    TEXT NOT NULL,
                PRIMARY KEY (account_id, network)
            );
            CREATE INDEX IF NOT EXISTS ix_account_networks_network ON account_networks(network);
            CREATE TABLE IF NOT EXISTS referral_shares (
                device_id  TEXT PRIMARY KEY,
                account_id TEXT NOT NULL,
                usd        REAL NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_referral_shares_account ON referral_shares(account_id);
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

    /// <summary>Verified friends per referral code who joined since <paramref name="since"/>.</summary>
    public IReadOnlyList<(string Code, long Friends)> VerifiedReferralsSince(DateTimeOffset since)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT referred_by, COUNT(*) FROM accounts
            WHERE referred_by IS NOT NULL AND merged_into IS NULL AND verified = 1 AND referred_at >= $s
            GROUP BY referred_by
            """;
        cmd.Parameters.AddWithValue("$s", Format(since));
        using var reader = cmd.ExecuteReader();
        var result = new List<(string, long)>();
        while (reader.Read())
            result.Add((reader.GetString(0), reader.GetInt64(1)));
        return result;
    }

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

    /// <summary>Accounts folded into this one (directly or through other merged accounts).</summary>
    public IReadOnlyList<AccountRecord> MergedAccounts(string accountId)
    {
        var result = new List<AccountRecord>();
        var pending = new Queue<string>([accountId]);
        while (pending.Count > 0 && result.Count < 1000)
        {
            foreach (var merged in QueryAccounts("SELECT * FROM accounts WHERE merged_into = $p", ("$p", pending.Dequeue())))
            {
                result.Add(merged);
                pending.Enqueue(merged.AccountId);
            }
        }
        return result;
    }

    /// <summary>
    /// Deletes an account at the user's request, keeping what bookkeeping needs. Ledger and cash-out rows stay so
    /// totals and the operator's records still add up, but nothing in them points to the person any more: the email,
    /// pool worker id, referral code and wallets are dropped, computers (and their secrets) and sign-in codes are
    /// removed, cash-out destinations are masked and bandwidth reports lose their IP address and computer id. Accounts
    /// folded into this one earlier are anonymized too, since their worker ids and codes lead here. Friends this
    /// account invited keep their history and fee-free time but no longer point to it, and its own link to whoever
    /// invited it is dropped (the referrer's past rewards stay in their ledger).
    /// </summary>
    /// <returns>False (and nothing changed) when a cash-out is still open.</returns>
    public bool AnonymizeAccount(string accountId, Func<string, string, string> maskDestination, DateTimeOffset now)
    {
        var ids = new List<string> { accountId };
        ids.AddRange(MergedAccounts(accountId).Select(a => a.AccountId));

        using var connection = Open();
        using var tx = connection.BeginTransaction(deferred: false);

        long Run(string sql, params (string Name, object? Value)[] parameters)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            Bind(cmd, parameters);
            return cmd.ExecuteNonQuery();
        }

        List<string[]> Rows(string sql, params (string Name, object? Value)[] parameters)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            Bind(cmd, parameters);
            using var r = cmd.ExecuteReader();
            var rows = new List<string[]>();
            while (r.Read())
                rows.Add(Enumerable.Range(0, r.FieldCount).Select(i => r.IsDBNull(i) ? "" : r.GetString(i)).ToArray());
            return rows;
        }

        // Checked again inside the transaction, so a cash-out requested a moment ago can't be orphaned.
        var open = Rows("SELECT id FROM cashouts WHERE account_id = $a AND status IN ('queued','review','sending')", ("$a", accountId));
        if (open.Count > 0)
        {
            tx.Rollback();
            return false;
        }

        foreach (var id in ids)
        {
            var account = Rows("SELECT worker_id, code, COALESCE(email, '') FROM accounts WHERE account_id = $a", ("$a", id)).Single();
            var (worker, code, email) = (account[0], account[1], account[2]);
            var tombstone = $"deleted-{id}";

            Run("UPDATE accounts SET referred_by = NULL WHERE referred_by = $code", ("$code", code));
            // Crediting progress is keyed by worker id ("kx:<coin>:" + worker for Kryptex).
            Run("DELETE FROM worker_progress WHERE worker_id = $w OR worker_id LIKE '%:' || $w", ("$w", worker));
            Run("DELETE FROM email_codes WHERE email = $e OR device_id IN (SELECT device_id FROM devices WHERE account_id = $a)",
                ("$e", email), ("$a", id));
            Run("DELETE FROM devices WHERE account_id = $a", ("$a", id));
            Run("""
                UPDATE accounts SET worker_id = $t, code = $t, email = NULL, pool_wallet = NULL, own_wallet = NULL,
                    referred_by = NULL, referred_at = NULL, deleted_at = $now
                WHERE account_id = $a
                """, ("$t", tombstone), ("$now", Format(now)), ("$a", id));

            foreach (var cashout in Rows("SELECT id, method, destination FROM cashouts WHERE account_id = $a", ("$a", id)))
                Run("UPDATE cashouts SET destination = $d WHERE id = $id", ("$d", maskDestination(cashout[1], cashout[2])), ("$id", cashout[0]));

            // The record of a paid Mysterium fee stays (it's Otium's spending), without the computer or connection.
            Run("UPDATE myst_sponsorships SET device_id = $t, ip = '' WHERE account_id = $a", ("$t", tombstone), ("$a", id));

            // Earnings statistics the user chose to share, and what the pools paid their address, go with the account.
            Run("DELETE FROM earnings_reports WHERE account_id = $a", ("$a", id));
            Run("DELETE FROM problem_reports WHERE account_id = $a", ("$a", id));
            Run("DELETE FROM account_networks WHERE account_id = $a", ("$a", id));
            Run("DELETE FROM referral_shares WHERE account_id = $a", ("$a", id));
            Run("DELETE FROM pool_payouts WHERE account_id = $a", ("$a", id));

            // A new random id per computer keeps (device, sequence) unique if this computer starts sharing again.
            foreach (var device in Rows("SELECT DISTINCT device_id FROM bandwidth_reports WHERE account_id = $a", ("$a", id)))
                Run("UPDATE bandwidth_reports SET device_id = $t, ip = '' WHERE account_id = $a AND device_id = $d",
                    ("$t", $"deleted-{Guid.NewGuid():N}"), ("$a", id), ("$d", device[0]));
        }

        tx.Commit();
        return true;
    }

    // ---------- devices ----------

    public DeviceRecord? FindDevice(string deviceId) => QueryDevices("SELECT * FROM devices WHERE device_id = $p", ("$p", deviceId)).FirstOrDefault();

    public IReadOnlyList<DeviceRecord> DevicesFor(string accountId) =>
        QueryDevices("SELECT * FROM devices WHERE account_id = $p ORDER BY created_at", ("$p", accountId));

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
                r.GetString(r.GetOrdinal("account_id")),
                Parse(r.GetString(r.GetOrdinal("created_at"))),
                Parse(r.GetString(r.GetOrdinal("last_seen_at")))));
        }
        return list;
    }

    public void Upsert(DeviceRecord d) =>
        Execute("""
            INSERT INTO devices (device_id, secret_hash, account_id, created_at, last_seen_at)
            VALUES ($id, $secret, $account, $created, $seen)
            ON CONFLICT(device_id) DO UPDATE SET account_id = excluded.account_id, last_seen_at = excluded.last_seen_at;
            """,
            ("$id", d.DeviceId), ("$secret", d.SecretHash), ("$account", d.AccountId), ("$created", Format(d.CreatedAt)), ("$seen", Format(d.LastSeenAt)));

    /// <summary>
    /// Records that a computer checked in, at most once per <paramref name="every"/> (the app checks in every minute;
    /// "online now" on the admin dashboard needs only a few minutes' precision, so most check-ins write nothing).
    /// </summary>
    public void TouchDevice(string deviceId, DateTimeOffset now, TimeSpan every) =>
        Execute("UPDATE devices SET last_seen_at = $n WHERE device_id = $d AND last_seen_at < $cut",
            ("$n", Format(now)), ("$d", deviceId), ("$cut", Format(now - every)));

    public long DeviceCount(string accountId) => Scalar<long>("SELECT COUNT(*) FROM devices WHERE account_id = $a", ("$a", accountId));

    // ---------- connections and referral shares (anti self-invites; "earned from friends") ----------

    /// <summary>Notes that one of the account's computers used this connection (a keyed hash), at most hourly per connection.</summary>
    public void RecordAccountNetwork(string accountId, string network, DateTimeOffset now) =>
        Execute("""
            INSERT INTO account_networks (account_id, network, seen_at) VALUES ($a, $n, $t)
            ON CONFLICT(account_id, network) DO UPDATE SET seen_at = excluded.seen_at WHERE account_networks.seen_at < $cut
            """, ("$a", accountId), ("$n", network), ("$t", Format(now)), ("$cut", Format(now - TimeSpan.FromHours(1))));

    public bool AccountUsedNetwork(string accountId, string network, DateTimeOffset since) =>
        Scalar<long>("SELECT COUNT(*) FROM account_networks WHERE account_id = $a AND network = $n AND seen_at >= $s",
            ("$a", accountId), ("$n", network), ("$s", Format(since))) > 0;

    public void DeleteAccountNetworksBefore(DateTimeOffset before) =>
        Execute("DELETE FROM account_networks WHERE seen_at < $b", ("$b", Format(before)));

    /// <summary>A friend's computer's running total of what it mined for its inviter; it never goes down.</summary>
    public void SetReferralShare(string deviceId, string accountId, double usd, DateTimeOffset now) =>
        Execute("""
            INSERT INTO referral_shares (device_id, account_id, usd, updated_at) VALUES ($d, $a, $u, $t)
            ON CONFLICT(device_id) DO UPDATE SET account_id = excluded.account_id, usd = MAX(referral_shares.usd, excluded.usd), updated_at = excluded.updated_at
            """, ("$d", deviceId), ("$a", accountId), ("$u", usd), ("$t", Format(now)));

    /// <summary>What every friend who joined with this code has mined for its owner, by their apps' estimates.</summary>
    public double ReferralShareTotal(string code) =>
        Scalar<double>("""
            SELECT COALESCE(SUM(s.usd), 0) FROM referral_shares s JOIN accounts a ON a.account_id = s.account_id
            WHERE a.referred_by = $c
            """, ("$c", code));

    // ---------- invite links opened (see InviteLinks) ----------

    public void RecordInviteClick(string network, string code, DateTimeOffset now) =>
        Execute("""
            INSERT INTO invite_clicks (network, code, clicked_at) VALUES ($n, $c, $t)
            ON CONFLICT(network) DO UPDATE SET code = excluded.code, clicked_at = excluded.clicked_at
            """, ("$n", network), ("$c", code), ("$t", Format(now)));

    /// <summary>The code last opened on this network since <paramref name="since"/>, or null.</summary>
    public string? InviteClickCode(string network, DateTimeOffset since)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT code FROM invite_clicks WHERE network = $n AND clicked_at >= $s";
        Bind(cmd, [("$n", network), ("$s", Format(since))]);
        return cmd.ExecuteScalar() as string;
    }

    public void DeleteInviteClicksBefore(DateTimeOffset before) =>
        Execute("DELETE FROM invite_clicks WHERE clicked_at < $b", ("$b", Format(before)));

    public long InviteClickCount() => Scalar<long>("SELECT COUNT(*) FROM invite_clicks");

    /// <summary>A random 32-byte key (base64) made the first time it's asked for and kept in the database.</summary>
    public string ServerSecret(string name)
    {
        Execute("INSERT OR IGNORE INTO server_secrets (name, value) VALUES ($n, $v)",
            ("$n", name), ("$v", Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))));
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM server_secrets WHERE name = $n";
        Bind(cmd, [("$n", name)]);
        return (string)cmd.ExecuteScalar()!;
    }

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

    /// <summary>Wrong tries over every code sent to this email since <paramref name="since"/>.</summary>
    public long EmailCodeAttemptsSince(string email, DateTimeOffset since) =>
        Scalar<long>("SELECT COALESCE(SUM(attempts), 0) FROM email_codes WHERE email = $e AND created_at >= $s", ("$e", email), ("$s", Format(since)));

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

    public IReadOnlyList<BandwidthReportRecord> BandwidthReportsFor(string accountId) =>
        QueryBandwidth("SELECT * FROM bandwidth_reports WHERE account_id = $a ORDER BY period_start", ("$a", accountId));

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

    // ---------- problem reports ----------

    public sealed record ProblemReportRecord(string Reference, string AccountId, string DeviceId, string AppVersion, string Platform,
        string? Note, string Details, DateTimeOffset CreatedAt);

    public void AddProblemReport(ProblemReportRecord r) =>
        Execute("""
            INSERT INTO problem_reports (reference, account_id, device_id, app_version, platform, note, details, created_at)
            VALUES ($r, $a, $d, $v, $p, $n, $t, $c)
            """, ("$r", r.Reference), ("$a", r.AccountId), ("$d", r.DeviceId), ("$v", r.AppVersion), ("$p", r.Platform),
            ("$n", r.Note), ("$t", r.Details), ("$c", Format(r.CreatedAt)));

    /// <summary>Characters stored in all problem reports' details and notes.</summary>
    public long ProblemReportCharacters() =>
        Scalar<long>("SELECT COALESCE(SUM(LENGTH(details) + COALESCE(LENGTH(note), 0)), 0) FROM problem_reports");

    public long ProblemReportsSince(string deviceId, DateTimeOffset since) =>
        Scalar<long>("SELECT COUNT(*) FROM problem_reports WHERE device_id = $d AND created_at > $s", ("$d", deviceId), ("$s", Format(since)));

    /// <summary>The newest reports first, without their details (<see cref="FindProblemReport"/> has those).</summary>
    public IReadOnlyList<ProblemReportRecord> ProblemReports(int limit = 50) =>
        QueryProblemReports("SELECT reference, account_id, device_id, app_version, platform, note, '' AS details, created_at FROM problem_reports ORDER BY created_at DESC LIMIT $l",
            ("$l", limit));

    public ProblemReportRecord? FindProblemReport(string reference) =>
        QueryProblemReports("SELECT * FROM problem_reports WHERE reference = $r", ("$r", reference)).FirstOrDefault();

    public IReadOnlyList<ProblemReportRecord> ProblemReportsFor(string accountId) =>
        QueryProblemReports("SELECT * FROM problem_reports WHERE account_id = $a ORDER BY created_at", ("$a", accountId));

    private IReadOnlyList<ProblemReportRecord> QueryProblemReports(string sql, params (string, object?)[] parameters)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        Bind(cmd, parameters);
        using var r = cmd.ExecuteReader();
        var list = new List<ProblemReportRecord>();
        while (r.Read())
        {
            list.Add(new ProblemReportRecord(
                r.GetString(r.GetOrdinal("reference")), r.GetString(r.GetOrdinal("account_id")), r.GetString(r.GetOrdinal("device_id")),
                r.GetString(r.GetOrdinal("app_version")), r.GetString(r.GetOrdinal("platform")),
                r.IsDBNull(r.GetOrdinal("note")) ? null : r.GetString(r.GetOrdinal("note")),
                r.GetString(r.GetOrdinal("details")), Parse(r.GetString(r.GetOrdinal("created_at")))));
        }
        return list;
    }

    // ---------- real earnings (opt-in reports, pool payouts) ----------

    /// <summary>Deletes everything a computer shared for the real-earnings figures (the user withdrew their consent).</summary>
    public void DeleteEarningsReports(string deviceId)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM earnings_reports WHERE device_id = $d";
        cmd.Parameters.AddWithValue("$d", deviceId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Stores a report's rows. Returns false when this report (device and sequence) was already stored.</summary>
    public bool AddEarningsReport(string deviceId, string accountId, EarningsReport report, DateTimeOffset now)
    {
        using var connection = Open();
        using var tx = connection.BeginTransaction(deferred: false);
        var added = 0;
        foreach (var device in report.Devices)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT OR IGNORE INTO earnings_reports (device_id, seq, kind, account_id, model, period_start, period_end,
                                                        seconds, user_usd, electricity_usd, created_at)
                VALUES ($d, $s, $k, $a, $m, $ps, $pe, $sec, $u, $e, $c)
                """;
            Bind(cmd, [("$d", deviceId), ("$s", report.Sequence), ("$k", device.Kind), ("$a", accountId), ("$m", device.Model),
                ("$ps", Format(report.PeriodStart)), ("$pe", Format(report.PeriodEnd)), ("$sec", device.EarningSeconds),
                ("$u", device.UserUsd), ("$e", device.ElectricityUsd), ("$c", Format(now))]);
            added += cmd.ExecuteNonQuery();
        }
        tx.Commit();
        return added > 0;
    }

    /// <summary>Per device and model, what was earned in reports ending after <paramref name="since"/>.</summary>
    public IReadOnlyList<(string DeviceId, string Kind, string Model, double Seconds, double UserUsd, double ElectricityUsd)> EarningsSince(DateTimeOffset since)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT device_id, kind, model, SUM(seconds), SUM(user_usd), SUM(electricity_usd) FROM earnings_reports
            WHERE period_end > $s AND model IS NOT NULL GROUP BY device_id, kind, model
            """;
        Bind(cmd, [("$s", Format(since))]);
        using var r = cmd.ExecuteReader();
        var list = new List<(string, string, string, double, double, double)>();
        while (r.Read())
            list.Add((r.GetString(0), r.GetString(1), r.GetString(2), r.GetDouble(3), r.GetDouble(4), r.GetDouble(5)));
        return list;
    }

    public IReadOnlyList<(string Kind, string? Model, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd, double Seconds, double UserUsd, double ElectricityUsd)> EarningsReportsFor(string accountId)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT kind, model, period_start, period_end, seconds, user_usd, electricity_usd FROM earnings_reports
            WHERE account_id = $a ORDER BY period_start, kind
            """;
        Bind(cmd, [("$a", accountId)]);
        using var r = cmd.ExecuteReader();
        var list = new List<(string, string?, DateTimeOffset, DateTimeOffset, double, double, double)>();
        while (r.Read())
            list.Add((r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1), Parse(r.GetString(2)), Parse(r.GetString(3)), r.GetDouble(4), r.GetDouble(5), r.GetDouble(6)));
        return list;
    }

    /// <summary>Accounts paid at their own address (not merged or deleted), with the address as stored.</summary>
    public IReadOnlyList<(string AccountId, string Wallet)> OwnWallets()
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT account_id, own_wallet FROM accounts WHERE own_wallet IS NOT NULL AND merged_into IS NULL AND deleted_at IS NULL";
        using var r = cmd.ExecuteReader();
        var list = new List<(string, string)>();
        while (r.Read())
            list.Add((r.GetString(0), r.GetString(1)));
        return list;
    }

    public void SetPoolPayout(string accountId, string coin, double paid, double paidUsd, double rewarded30dUsd, DateTimeOffset now) =>
        Execute("""
            INSERT INTO pool_payouts (account_id, coin, paid, paid_usd, rewarded_30d_usd, checked_at)
            VALUES ($a, $c, $p, $pu, $r, $t)
            ON CONFLICT(account_id) DO UPDATE SET coin = $c, paid = $p, paid_usd = $pu, rewarded_30d_usd = $r, checked_at = $t
            """, ("$a", accountId), ("$c", coin), ("$p", paid), ("$pu", paidUsd), ("$r", rewarded30dUsd), ("$t", Format(now)));

    /// <summary>
    /// What the pools have paid Otium users' own addresses, in dollars at the time of each check, how many were paid, and
    /// how many the pools have credited or paid at all.
    /// </summary>
    public (double PaidUsd, double Rewarded30dUsd, long PaidAccounts, long EarningAccounts) PoolPayoutTotals()
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COALESCE(SUM(p.paid_usd), 0), COALESCE(SUM(p.rewarded_30d_usd), 0), COALESCE(SUM(CASE WHEN p.paid > 0 THEN 1 ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN p.paid > 0 OR p.rewarded_30d_usd > 0 THEN 1 ELSE 0 END), 0)
            FROM pool_payouts p JOIN accounts a ON a.account_id = p.account_id
            WHERE a.merged_into IS NULL AND a.deleted_at IS NULL
            """;
        using var r = cmd.ExecuteReader();
        r.Read();
        return (r.GetDouble(0), r.GetDouble(1), r.GetInt64(2), r.GetInt64(3));
    }

    // ---------- ledger ----------

    public void AddLedger(string accountId, string kind, long micros, DateTimeOffset now, DateTimeOffset availableAt, string? reference) =>
        Execute("INSERT INTO ledger (account_id, kind, micros, created_at, available_at, reference) VALUES ($d, $k, $m, $c, $a, $r)",
            ("$d", accountId), ("$k", kind), ("$m", micros), ("$c", Format(now)), ("$a", Format(availableAt)), ("$r", reference));

    public IReadOnlyList<LedgerEntry> LedgerFor(string accountId) =>
        QueryLedger("SELECT * FROM ledger WHERE account_id = $p ORDER BY id", ("$p", accountId));

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

    /// <summary>Cash-outs debited from balances but not paid yet.</summary>
    public long OpenCashoutMicros() =>
        Scalar<long>("SELECT COALESCE(SUM(micros), 0) FROM cashouts WHERE status IN ('queued','review','sending')");

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
    // ---------- Mysterium sponsorships ----------

    public void AddMystSponsorship(MystSponsorshipRecord r) =>
        Execute("""
            INSERT INTO myst_sponsorships (identity, channel, account_id, device_id, ip, amount_micro, tx, created_at)
            VALUES ($i, $c, $a, $d, $ip, $m, $t, $at)
            """, ("$i", r.Identity), ("$c", r.ChannelAddress), ("$a", r.AccountId), ("$d", r.DeviceId), ("$ip", r.Ip),
            ("$m", (long)decimal.Round(r.AmountMyst * 1_000_000m)), ("$t", r.TransactionId), ("$at", Format(r.CreatedAt)));

    public MystSponsorshipRecord? FindMystSponsorship(string identity)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT identity, channel, account_id, device_id, ip, amount_micro, tx, created_at FROM myst_sponsorships WHERE identity = $i";
        cmd.Parameters.AddWithValue("$i", identity);
        using var reader = cmd.ExecuteReader();
        return reader.Read()
            ? new MystSponsorshipRecord(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetInt64(5) / 1_000_000m, reader.GetString(6), Parse(reader.GetString(7)))
            : null;
    }

    public long MystSponsorshipsForDevice(string deviceId) =>
        Scalar<long>("SELECT COUNT(*) FROM myst_sponsorships WHERE device_id = $d", ("$d", deviceId));

    public long MystSponsorshipsForAccount(string accountId) =>
        Scalar<long>("SELECT COUNT(*) FROM myst_sponsorships WHERE account_id = $a", ("$a", accountId));

    public long MystSponsorshipsFromIpSince(string ip, DateTimeOffset since) =>
        Scalar<long>("SELECT COUNT(*) FROM myst_sponsorships WHERE ip = $ip AND created_at >= $s", ("$ip", ip), ("$s", Format(since)));

    /// <summary>MYST paid out in registration fees since <paramref name="since"/> (or ever).</summary>
    public decimal MystSponsoredSince(DateTimeOffset? since = null) =>
        Scalar<long>("SELECT COALESCE(SUM(amount_micro), 0) FROM myst_sponsorships WHERE created_at >= $s",
            ("$s", Format(since ?? DateTimeOffset.MinValue))) / 1_000_000m;

    public long MystSponsorshipCount() => Scalar<long>("SELECT COUNT(*) FROM myst_sponsorships");

    /// <summary>
    /// Live accounts by payout coin: the part before ":" of the payout target, "XMR" for a bare Monero address, and
    /// "none" for accounts without one yet. For the non-custodial admin dashboard.
    /// </summary>
    public IReadOnlyList<(string Coin, long Accounts)> PayoutCoinCounts()
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT CASE WHEN own_wallet IS NULL THEN 'none'
                        WHEN instr(own_wallet, ':') > 0 THEN substr(own_wallet, 1, instr(own_wallet, ':') - 1)
                        ELSE 'XMR' END AS coin,
                   COUNT(*) AS n
            FROM accounts WHERE merged_into IS NULL AND deleted_at IS NULL
            GROUP BY coin ORDER BY n DESC, coin
            """;
        using var reader = cmd.ExecuteReader();
        var result = new List<(string, long)>();
        while (reader.Read())
            result.Add((reader.GetString(0), reader.GetInt64(1)));
        return result;
    }

    /// <summary>Accounts that joined through an invite, and how many of them have been verified by their pool.</summary>
    public (long Referred, long Verified) ReferralCounts() => (
        Scalar<long>("SELECT COUNT(*) FROM accounts WHERE referred_by IS NOT NULL AND merged_into IS NULL"),
        Scalar<long>("SELECT COUNT(*) FROM accounts WHERE referred_by IS NOT NULL AND verified = 1 AND merged_into IS NULL"));

    public long DevicesSeenSince(DateTimeOffset since) =>
        Scalar<long>("SELECT COUNT(*) FROM devices WHERE last_seen_at >= $s", ("$s", Format(since)));

    /// <summary>Live accounts (not merged or deleted), and how many of them were created after <paramref name="since"/>.</summary>
    public (long Total, long New) AccountCounts(DateTimeOffset since) => (
        Scalar<long>("SELECT COUNT(*) FROM accounts WHERE merged_into IS NULL AND deleted_at IS NULL"),
        Scalar<long>("SELECT COUNT(*) FROM accounts WHERE merged_into IS NULL AND deleted_at IS NULL AND created_at >= $s", ("$s", Format(since))));

    public long DeviceCount() => Scalar<long>("SELECT COUNT(*) FROM devices");

    /// <summary>
    /// Computers registered on each of the last <paramref name="days"/> days (UTC), oldest first, including days with none.
    /// Timestamps are stored as round-trip strings, so the first ten characters are the UTC date.
    /// </summary>
    public IReadOnlyList<(DateOnly Day, long Devices)> NewDevicesPerDay(DateTimeOffset now, int days)
    {
        var first = DateOnly.FromDateTime(now.UtcDateTime).AddDays(1 - days);
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT substr(created_at, 1, 10) AS day, COUNT(*) FROM devices WHERE created_at >= $s GROUP BY day";
        Bind(cmd, [("$s", Format(new DateTimeOffset(first.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)))]);
        using var r = cmd.ExecuteReader();
        var counts = new Dictionary<string, long>();
        while (r.Read())
            counts[r.GetString(0)] = r.GetInt64(1);
        return Enumerable.Range(0, days)
            .Select(i => first.AddDays(i))
            .Select(d => (d, counts.GetValueOrDefault(d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))))
            .ToList();
    }

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
                Parse(r.GetString(r.GetOrdinal("created_at"))),
                ParseNullable(NullableString(r, "deleted_at"))));
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
