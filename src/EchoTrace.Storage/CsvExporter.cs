using System.Globalization;
using System.Text;
using EchoTrace.Core;

namespace EchoTrace.Storage;

public static class CsvExporter
{
    public static async Task ExportEventsAsync(string path, IEnumerable<AdvertisementEvent> events)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        await using var writer = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        await writer.WriteLineAsync("ReceivedAtUtc,ReceiverId,Sequence,Address,AddressType,Rssi,Name,AdvertisementType,DataLength,RawJson");
        foreach (AdvertisementEvent item in events)
        {
            await writer.WriteLineAsync(string.Join(",",
                Csv(item.ReceivedAtUtc.ToString("O")),
                Csv(item.ReceiverId),
                item.Sequence.ToString(CultureInfo.InvariantCulture),
                Csv(item.Address),
                Csv(item.AddressType),
                item.Rssi.ToString(CultureInfo.InvariantCulture),
                Csv(item.Name),
                Csv(item.AdvertisementType),
                item.DataLength.ToString(CultureInfo.InvariantCulture),
                Csv(item.RawJson)));
        }
    }

    public static async Task ExportDevicesAsync(string path, IEnumerable<DeviceSummary> devices)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        await using var writer = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        await writer.WriteLineAsync("ReceiverId,Address,Name,CurrentRssi,FirstSeen,LastSeen,SeenCount,RssiMin,RssiMax,RssiAvg,IsPresent");
        foreach (DeviceSummary item in devices)
        {
            await writer.WriteLineAsync(string.Join(",",
                Csv(item.ReceiverId),
                Csv(item.Address),
                Csv(item.Name),
                item.CurrentRssi.ToString(CultureInfo.InvariantCulture),
                Csv(item.FirstSeen.ToString("O")),
                Csv(item.LastSeen.ToString("O")),
                item.SeenCount.ToString(CultureInfo.InvariantCulture),
                item.RssiMin.ToString(CultureInfo.InvariantCulture),
                item.RssiMax.ToString(CultureInfo.InvariantCulture),
                item.RssiAvg.ToString("F2", CultureInfo.InvariantCulture),
                item.IsPresent ? "true" : "false"));
        }
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
