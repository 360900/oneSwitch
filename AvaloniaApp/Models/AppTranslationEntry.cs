using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApp.Models;

public partial class AppTranslationEntry : ObservableObject
{
    public required string SourceApp { get; init; }
    public required string SourceLogoGlyph { get; init; }
    public required IBrush SourceLogoColor { get; init; }

    public required string TargetApp { get; init; }
    public required string TargetLogoGlyph { get; init; }
    public required IBrush TargetLogoColor { get; init; }
    public required string TargetPackageManager { get; init; }
    public string? Explanation { get; init; }

    // Substrings to match against real installed program names. Empty means
    // match against SourceApp itself (it's already a distinctive substring).
    public string[] DetectionAliases { get; init; } = Array.Empty<string>();

    [ObservableProperty]
    private bool _isIncluded = true;
}
