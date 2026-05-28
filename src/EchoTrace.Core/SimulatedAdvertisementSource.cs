using System.Runtime.CompilerServices;
using System.Text.Json;

namespace EchoTrace.Core;

public sealed class SimulatedAdvertisementSource
{
    private readonly Random _random = new(17);
    private readonly SimulatedDevice[] _devices =
    [
        new("C8:3A:35:10:27:91", "Pixel Watch", -62),
        new("F0:99:B6:24:AA:01", "Tile", -74),
        new("7C:D9:5C:E1:44:12", "Headphones", -68),
        new("D2:31:7A:08:8F:66", "Beacon", -55),
        new("4A:5B:6C:7D:8E:9F", null, -82)
    ];

    public async IAsyncEnumerable<AdvertisementEvent> ReadEventsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        long sequence = 0;
        long started = Environment.TickCount64;
        var parser = new AdvertisementEventParser();

        while (!cancellationToken.IsCancellationRequested)
        {
            SimulatedDevice device = _devices[_random.Next(_devices.Length)];
            int rssi = device.BaseRssi + _random.Next(-8, 9);
            long uptime = Math.Max(0, Environment.TickCount64 - started);
            string json = JsonSerializer.Serialize(new
            {
                v = 1,
                type = "adv",
                seq = ++sequence,
                receiver = "SIM",
                uptimeMs = uptime,
                addr = device.Address,
                addrType = "random",
                rssi,
                name = device.Name,
                advType = "connectable",
                dataLen = _random.Next(12, 32)
            });

            AdvertisementParseResult parsed = parser.ParseLine(json, DateTimeOffset.UtcNow);
            if (parsed.Event is not null)
            {
                yield return parsed.Event;
            }

            await Task.Delay(_random.Next(90, 260), cancellationToken);
        }
    }

    private sealed record SimulatedDevice(string Address, string? Name, int BaseRssi);
}
