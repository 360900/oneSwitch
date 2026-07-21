using System.Collections.Generic;
using System.Linq;
using System.Text;
using AvaloniaApp.Models;

namespace AvaloniaApp.Services;

// Generates kargo.sh, the first-boot payload script (Architecture.md §7) that runs once
// on the freshly-installed Linux system. Pure string generation - no filesystem/process
// access here, so the output can be inspected/tested without needing a real Linux box.
//
// Every install line is best-effort (|| true / command -v guards): a missing vendor repo
// or AUR helper should degrade to a skipped app, never abort the rest of the script.
public static class FirstBootPayloadBuilder
{
    // Verified against the live extension.gnome.org listing - note the dot before "gmail",
    // easy to mistype as one word.
    private const string DashToDockUuid = "dash-to-dock@micxgx.gmail.com";

    public static string Build(MigrationState state, IReadOnlyList<AppTranslationEntry> entries)
    {
        var preferred = DistroResolver.ResolvePackageManager(state);
        var targetDistro = DistroResolver.ResolveTargetDistro(state);
        var paradigm = state.SelectedParadigm == DesktopParadigm.MacOS ? "macos" : "windows";

        var sb = new StringBuilder();
        AppendPreamble(sb, paradigm);
        AppendAppInstalls(sb, entries, preferred);
        AppendDockProvisioning(sb, state, targetDistro);
        AppendWallpaperApply(sb);
        AppendRclonePullStub(sb);
        // Generator uses sb.AppendLine throughout, which produces Environment.NewLine
        // (CRLF on Windows). The script is a Unix shell script and the target Linux
        // system's bash can't parse the resulting CRLF (fails at the first elif),
        // so normalize to LF at the boundary. Same string is also what gets
        // base64-embedded into the unattended install-config by the factory.
        return sb.ToString().Replace("\r\n", "\n");
    }

    private static void AppendPreamble(StringBuilder sb, string paradigm)
    {
        sb.AppendLine("#!/usr/bin/env bash");
        sb.AppendLine("# Kargo first-boot payload (Architecture.md §7) - runs once on the freshly-installed system.");
        sb.AppendLine("set -uo pipefail");
        sb.AppendLine();
        sb.AppendLine("# --- Section a: environment detection ---");
        sb.AppendLine("# KARGO_PARADIGM is normally injected by the unattended-install script factory that");
        sb.AppendLine("# invokes this file, carrying forward the Slide 1 desktop-paradigm choice. The value");
        sb.AppendLine("# baked in below is this install's actual choice, used only if nothing injects it.");
        sb.AppendLine($"KARGO_PARADIGM=\"${{KARGO_PARADIGM:-{paradigm}}}\"");
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
        sb.AppendLine("echo \"Kargo first-boot payload starting (paradigm=$KARGO_PARADIGM distro=$DISTRO_ID pkg=$PKG_MANAGER de=$DE)\"");
        sb.AppendLine();
    }

    private static void AppendAppInstalls(StringBuilder sb, IReadOnlyList<AppTranslationEntry> entries, PackageManagerFamily preferred)
    {
        sb.AppendLine("# --- Section b: install the translated application array (Architecture.md §7) ---");
        sb.AppendLine("# Known gaps: Brave, Docker, 1Password, VS Code, Sublime Text, and Mullvad need a");
        sb.AppendLine("# vendor apt/dnf repo added before these installs will find the package - not done");
        sb.AppendLine("# here, so those specific lines will silently no-op until that repo setup exists.");
        sb.AppendLine("# AUR packages that haven't graduated to Arch's official repo need a helper (yay/paru);");
        sb.AppendLine("# if neither is installed, that line just prints a skip message instead of failing.");
        sb.AppendLine();
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
            return $"echo 'Skipping {entry.TargetApp}: no package available for any manager'";

        return family switch
        {
            PackageManagerFamily.Apt =>
                $"[ \"$PKG_MANAGER\" = \"apt\" ] && (apt-get install -y {id} || true)  # {entry.TargetApp}",
            PackageManagerFamily.Dnf =>
                $"[ \"$PKG_MANAGER\" = \"dnf\" ] && (dnf install -y {id} || true)  # {entry.TargetApp}",
            PackageManagerFamily.Pacman => entry.AurIsOfficialRepo
                ? $"[ \"$PKG_MANAGER\" = \"pacman\" ] && (pacman -S --noconfirm {id} || true)  # {entry.TargetApp}"
                : $"[ \"$PKG_MANAGER\" = \"pacman\" ] && (command -v yay >/dev/null 2>&1 && yay -S --noconfirm {id} || command -v paru >/dev/null 2>&1 && paru -S --noconfirm {id} || echo 'Skipping {id} ({entry.TargetApp}): needs an AUR helper (yay/paru)')  # {entry.TargetApp}",
            _ =>
                $"command -v flatpak >/dev/null 2>&1 && (flatpak install -y flathub {id} || true)  # {entry.TargetApp}"
        };
    }

