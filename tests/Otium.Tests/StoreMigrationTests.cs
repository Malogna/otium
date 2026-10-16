using Microsoft.Data.Sqlite;
using Otium.Server.Data;

namespace Otium.Tests;

public sealed class StoreMigrationTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"otium-v1-{Guid.NewGuid():N}.db");

    [Fact]
    public void Version_one_databases_are_upgraded_without_losing_money()
    {
        // The pre-sign-in schema: one row per device holding the account data, ledger keyed by device.
        using (var connection = new SqliteConnection($"Data Source={_dbPath};Pooling=False"))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE devices (device_id TEXT PRIMARY KEY, secret_hash TEXT NOT NULL, worker_id TEXT NOT NULL UNIQUE,
                    pool_wallet TEXT NULL, own_wallet TEXT NULL, code TEXT NOT NULL UNIQUE, referred_by TEXT NULL, referred_at TEXT NULL,
                    fee_free_until TEXT NULL, verified INTEGER NOT NULL DEFAULT 0, last_checked_at TEXT NULL, created_at TEXT NOT NULL);
                CREATE TABLE ledger (id INTEGER PRIMARY KEY AUTOINCREMENT, device_id TEXT NOT NULL, kind TEXT NOT NULL, micros INTEGER NOT NULL,
                    created_at TEXT NOT NULL, available_at TEXT NOT NULL, reference TEXT NULL);
                CREATE TABLE cashouts (id TEXT PRIMARY KEY, device_id TEXT NOT NULL, method TEXT NOT NULL, destination TEXT NOT NULL,
                    micros INTEGER NOT NULL, status TEXT NOT NULL, created_at TEXT NOT NULL, updated_at TEXT NOT NULL,
                    provider_reference TEXT NULL, note TEXT NULL, attempts INTEGER NOT NULL DEFAULT 0);
                INSERT INTO devices VALUES ('dev1', 'h', 'otabc', 'pool', NULL, 'ABCDEFG', NULL, NULL, NULL, 0, NULL, '2026-10-01T00:00:00.0000000Z');
                INSERT INTO ledger (device_id, kind, micros, created_at, available_at) VALUES
                    ('dev1', 'earning', 3000000, '2026-10-01T00:00:00.0000000Z', '2026-10-01T00:00:00.0000000Z');
                INSERT INTO cashouts VALUES ('co_1', 'dev1', 'paypal', 'a@b.co', 1000000, 'paid', '2026-10-02T00:00:00.0000000Z',
                    '2026-10-02T00:00:00.0000000Z', NULL, NULL, 0);
                INSERT INTO ledger (device_id, kind, micros, created_at, available_at, reference) VALUES
                    ('dev1', 'cashout', -1000000, '2026-10-02T00:00:00.0000000Z', '2026-10-02T00:00:00.0000000Z', 'co_1');
                """;
            cmd.ExecuteNonQuery();
        }

        var store = new OtiumStore($"Data Source={_dbPath};Pooling=False");

        var account = store.FindAccountByWorker("otabc");
        Assert.NotNull(account);
        Assert.Equal("ABCDEFG", account.Code);
        Assert.Equal(account.AccountId, store.FindDevice("dev1")!.AccountId);
        Assert.Equal(2_000_000, store.Balance(account.AccountId, DateTimeOffset.UtcNow).AvailableMicros);
        Assert.Single(store.CashoutsFor(account.AccountId));

        // Opening again is a no-op.
        var again = new OtiumStore($"Data Source={_dbPath};Pooling=False");
        Assert.Equal(2_000_000, again.Balance(account.AccountId, DateTimeOffset.UtcNow).AvailableMicros);
    }

    [Fact]
    public void Version_three_databases_gain_the_deletion_time()
    {
        // Version 3 accounts had no deleted_at column; the rest of the schema is created on demand.
        using (var connection = new SqliteConnection($"Data Source={_dbPath};Pooling=False"))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE accounts (account_id TEXT PRIMARY KEY, worker_id TEXT NOT NULL UNIQUE, pool_wallet TEXT NULL,
                    own_wallet TEXT NULL, code TEXT NOT NULL UNIQUE, email TEXT NULL UNIQUE, referred_by TEXT NULL, referred_at TEXT NULL,
                    fee_free_until TEXT NULL, verified INTEGER NOT NULL DEFAULT 0, last_checked_at TEXT NULL, merged_into TEXT NULL,
                    created_at TEXT NOT NULL);
                INSERT INTO accounts (account_id, worker_id, pool_wallet, code, email, created_at)
                    VALUES ('acc1', 'otabc', 'pool', 'ABCDEFG', 'a@b.co', '2026-10-01T00:00:00.0000000Z');
                PRAGMA user_version = 3;
                """;
            cmd.ExecuteNonQuery();
        }

        var store = new OtiumStore($"Data Source={_dbPath};Pooling=False");

        var account = store.FindAccount("acc1");
        Assert.NotNull(account);
        Assert.Equal("a@b.co", account.Email);
        Assert.Null(account.DeletedAt);

        var now = new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
        Assert.True(store.AnonymizeAccount("acc1", (_, d) => d, now));
        var deleted = store.FindAccount("acc1")!;
        Assert.Equal(now, deleted.DeletedAt);
        Assert.Null(deleted.Email);
        Assert.Null(store.FindAccountByWorker("otabc"));

        // Opening again is a no-op.
        Assert.Equal(now, new OtiumStore($"Data Source={_dbPath};Pooling=False").FindAccount("acc1")!.DeletedAt);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
            }
        }
    }
}
