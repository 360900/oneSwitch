using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using AvaloniaApp.Models;

namespace AvaloniaApp.Services;

// Curated Windows-app -> Linux-equivalent mapping table. This part stays hand-picked
// (it's recommendation, not detection) - what's real is which of these entries actually
// apply, filtered against InstalledProgramScanner's real registry scan.
public class AppTranslationCatalog
{
    public IReadOnlyList<AppTranslationEntry> GetSuggestions(IReadOnlyList<string> installedPrograms)
    {
        return AllEntries().Where(entry =>
        {
            var aliases = entry.DetectionAliases.Length > 0 ? entry.DetectionAliases : new[] { entry.SourceApp };
            return aliases.Any(alias => installedPrograms.Any(installed =>
                installed.Contains(alias, StringComparison.OrdinalIgnoreCase)));
        }).ToList();
    }

    private static IReadOnlyList<AppTranslationEntry> AllEntries() => new List<AppTranslationEntry>
    {
        new()
        {
            SourceApp = "MS Office", SourceLogoGlyph = "W", SourceLogoColor = Brush.Parse("#0078D7"),
            TargetApp = "OnlyOffice", TargetLogoGlyph = "O", TargetLogoColor = Brush.Parse("#D83B01"),
            TargetPackageManager = "Flatpak",
            Explanation = "Highly compatible alternative that opens .docx/.xlsx/.pptx without conversion issues.",
            DetectionAliases = new[] { "Microsoft Office", "Microsoft 365" }
        },
        new()
        {
            SourceApp = "Spotify", SourceLogoGlyph = "S", SourceLogoColor = Brush.Parse("#1DB954"),
            TargetApp = "Spotify", TargetLogoGlyph = "S", TargetLogoColor = Brush.Parse("#1DB954"),
            TargetPackageManager = "Native Package",
            Explanation = "Spotify ships an official Linux client."
        },
        new()
        {
            SourceApp = "Adobe Photoshop", SourceLogoGlyph = "Ps", SourceLogoColor = Brush.Parse("#31A8FF"),
            TargetApp = "GIMP", TargetLogoGlyph = "G", TargetLogoColor = Brush.Parse("#5C5543"),
            TargetPackageManager = "Flatpak",
            Explanation = "Free, full-featured raster editor with a comparable layers/plugins workflow."
        },
        new()
        {
            SourceApp = "Adobe Acrobat", SourceLogoGlyph = "A", SourceLogoColor = Brush.Parse("#DC3E28"),
            TargetApp = "Okular", TargetLogoGlyph = "O", TargetLogoColor = Brush.Parse("#1D99F3"),
            TargetPackageManager = "APT/DNF",
            Explanation = "Handles PDF annotation and forms out of the box."
        },
        new()
        {
            SourceApp = "Notepad++", SourceLogoGlyph = "N", SourceLogoColor = Brush.Parse("#90E59A"),
            TargetApp = "VS Code", TargetLogoGlyph = "VS", TargetLogoColor = Brush.Parse("#007ACC"),
            TargetPackageManager = "Flatpak",
            Explanation = "Closest match for lightweight text/code editing."
        },
        new()
        {
            SourceApp = "WinRAR", SourceLogoGlyph = "R", SourceLogoColor = Brush.Parse("#8B0000"),
            TargetApp = "File Roller", TargetLogoGlyph = "FR", TargetLogoColor = Brush.Parse("#F6A429"),
            TargetPackageManager = "APT/DNF",
            Explanation = "Built-in archive manager, supports .rar/.zip/.7z."
        },
    };
}
