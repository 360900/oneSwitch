using System.Collections.Generic;
using System.Linq;
using System.Text;
using OneSwitch.Models;

namespace OneSwitch.Services;

// Generates oneswitch.sh, the first-boot payload script (Architecture.md §7) that runs once
// as root from a systemd oneshot unit on the freshly-installed Linux system. Pure string
// generation - no filesystem/process access here, so the output can be inspected/tested
// without needing a real Linux box.
//
// Every install line is best-effort: a missing vendor repo should degrade to a skipped
// app, never abort the rest of the script. The script always exits 0 so the unit's
// self-cleanup runs.
public static class FirstBootPayloadBuilder
{
    // Verified against the live extension.gnome.org listing - note the dot before "gmail",
    // easy to mistype as one word.
    private const string DashToDockUuid = "dash-to-dock@micxgx.gmail.com";

    public const string WallpaperPath = "/usr/share/backgrounds/oneswitch-wallpaper.jpg";
    private const string AurPendingPath = "/var/lib/oneswitch/aur-pending.txt";

    public static string Build(MigrationState state, IReadOnlyList<AppTranslationEntry> entries)
    {
        var preferred = DistroResolver.ResolvePackageManager(state);
        var targetDistro = DistroResolver.ResolveTargetDistro(state);
        var paradigm = state.SelectedParadigm == DesktopParadigm.MacOS ? "macos" : "windows";

        var sb = new StringBuilder();
        AppendPreamble(sb, paradigm);
        AppendAppInstalls(sb, entries, preferred);
        AppendDconfProfile(sb);
        AppendDockProvisioning(sb, state, targetDistro);
        AppendWallpaperApply(sb);
        AppendRclonePullStub(sb);
        sb.AppendLine();
        sb.AppendLine("echo \"oneSwitch first-boot payload finished\"");
        sb.AppendLine("exit 0");
        // AppendLine produces Environment.NewLine (CRLF on Windows), which bash on the
        // target can't parse. Normalize to LF at the boundary.
        return sb.ToString().Replace("\r\n", "\n");
    }

    private static void AppendPreamble(StringBuilder sb, string paradigm)
    {
        sb.AppendLine("#!/usr/bin/env bash");
        sb.AppendLine("# oneSwitch first-boot payload (Architecture.md §7) - runs once on the freshly-installed system.");
        sb.AppendLine("set -uo pipefail");
        sb.AppendLine();
        sb.AppendLine("# --- Section a: environment detection ---");
        sb.AppendLine("# The Slide 1 desktop-paradigm choice, baked in at generation time. An environment");
        sb.AppendLine("# variable of the same name overrides it.");
        sb.AppendLine($"ONESWITCH_PARADIGM=\"${{ONESWITCH_PARADIGM:-{paradigm}}}\"");
        sb.AppendLine();
        sb.AppendLine("DISTRO_ID=\"\"");
        sb.AppendLine("if [ -f /etc/os-release ]; then");
        sb.AppendLine("    . /etc/os-release");
        sb.AppendLine("    DISTRO_ID=\"${ID:-}\"");
        sb.AppendLine("fi");
        sb.AppendLine();
        sb.AppendLine("PKG_MANAGER=\"\"");
        sb.AppendLine("if command -v apt-get >/dev/null 2>&1; then");
        sb.AppendLine("    PKG_MANAGER=\"apt\"");
        sb.AppendLine("elif command -v dnf >/dev/null 2>&1; then");
        sb.AppendLine("    PKG_MANAGER=\"dnf\"");
        sb.AppendLine("elif command -v pacman >/dev/null 2>&1; then");
        sb.AppendLine("    PKG_MANAGER=\"pacman\"");
        sb.AppendLine("fi");
        sb.AppendLine();
        sb.AppendLine("DE=\"\"");
        sb.AppendLine("if command -v gnome-shell >/dev/null 2>&1; then");
        sb.AppendLine("    DE=\"gnome\"");
        sb.AppendLine("elif command -v plasmashell >/dev/null 2>&1; then");
        sb.AppendLine("    DE=\"kde\"");
        sb.AppendLine("fi");
        sb.AppendLine();
        sb.AppendLine("export DEBIAN_FRONTEND=noninteractive");
        sb.AppendLine();
        sb.AppendLine("echo \"oneSwitch first-boot payload starting (paradigm=$ONESWITCH_PARADIGM distro=$DISTRO_ID pkg=$PKG_MANAGER de=$DE)\"");
        sb.AppendLine();
    }

