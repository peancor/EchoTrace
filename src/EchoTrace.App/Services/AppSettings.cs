namespace EchoTrace.App.Services;

public sealed class AppSettings
{
    public string SelectedTheme { get; set; } = "Dark";
    public string SelectedSourceMode { get; set; } = "Simulator";
    public string? SelectedPort { get; set; }
    public string? SelectedDeviceKey { get; set; }
    public string SelectedTimeWindow { get; set; } = "30s";
    public string MinimumRssiText { get; set; } = "-100";
    public bool ShowPresentOnly { get; set; } = true;
}
