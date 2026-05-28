namespace EchoTrace.Core;

public sealed record AdvertisementEvent
{
    public int Version { get; init; }
    public string Type { get; init; } = "adv";
    public long Sequence { get; init; }
    public string ReceiverId { get; init; } = "A";
    public long UptimeMs { get; init; }
    public DateTimeOffset ReceivedAtUtc { get; init; }
    public string Address { get; init; } = string.Empty;
    public string AddressType { get; init; } = string.Empty;
    public int Rssi { get; init; }
    public string? Name { get; init; }
    public string AdvertisementType { get; init; } = string.Empty;
    public int DataLength { get; init; }
    public string RawJson { get; init; } = string.Empty;
}
