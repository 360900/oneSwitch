using System;
using System.Linq;
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
    public string? Explanation { get; init; }

    // Real package identifiers per manager - what Kargo.sh (Architecture.md §7) will
    // eventually need to run the actual install. Null means no real package/repo exists
    // for that manager (verified against Flathub/AUR/Debian/Fedora, not guessed).
    public string? AptPackage { get; init; }
    public string? DnfPackage { get; init; }
    public string? AurPackage { get; init; }
    public string? FlatpakId { get; init; }

    // True when AurPackage actually lives in Arch's official extra/multilib repo (plain
    // `pacman -S`) rather than genuine AUR (needs a helper like yay/paru) - Arch policy
    // blocks AUR duplicates of official-repo names, so many "AUR" packages people assume
    // are AUR have since graduated to the official repo under the same name.
    public bool AurIsOfficialRepo { get; init; }

    // Substrings to match against real display-name sources (Uninstall registry, App
    // Paths, Start Menu shortcuts). Empty means match against SourceApp itself (it's
    // already a distinctive substring). Matched word-boundary-aware, not raw substring.
    public string[] DetectionAliases { get; init; } = Array.Empty<string>();

    // AppX PackageFullName prefixes for apps with a real Microsoft Store distribution
    // (e.g. "SpotifyAB.SpotifyMusic", "MSTeams"). Matched via StartsWith, not Contains -
    // PackageFullName is "<PackageName>_<Version>_<Arch>_<ResourceId>__<PublisherId>", and
    // the PackageName segment is how Windows recognizes an update to the same app, so it
    // stays constant across ordinary version updates; only what's checked here is
    // guaranteed stable. Empty means this entry is never matched against Store-app
    // detections - deliberately opt-in, not inferred from SourceApp/DetectionAliases,
    // because compound package identifiers glue words together with no separator (no clean
    // word boundary to check), so an unrelated app's identifier can coincidentally contain
    // another app's name (e.g. "MSTeams" contains "Steam"). A publisher occasionally
    // re-publishes under a genuinely new PackageName (not just a version bump, e.g. classic
    // Teams vs. the current MSIX Teams) - that's a real detection gap no static prefix can
    // predict, only fixable when reported.
    public string[] AppxPackageFamilyPrefixes { get; init; } = Array.Empty<string>();

    // Marks the web-browser entries so the modal can default Edge's checkbox off when
    // another browser was also detected - Edge showing up alongside a real browser choice
    // usually means it's just the Windows-bundled default nobody opted into.
    public bool IsBrowser { get; init; }

    [ObservableProperty]
    private bool _isIncluded = true;

    [ObservableProperty]
    private string _resolvedPackageDisplay = string.Empty;

    // Picks the preferred manager's package id, falling back through the others
    // (in Flatpak -> Apt -> Dnf -> Aur order) if unavailable for this app.
    public string ResolvePackageDisplay(PackageManagerFamily preferred)
    {
        (PackageManagerFamily Family, string? Id)[] candidates =
        {
            (preferred, PackageFor(preferred)),
            (PackageManagerFamily.Flatpak, FlatpakId),
            (PackageManagerFamily.Apt, AptPackage),
            (PackageManagerFamily.Dnf, DnfPackage),
            (PackageManagerFamily.Pacman, AurPackage),
        };

        var match = candidates.FirstOrDefault(c => !string.IsNullOrEmpty(c.Id));
        if (match.Id is null) return string.Empty;

        var label = match.Family switch
        {
            PackageManagerFamily.Apt => "APT",
            PackageManagerFamily.Dnf => "DNF",
            PackageManagerFamily.Pacman => AurIsOfficialRepo ? "Pacman" : "AUR",
            _ => "Flatpak"
        };
        return $"{label}: {match.Id}";
    }

    private string? PackageFor(PackageManagerFamily family) => family switch
    {
        PackageManagerFamily.Apt => AptPackage,
        PackageManagerFamily.Dnf => DnfPackage,
        PackageManagerFamily.Pacman => AurPackage,
        _ => FlatpakId
    };
}
