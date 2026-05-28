using EchoTrace.Core;
using Xunit;

namespace EchoTrace.Core.Tests;

public sealed class DeviceAggregatorTests
{
    [Fact]
    public void Apply_UpdatesRssiStatistics()
    {
        var aggregator = new DeviceAggregator();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        aggregator.Apply(CreateEvent(-70, now));
        DeviceSummary summary = aggregator.Apply(CreateEvent(-60, now.AddSeconds(1)));

        Assert.Equal(2, summary.SeenCount);
        Assert.Equal(-70, summary.RssiMin);
        Assert.Equal(-60, summary.RssiMax);
        Assert.Equal(-65, summary.RssiAvg);
        Assert.Equal(-60, summary.CurrentRssi);
        Assert.Equal(now.AddSeconds(1), summary.LastSeen);
    }

    private static AdvertisementEvent CreateEvent(int rssi, DateTimeOffset receivedAt)
    {
        return new AdvertisementEvent
        {
            Version = 1,
            Type = "adv",
            Sequence = 1,
            ReceiverId = "A",
            ReceivedAtUtc = receivedAt,
            Address = "AA:BB:CC:DD:EE:FF",
            AddressType = "random",
            Rssi = rssi,
            AdvertisementType = "connectable",
            DataLength = 20,
            RawJson = "{}"
        };
    }
}