    private static void AppendAppInstalls(StringBuilder sb, IReadOnlyList<AppTranslationEntry> entries, PackageManagerFamily preferred)
    {
        sb.AppendLine("# --- Section b: install the translated application array (Architecture.md §7) ---");
        sb.AppendLine("# Known gaps: Brave, Docker, 1Password, VS Code, Sublime Text, and Mullvad need a");
        sb.AppendLine("# vendor apt/dnf repo added before these installs will find the package - not done");
        sb.AppendLine("# here, so those specific lines will no-op until that repo setup exists.");
        sb.AppendLine("# AUR helpers (yay/paru) refuse to run as root, so genuine AUR packages are written");
        sb.AppendLine($"# to {AurPendingPath} for the user to install after logging in.");
        sb.AppendLine();
        sb.AppendLine("case \"$PKG_MANAGER\" in");
        sb.AppendLine("    apt) apt-get update || true ;;");
        sb.AppendLine("    dnf) dnf makecache || true ;;");
        sb.AppendLine("    pacman) pacman -Sy --noconfirm || true ;;");
        sb.AppendLine("esac");
        sb.AppendLine();
        sb.AppendLine("if ! command -v flatpak >/dev/null 2>&1; then");
        sb.AppendLine("    case \"$PKG_MANAGER\" in");
        sb.AppendLine("        apt) apt-get install -y flatpak || true ;;");
        sb.AppendLine("        dnf) dnf install -y flatpak || true ;;");
        sb.AppendLine("        pacman) pacman -S --noconfirm --needed flatpak || true ;;");
        sb.AppendLine("    esac");
        sb.AppendLine("fi");
        sb.AppendLine("if command -v flatpak >/dev/null 2>&1; then");
        sb.AppendLine("    flatpak remote-add --if-not-exists flathub https://flathub.org/repo/flathub.flatpakrepo || true");
        sb.AppendLine("fi");
        sb.AppendLine();

        foreach (var entry in entries.Where(e => e.IsIncluded))
        {
            sb.AppendLine(EmitInstallLine(entry, preferred));
        }
        sb.AppendLine();
    }

    private static string EmitInstallLine(AppTranslationEntry entry, PackageManagerFamily preferred)
    {
        var (family, id) = entry.ResolvePackage(preferred);
        if (string.IsNullOrEmpty(id))
            return $"echo 'Skipping {entry.TargetApp}: no package available'";

        return family switch
        {
            PackageManagerFamily.Apt =>
                $"if [ \"$PKG_MANAGER\" = \"apt\" ]; then apt-get install -y {id} || true; fi  # {entry.TargetApp}",
            PackageManagerFamily.Dnf =>
                $"if [ \"$PKG_MANAGER\" = \"dnf\" ]; then dnf install -y {id} || true; fi  # {entry.TargetApp}",
            PackageManagerFamily.Pacman => entry.AurIsOfficialRepo
                ? $"if [ \"$PKG_MANAGER\" = \"pacman\" ]; then pacman -S --noconfirm --needed {id} || true; fi  # {entry.TargetApp}"
                : $"if [ \"$PKG_MANAGER\" = \"pacman\" ]; then mkdir -p {Dirname(AurPendingPath)} && echo '{id}' >> {AurPendingPath}; fi  # {entry.TargetApp} (AUR)",
            _ =>
                $"if command -v flatpak >/dev/null 2>&1; then flatpak install -y --noninteractive flathub {id} || true; fi  # {entry.TargetApp}"
        };
    }

    private static string Dirname(string path) => path[..path.LastIndexOf('/')];

    // GNOME settings are written as dconf system defaults (not gsettings), because this
    // script runs as root with no user session. The profile must reference the "local"
    // system db. An existing profile (Fedora ships one) is extended, never replaced.
    private static void AppendDconfProfile(StringBuilder sb)
    {
        sb.AppendLine("# --- dconf system defaults (GNOME) ---");
        sb.AppendLine("if [ \"$DE\" = \"gnome\" ]; then");
        sb.AppendLine("    mkdir -p /etc/dconf/profile /etc/dconf/db/local.d");
        sb.AppendLine("    if [ ! -f /etc/dconf/profile/user ]; then");
        sb.AppendLine("        printf 'user-db:user\\nsystem-db:local\\n' > /etc/dconf/profile/user");
        sb.AppendLine("    elif ! grep -qx 'system-db:local' /etc/dconf/profile/user; then");
        sb.AppendLine("        echo 'system-db:local' >> /etc/dconf/profile/user");
        sb.AppendLine("    fi");
        sb.AppendLine("fi");
        sb.AppendLine();
    }

