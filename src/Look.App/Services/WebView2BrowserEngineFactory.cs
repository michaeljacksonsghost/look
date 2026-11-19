namespace Look.Services;

public sealed class WebView2BrowserEngineFactory : IBrowserEngineFactory
{
    public IBrowserEngine Create() => new WebView2BrowserEngine();
}
