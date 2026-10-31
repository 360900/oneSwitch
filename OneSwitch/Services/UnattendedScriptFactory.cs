using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using OneSwitch.Models;

namespace OneSwitch.Services;

// Generates the unattended-install configuration for the chosen target distro
// (Architecture.md §7). Pure string generation - no filesystem/process access, so
// each builder can be unit-tested or hand-inspected without a real Linux box.
//
// All three supported builders (Debian, Fedora, Arch) share the same first-boot
// delivery mechanism: oneswitch.sh is base64-embedded into the target filesystem, along
// with a self-disabling systemd oneshot unit that runs it on the first boot. Base64
// is used specifically because oneswitch.sh itself contains heredocs (the dconf blocks),
// and a heredoc-in-heredoc approach would need fragile unique-marker bookkeeping across
// three different outer syntaxes (preseed in-target / kickstart %post / archinstall
// JSON string array). Base64's alphabet (A-Za-z0-9+/=) needs no escaping in any of
// them, so the same per-distro command list works unchanged.
//
// Bazzite and Mint return Content=null with an explicit UnsupportedReason - same
// "documented gap" honesty pattern Architecture.md already uses for ProtonDB, Rclone,
// and wallpaper staging. The first-boot payload itself still gets written to the
// Desktop for inspection regardless, so the builder below only governs the unattended
// install-config side of the workflow.
public static class UnattendedScriptFactory
{
    public readonly record struct UnattendedInstallScript(
        string FileName,
        string? Content,
        string? UnsupportedReason);

    private const string FirstBootScriptPath = "/usr/local/bin/oneswitch.sh";
    private const string FirstBootUnitName = "oneswitch-first-boot.service";
    private const string FirstBootUnitPath = "/etc/systemd/system/" + FirstBootUnitName;

    public static UnattendedInstallScript Build(MigrationState state, string firstBootPayload) =>
        DistroResolver.ResolveTargetDistro(state) switch
        {
            TargetDistro.Debian => BuildDebianPreseed(firstBootPayload),
            TargetDistro.Fedora => BuildFedoraKickstart(firstBootPayload),
            TargetDistro.Arch => BuildArchInstallConfig(state, firstBootPayload),
            TargetDistro.Bazzite => new(
                "bazzite.ks",
                null,
                // Bazzite generates its kickstart in its own installer hook with no
                // documented custom %post path; needs real-ISO investigation first.
                "Bazzite doesn't support automated installs yet."),
            TargetDistro.Mint => new(
                "linuxmint-preseed.cfg",
                null,
                // Mint has no first-party unattended install; community preseeds need
                // custom boot media and VM verification before shipping.
                "Linux Mint doesn't support automated installs yet."),
            _ => new("unknown", null, "Unsupported target distro.")
        };

