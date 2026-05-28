using System.Text.Json;

namespace EchoTrace.Core;

public sealed class AdvertisementEventParser
{
    public AdvertisementParseResult ParseLine(string? line, DateTimeOffset? receivedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return AdvertisementParseResult.Failed("Empty line.");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;

            string type = ReadString(root, "type") ?? string.Empty;
            if (!string.Equals(type, "adv", StringComparison.OrdinalIgnoreCase))
            {
                return AdvertisementParseResult.Failed($"Unsupported event type '{type}'.");
            }

            string? address = ReadString(root, "addr");
            if (string.IsNullOrWhiteSpace(address))
            {
                return AdvertisementParseResult.Failed("Missing addr.");
            }

            if (!TryReadInt(root, "rssi", out int rssi))
            {
                return AdvertisementParseResult.Failed("Missing rssi.");
            }

            var advertisement = new AdvertisementEvent
            {
                Version = ReadInt(root, "v", 1),
                Type = "adv",
                Sequence = ReadLong(root, "seq", 0),
                ReceiverId = ReadString(root, "receiver") ?? "A",
                UptimeMs = ReadLong(root, "uptimeMs", 0),
                ReceivedAtUtc = receivedAtUtc ?? DateTimeOffset.UtcNow,
                Address = address,
                AddressType = ReadString(root, "addrType") ?? string.Empty,
                Rssi = rssi,
                Name = ReadString(root, "name"),
                AdvertisementType = ReadString(root, "advType") ?? string.Empty,
                DataLength = ReadInt(root, "dataLen", 0),
                RawJson = line
            };

            return AdvertisementParseResult.Parsed(advertisement);
        }
        catch (JsonException ex)
        {
            return AdvertisementParseResult.Failed($"Invalid JSON: {ex.Message}");
        }
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static int ReadInt(JsonElement root, string propertyName, int fallback)
    {
        return TryReadInt(root, propertyName, out int value) ? value : fallback;
    }

    private static bool TryReadInt(JsonElement root, string propertyName, out int value)
    {
        value = 0;
        if (!root.TryGetProperty(propertyName, out JsonElement element))
        {
            return false;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetInt32(out value),
            JsonValueKind.String => int.TryParse(element.GetString(), out value),
            _ => false
        };
    }

    private static long ReadLong(JsonElement root, string propertyName, long fallback)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement element))
        {
            return fallback;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt64(out long value) => value,
            JsonValueKind.String when long.TryParse(element.GetString(), out long value) => value,
            _ => fallback
        };
    }
}
