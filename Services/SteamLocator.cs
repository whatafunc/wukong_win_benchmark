using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace WukongBenchmarkAutomator.Services;

/// <summary>
/// Locates the Black Myth: Wukong Benchmark Tool install directory.
/// AppID = 3132990, install folder name = "Black Myth Wukong Benchmark Tool"
/// </summary>
public static class SteamLocator
{
    private const string AppFolderName = "Black Myth Wukong Benchmark Tool";
    private const string AppId = "3132990";

    public static string? FindInstallDirectory()
    {
        // 1. Try registry (most reliable on Windows)
        var fromRegistry = TryFromRegistry();
        if (fromRegistry is not null && Directory.Exists(fromRegistry))
            return fromRegistry;

        // 2. Common Steam library folders
        foreach (var lib in GetSteamLibraryFolders())
        {
            var candidate = Path.Combine(lib, "steamapps", "common", AppFolderName);
            if (Directory.Exists(candidate))
                return candidate;
        }

        // 3. Environment variable override (useful for CI / custom installs)
        var env = Environment.GetEnvironmentVariable("WUKONG_BENCHMARK_PATH");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return env;

        return null;
    }

    private static string? TryFromRegistry()
    {
        try
        {
            // 64-bit Steam
            using var key = Registry.LocalMachine.OpenSubKey(
                $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {AppId}");
            var path = key?.GetValue("InstallLocation") as string;
            if (!string.IsNullOrEmpty(path))
                return path;

            // 32-bit view
            using var key32 = Registry.LocalMachine.OpenSubKey(
                $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {AppId}");
            path = key32?.GetValue("InstallLocation") as string;
            return path;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> GetSteamLibraryFolders()
    {
        var candidates = new List<string>();

        // Default locations
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        candidates.Add(Path.Combine(programFiles, "Steam"));

        var programFiles64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        candidates.Add(Path.Combine(programFiles64, "Steam"));

        // User-level Steam
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        candidates.Add(Path.Combine(localAppData, "Steam"));

        // Parse libraryfolders.vdf if present
        foreach (var steamRoot in candidates.ToList())
        {
            var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;

            try
            {
                var text = File.ReadAllText(vdf);
                // very simple extraction of "path" values
                foreach (Match m in Regex.Matches(text, @"""path""\s+""([^""]+)"""))
                {
                    var p = m.Groups[1].Value.Replace(@"\\", @"\");
                    if (Directory.Exists(p))
                        candidates.Add(p);
                }
            }
            catch { /* ignore malformed vdf */ }
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
