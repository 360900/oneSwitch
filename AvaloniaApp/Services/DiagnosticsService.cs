using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using AvaloniaApp.Models;

namespace AvaloniaApp.Services;

public record GpuDetectionResult(GpuManufacturer Manufacturer, string Details, string? Error);

public class DiagnosticsService
{
    public GpuDetectionResult DetectGpu()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? DetectGpuWindows()
            : DetectGpuLinux();
    }

    private GpuDetectionResult DetectGpuWindows()
    {
        try
        {
            // Note: System.Management would give a proper WMI query; to keep the footprint light
            // we shell out to wmic instead. A real deployment may want to reference System.Management.
            var output = RunProcess("wmic", "path win32_VideoController get name");
            return ParseGpuOutput(output);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return new GpuDetectionResult(GpuManufacturer.Unknown, string.Empty, "Couldn't detect your GPU automatically.");
        }
    }

    private GpuDetectionResult DetectGpuLinux()
    {
        try
        {
            var output = RunProcess("lspci", string.Empty);
            return ParseGpuOutput(output);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return new GpuDetectionResult(GpuManufacturer.Unknown, string.Empty, "Couldn't detect your GPU automatically.");
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

    private static GpuDetectionResult ParseGpuOutput(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var line = lines.FirstOrDefault(l => l.Contains("nvidia", StringComparison.OrdinalIgnoreCase));
        if (line != null) return new GpuDetectionResult(GpuManufacturer.Nvidia, line, null);

        line = lines.FirstOrDefault(l => l.Contains("amd", StringComparison.OrdinalIgnoreCase) || l.Contains("radeon", StringComparison.OrdinalIgnoreCase) || l.Contains("advanced micro devices", StringComparison.OrdinalIgnoreCase));
        if (line != null) return new GpuDetectionResult(GpuManufacturer.AMD, line, null);

        line = lines.FirstOrDefault(l => l.Contains("intel", StringComparison.OrdinalIgnoreCase) && (l.Contains("VGA", StringComparison.Ordinal) || l.Contains("graphics", StringComparison.OrdinalIgnoreCase) || l.Contains("display", StringComparison.OrdinalIgnoreCase)));
        if (line != null) return new GpuDetectionResult(GpuManufacturer.Intel, line, null);

        return new GpuDetectionResult(GpuManufacturer.Unknown, string.Empty, null);
    }
}
