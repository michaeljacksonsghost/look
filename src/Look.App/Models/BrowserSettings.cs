namespace Look.Models;

public sealed class BrowserSettings
{
    public bool ContentBlockingEnabled { get; set; }
    public List<string> BlockedDomains { get; set; } = [];
    public List<string> AllowedDomains { get; set; } = [];
    public Dictionary<string, string> CustomCssByDomain { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
