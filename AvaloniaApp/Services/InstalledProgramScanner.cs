using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace AvaloniaApp.Services;

// A detected program name plus how it should be matched against catalog aliases.
// AppX PackageFullNames glue words together with no separator (e.g.
// "SpotifyAB.SpotifyMusic_1.230.658.0_..."), so they need raw substring matching.
// Every other source is a real human-readable display name, where raw substring
// matching produces false positives (e.g. SourceApp "Steam" matching the
// "Steamworks Common Redistributables" entry that many non-Steam games install) -
// those need word-boundary matching instead.
public readonly record struct DetectedProgram(string Name, bool IsCompoundIdentifier);

// Real detection of installed Windows programs. Combines every source Windows
// itself uses to track "what's installed," since no single one is complete:
// - The registry Uninstall keys (HKLM/HKLM\WOW6432Node/HKCU), the same source
//   Control Panel's "Programs and Features" reads from - covers classic Win32
//   installers.
// - The AppX package repository - covers Microsoft Store (MSIX/UWP) apps, which
//   never write an Uninstall key entry at all (e.g. Spotify installed from the
//   Store rather than spotify.com's installer).
// - App Paths (HKLM/HKLM\WOW6432Node) - the Win+R exe-resolution registry, a
//   separate registration many installers write even when Uninstall entries are
//   missing or hidden (SystemComponent=1).
// - Start Menu shortcuts (per-user and all-users Programs folders) - catches
//   portable/manually-copied apps that were never "installed" via any registry
//   path but still show up in the Start Menu a user would actually launch them
//   from.
// On Linux (this dev box) there's no equivalent to detect: this feature's whole
// premise is running on the user's Windows machine before migration, so it's
// verified via the WinBoat/dockur Windows VM test setup instead.
public class InstalledProgramScanner
{
    private const string UninstallSubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string UninstallSubKeyWow6432 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string AppxPackagesSubKey = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";
    private const string AppPathsSubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
    private const string AppPathsSubKeyWow6432 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths";

    public IReadOnlyList<DetectedProgram> DetectInstalledPrograms()
    {
        if (!OperatingSystem.IsWindows()) return Array.Empty<DetectedProgram>();

        var programs = new List<DetectedProgram>();
        ReadUninstallKeys(Registry.LocalMachine, UninstallSubKey, programs);
        ReadUninstallKeys(Registry.LocalMachine, UninstallSubKeyWow6432, programs);
        ReadUninstallKeys(Registry.CurrentUser, UninstallSubKey, programs);
        ReadAppxPackages(programs);
        ReadAppPaths(Registry.LocalMachine, AppPathsSubKey, programs);
        ReadAppPaths(Registry.LocalMachine, AppPathsSubKeyWow6432, programs);
        ReadStartMenuShortcuts(Environment.SpecialFolder.Programs, programs);
        ReadStartMenuShortcuts(Environment.SpecialFolder.CommonPrograms, programs);

        return programs.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
    }

    [SupportedOSPlatform("windows")]
    private static void ReadUninstallKeys(RegistryKey hive, string subKeyPath, List<DetectedProgram> programs)
    {
        try
        {
            using var uninstallKey = hive.OpenSubKey(subKeyPath);
            if (uninstallKey == null) return;

            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                try
                {
                    using var entry = uninstallKey.OpenSubKey(subKeyName);
                    if (entry == null) continue;

                    if (entry.GetValue("SystemComponent") is int systemComponent && systemComponent == 1) continue;
                    if (entry.GetValue("ParentKeyName") != null) continue;

                    if (entry.GetValue("DisplayName") is string displayName && !string.IsNullOrWhiteSpace(displayName))
                        programs.Add(new DetectedProgram(displayName, IsCompoundIdentifier: false));
                }
                catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
                {
                    // Skip this entry, keep scanning the rest.
                }
            }
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            // Hive/subkey unreadable (e.g. missing WOW6432Node on a 32-bit OS) - skip it.
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ReadAppxPackages(List<DetectedProgram> programs)
    {
        try
        {
            using var packagesKey = Registry.CurrentUser.OpenSubKey(AppxPackagesSubKey);
            if (packagesKey == null) return;

            foreach (var packageFullName in packagesKey.GetSubKeyNames())
            {
                // Subkey names are PackageFullNames, e.g.
                // "SpotifyAB.SpotifyMusic_1.230.658.0_x86__zpdnekdrzrea0" - words are
                // glued together with no separator, so this needs substring matching
                // rather than the word-boundary matching real display names get.
                programs.Add(new DetectedProgram(packageFullName, IsCompoundIdentifier: true));
            }
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            // Hive/subkey unreadable - skip Store-app detection, Win32 uninstall keys still work.
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ReadAppPaths(RegistryKey hive, string subKeyPath, List<DetectedProgram> programs)
    {
        try
        {
            using var appPathsKey = hive.OpenSubKey(subKeyPath);
            if (appPathsKey == null) return;

            foreach (var subKeyName in appPathsKey.GetSubKeyNames())
            {
                try
                {
                    using var entry = appPathsKey.OpenSubKey(subKeyName);
                    // The default value holds the full exe path. Uninstallers routinely
                    // leave this key behind (it's not tracked as part of MSI/uninstall
                    // cleanup), so a stale entry pointing at a now-deleted exe is common -
                    // require the target to still exist on disk before trusting it.
                    if (entry?.GetValue(null) is not string exePath || !File.Exists(exePath)) continue;

                    programs.Add(new DetectedProgram(Path.GetFileNameWithoutExtension(subKeyName), IsCompoundIdentifier: false));
                }
                catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
                {
                    // Skip this entry, keep scanning the rest.
                }
            }
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            // Hive/subkey unreadable - skip App Paths detection, other sources still work.
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ReadStartMenuShortcuts(Environment.SpecialFolder folder, List<DetectedProgram> programs)
    {
        try
        {
            var programsPath = Environment.GetFolderPath(folder);
            if (string.IsNullOrEmpty(programsPath) || !Directory.Exists(programsPath)) return;

            foreach (var shortcutPath in Directory.EnumerateFiles(programsPath, "*.lnk", SearchOption.AllDirectories))
            {
                // Shortcuts can outlive the program they pointed to (many uninstallers
                // don't clean up the Start Menu folder) - resolve the .lnk target and
                // require it to still exist on disk before trusting the entry.
                var target = ResolveShortcutTarget(shortcutPath);
                if (string.IsNullOrEmpty(target) || !File.Exists(target)) continue;

                programs.Add(new DetectedProgram(Path.GetFileNameWithoutExtension(shortcutPath), IsCompoundIdentifier: false));
            }
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            // Folder unreadable - skip shortcut-based detection, other sources still work.
        }
    }

    [SupportedOSPlatform("windows")]
    private static string? ResolveShortcutTarget(string shortcutPath)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return null;

            dynamic? shell = Activator.CreateInstance(shellType);
            if (shell == null) return null;

            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            return shortcut.TargetPath as string;
        }
        catch (Exception)
        {
            // COM resolution failed for this shortcut (corrupt .lnk, etc.) - skip it.
            return null;
        }
    }
}
