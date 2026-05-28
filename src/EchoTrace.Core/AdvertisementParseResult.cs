namespace EchoTrace.Core;

public sealed record AdvertisementParseResult(AdvertisementEvent? Event, string? Error)
{
    public bool Success => Event is not null;

    public static AdvertisementParseResult Parsed(AdvertisementEvent advertisement) => new(advertisement, null);

    public static AdvertisementParseResult Failed(string error) => new(null, error);
}