    // Shared shell commands that all three supported builders embed into their
    // respective install-config syntax. Each is a single self-contained line that runs
    // inside the target system (kickstart %post and archinstall custom commands are
    // already chrooted; the preseed wraps each in `in-target sh -c`).
    private static IReadOnlyList<string> BuildFirstBootHookCommands(string firstBootPayload)
    {
        var payloadB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(firstBootPayload));
        var unitB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(BuildSystemdUnitContent()));

        return new[]
        {
            $"echo {payloadB64} | base64 -d > {FirstBootScriptPath}",
            $"echo {unitB64} | base64 -d > {FirstBootUnitPath}",
            $"chmod +x {FirstBootScriptPath}",
            $"systemctl enable {FirstBootUnitName}"
        };
    }

    // Self-disabling oneshot unit: runs oneswitch.sh once, then disables and deletes
    // itself in ExecStartPost. The "-" prefix makes a failing script count as success,
    // otherwise ExecStartPost would be skipped and the unit would re-run every boot.
    private static string BuildSystemdUnitContent() =>
        "[Unit]\n" +
        "Description=oneSwitch first-boot payload\n" +
        "Wants=network-online.target\n" +
        "After=network-online.target\n" +
        "\n" +
        "[Service]\n" +
        "Type=oneshot\n" +
        "TimeoutStartSec=infinity\n" +
        $"ExecStart=-{FirstBootScriptPath}\n" +
        $"ExecStartPost=/usr/bin/systemctl disable {FirstBootUnitName}\n" +
        $"ExecStartPost=/usr/bin/rm -f {FirstBootUnitPath} {FirstBootScriptPath}\n" +
        "\n" +
        "[Install]\n" +
        "WantedBy=multi-user.target\n";

    private static UnattendedInstallScript BuildDebianPreseed(string firstBootPayload)
    {
        var hookCommands = BuildFirstBootHookCommands(firstBootPayload);
        var lateCommand = string.Join("; \\\n    ", hookCommands.Select(c => $"in-target sh -c \"{c}\""));

        var sb = new StringBuilder();
        sb.AppendLine("# oneSwitch Debian preseed (Architecture.md §7).");
        sb.AppendLine("# Locale/keyboard/timezone are placeholders pending a real detection step");
        sb.AppendLine("# (same documented-seam pattern as the wallpaper/rclone stubs in oneswitch.sh).");
        sb.AppendLine("d-i debian-installer/locale string en_US.UTF-8");
        sb.AppendLine("d-i keyboard-configuration/xkb-keymap select us");
        sb.AppendLine("d-i clock-setup/utc boolean true");
        sb.AppendLine("d-i time/zone string UTC");
        sb.AppendLine();
        sb.AppendLine("d-i netcfg/choose_interface select auto");
        sb.AppendLine("d-i netcfg/get_hostname string oneswitch");
        sb.AppendLine("d-i netcfg/get_domain string local");
        sb.AppendLine();
        sb.AppendLine("d-i mirror/country string manual");
        sb.AppendLine("d-i mirror/http/hostname string deb.debian.org");
        sb.AppendLine("d-i mirror/http/directory string /debian");
        sb.AppendLine("d-i mirror/http/proxy string");
        sb.AppendLine();
        sb.AppendLine("# Whole-disk guided partitioning - consistent with the rest of the tool's");
        sb.AppendLine("# 'replace Windows' premise. The user can still interrupt at the partman");
        sb.AppendLine("# confirmation step if they want a custom layout.");
        sb.AppendLine("d-i partman-auto/method string regular");
        sb.AppendLine("d-i partman-auto/choose_recipe select atomic");
        sb.AppendLine("d-i partman-partitioning/confirm_write_new_label boolean true");
        sb.AppendLine("d-i partman/choose_partition select finish");
        sb.AppendLine("d-i partman/confirm boolean true");
        sb.AppendLine("d-i partman/confirm_nooverwrite boolean true");
        sb.AppendLine("d-i partman-lvm/device_remove_lvm boolean true");
        sb.AppendLine("d-i partman-md/device_remove_md boolean true");
        sb.AppendLine();
        sb.AppendLine("tasksel tasksel/first multiselect standard, gnome-desktop");
        sb.AppendLine("d-i pkgsel/upgrade select full-upgrade");
        sb.AppendLine("popularity-contest popularity-contest/participate boolean false");
        sb.AppendLine("d-i grub-installer/only_debian boolean true");
        sb.AppendLine("d-i grub-installer/bootdev string default");
        sb.AppendLine();
        sb.AppendLine("# Account creation deliberately omitted. Debian's installer falls back to its");
        sb.AppendLine("# normal interactive 'set up users' screen when passwd/root-password are absent,");
        sb.AppendLine("# which is the same screen a manual install would show. Shipping a hardcoded");
        sb.AppendLine("# placeholder account would be a default-credential smell for a tool that is");
        sb.AppendLine("# explicitly meant to run unattended and be walked away from.");
        sb.AppendLine();
        sb.AppendLine("# oneSwitch first-boot hook: stage oneswitch.sh + self-disabling systemd oneshot unit,");
        sb.AppendLine("# then enable the unit. Runs once on the first real boot after install.");
        sb.Append($"d-i preseed/late_command string {lateCommand}");
        sb.AppendLine();

        // StringBuilder.AppendLine uses Environment.NewLine (CRLF on Windows), but the
        // target Linux installers handle either. Normalize to LF for consistency with
        // FirstBootPayloadBuilder (which must be LF because bash parses it on boot).
        return new("oneswitch-preseed.cfg", sb.ToString().Replace("\r\n", "\n"), null);
    }

    private static UnattendedInstallScript BuildFedoraKickstart(string firstBootPayload)
    {
        var hookCommands = BuildFirstBootHookCommands(firstBootPayload);
        var postBlock = string.Join("\n", hookCommands);

        var sb = new StringBuilder();
        sb.AppendLine("# oneSwitch Fedora kickstart (Architecture.md §7).");
        sb.AppendLine("# Locale/keyboard/timezone are placeholders pending a real detection step.");
        sb.AppendLine("lang en_US.UTF-8");
        sb.AppendLine("keyboard us");
        sb.AppendLine("timezone --utc UTC");
        sb.AppendLine();
        sb.AppendLine("# Whole-disk wipe + autopart - consistent with the rest of the tool's");
        sb.AppendLine("# 'replace Windows' premise. The user can still interrupt at the storage");
        sb.AppendLine("# configuration step if they want a custom layout.");
        sb.AppendLine("zerombr");
        sb.AppendLine("clearpart --all --initlabel");
        sb.AppendLine("autopart");
        sb.AppendLine();
        sb.AppendLine("network --bootproto=dhcp");
        sb.AppendLine();
        sb.AppendLine("# Account creation deliberately omitted. Fedora falls back to initial-setup");
        sb.AppendLine("# at first real boot when rootpw/user are absent - same screen a manual");
        sb.AppendLine("# install would show. No hardcoded placeholder account.");
        sb.AppendLine();
        sb.AppendLine("%packages");
        sb.AppendLine("@^gnome-desktop-environment");
        sb.AppendLine("%end");
        sb.AppendLine();
        sb.AppendLine("# oneSwitch first-boot hook: stage oneswitch.sh + self-disabling systemd oneshot unit,");
        sb.AppendLine("# then enable the unit. Runs once on the first real boot after install.");
        sb.AppendLine("%post");
        sb.AppendLine(postBlock);
        sb.AppendLine("%end");

        // StringBuilder.AppendLine uses Environment.NewLine (CRLF on Windows), but the
        // target Linux installers handle either. Normalize to LF for consistency with
        // FirstBootPayloadBuilder (which must be LF because bash parses it on boot).
        return new("oneswitch.ks", sb.ToString().Replace("\r\n", "\n"), null);
    }

    // archinstall (4.x) schema, verified against archinstall/lib/args.py. disk_config and
    // users are deliberately omitted: a silent disk layout needs the exact device path and
    // byte-exact partition sizes, which can't be known on the Windows side. The config is
    // meant for `archinstall --config user_configuration.json`, which preloads everything
    // below so the user only picks the disk and creates an account before installing.
    private static UnattendedInstallScript BuildArchInstallConfig(MigrationState state, string firstBootPayload)
    {
        var de = DistroResolver.ResolveDesktopEnvironment(state);
        var isKde = de == DesktopEnvironment.Kde;

        var config = new Dictionary<string, object?>
        {
            ["archinstall-language"] = "English",
            ["hostname"] = "oneswitch",
            ["timezone"] = "UTC",
            ["ntp"] = true,
            ["kernels"] = new[] { "linux" },
            ["locale_config"] = new Dictionary<string, string>
            {
                ["kb_layout"] = "us",
                ["sys_lang"] = "en_US.UTF-8",
                ["sys_enc"] = "UTF-8",
            },
            ["bootloader_config"] = new Dictionary<string, object>
            {
                ["bootloader"] = "Grub",
                ["uki"] = false,
                ["removable"] = false,
            },
            ["network_config"] = new Dictionary<string, string> { ["type"] = "nm" },
            ["profile_config"] = new Dictionary<string, object>
            {
                ["profile"] = new Dictionary<string, object>
                {
                    ["main"] = "Desktop",
                    ["details"] = new[] { isKde ? "KDE Plasma" : "GNOME" },
                },
                ["greeter"] = isKde ? "sddm" : "gdm",
                ["gfx_driver"] = state.DetectedGpu == GpuManufacturer.Nvidia
                    ? "Nvidia (open kernel module for newer GPUs, Turing+)"
                    : "All open-source",
            },
            ["app_config"] = new Dictionary<string, object>
            {
                ["audio_config"] = new Dictionary<string, string> { ["audio"] = "pipewire" },
            },
            ["custom_commands"] = BuildFirstBootHookCommands(firstBootPayload),
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            NewLine = "\n",
        };

        return new(
            "user_configuration.json",
            JsonSerializer.Serialize(config, options),
            null);
    }
}
