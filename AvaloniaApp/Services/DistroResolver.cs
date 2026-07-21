using AvaloniaApp.Models;

namespace AvaloniaApp.Services;

// Mirrors the distro choice Slide2ViewModel already displays (Gaming->Bazzite,
// Productivity->Debian, Enthusiast->Arch, DailyUse->Fedora/Mint by paradigm), so
// other consumers of MigrationState can resolve the same target distro/package manager.
public static class DistroResolver
{
    public static TargetDistro ResolveTargetDistro(MigrationState state) => state.SelectedRole switch
    {
        PrimaryRole.Gaming => TargetDistro.Bazzite,
        PrimaryRole.Productivity => TargetDistro.Debian,
        PrimaryRole.Enthusiast => TargetDistro.Arch,
        PrimaryRole.DailyUse => state.SelectedParadigm == DesktopParadigm.MacOS
            ? TargetDistro.Fedora
            : TargetDistro.Mint,
        _ => TargetDistro.Bazzite
    };

    public static PackageManagerFamily ResolvePackageManager(MigrationState state) => ResolveTargetDistro(state) switch
    {
        TargetDistro.Bazzite => PackageManagerFamily.Flatpak,
        TargetDistro.Fedora => PackageManagerFamily.Dnf,
        TargetDistro.Debian => PackageManagerFamily.Apt,
        TargetDistro.Arch => PackageManagerFamily.Pacman,
        TargetDistro.Mint => PackageManagerFamily.Apt,
        _ => PackageManagerFamily.Flatpak
    };

    // Fedora/Debian/Mint have a fixed DE; Bazzite/Arch follow the Slide 1 paradigm
    // (macOS picks GNOME for the centered-dock path, Windows picks KDE for the
    // bottom-panel path). Used by the kargo.sh dock-provisioning gate and by
    // UnattendedScriptFactory for archinstall's profile_config.
    public static DesktopEnvironment ResolveDesktopEnvironment(MigrationState state) => ResolveTargetDistro(state) switch
    {
        TargetDistro.Mint => DesktopEnvironment.Cinnamon,
        TargetDistro.Fedora => DesktopEnvironment.Gnome,
        TargetDistro.Debian => DesktopEnvironment.Gnome,
        TargetDistro.Bazzite => state.SelectedParadigm == DesktopParadigm.MacOS
            ? DesktopEnvironment.Gnome
            : DesktopEnvironment.Kde,
        TargetDistro.Arch => state.SelectedParadigm == DesktopParadigm.MacOS
            ? DesktopEnvironment.Gnome
            : DesktopEnvironment.Kde,
        _ => DesktopEnvironment.Gnome
    };
}
