using EchoTrace.Core;

namespace EchoTrace.App.ViewModels;

public sealed class DeviceRowViewModel : ObservableObject
{
    private string? _name;
    private int _currentRssi;
    private DateTimeOffset _firstSeen;
    private DateTimeOffset _lastSeen;
    private int _seenCount;
    private int _rssiMin;
    private int _rssiMax;
    private double _rssiAvg;
    private bool _isPresent;

    public DeviceRowViewModel(DeviceSummary summary)
    {
        ReceiverId = summary.ReceiverId;
        Address = summary.Address;
        Apply(summary);
    }

    public string ReceiverId { get; }
    public string Address { get; }
    public string? Name { get => _name; private set => SetProperty(ref _name, value); }
    public int CurrentRssi { get => _currentRssi; private set => SetProperty(ref _currentRssi, value); }
    public DateTimeOffset FirstSeen { get => _firstSeen; private set => SetProperty(ref _firstSeen, value); }
    public DateTimeOffset LastSeen { get => _lastSeen; private set => SetProperty(ref _lastSeen, value); }
    public int SeenCount { get => _seenCount; private set => SetProperty(ref _seenCount, value); }
    public int RssiMin { get => _rssiMin; private set => SetProperty(ref _rssiMin, value); }
    public int RssiMax { get => _rssiMax; private set => SetProperty(ref _rssiMax, value); }
    public double RssiAvg { get => _rssiAvg; private set => SetProperty(ref _rssiAvg, value); }
    public string RssiAvgText => RssiAvg.ToString("F1");
    public int SignalPercent => Math.Clamp((CurrentRssi + 100) * 2, 0, 100);
    public string SignalState => !IsPresent
        ? "Lost"
        : CurrentRssi >= -60
            ? "Near"
            : CurrentRssi >= -75
                ? "Medium"
                : "Far";
    public string DisplayName => string.IsNullOrWhiteSpace(Name) || Name == "(unnamed)" ? Address : Name;
    public string FirstSeenLocal => FirstSeen.ToLocalTime().ToString("HH:mm:ss");
    public string LastSeenLocal => LastSeen.ToLocalTime().ToString("HH:mm:ss");
    public bool IsPresent { get => _isPresent; private set => SetProperty(ref _isPresent, value); }

    public void Apply(DeviceSummary summary)
    {
        string name = string.IsNullOrWhiteSpace(summary.Name) ? "(unnamed)" : summary.Name;
        bool nameChanged = _name != name;
        bool rssiChanged = _currentRssi != summary.CurrentRssi;
        bool firstSeenChanged = _firstSeen != summary.FirstSeen;
        bool lastSeenChanged = _lastSeen != summary.LastSeen;
        bool rssiAvgChanged = Math.Abs(_rssiAvg - summary.RssiAvg) > 0.001;
        bool presenceChanged = _isPresent != summary.IsPresent;

        Name = name;
        CurrentRssi = summary.CurrentRssi;
        FirstSeen = summary.FirstSeen;
        LastSeen = summary.LastSeen;
        SeenCount = summary.SeenCount;
        RssiMin = summary.RssiMin;
        RssiMax = summary.RssiMax;
        RssiAvg = summary.RssiAvg;
        IsPresent = summary.IsPresent;

        if (rssiAvgChanged)
        {
            OnPropertyChanged(nameof(RssiAvgText));
        }

        if (rssiChanged)
        {
            OnPropertyChanged(nameof(SignalPercent));
        }

        if (rssiChanged || presenceChanged)
        {
            OnPropertyChanged(nameof(SignalState));
        }

        if (nameChanged)
        {
            OnPropertyChanged(nameof(DisplayName));
        }

        if (firstSeenChanged)
        {
            OnPropertyChanged(nameof(FirstSeenLocal));
        }

        if (lastSeenChanged)
        {
            OnPropertyChanged(nameof(LastSeenLocal));
        }
    }
}
