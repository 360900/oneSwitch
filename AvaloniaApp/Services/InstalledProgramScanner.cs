using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace AvaloniaApp.Services;

// Real detection of installed Windows programs via the registry Uninstall keys -
// the same source Control Panel's "Programs and Features" reads from. On Linux
// (this dev box) there's no equivalent to detect: this feature's whole premise
// is running on the user's Windows machine before migration, so it's verified
// via the WinBoat/dockur Windows VM test setup instead.
public class InstalledProgramScanner
{
    private const string UninstallSubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string UninstallSubKeyWow6432 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    public IReadOnlyList<string> DetectInstalledPrograms()
    {
        if (!OperatingSystem.IsWindows()) return Array.Empty<string>();

        var names = new List<string>();
        ReadUninstallKeys(Registry.LocalMachine, UninstallSubKey, names);
        ReadUninstallKeys(Registry.LocalMachine, UninstallSubKeyWow6432, names);
        ReadUninstallKeys(Registry.CurrentUser, UninstallSubKey, names);

        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    [SupportedOSPlatform("windows")]
    private static void ReadUninstallKeys(RegistryKey hive, string subKeyPath, List<string> names)
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
                        names.Add(displayName);
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
}
