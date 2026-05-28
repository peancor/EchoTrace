namespace EchoTrace.App.ViewModels;

public sealed class ShellSectionViewModel(string key, string title, string description, bool isAvailable)
{
    public string Key { get; } = key;
    public string Title { get; } = title;
    public string Description { get; } = description;
    public bool IsAvailable { get; } = isAvailable;
}
