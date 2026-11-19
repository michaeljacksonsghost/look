using System.IO;
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
        CleanupAbandonedPrivateData();
    }

    private static void CleanupAbandonedPrivateData()
    {
        var privateRoot = Path.Combine(Path.GetTempPath(), "look-private");
        if (!Directory.Exists(privateRoot)) return;

        foreach (var directory in Directory.EnumerateDirectories(privateRoot))
        {
            try
            {
                var lockPath = Path.Combine(directory, "private.lock");
                if (File.Exists(lockPath))
                {
                    using (var probe = new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                }
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException) { /* An active private window holds the exclusive lock. */ }
            catch (UnauthorizedAccessException) { /* Leave protected temporary data untouched. */ }
        }
    }
}
