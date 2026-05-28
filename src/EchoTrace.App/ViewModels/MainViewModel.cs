using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using EchoTrace.App.Services;
using EchoTrace.Core;
using EchoTrace.Serial;
using EchoTrace.Storage;

namespace EchoTrace.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly DeviceAggregator _aggregator = new();
    private readonly Dictionary<string, DeviceRowViewModel> _deviceRows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<RssiPoint>> _rssiPoints = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<RssiPoint> _eventRatePoints = [];
    private readonly List<AdvertisementEvent> _eventBuffer = [];
    private readonly AppSettingsStore _settingsStore = new();
    private readonly AppSettings _settings;
    private readonly CaptureStore _store;
    private readonly DispatcherTimer _statsTimer;
    private CancellationTokenSource? _connectionCts;
    private CaptureSession? _currentSession;
    private DeviceRowViewModel? _selectedDevice;
    private string? _selectedPort;
    private string _selectedSourceMode = "Simulator";
    private string _filterText = string.Empty;
    private string _minimumRssiText = "-100";
    private string _selectedTimeWindow = "30s";
    private string _selectedTheme = "Dark";
    private string _statusText = "Ready";
    private int _eventsThisSecond;
    private int _eventsPerSecond;
    private int _totalEvents;
    private ShellSectionViewModel? _selectedShellSection;
    private bool _isConnected;
    private bool _isCapturing;
    private bool _showPresentOnly = true;
    private bool _isChartPaused;

    public MainViewModel()
    {
        _settings = _settingsStore.Load();
        _selectedTheme = NormalizeTheme(_settings.SelectedTheme);
        _selectedSourceMode = SourceModes.Contains(_settings.SelectedSourceMode) ? _settings.SelectedSourceMode : "Simulator";
        _selectedPort = _settings.SelectedPort;
        _selectedTimeWindow = TimeWindowOptions.Contains(_settings.SelectedTimeWindow) ? _settings.SelectedTimeWindow : "30s";
        _minimumRssiText = string.IsNullOrWhiteSpace(_settings.MinimumRssiText) ? "-100" : _settings.MinimumRssiText;
        _showPresentOnly = _settings.ShowPresentOnly;

        string dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EchoTrace",
            "EchoTrace.db");
        _store = new CaptureStore(dbPath);

        ToggleConnectionCommand = new AsyncCommand(ToggleConnectionAsync);
        ToggleCaptureCommand = new AsyncCommand(ToggleCaptureAsync);
        ExportCommand = new AsyncCommand(ExportAsync);
        ToggleChartPauseCommand = new AsyncCommand(async () =>
        {
            IsChartPaused = !IsChartPaused;
            await Task.CompletedTask;
        });
        ClearFiltersCommand = new AsyncCommand(async () =>
        {
            FilterText = string.Empty;
            MinimumRssiText = "-100";
            ShowPresentOnly = true;
            await Task.CompletedTask;
        });
        RefreshPortsCommand = new AsyncCommand(async () =>
        {
            RefreshPorts();
            await Task.CompletedTask;
        });

        _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statsTimer.Tick += (_, _) =>
        {
            EventsPerSecond = _eventsThisSecond;
            _eventRatePoints.Add(new RssiPoint(DateTime.Now, EventsPerSecond));
            if (_eventRatePoints.Count > 600)
            {
                _eventRatePoints.RemoveRange(0, _eventRatePoints.Count - 600);
            }
            _eventsThisSecond = 0;
            RefreshPresence();
            ApplyFiltersAndRanking();
        };
        _statsTimer.Start();
        SelectedShellSection = ShellSections[0];
    }

    public ObservableCollection<ShellSectionViewModel> ShellSections { get; } =
    [
        new("dashboard", "Dashboard", "Live BLE", true),
        new("sessions", "Sessions", "Capture history", false),
        new("receivers", "Receivers", "COM and nodes", false),
        new("settings", "Settings", "Theme and app", true)
    ];

    public ObservableCollection<string> SourceModes { get; } = ["Simulator", "Serial"];
    public ObservableCollection<string> TimeWindowOptions { get; } = ["10s", "30s", "2m", "5m"];
    public ObservableCollection<string> ThemeOptions { get; } = ["Dark", "Light"];
    public ObservableCollection<string> Ports { get; } = [];
    public ObservableCollection<DeviceRowViewModel> Devices { get; } = [];
    public ObservableCollection<DeviceRowViewModel> FilteredDevices { get; } = [];
    public ObservableCollection<DeviceRowViewModel> RankedDevices { get; } = [];
    public ObservableCollection<string> ActivityLog { get; } = [];
    public AsyncCommand ToggleConnectionCommand { get; }
    public AsyncCommand ToggleCaptureCommand { get; }
    public AsyncCommand ToggleChartPauseCommand { get; }
    public AsyncCommand ClearFiltersCommand { get; }
    public AsyncCommand ExportCommand { get; }
    public AsyncCommand RefreshPortsCommand { get; }
    public event EventHandler? ChartChanged;
    public event EventHandler? ThemeChanged;

    public ShellSectionViewModel? SelectedShellSection
    {
        get => _selectedShellSection;
        set
        {
            if (value is not null && !value.IsAvailable)
            {
                AddActivity($"{value.Title} page is prepared for navigation but not enabled in V1.");
                value = ShellSections[0];
            }

            if (SetProperty(ref _selectedShellSection, value))
            {
                OnPropertyChanged(nameof(ShellStatusText));
                OnPropertyChanged(nameof(IsDashboardSelected));
                OnPropertyChanged(nameof(IsSettingsSelected));
            }
        }
    }

    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                PersistSettings();
                OnPropertyChanged(nameof(IsLightTheme));
                OnPropertyChanged(nameof(ThemeDescription));
                ThemeChanged?.Invoke(this, EventArgs.Empty);
                ChartChanged?.Invoke(this, EventArgs.Empty);
                AddActivity($"Theme changed to {SelectedTheme}.");
            }
        }
    }

    public string SelectedSourceMode
    {
        get => _selectedSourceMode;
        set
        {
            if (SetProperty(ref _selectedSourceMode, value))
            {
                PersistSettings();
                OnPropertyChanged(nameof(ReceiverText));
            }
        }
    }

    public string? SelectedPort
    {
        get => _selectedPort;
        set
        {
            if (SetProperty(ref _selectedPort, value))
            {
                PersistSettings();
                OnPropertyChanged(nameof(ReceiverText));
            }
        }
    }

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value))
            {
                ApplyFiltersAndRanking();
            }
        }
    }

    public string MinimumRssiText
    {
        get => _minimumRssiText;
        set
        {
            if (SetProperty(ref _minimumRssiText, value))
            {
                PersistSettings();
                ApplyFiltersAndRanking();
            }
        }
    }

    public bool ShowPresentOnly
    {
        get => _showPresentOnly;
        set
        {
            if (SetProperty(ref _showPresentOnly, value))
            {
                PersistSettings();
                ApplyFiltersAndRanking();
            }
        }
    }

    public string SelectedTimeWindow
    {
        get => _selectedTimeWindow;
        set
        {
            if (SetProperty(ref _selectedTimeWindow, value))
            {
                PersistSettings();
                OnPropertyChanged(nameof(SelectedRssiPoints));
                OnPropertyChanged(nameof(EventRatePoints));
                OnPropertyChanged(nameof(TimeWindowSeconds));
                ChartChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool IsChartPaused
    {
        get => _isChartPaused;
        set
        {
            if (SetProperty(ref _isChartPaused, value))
            {
                OnPropertyChanged(nameof(ChartPauseButtonText));
            }
        }
    }

    public DeviceRowViewModel? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
            {
                OnPropertyChanged(nameof(SelectedRssiPoints));
                OnPropertyChanged(nameof(SelectedChartTitle));
                ChartChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public int EventsPerSecond { get => _eventsPerSecond; private set => SetProperty(ref _eventsPerSecond, value); }
    public int TotalEvents { get => _totalEvents; private set => SetProperty(ref _totalEvents, value); }
    public int DeviceCount => Devices.Count;
    public int VisibleDeviceCount => FilteredDevices.Count;
    public string ConnectButtonText => _isConnected ? "Disconnect" : "Connect";
    public string CaptureButtonText => _isCapturing ? "Stop capture" : "Start capture";
    public string ChartPauseButtonText => IsChartPaused ? "Resume chart" : "Pause chart";
    public string SessionText => _isCapturing && _currentSession is not null ? $"#{_currentSession.Id}" : "Idle";
    public string ReceiverText => SelectedSourceMode == "Serial" ? SelectedPort ?? "No port" : "SIM";
    public string CaptureStateText => _isCapturing ? "Capturing" : "Idle";
    public string ConnectionStateText => _isConnected ? "Connected" : "Disconnected";
    public string StrongestDeviceText => RankedDevices.FirstOrDefault() is { } device ? $"{device.DisplayName} {device.CurrentRssi} dBm" : "No devices";
    public string SelectedChartTitle => SelectedDevice is null ? "Select a device" : $"{SelectedDevice.Name} {SelectedDevice.Address}";
    public string ShellStatusText => SelectedShellSection is null ? "Dashboard" : SelectedShellSection.Title;
    public bool IsDashboardSelected => SelectedShellSection?.Key == "dashboard";
    public bool IsSettingsSelected => SelectedShellSection?.Key == "settings";
    public bool IsLightTheme => SelectedTheme == "Light";
    public string ThemeDescription => IsLightTheme
        ? "Light theme optimized for bright rooms and screenshots."
        : "Dark theme optimized for lab benches and long captures.";
    public int TimeWindowSeconds => SelectedTimeWindow switch
    {
        "10s" => 10,
        "2m" => 120,
        "5m" => 300,
        _ => 30
    };

    public IReadOnlyList<RssiPoint> SelectedRssiPoints
    {
        get
        {
            if (SelectedDevice is null)
            {
                return [];
            }

            string key = Key(SelectedDevice.ReceiverId, SelectedDevice.Address);
            if (!_rssiPoints.TryGetValue(key, out List<RssiPoint>? points))
            {
                return [];
            }

            DateTime cutoff = DateTime.Now.AddSeconds(-TimeWindowSeconds);
            return points.Where(point => point.Timestamp >= cutoff).ToArray();
        }
    }

    public IReadOnlyList<RssiPoint> EventRatePoints
    {
        get
        {
            DateTime cutoff = DateTime.Now.AddSeconds(-TimeWindowSeconds);
            return _eventRatePoints.Where(point => point.Timestamp >= cutoff).ToArray();
        }
    }

    public async Task InitializeAsync()
    {
        await _store.InitializeAsync();
        RefreshPorts();
        StatusText = "Ready. Use Simulator or connect to the receiver serial port.";
        AddActivity("EchoTrace initialized.");
    }

    public async Task ShutdownAsync()
    {
        await DisconnectAsync();
        if (_isCapturing && _currentSession is not null)
        {
            await _store.EndSessionAsync(_currentSession.Id);
        }
    }

    private async Task ToggleConnectionAsync()
    {
        if (_isConnected)
        {
            await DisconnectAsync();
            return;
        }

        await ConnectAsync();
    }

    private async Task ConnectAsync()
    {
        _connectionCts = new CancellationTokenSource();
        _isConnected = true;
        OnConnectionStateChanged();
        StatusText = SelectedSourceMode == "Serial" ? $"Connecting to {SelectedPort}..." : "Simulator running.";
        AddActivity(StatusText);

        if (SelectedSourceMode == "Serial")
        {
            if (string.IsNullOrWhiteSpace(SelectedPort))
            {
                await DisconnectAsync();
                StatusText = "Select a serial port first.";
                AddActivity(StatusText);
                return;
            }

            _ = Task.Run(() => ReadSerialAsync(SelectedPort, _connectionCts.Token));
        }
        else
        {
            _ = Task.Run(() => ReadSimulatorAsync(_connectionCts.Token));
        }
    }

    private async Task DisconnectAsync()
    {
        if (_connectionCts is not null)
        {
            await _connectionCts.CancelAsync();
            _connectionCts.Dispose();
            _connectionCts = null;
        }

        await DispatchAsync(() =>
        {
            _isConnected = false;
            OnConnectionStateChanged();
            StatusText = "Disconnected.";
            AddActivity(StatusText);
        });
    }

    private async Task ReadSimulatorAsync(CancellationToken cancellationToken)
    {
        var simulator = new SimulatedAdvertisementSource();
        try
        {
            await foreach (AdvertisementEvent advertisement in simulator.ReadEventsAsync(cancellationToken))
            {
                await HandleAdvertisementAsync(advertisement);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ReadSerialAsync(string portName, CancellationToken cancellationToken)
    {
        var reader = new SerialAdvertisementReader();
        try
        {
            await foreach (SerialReadResult result in reader.ReadEventsAsync(portName, 115200, cancellationToken))
            {
                if (result.Event is not null)
                {
                    await HandleAdvertisementAsync(result.Event);
                }
                else
                {
                    await DispatchAsync(() => AddActivity($"Parse error: {result.Error}"));
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await DispatchAsync(() =>
            {
                StatusText = $"Serial error: {ex.Message}";
                AddActivity(StatusText);
            });
            await DisconnectAsync();
        }
    }

    private async Task HandleAdvertisementAsync(AdvertisementEvent advertisement)
    {
        DeviceSummary summary = _aggregator.Apply(advertisement);
        _eventBuffer.Add(advertisement);
        if (_eventBuffer.Count > 20000)
        {
            _eventBuffer.RemoveRange(0, _eventBuffer.Count - 20000);
        }

        if (_isCapturing && _currentSession is not null)
        {
            await _store.SaveEventAsync(_currentSession.Id, advertisement);
            await _store.UpsertDeviceAsync(_currentSession.Id, summary);
        }

        await DispatchAsync(() =>
        {
            _eventsThisSecond++;
            TotalEvents++;
            UpsertDeviceRow(summary);
            AddRssiPoint(advertisement);
            ApplyFiltersAndRanking();
            if (TotalEvents % 25 == 0)
            {
                AddActivity($"{TotalEvents} events received. {Devices.Count} devices visible.");
            }
        });
    }

    private async Task ToggleCaptureAsync()
    {
        if (_isCapturing)
        {
            if (_currentSession is not null)
            {
                await _store.EndSessionAsync(_currentSession.Id);
                AddActivity($"Capture session #{_currentSession.Id} stopped.");
            }

            _currentSession = null;
            _isCapturing = false;
            OnCaptureStateChanged();
            return;
        }

        string receiver = SelectedSourceMode == "Serial" ? SelectedPort ?? "A" : "SIM";
        _currentSession = await _store.StartSessionAsync(receiver, SelectedSourceMode);
        _isCapturing = true;
        OnCaptureStateChanged();
        AddActivity($"Capture session #{_currentSession.Id} started.");
    }

    private async Task ExportAsync()
    {
        string exportRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "EchoTrace",
            "Exports",
            DateTime.Now.ToString("yyyyMMdd-HHmmss"));

        await CsvExporter.ExportEventsAsync(Path.Combine(exportRoot, "events.csv"), _eventBuffer);
        await CsvExporter.ExportDevicesAsync(Path.Combine(exportRoot, "devices.csv"), _aggregator.Devices);
        StatusText = $"CSV exported to {exportRoot}";
        AddActivity(StatusText);
    }

    private void RefreshPorts()
    {
        string? previous = SelectedPort;
        Ports.Clear();
        foreach (string port in SerialPortDiscovery.GetPortNames())
        {
            Ports.Add(port);
        }

        SelectedPort = previous is not null && Ports.Contains(previous)
            ? previous
            : Ports.FirstOrDefault(port => port.Equals("COM8", StringComparison.OrdinalIgnoreCase)) ?? Ports.FirstOrDefault();
        AddActivity(Ports.Count == 0 ? "No serial ports detected." : $"Detected ports: {string.Join(", ", Ports)}");
    }

    private void UpsertDeviceRow(DeviceSummary summary)
    {
        string key = Key(summary.ReceiverId, summary.Address);
        if (!_deviceRows.TryGetValue(key, out DeviceRowViewModel? row))
        {
            row = new DeviceRowViewModel(summary);
            _deviceRows.Add(key, row);
            Devices.Add(row);
            FilteredDevices.Add(row);
            if (SelectedDevice is null)
            {
                SelectedDevice = row;
            }
            OnPropertyChanged(nameof(DeviceCount));
            OnPropertyChanged(nameof(VisibleDeviceCount));
        }
        else
        {
            row.Apply(summary);
        }
    }

    private void AddRssiPoint(AdvertisementEvent advertisement)
    {
        string key = Key(advertisement.ReceiverId, advertisement.Address);
        if (!_rssiPoints.TryGetValue(key, out List<RssiPoint>? points))
        {
            points = [];
            _rssiPoints.Add(key, points);
        }

        points.Add(new RssiPoint(advertisement.ReceivedAtUtc.LocalDateTime, advertisement.Rssi));
        if (points.Count > 600)
        {
            points.RemoveRange(0, points.Count - 600);
        }

    }

    private void ApplyFiltersAndRanking()
    {
        int minimumRssi = int.TryParse(MinimumRssiText, out int parsedRssi) ? parsedRssi : -100;
        string filter = FilterText.Trim();
        DeviceRowViewModel[] filtered = Devices
            .Where(device => !ShowPresentOnly || device.IsPresent)
            .Where(device => device.CurrentRssi >= minimumRssi)
            .Where(device => string.IsNullOrWhiteSpace(filter) ||
                             device.Address.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                             (device.Name?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderByDescending(device => device.CurrentRssi)
            .ThenBy(device => device.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        ReplaceCollection(FilteredDevices, filtered);
        ReplaceCollection(RankedDevices, filtered.Take(8));
        if (SelectedDevice is null || !filtered.Contains(SelectedDevice))
        {
            SelectedDevice = filtered.FirstOrDefault();
        }
        OnPropertyChanged(nameof(VisibleDeviceCount));
        OnPropertyChanged(nameof(StrongestDeviceText));
    }

    private void RefreshPresence()
    {
        foreach (DeviceSummary summary in _aggregator.Devices)
        {
            if (_deviceRows.TryGetValue(Key(summary.ReceiverId, summary.Address), out DeviceRowViewModel? row))
            {
                row.Apply(summary);
            }
        }
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        T[] desired = items.ToArray();
        if (target.SequenceEqual(desired))
        {
            return;
        }

        target.Clear();
        foreach (T item in desired)
        {
            target.Add(item);
        }
    }

    private void AddActivity(string message)
    {
        ActivityLog.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}");
        while (ActivityLog.Count > 100)
        {
            ActivityLog.RemoveAt(ActivityLog.Count - 1);
        }
    }

    private void OnConnectionStateChanged()
    {
        OnPropertyChanged(nameof(ConnectButtonText));
        OnPropertyChanged(nameof(ConnectionStateText));
        OnPropertyChanged(nameof(ReceiverText));
        ToggleConnectionCommand.RaiseCanExecuteChanged();
    }

    private void OnCaptureStateChanged()
    {
        OnPropertyChanged(nameof(CaptureButtonText));
        OnPropertyChanged(nameof(CaptureStateText));
        OnPropertyChanged(nameof(SessionText));
        ToggleCaptureCommand.RaiseCanExecuteChanged();
    }

    private static string Key(string receiverId, string address) => $"{receiverId}|{address}";

    private void PersistSettings()
    {
        _settings.SelectedTheme = SelectedTheme;
        _settings.SelectedSourceMode = SelectedSourceMode;
        _settings.SelectedPort = SelectedPort;
        _settings.SelectedTimeWindow = SelectedTimeWindow;
        _settings.MinimumRssiText = MinimumRssiText;
        _settings.ShowPresentOnly = ShowPresentOnly;
        _settingsStore.Save(_settings);
    }

    private static string NormalizeTheme(string? theme)
    {
        return string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";
    }

    private static Task DispatchAsync(Action action)
    {
        return Application.Current.Dispatcher.InvokeAsync(action).Task;
    }
}
