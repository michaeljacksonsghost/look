namespace Look.Models;

/// <summary>UI-neutral request to choose the destination for a browser download.</summary>
public sealed class BrowserDownloadRequest : EventArgs
{
    private readonly Action<string?> _complete;

    public BrowserDownloadRequest(string sourceUri, string suggestedFileName, Action<string?> complete)
    {
        SourceUri = sourceUri;
        SuggestedFileName = string.IsNullOrWhiteSpace(suggestedFileName) ? "download" : suggestedFileName;
        _complete = complete;
    }

    public event EventHandler<bool>? Finished;
    public string? DestinationPath { get; private set; }

    public string SourceUri { get; }
    public string SuggestedFileName { get; }
    public void Complete(string? destinationPath)
    {
        DestinationPath = destinationPath;
        _complete(destinationPath);
    }

    public void ReportFinished(bool success) => Finished?.Invoke(this, success);
}
