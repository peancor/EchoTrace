namespace EchoTrace.Core;

public sealed class DeviceAggregator
{
    private readonly Dictionary<string, DeviceSummary> _devices = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<DeviceSummary> Devices => _devices.Values;

    public DeviceSummary Apply(AdvertisementEvent advertisement)
    {
        string key = $"{advertisement.ReceiverId}|{advertisement.Address}";
        if (!_devices.TryGetValue(key, out DeviceSummary? summary))
        {
            summary = new DeviceSummary(advertisement);
            _devices.Add(key, summary);
            return summary;
        }

        summary.Apply(advertisement);
        return summary;
    }

    public void Clear() => _devices.Clear();
}
