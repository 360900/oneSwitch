using System;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneSwitch.Models;

public partial class AppTranslationEntry : ObservableObject
{
    public required string SourceApp { get; init; }
    public required string SourceLogoGlyph { get; init; }
    public required IBrush SourceLogoColor { get; init; }

    public required string TargetApp { get; init; }
    public required string TargetLogoGlyph { get; init; }
    public required IBrush TargetLogoColor { get; init; }
    public string? Explanation { get; init; }

    // Real package identifiers per manager - what oneswitch.sh (Architecture.md §7) will
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

    // Picks the preferred manager's package id, falling back to Flatpak. Other native
    // managers are never used as a fallback: an apt id is useless on a dnf system.
    // Null Id means no package exists for this app on the target.
    public (PackageManagerFamily Family, string? Id) ResolvePackage(PackageManagerFamily preferred)
    {
        var native = PackageFor(preferred);
        if (!string.IsNullOrEmpty(native)) return (preferred, native);
        if (!string.IsNullOrEmpty(FlatpakId)) return (PackageManagerFamily.Flatpak, FlatpakId);
        return (preferred, null);
    }

    public string ResolvePackageDisplay(PackageManagerFamily preferred)
    {
        var (family, id) = ResolvePackage(preferred);
        if (id is null) return "Not packaged for this distro";

        var label = family switch
        {
            PackageManagerFamily.Apt => "APT",
            PackageManagerFamily.Dnf => "DNF",
            PackageManagerFamily.Pacman => AurIsOfficialRepo ? "Pacman" : "AUR",
            _ => "Flatpak"
        };
        return $"{label}: {id}";
    }

    private string? PackageFor(PackageManagerFamily family) => family switch
    {
        PackageManagerFamily.Apt => AptPackage,
        PackageManagerFamily.Dnf => DnfPackage,
        PackageManagerFamily.Pacman => AurPackage,
        _ => FlatpakId
    };
}
