using EchoTrace.Core;

namespace EchoTrace.Serial;

public sealed record SerialReadResult(AdvertisementEvent? Event, string? Error, string? RawLine)
{
    public bool Success => Event is not null;

    public static SerialReadResult FromEvent(AdvertisementEvent advertisement) => new(advertisement, null, advertisement.RawJson);

    public static SerialReadResult FromError(string error, string? rawLine) => new(null, error, rawLine);
}