    private static void AppendDockProvisioning(StringBuilder sb, MigrationState state, TargetDistro targetDistro)
    {
        sb.AppendLine("# --- Section c: dock provisioning (Architecture.md §3) ---");

        var isMacParadigm = state.SelectedParadigm == DesktopParadigm.MacOS;
        var de = DistroResolver.ResolveDesktopEnvironment(state);

        var installCmd = targetDistro switch
        {
            TargetDistro.Fedora => "dnf install -y gnome-shell-extension-dash-to-dock",
            TargetDistro.Debian => "apt-get install -y gnome-shell-extension-dashtodock",
            // Arch package name still needs real-ISO VM verification.
            TargetDistro.Arch => "pacman -S --noconfirm --needed gnome-shell-extension-dash-to-dock",
            _ => null
        };

        if (!isMacParadigm || de != DesktopEnvironment.Gnome || installCmd is null)
        {
            sb.AppendLine("# Not applicable: the centered dock only applies to the macOS paradigm on a");
            sb.AppendLine("# GNOME target (Fedora, Debian or Arch).");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("if [ \"$DE\" = \"gnome\" ]; then");
        sb.AppendLine($"    {installCmd} || true");
        sb.AppendLine("    cat > /etc/dconf/db/local.d/01-oneswitch-dock <<'DCONF_DB'");
        sb.AppendLine("[org/gnome/shell]");
        sb.AppendLine($"enabled-extensions=['{DashToDockUuid}']");
        sb.AppendLine();
        sb.AppendLine("[org/gnome/shell/extensions/dash-to-dock]");
        sb.AppendLine("dock-position='BOTTOM'");
        sb.AppendLine("extend-height=false");
        sb.AppendLine("dock-fixed=true");
        sb.AppendLine("transparency-mode='DYNAMIC'");
        sb.AppendLine("running-indicator-style='DOTS'");
        sb.AppendLine("DCONF_DB");
        sb.AppendLine("    # No locks written here, so all of this stays user-overridable afterward.");
        sb.AppendLine("fi");
        sb.AppendLine();
    }

    private static void AppendWallpaperApply(StringBuilder sb)
    {
        sb.AppendLine("# --- Section d: wallpaper apply (Architecture.md §5) ---");
        sb.AppendLine("# The wallpaper file itself is staged by §5/§6, neither implemented yet - this section");
        sb.AppendLine("# no-ops until that file exists. KDE has no root-side default that survives Plasma's");
        sb.AppendLine("# per-user config, so it is GNOME only for now.");
        sb.AppendLine($"WALLPAPER_PATH=\"{WallpaperPath}\"");
        sb.AppendLine("if [ -f \"$WALLPAPER_PATH\" ] && [ \"$DE\" = \"gnome\" ]; then");
        sb.AppendLine("    cat > /etc/dconf/db/local.d/02-oneswitch-wallpaper <<DCONF_DB");
        sb.AppendLine("[org/gnome/desktop/background]");
        sb.AppendLine("picture-uri='file://$WALLPAPER_PATH'");
        sb.AppendLine("picture-uri-dark='file://$WALLPAPER_PATH'");
        sb.AppendLine("DCONF_DB");
        sb.AppendLine("fi");
        sb.AppendLine();
        sb.AppendLine("if [ \"$DE\" = \"gnome\" ] && command -v dconf >/dev/null 2>&1; then");
        sb.AppendLine("    dconf update || true");
        sb.AppendLine("fi");
        sb.AppendLine();
    }

    private static void AppendRclonePullStub(StringBuilder sb)
    {
        sb.AppendLine("# --- Section e: Rclone /home restore (Architecture.md §5) ---");
        sb.AppendLine("# Not implemented: §5 User Data Staging (capturing the user's Rclone remote config on");
        sb.AppendLine("# the Windows side) doesn't exist yet. Once it does, this section runs something like:");
        sb.AppendLine("#   rclone copy oneswitch-remote: \"$HOME\" --progress");
    }
}
