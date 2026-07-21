using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace AvaloniaApp.Services;

public record InstalledSteamApp(string AppId, string Name);

// Real detection of Steam library folders + installed games via Valve's VDF
// ("KeyValues") text format. libraryfolders.vdf lists library roots;
// each root's steamapps/appmanifest_*.acf describes one installed game.
// On Windows this reads the real registry/filesystem; on Linux (this dev
// environment) it falls back to the standard Steam-on-Linux locations.
public class SteamLibraryService
{
    public IReadOnlyList<string> DetectLibraryPaths()
    {
        var basePath = FindSteamBasePath();
        if (basePath == null) return Array.Empty<string>();

        var paths = new List<string> { basePath };

        var libraryFoldersVdf = Path.Combine(basePath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(libraryFoldersVdf))
        {
            foreach (var path in ParseVdfValues(File.ReadAllText(libraryFoldersVdf), "path"))
            {
                if (!paths.Contains(path, StringComparer.OrdinalIgnoreCase))
                    paths.Add(path);
            }
        }

        return paths;
    }

    public IReadOnlyList<InstalledSteamApp> DetectInstalledApps()
    {
        var apps = new List<InstalledSteamApp>();

        foreach (var libraryPath in DetectLibraryPaths())
        {
            var steamAppsDir = Path.Combine(libraryPath, "steamapps");
            if (!Directory.Exists(steamAppsDir)) continue;

            foreach (var manifest in Directory.EnumerateFiles(steamAppsDir, "appmanifest_*.acf"))
            {
                var content = File.ReadAllText(manifest);
                var appId = ParseVdfValues(content, "appid").FirstOrDefault();
                var name = ParseVdfValues(content, "name").FirstOrDefault();
                if (appId != null && name != null)
                    apps.Add(new InstalledSteamApp(appId, name));
            }
        }

        return apps;
    }

    private static string? FindSteamBasePath()
    {
        if (OperatingSystem.IsWindows())
        {
            var registryPath = TryReadSteamRegistryPath();
            if (registryPath != null && Directory.Exists(registryPath)) return registryPath;

            var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)";
            var defaultPath = Path.Combine(programFilesX86, "Steam");
            return Directory.Exists(defaultPath) ? defaultPath : null;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Path.Combine(home, ".local", "share", "Steam"),
            Path.Combine(home, ".steam", "steam"),
            Path.Combine(home, ".steam", "root"),
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }

    [SupportedOSPlatform("windows")]
    private static string? TryReadSteamRegistryPath()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            return key?.GetValue("SteamPath") as string;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static IEnumerable<string> ParseVdfValues(string vdfContent, string key)
    {
        var pattern = $"\"{Regex.Escape(key)}\"\\s+\"((?:[^\"\\\\]|\\\\.)*)\"";
        foreach (Match match in Regex.Matches(vdfContent, pattern, RegexOptions.IgnoreCase))
        {
            yield return Regex.Unescape(match.Groups[1].Value);
        }
    }
}