    private static void AppendDockProvisioning(StringBuilder sb, MigrationState state, TargetDistro targetDistro)
    {
        sb.AppendLine("# --- Section c: dock provisioning (Architecture.md §3) ---");

        var isMacParadigm = state.SelectedParadigm == DesktopParadigm.MacOS;
        var de = DistroResolver.ResolveDesktopEnvironment(state);

        if (!isMacParadigm || de != DesktopEnvironment.Gnome)
        {
            sb.AppendLine("# Not applicable to this install (the macOS-paradigm centered dock only applies to");
            sb.AppendLine("# the GNOME pick - Windows-paradigm Arch/Bazzite resolve to KDE, Fedora/Debian/Mint");
            sb.AppendLine("# are not GNOME under Windows-paradigm, Bazzite is unsupported by the script factory) -");
            sb.AppendLine("# block omitted.");
            sb.AppendLine();
            return;
        }

        var distroId = targetDistro switch
        {
            TargetDistro.Fedora => "fedora",
            TargetDistro.Debian => "debian",
            TargetDistro.Arch => "arch",
            _ => null
        };

        if (distroId is null)
        {
            sb.AppendLine("# Resolved DE is GNOME but the target distro is neither Fedora, Debian, nor Arch -");
            sb.AppendLine("# block omitted.");
            sb.AppendLine();
            return;
        }

        var installCmd = targetDistro switch
        {
            TargetDistro.Fedora => "    dnf install -y gnome-shell-extension-dash-to-dock || true",
            TargetDistro.Debian => "    apt-get install -y gnome-shell-extension-dashtodock || true",
            // Arch package name is flagged as needing real-ISO VM verification - the extra repo
            // has carried dash-to-dock under this name but that's a per-distro-version question.
            TargetDistro.Arch => "    pacman -S --noconfirm gnome-shell-extension-dash-to-dock || true",
            _ => null
        };

        sb.AppendLine($"if [ \"$DE\" = \"gnome\" ] && [ \"$DISTRO_ID\" = \"{distroId}\" ]; then");
        sb.AppendLine(installCmd);
        sb.AppendLine();
        sb.AppendLine("    mkdir -p /etc/dconf/profile");
        sb.AppendLine("    cat > /etc/dconf/profile/user <<'DCONF_PROFILE'");
        sb.AppendLine("user-db:user");
        sb.AppendLine("system-db:local");
        sb.AppendLine("file-db:/usr/share/glib-2.0/schemas");
        sb.AppendLine("DCONF_PROFILE");
        sb.AppendLine();
        sb.AppendLine("    mkdir -p /etc/dconf/db/local.d");
        sb.AppendLine("    cat > /etc/dconf/db/local.d/01-kargo-dock <<DCONF_DB");
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
        sb.AppendLine();
        sb.AppendLine("    dconf update || true");
        sb.AppendLine("    # No locks written here, so all of this stays user-overridable afterward.");
        sb.AppendLine("fi");
        sb.AppendLine();
    }

    private static void AppendWallpaperApply(StringBuilder sb)
    {
        sb.AppendLine("# --- Section d: wallpaper apply (Architecture.md §5) ---");
        sb.AppendLine("# The wallpaper file itself is staged by §5/§6, neither implemented yet - this section");
        sb.AppendLine("# is a documented seam: it no-ops until that file actually exists at boot time.");
        sb.AppendLine("WALLPAPER_PATH=\"/usr/share/backgrounds/kargo-wallpaper.jpg\"");
        sb.AppendLine("if [ -f \"$WALLPAPER_PATH\" ]; then");
        sb.AppendLine("    if [ \"$DE\" = \"gnome\" ]; then");
        sb.AppendLine("        gsettings set org.gnome.desktop.background picture-uri \"file://$WALLPAPER_PATH\" || true");
        sb.AppendLine("        gsettings set org.gnome.desktop.background picture-uri-dark \"file://$WALLPAPER_PATH\" || true");
        sb.AppendLine("    elif [ \"$DE\" = \"kde\" ]; then");
        sb.AppendLine("        plasma-apply-wallpaperimage \"$WALLPAPER_PATH\" || true");
        sb.AppendLine("    fi");
        sb.AppendLine("fi");
        sb.AppendLine();
    }

    private static void AppendRclonePullStub(StringBuilder sb)
    {
        sb.AppendLine("# --- Section e: Rclone /home restore (Architecture.md §5) ---");
        sb.AppendLine("# Not implemented: §5 User Data Staging (capturing the user's Rclone remote config on");
        sb.AppendLine("# the Windows side) doesn't exist yet. Once it does, this section runs something like:");
        sb.AppendLine("#   rclone copy kargo-remote: \"$HOME\" --progress");
        sb.AppendLine("# No remote config is generated today, so intentionally left as a stub.");
    }
}
