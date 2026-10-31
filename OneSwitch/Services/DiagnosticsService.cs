using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using OneSwitch.Models;

namespace OneSwitch.Services;

public record GpuDetectionResult(GpuManufacturer Manufacturer, string Details, string? Error);

public class DiagnosticsService
{
    // Display adapter device class. Each numbered subkey is one installed adapter.
    private const string DisplayClassKey =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    private const string DetectionFailed = "Couldn't detect your GPU automatically.";

    public GpuDetectionResult DetectGpu()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? DetectGpuWindows()
            : DetectGpuLinux();
    }

    // Reads adapter names from the registry instead of shelling out to wmic, which
    // is no longer installed by default on current Windows 11.
    [SupportedOSPlatform("windows")]
    private static GpuDetectionResult DetectGpuWindows()
    {
        try
        {
            using var classKey = Registry.LocalMachine.OpenSubKey(DisplayClassKey);
            if (classKey is null)
                return new GpuDetectionResult(GpuManufacturer.Unknown, string.Empty, DetectionFailed);

            var names = new List<string>();
            foreach (var sub in classKey.GetSubKeyNames().Where(n => n.All(char.IsDigit)))
            {
                try
                {
                    using var adapter = classKey.OpenSubKey(sub);
                    if (adapter?.GetValue("DriverDesc") is string desc && !string.IsNullOrWhiteSpace(desc))
                        names.Add(desc.Trim());
                }
                catch (System.Security.SecurityException)
                {
                    // Some adapter subkeys are ACL-restricted; skip them.
                }
            }
            return Classify(names);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return new GpuDetectionResult(GpuManufacturer.Unknown, string.Empty, DetectionFailed);
        }
    }

    private static GpuDetectionResult DetectGpuLinux()
    {
        try
        {
            // Only display controllers: VGA, 3D and Display classes. Scanning every PCI
            // line misreports AMD CPUs/chipsets as AMD GPUs.
            var lines = RunProcess("lspci", string.Empty)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(l => l.Contains("VGA compatible controller", StringComparison.Ordinal)
                         || l.Contains("3D controller", StringComparison.Ordinal)
                         || l.Contains("Display controller", StringComparison.Ordinal));
            return Classify(lines);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return new GpuDetectionResult(GpuManufacturer.Unknown, string.Empty, DetectionFailed);
        }
    }

    private static string RunProcess(string fileName, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    // Discrete vendors win over Intel so hybrid laptops report the GPU that matters
    // for driver selection.
    private static GpuDetectionResult Classify(IEnumerable<string> adapters)
    {
        var list = adapters.ToList();

        var line = list.FirstOrDefault(l => l.Contains("nvidia", StringComparison.OrdinalIgnoreCase));
        if (line != null) return new GpuDetectionResult(GpuManufacturer.Nvidia, line, null);

        line = list.FirstOrDefault(l => l.Contains("amd", StringComparison.OrdinalIgnoreCase)
                                     || l.Contains("radeon", StringComparison.OrdinalIgnoreCase)
                                     || l.Contains("advanced micro devices", StringComparison.OrdinalIgnoreCase));
        if (line != null) return new GpuDetectionResult(GpuManufacturer.AMD, line, null);

        line = list.FirstOrDefault(l => l.Contains("intel", StringComparison.OrdinalIgnoreCase));
        if (line != null) return new GpuDetectionResult(GpuManufacturer.Intel, line, null);

        return new GpuDetectionResult(GpuManufacturer.Unknown, string.Empty, null);
    }
}
