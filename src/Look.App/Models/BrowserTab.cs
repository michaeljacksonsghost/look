using CommunityToolkit.Mvvm.ComponentModel;

namespace Look.Models;

public partial class BrowserTab : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    [ObservableProperty]
    private string title = "New tab";

    [ObservableProperty]
    private string address = "";

    [ObservableProperty]
    private bool isPinned;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isMuted;

    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? "New tab" : Title;

    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(DisplayTitle));
    partial void OnIsPinnedChanged(bool value) => OnPropertyChanged(nameof(DisplayTitle));
}
