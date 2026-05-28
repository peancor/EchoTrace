namespace EchoTrace.Core;

public sealed class DeviceSummary
{
    private long _rssiTotal;

    public string ReceiverId { get; }
    public string Address { get; }
    public string? Name { get; private set; }
    public int CurrentRssi { get; private set; }
    public DateTimeOffset FirstSeen { get; private set; }
    public DateTimeOffset LastSeen { get; private set; }
    public int SeenCount { get; private set; }
    public int RssiMin { get; private set; }
    public int RssiMax { get; private set; }
    public double RssiAvg => SeenCount == 0 ? 0 : (double)_rssiTotal / SeenCount;
    public bool IsPresent => DateTimeOffset.UtcNow - LastSeen <= TimeSpan.FromSeconds(10);

    public DeviceSummary(AdvertisementEvent firstEvent)
    {
        ReceiverId = firstEvent.ReceiverId;
        Address = firstEvent.Address;
        FirstSeen = firstEvent.ReceivedAtUtc;
        LastSeen = firstEvent.ReceivedAtUtc;
        Name = firstEvent.Name;
        CurrentRssi = firstEvent.Rssi;
        RssiMin = firstEvent.Rssi;
        RssiMax = firstEvent.Rssi;
        SeenCount = 1;
        _rssiTotal = firstEvent.Rssi;
    }

    public void Apply(AdvertisementEvent advertisement)
    {
        if (!string.IsNullOrWhiteSpace(advertisement.Name))
        {
            Name = advertisement.Name;
        }

        CurrentRssi = advertisement.Rssi;
        LastSeen = advertisement.ReceivedAtUtc;
        SeenCount++;
        _rssiTotal += advertisement.Rssi;
        RssiMin = Math.Min(RssiMin, advertisement.Rssi);
        RssiMax = Math.Max(RssiMax, advertisement.Rssi);
    }
}
