using Dapper;
using EchoTrace.Core;
using Microsoft.Data.Sqlite;

namespace EchoTrace.Storage;

public sealed class CaptureStore
{
    private readonly string _connectionString;

    public CaptureStore(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? ".");
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
    }

    public async Task InitializeAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS Sessions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                StartedAtUtc TEXT NOT NULL,
                EndedAtUtc TEXT NULL,
                ReceiverId TEXT NOT NULL,
                Source TEXT NOT NULL,
                Notes TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS AdvertisementEvents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SessionId INTEGER NOT NULL,
                Version INTEGER NOT NULL,
                Type TEXT NOT NULL,
                Sequence INTEGER NOT NULL,
                ReceiverId TEXT NOT NULL,
                UptimeMs INTEGER NOT NULL,
                ReceivedAtUtc TEXT NOT NULL,
                Address TEXT NOT NULL,
                AddressType TEXT NOT NULL,
                Rssi INTEGER NOT NULL,
                Name TEXT NULL,
                AdvertisementType TEXT NOT NULL,
                DataLength INTEGER NOT NULL,
                RawJson TEXT NOT NULL,
                FOREIGN KEY(SessionId) REFERENCES Sessions(Id)
            );

            CREATE TABLE IF NOT EXISTS Devices (
                SessionId INTEGER NOT NULL,
                ReceiverId TEXT NOT NULL,
                Address TEXT NOT NULL,
                Name TEXT NULL,
                CurrentRssi INTEGER NOT NULL,
                FirstSeen TEXT NOT NULL,
                LastSeen TEXT NOT NULL,
                SeenCount INTEGER NOT NULL,
                RssiMin INTEGER NOT NULL,
                RssiMax INTEGER NOT NULL,
                RssiAvg REAL NOT NULL,
                PRIMARY KEY(SessionId, ReceiverId, Address),
                FOREIGN KEY(SessionId) REFERENCES Sessions(Id)
            );
            """);
    }

    public async Task<CaptureSession> StartSessionAsync(string receiverId, string source, string? notes = null)
    {
        var started = DateTimeOffset.UtcNow;
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        long id = await connection.ExecuteScalarAsync<long>("""
            INSERT INTO Sessions (StartedAtUtc, ReceiverId, Source, Notes)
            VALUES (@StartedAtUtc, @ReceiverId, @Source, @Notes);
            SELECT last_insert_rowid();
            """, new
        {
            StartedAtUtc = started.ToString("O"),
            ReceiverId = receiverId,
            Source = source,
            Notes = notes
        });

        return new CaptureSession
        {
            Id = id,
            StartedAtUtc = started,
            ReceiverId = receiverId,
            Source = source,
            Notes = notes
        };
    }

    public async Task EndSessionAsync(long sessionId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            "UPDATE Sessions SET EndedAtUtc = @EndedAtUtc WHERE Id = @SessionId",
            new { SessionId = sessionId, EndedAtUtc = DateTimeOffset.UtcNow.ToString("O") });
    }

    public async Task SaveEventAsync(long sessionId, AdvertisementEvent advertisement)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync("""
            INSERT INTO AdvertisementEvents
                (SessionId, Version, Type, Sequence, ReceiverId, UptimeMs, ReceivedAtUtc, Address,
                 AddressType, Rssi, Name, AdvertisementType, DataLength, RawJson)
            VALUES
                (@SessionId, @Version, @Type, @Sequence, @ReceiverId, @UptimeMs, @ReceivedAtUtc, @Address,
                 @AddressType, @Rssi, @Name, @AdvertisementType, @DataLength, @RawJson)
            """, ToEventRow(sessionId, advertisement));
    }

    public async Task UpsertDeviceAsync(long sessionId, DeviceSummary device)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync("""
            INSERT INTO Devices
                (SessionId, ReceiverId, Address, Name, CurrentRssi, FirstSeen, LastSeen, SeenCount, RssiMin, RssiMax, RssiAvg)
            VALUES
                (@SessionId, @ReceiverId, @Address, @Name, @CurrentRssi, @FirstSeen, @LastSeen, @SeenCount, @RssiMin, @RssiMax, @RssiAvg)
            ON CONFLICT(SessionId, ReceiverId, Address) DO UPDATE SET
                Name = excluded.Name,
                CurrentRssi = excluded.CurrentRssi,
                LastSeen = excluded.LastSeen,
                SeenCount = excluded.SeenCount,
                RssiMin = excluded.RssiMin,
                RssiMax = excluded.RssiMax,
                RssiAvg = excluded.RssiAvg
            """, new
        {
            SessionId = sessionId,
            device.ReceiverId,
            device.Address,
            device.Name,
            device.CurrentRssi,
            FirstSeen = device.FirstSeen.ToString("O"),
            LastSeen = device.LastSeen.ToString("O"),
            device.SeenCount,
            device.RssiMin,
            device.RssiMax,
            device.RssiAvg
        });
    }

    public async Task<IReadOnlyList<AdvertisementEvent>> GetSessionEventsAsync(long sessionId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var rows = await connection.QueryAsync<EventRow>("""
            SELECT Version, Type, Sequence, ReceiverId, UptimeMs, ReceivedAtUtc, Address,
                   AddressType, Rssi, Name, AdvertisementType, DataLength, RawJson
            FROM AdvertisementEvents
            WHERE SessionId = @SessionId
            ORDER BY Id
            """, new { SessionId = sessionId });

        return rows.Select(ToEvent).ToArray();
    }

    private static object ToEventRow(long sessionId, AdvertisementEvent advertisement)
    {
        return new
        {
            SessionId = sessionId,
            advertisement.Version,
            advertisement.Type,
            advertisement.Sequence,
            advertisement.ReceiverId,
            advertisement.UptimeMs,
            ReceivedAtUtc = advertisement.ReceivedAtUtc.ToString("O"),
            advertisement.Address,
            advertisement.AddressType,
            advertisement.Rssi,
            advertisement.Name,
            advertisement.AdvertisementType,
            advertisement.DataLength,
            advertisement.RawJson
        };
    }

    private static AdvertisementEvent ToEvent(EventRow row)
    {
        return new AdvertisementEvent
        {
            Version = checked((int)row.Version),
            Type = row.Type,
            Sequence = row.Sequence,
            ReceiverId = row.ReceiverId,
            UptimeMs = row.UptimeMs,
            ReceivedAtUtc = DateTimeOffset.Parse(row.ReceivedAtUtc),
            Address = row.Address,
            AddressType = row.AddressType,
            Rssi = checked((int)row.Rssi),
            Name = row.Name,
            AdvertisementType = row.AdvertisementType,
            DataLength = checked((int)row.DataLength),
            RawJson = row.RawJson
        };
    }

    private sealed class EventRow
    {
        public long Version { get; init; }
        public string Type { get; init; } = string.Empty;
        public long Sequence { get; init; }
        public string ReceiverId { get; init; } = string.Empty;
        public long UptimeMs { get; init; }
        public string ReceivedAtUtc { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public string AddressType { get; init; } = string.Empty;
        public long Rssi { get; init; }
        public string? Name { get; init; }
        public string AdvertisementType { get; init; } = string.Empty;
        public long DataLength { get; init; }
        public string RawJson { get; init; } = string.Empty;
    }
}
