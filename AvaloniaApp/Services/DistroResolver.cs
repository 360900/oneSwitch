using AvaloniaApp.Models;

namespace AvaloniaApp.Services;

// Mirrors the distro choice Slide2ViewModel already displays (Gaming->Bazzite,
// Productivity->Debian, Enthusiast->Arch, DailyUse->Fedora/Mint by paradigm), so
// other consumers of MigrationState can resolve the same target package manager.
public static class DistroResolver
{
    public static PackageManagerFamily ResolvePackageManager(MigrationState state) => state.SelectedRole switch
    {
        PrimaryRole.Gaming => PackageManagerFamily.Flatpak,
        PrimaryRole.Productivity => PackageManagerFamily.Apt,
        PrimaryRole.Enthusiast => PackageManagerFamily.Pacman,
        PrimaryRole.DailyUse => state.SelectedParadigm == DesktopParadigm.MacOS
            ? PackageManagerFamily.Dnf
            : PackageManagerFamily.Apt,
        _ => PackageManagerFamily.Flatpak
    };
}
