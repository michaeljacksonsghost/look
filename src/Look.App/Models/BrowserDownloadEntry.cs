namespace Look.Models;

public sealed record BrowserDownloadEntry(Guid Id, string FileName, string SourceUri,
    string DestinationPath, bool IsComplete, DateTimeOffset StartedAt);
