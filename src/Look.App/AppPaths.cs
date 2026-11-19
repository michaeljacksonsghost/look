namespace Look;

/// <summary>Resolves the browser's data location before any WebView2 control is created.</summary>
public static class AppPaths
{
    public static bool IsPortable { get; private set; }
    public static string DataRoot { get; private set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "look");

    public static void Initialize(IEnumerable<string> arguments)
    {
        IsPortable = arguments.Any(argument =>
            string.Equals(argument, "--portable", StringComparison.OrdinalIgnoreCase));

        DataRoot = IsPortable
            ? Path.Combine(AppContext.BaseDirectory, "Data")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "look");

        Directory.CreateDirectory(DataRoot);
    }
}
