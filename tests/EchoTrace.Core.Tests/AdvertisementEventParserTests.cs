using EchoTrace.Core;
using Xunit;

namespace EchoTrace.Core.Tests;

public sealed class AdvertisementEventParserTests
{
    [Fact]
    public void ParseLine_AcceptsValidAdvertisement()
    {
        var parser = new AdvertisementEventParser();
        DateTimeOffset receivedAt = DateTimeOffset.Parse("2026-05-28T08:00:00Z");

        AdvertisementParseResult result = parser.ParseLine(
            """{"v":1,"type":"adv","seq":12,"receiver":"A","uptimeMs":345678,"addr":"AA:BB:CC:DD:EE:FF","addrType":"random","rssi":-67,"name":"Device","advType":"connectable","dataLen":31}""",
            receivedAt);

        Assert.True(result.Success);
        Assert.NotNull(result.Event);
        Assert.Equal("AA:BB:CC:DD:EE:FF", result.Event.Address);
        Assert.Equal(-67, result.Event.Rssi);
        Assert.Equal(receivedAt, result.Event.ReceivedAtUtc);
    }

    [Fact]
    public void ParseLine_ReturnsErrorForInvalidJson()
    {
        var parser = new AdvertisementEventParser();

        AdvertisementParseResult result = parser.ParseLine("{not-json");

        Assert.False(result.Success);
        Assert.Contains("Invalid JSON", result.Error);
    }
}
