namespace Look.Services;

public static class AddressParser
{
    public static Uri Parse(string input)
    {
        var value = input.Trim();
        if (string.IsNullOrWhiteSpace(value))
            return new Uri("https://www.google.com");

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
        {
            if (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps)
                return absolute;

            // Never reinterpret an explicit non-web scheme (file:, ftp:, javascript:,
            // chrome:, etc.) as a host name and navigate it directly.
            if (absolute.IsFile || value.Contains("://", StringComparison.Ordinal))
                return Search(value);
        }

        var colonIndex = value.IndexOf(':');
        var slashIndex = value.IndexOf('/');
        if (colonIndex > 0 && (slashIndex < 0 || colonIndex < slashIndex))
        {
            var prefix = value[..colonIndex];
            var hostWithPort = prefix.Contains('.') ||
                string.Equals(prefix, "localhost", StringComparison.OrdinalIgnoreCase);
            if (!hostWithPort && Uri.CheckSchemeName(prefix))
                return Search(value);
        }

        var looksLikeHost = !value.Contains(' ') &&
            (value.Contains('.') || value.StartsWith("localhost", StringComparison.OrdinalIgnoreCase)) &&
            !value.Contains("://", StringComparison.Ordinal);

        if (looksLikeHost && Uri.TryCreate("https://" + value, UriKind.Absolute, out var host) &&
            (host.Scheme == Uri.UriSchemeHttps))
            return host;

        return Search(value);
    }

    private static Uri Search(string query) =>
        new("https://www.google.com/search?q=" + Uri.EscapeDataString(query));
}
