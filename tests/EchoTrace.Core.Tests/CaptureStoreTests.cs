using EchoTrace.Core;
using EchoTrace.Storage;
using Xunit;

namespace EchoTrace.Core.Tests;

public sealed class CaptureStoreTests
{
    [Fact]
    public async Task Store_CreatesSessionAndPersistsEvents()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "EchoTraceTests", Guid.NewGuid().ToString("N"));
        string dbPath = Path.Combine(tempDir, "capture.db");
        var store = new CaptureStore(dbPath);
        await store.InitializeAsync();

        CaptureSession session = await store.StartSessionAsync("SIM", "Simulator");
        var advertisement = new AdvertisementEvent
        {
            Version = 1,
            Type = "adv",
            Sequence = 7,
            ReceiverId = "SIM",
            UptimeMs = 123,
            ReceivedAtUtc = DateTimeOffset.UtcNow,
            Address = "AA:BB:CC:DD:EE:FF",
            AddressType = "random",
            Rssi = -51,
            Name = "Device",
            AdvertisementType = "connectable",
            DataLength = 18,
            RawJson = "{}"
        };

        await store.SaveEventAsync(session.Id, advertisement);
        IReadOnlyList<AdvertisementEvent> events = await store.GetSessionEventsAsync(session.Id);

        Assert.Single(events);
        Assert.Equal("AA:BB:CC:DD:EE:FF", events[0].Address);
        Assert.Equal(-51, events[0].Rssi);
    }
}
