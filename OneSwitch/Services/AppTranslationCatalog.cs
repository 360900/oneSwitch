using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using OneSwitch.Models;

namespace OneSwitch.Services;

// Curated Windows-app -> Linux-equivalent mapping table. This part stays hand-picked
// (it's recommendation, not detection) - what's real is which of these entries actually
// apply, filtered against InstalledProgramScanner's real registry scan, and which package
// manager column gets shown, resolved against the user's actual chosen distro.
//
// A few source apps from the original curation list are deliberately absent: Notepad, MS
// Paint, Snipping Tool, and Task Manager ship built into Windows and never register an
// Uninstall registry entry, so they could never be detected here. Riot Client's only
// "translation" is an anti-cheat incompatibility warning, not a real target app, so it
// doesn't fit this model - that's Game Compatibility modal territory. Word/Excel/PowerPoint
// are folded into one "MS Office" entry since a real Office install registers as a single
// suite-wide Uninstall entry, not three separate ones. Skype is absent too: Microsoft
// retired the service in May 2025, so there's no backend left to recommend a client for.
//
// Every package identifier below was individually verified against Flathub, AUR,
// packages.debian.org, and packages.fedoraproject.org (not left as originally guessed).
// Fields are null where no real package or repo exists for that manager - Arch policy in
// particular blocks AUR duplicates of anything that's graduated to the official extra/
// multilib repo, so a lot of "AUR" assumptions here turned out to actually be plain
// `pacman -S` (see AurIsOfficialRepo on AppTranslationEntry). Where a manager has no
// package at all (author only ships GitHub-release .deb/.rpm downloads, or a Flatpak got
// delisted), that field is left null rather than a name that would 404 - ResolvePackageDisplay
// falls back to whichever manager is actually real for that app. A separate, common case is
// a package name that's correct but needs a vendor repo added first (e.g. Brave, Docker,
// 1Password, Mullvad, VS Code, Sublime Text) - those are left as-is since the name itself is
// right; only genuinely nonexistent packages were nulled out.
//
// Store-app (AppX) detection is opt-in via AppxPackageFamilyPrefixes, not inferred from
// SourceApp/DetectionAliases: package identifiers glue words together with no separator
// (e.g. "MSTeams_...", "SpotifyAB.SpotifyMusic_..."), so an unrelated app's identifier can
// coincidentally contain another app's name ("MSTeams" contains "Steam", which used to
// make the Steam entry false-positive on a Microsoft Teams Store install). Only entries
// with a real, confirmed Store package declare a prefix.
//
// Every catalog entry was checked against the live Microsoft Store catalog API for a
// genuine MSIX distribution (not just a Store *listing* - many apps have a Store page that
// silently runs the vendor's ordinary .exe/.msi installer with no real package identity, so
// they're indistinguishable from a direct download and can't be detected this way; those
// were deliberately left without a prefix). Entries with no prefix either have no Store
// presence at all, or only a Store wrapper around the classic installer.
public class AppTranslationCatalog
{
    public IReadOnlyList<AppTranslationEntry> GetSuggestions(IReadOnlyList<DetectedProgram> installedPrograms)
    {
        var displayNames = installedPrograms.Where(p => !p.IsCompoundIdentifier).ToList();
        var appxPackages = installedPrograms.Where(p => p.IsCompoundIdentifier).ToList();

        return AllEntries().Where(entry =>
        {
            var aliases = entry.DetectionAliases.Length > 0 ? entry.DetectionAliases : new[] { entry.SourceApp };
            if (aliases.Any(alias => displayNames.Any(installed => MatchesDisplayName(installed.Name, alias))))
                return true;

            return entry.AppxPackageFamilyPrefixes.Any(prefix =>
                appxPackages.Any(installed => installed.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
        }).ToList();
    }

    // Real display names (from Uninstall keys, App Paths, or Start Menu shortcuts) need
    // word-boundary matching, not raw substring - otherwise SourceApp "Steam"
    // false-positives on "Steamworks Common Redistributables", a component many
    // non-Steam games install for achievements/multiplayer.
    private static bool MatchesDisplayName(string name, string alias)
    {
        var index = name.IndexOf(alias, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            var beforeOk = index == 0 || !char.IsLetterOrDigit(name[index - 1]);
            var afterIndex = index + alias.Length;
            var afterOk = afterIndex >= name.Length || !char.IsLetterOrDigit(name[afterIndex]);
            if (beforeOk && afterOk) return true;

            index = name.IndexOf(alias, index + 1, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private static IReadOnlyList<AppTranslationEntry> AllEntries() => new List<AppTranslationEntry>
    {
        // --- Web Browsers ---
        new()
        {
            SourceApp = "Google Chrome", SourceLogoGlyph = "C", SourceLogoColor = Brush.Parse("#4285F4"),
            TargetApp = "Google Chrome", TargetLogoGlyph = "C", TargetLogoColor = Brush.Parse("#4285F4"),
            Explanation = "The real Google Chrome for Linux, so your existing Google sync (bookmarks, passwords, extensions) carries over by just signing in.",
            AptPackage = "google-chrome-stable", DnfPackage = "google-chrome-stable", AurPackage = "google-chrome",
            FlatpakId = "com.google.Chrome", IsBrowser = true
        },
        new()
        {
            SourceApp = "Microsoft Edge", SourceLogoGlyph = "E", SourceLogoColor = Brush.Parse("#0078D7"),
            TargetApp = "Vivaldi", TargetLogoGlyph = "V", TargetLogoColor = Brush.Parse("#EF3939"),
            Explanation = "Closest match to Edge's sidebar panels and tab stacking, without Edge's Microsoft account ties and telemetry.",
            AptPackage = "vivaldi-stable", DnfPackage = "vivaldi-stable", AurPackage = "vivaldi", AurIsOfficialRepo = true,
            FlatpakId = "com.vivaldi.Vivaldi", IsBrowser = true
        },
        new()
        {
            SourceApp = "Opera", SourceLogoGlyph = "O", SourceLogoColor = Brush.Parse("#FF1B2D"),
            TargetApp = "Opera", TargetLogoGlyph = "O", TargetLogoColor = Brush.Parse("#FF1B2D"),
            Explanation = "Opera ships an official Linux client.",
            AptPackage = "opera-stable", DnfPackage = "opera-stable", AurPackage = "opera", FlatpakId = "com.opera.Opera",
            IsBrowser = true
        },
        new()
        {
            SourceApp = "Vivaldi", SourceLogoGlyph = "V", SourceLogoColor = Brush.Parse("#EF3939"),
            TargetApp = "Vivaldi", TargetLogoGlyph = "V", TargetLogoColor = Brush.Parse("#EF3939"),
            Explanation = "Vivaldi ships an official Linux client.",
            AptPackage = "vivaldi-stable", DnfPackage = "vivaldi-stable", AurPackage = "vivaldi", AurIsOfficialRepo = true,
            FlatpakId = "com.vivaldi.Vivaldi", IsBrowser = true
        },
        new()
        {
            SourceApp = "Mozilla Firefox", SourceLogoGlyph = "F", SourceLogoColor = Brush.Parse("#FF7139"),
            TargetApp = "Firefox", TargetLogoGlyph = "F", TargetLogoColor = Brush.Parse("#FF7139"),
            Explanation = "Ships preinstalled on most distros already.",
            AptPackage = "firefox", DnfPackage = "firefox", AurPackage = "firefox", AurIsOfficialRepo = true,
            FlatpakId = "org.mozilla.firefox", IsBrowser = true,
            AppxPackageFamilyPrefixes = new[] { "Mozilla.Firefox" }
        },
        new()
        {
            SourceApp = "Brave Browser", SourceLogoGlyph = "B", SourceLogoColor = Brush.Parse("#FB542B"),
            TargetApp = "Brave Browser", TargetLogoGlyph = "B", TargetLogoColor = Brush.Parse("#FB542B"),
            Explanation = "Brave ships an official Linux client.",
            AptPackage = "brave-browser", DnfPackage = "brave-browser", AurPackage = "brave-bin", FlatpakId = "com.brave.Browser",
            IsBrowser = true
        },
        new()
        {
            SourceApp = "Tor Browser", SourceLogoGlyph = "T", SourceLogoColor = Brush.Parse("#7D4698"),
            TargetApp = "Tor Browser Launcher", TargetLogoGlyph = "T", TargetLogoColor = Brush.Parse("#7D4698"),
            Explanation = "Downloads and manages the real Tor Browser bundle.",
            AptPackage = "torbrowser-launcher", DnfPackage = "torbrowser-launcher", AurPackage = "torbrowser-launcher",
            AurIsOfficialRepo = true, FlatpakId = "org.torproject.torbrowser-launcher", IsBrowser = true
        },

        // --- Office & Productivity ---
        new()
        {
            SourceApp = "MS Office", SourceLogoGlyph = "W", SourceLogoColor = Brush.Parse("#0078D7"),
            TargetApp = "OnlyOffice", TargetLogoGlyph = "O", TargetLogoColor = Brush.Parse("#D83B01"),
            Explanation = "Highly compatible alternative that opens .docx/.xlsx/.pptx without conversion issues.",
            AptPackage = "onlyoffice-desktopeditors", DnfPackage = "onlyoffice-desktopeditors", AurPackage = "onlyoffice-bin",
            FlatpakId = "org.onlyoffice.desktopeditors",
            DetectionAliases = new[] { "Microsoft Office", "Microsoft 365" },
            // The full desktop Office suite installed from the Microsoft Store ("Office
            // Centennial") ships as this MSIX package family.
            AppxPackageFamilyPrefixes = new[] { "Microsoft.Office.Desktop" }
        },
        new()
        {
            SourceApp = "Notepad++", SourceLogoGlyph = "N+", SourceLogoColor = Brush.Parse("#90E59A"),
            TargetApp = "Notepadqq", TargetLogoGlyph = "Nq", TargetLogoColor = Brush.Parse("#4FC3A1"),
            Explanation = "Closest match for lightweight text/code editing with a similar UI.",
            AptPackage = "notepadqq", AurPackage = "notepadqq", FlatpakId = "com.notepadqq.Notepadqq"
        },
        new()
        {
            SourceApp = "Adobe Acrobat Reader", SourceLogoGlyph = "A", SourceLogoColor = Brush.Parse("#DC3E28"),
            TargetApp = "Okular", TargetLogoGlyph = "O", TargetLogoColor = Brush.Parse("#1D99F3"),
            Explanation = "Handles PDF annotation and forms out of the box.",
            AptPackage = "okular", DnfPackage = "okular", AurPackage = "okular", AurIsOfficialRepo = true, FlatpakId = "org.kde.okular"
        },
        new()
        {
            SourceApp = "Foxit Reader", SourceLogoGlyph = "F", SourceLogoColor = Brush.Parse("#FF6A00"),
            TargetApp = "Evince", TargetLogoGlyph = "E", TargetLogoColor = Brush.Parse("#5E5C64"),
            Explanation = "Lightweight document viewer, GNOME's default PDF reader.",
            AptPackage = "evince", DnfPackage = "evince", AurPackage = "evince", AurIsOfficialRepo = true, FlatpakId = "org.gnome.Evince"
        },
        new()
        {
            SourceApp = "Evernote", SourceLogoGlyph = "E", SourceLogoColor = Brush.Parse("#2DBE60"),
            TargetApp = "Joplin", TargetLogoGlyph = "J", TargetLogoColor = Brush.Parse("#1071D3"),
            Explanation = "Notebook/tag structure carries over closely, plus end-to-end encrypted sync.",
            AurPackage = "joplin-appimage", FlatpakId = "net.cozic.joplin_desktop",
            AppxPackageFamilyPrefixes = new[] { "Evernote.Evernote" }
        },
        new()
        {
            SourceApp = "Notion", SourceLogoGlyph = "N", SourceLogoColor = Brush.Parse("#000000"),
            TargetApp = "Obsidian", TargetLogoGlyph = "O", TargetLogoColor = Brush.Parse("#7C3AED"),
            Explanation = "Local-first, markdown-based notes with a plugin ecosystem covering most Notion workflows.",
            AurPackage = "obsidian", AurIsOfficialRepo = true, FlatpakId = "md.obsidian.Obsidian"
        },
        new()
        {
            SourceApp = "OneNote", SourceLogoGlyph = "On", SourceLogoColor = Brush.Parse("#7719AA"),
            TargetApp = "Xournal++", TargetLogoGlyph = "X", TargetLogoColor = Brush.Parse("#5A8F3C"),
            Explanation = "Free-form notebook with handwriting/annotation support.",
            AptPackage = "xournalpp", DnfPackage = "xournalpp", AurPackage = "xournalpp", AurIsOfficialRepo = true,
            FlatpakId = "com.github.xournalpp.xournalpp",
            // The legacy "OneNote for Windows 10" UWP app - still a genuine identity
            // present on many machines, though new Store installs redirect to Office
            // Click-to-Run instead and won't produce this package.
            AppxPackageFamilyPrefixes = new[] { "Microsoft.Office.OneNote" }
        },
        new()
        {
            SourceApp = "Obsidian", SourceLogoGlyph = "O", SourceLogoColor = Brush.Parse("#7C3AED"),
            TargetApp = "Obsidian", TargetLogoGlyph = "O", TargetLogoColor = Brush.Parse("#7C3AED"),
            Explanation = "Obsidian ships an official Linux client.",
            AurPackage = "obsidian", AurIsOfficialRepo = true, FlatpakId = "md.obsidian.Obsidian"
        },
        new()
        {
            SourceApp = "Sumatra PDF", SourceLogoGlyph = "S", SourceLogoColor = Brush.Parse("#0000AA"),
            TargetApp = "Zathura", TargetLogoGlyph = "Z", TargetLogoColor = Brush.Parse("#2E3436"),
            Explanation = "Minimal, keyboard-driven document viewer.",
            AptPackage = "zathura", DnfPackage = "zathura", AurPackage = "zathura", AurIsOfficialRepo = true, FlatpakId = "org.pwmt.zathura"
        },

        // --- Communication ---
        new()
        {
            SourceApp = "Discord", SourceLogoGlyph = "D", SourceLogoColor = Brush.Parse("#5865F2"),
            TargetApp = "Vesktop", TargetLogoGlyph = "V", TargetLogoColor = Brush.Parse("#6F76F5"),
            Explanation = "The standard Discord client on Linux: lighter than official Discord, supports Vencord plugins.",
            AurPackage = "vesktop-bin", FlatpakId = "dev.vencord.Vesktop"
        },
        new()
        {
            SourceApp = "Zoom", SourceLogoGlyph = "Z", SourceLogoColor = Brush.Parse("#2D8CFF"),
            TargetApp = "Zoom", TargetLogoGlyph = "Z", TargetLogoColor = Brush.Parse("#2D8CFF"),
            Explanation = "Zoom ships an official Linux client.",
            AptPackage = "zoom", DnfPackage = "zoom", AurPackage = "zoom", FlatpakId = "us.zoom.Zoom"
        },
        new()
        {
            SourceApp = "Slack", SourceLogoGlyph = "S", SourceLogoColor = Brush.Parse("#4A154B"),
            TargetApp = "Slack", TargetLogoGlyph = "S", TargetLogoColor = Brush.Parse("#4A154B"),
            Explanation = "Slack ships an official Linux client.",
            AurPackage = "slack-desktop", FlatpakId = "com.slack.Slack",
            AppxPackageFamilyPrefixes = new[] { "91750D7E.Slack" }
        },
        new()
        {
            SourceApp = "Microsoft Teams", SourceLogoGlyph = "T", SourceLogoColor = Brush.Parse("#6264A7"),
            TargetApp = "Teams for Linux", TargetLogoGlyph = "T", TargetLogoColor = Brush.Parse("#6264A7"),
            Explanation = "Unofficial but actively maintained wrapper, since Microsoft dropped the native Linux client.",
            AptPackage = "teams-for-linux", DnfPackage = "teams-for-linux", AurPackage = "teams-for-linux",
            FlatpakId = "com.github.IsmaelMartinez.teams_for_linux",
            // The current Teams client (bundled with Windows 11) ships as an MSIX Store
            // package under this family name rather than a classic Win32 installer.
            AppxPackageFamilyPrefixes = new[] { "MSTeams" }
        },
        new()
        {
            SourceApp = "Telegram", SourceLogoGlyph = "T", SourceLogoColor = Brush.Parse("#26A5E4"),
            TargetApp = "Telegram Desktop", TargetLogoGlyph = "T", TargetLogoColor = Brush.Parse("#26A5E4"),
            Explanation = "Telegram ships an official Linux client.",
            AptPackage = "telegram-desktop", DnfPackage = "telegram-desktop", AurPackage = "telegram-desktop",
            AurIsOfficialRepo = true, FlatpakId = "org.telegram.desktop",
            AppxPackageFamilyPrefixes = new[] { "TelegramMessengerLLP.TelegramDesktop" }
        },
        new()
        {
            SourceApp = "WhatsApp", SourceLogoGlyph = "W", SourceLogoColor = Brush.Parse("#25D366"),
            TargetApp = "WhatsApp for Linux", TargetLogoGlyph = "W", TargetLogoColor = Brush.Parse("#25D366"),
            Explanation = "Unofficial native wrapper, since WhatsApp has no official Linux client.",
            AurPackage = "whatsapp-for-linux-git",
            // WhatsApp Desktop installed from the Microsoft Store ships as this MSIX package family.
            AppxPackageFamilyPrefixes = new[] { "5319275A.WhatsAppDesktop" }
        },
        new()
        {
            SourceApp = "Signal", SourceLogoGlyph = "S", SourceLogoColor = Brush.Parse("#3A76F0"),
            TargetApp = "Signal Desktop", TargetLogoGlyph = "S", TargetLogoColor = Brush.Parse("#3A76F0"),
            Explanation = "Signal ships an official Linux client.",
            AptPackage = "signal-desktop", AurPackage = "signal-desktop", AurIsOfficialRepo = true, FlatpakId = "org.signal.Signal"
        },
        new()
        {
            SourceApp = "Viber", SourceLogoGlyph = "V", SourceLogoColor = Brush.Parse("#7360F2"),
            TargetApp = "Viber", TargetLogoGlyph = "V", TargetLogoColor = Brush.Parse("#7360F2"),
            Explanation = "Viber ships an official Linux client.",
            AptPackage = "viber", DnfPackage = "viber", AurPackage = "viber", FlatpakId = "com.viber.Viber"
        },
        new()
        {
            SourceApp = "TeamSpeak 3", SourceLogoGlyph = "TS", SourceLogoColor = Brush.Parse("#2580C3"),
            TargetApp = "TeamSpeak 3", TargetLogoGlyph = "TS", TargetLogoColor = Brush.Parse("#2580C3"),
            Explanation = "TeamSpeak ships an official Linux client.",
            AurPackage = "teamspeak3", AurIsOfficialRepo = true, FlatpakId = "com.teamspeak.TeamSpeak",
            DetectionAliases = new[] { "TeamSpeak" }
        },

        // --- Media & Entertainment ---
        new()
        {
            SourceApp = "Spotify", SourceLogoGlyph = "S", SourceLogoColor = Brush.Parse("#1DB954"),
            TargetApp = "Spotify", TargetLogoGlyph = "S", TargetLogoColor = Brush.Parse("#1DB954"),
            Explanation = "Spotify ships an official Linux client.",
            AptPackage = "spotify-client", AurPackage = "spotify", FlatpakId = "com.spotify.Client",
            // Spotify installed from the Microsoft Store ships as this MSIX package family.
            AppxPackageFamilyPrefixes = new[] { "SpotifyAB.SpotifyMusic" }
        },
        new()
        {
            SourceApp = "VLC Media Player", SourceLogoGlyph = "V", SourceLogoColor = Brush.Parse("#FF8800"),
            TargetApp = "VLC", TargetLogoGlyph = "V", TargetLogoColor = Brush.Parse("#FF8800"),
            Explanation = "VLC ships an official Linux client.",
            AptPackage = "vlc", DnfPackage = "vlc", AurPackage = "vlc", AurIsOfficialRepo = true, FlatpakId = "org.videolan.VLC",
            AppxPackageFamilyPrefixes = new[] { "VideoLAN.VLC" }
        },
        new()
        {
            SourceApp = "iTunes", SourceLogoGlyph = "iT", SourceLogoColor = Brush.Parse("#FA243C"),
            TargetApp = "Cider", TargetLogoGlyph = "C", TargetLogoColor = Brush.Parse("#FA243C"),
            Explanation = "Apple Music client with a comparable library/playlist experience.",
            AurPackage = "cider", FlatpakId = "sh.cider.Cider",
            AppxPackageFamilyPrefixes = new[] { "AppleInc.iTunes" }
        },
        new()
        {
            SourceApp = "Winamp", SourceLogoGlyph = "W", SourceLogoColor = Brush.Parse("#B4B4B4"),
            TargetApp = "Audacious", TargetLogoGlyph = "A", TargetLogoColor = Brush.Parse("#C1272D"),
            Explanation = "Lightweight player with Winamp-style skins and equalizer.",
            AptPackage = "audacious", DnfPackage = "audacious", AurPackage = "audacious", AurIsOfficialRepo = true,
            FlatpakId = "org.atheme.audacious"
        },
        new()
        {
            SourceApp = "Foobar2000", SourceLogoGlyph = "F", SourceLogoColor = Brush.Parse("#5B5B5B"),
            TargetApp = "Strawberry", TargetLogoGlyph = "S", TargetLogoColor = Brush.Parse("#2E86AB"),
            Explanation = "Advanced music library manager and player with a similar power-user focus.",
            AptPackage = "strawberry", DnfPackage = "strawberry", AurPackage = "strawberry", AurIsOfficialRepo = true,
            FlatpakId = "org.strawberrymusicplayer.strawberry",
            AppxPackageFamilyPrefixes = new[] { "Resolute.foobar2000" }
        },
        new()
        {
            SourceApp = "MPC-HC", SourceLogoGlyph = "M", SourceLogoColor = Brush.Parse("#3E7EBD"),
            TargetApp = "MPV", TargetLogoGlyph = "mpv", TargetLogoColor = Brush.Parse("#682890"),
            Explanation = "Minimal, fast, keyboard-driven video player.",
            AptPackage = "mpv", DnfPackage = "mpv", AurPackage = "mpv", AurIsOfficialRepo = true, FlatpakId = "io.mpv.Mpv"
        },
        new()
        {
            SourceApp = "Kodi", SourceLogoGlyph = "K", SourceLogoColor = Brush.Parse("#17B2E7"),
            TargetApp = "Kodi", TargetLogoGlyph = "K", TargetLogoColor = Brush.Parse("#17B2E7"),
            Explanation = "Kodi ships an official Linux client.",
            AptPackage = "kodi", DnfPackage = "kodi", AurPackage = "kodi", AurIsOfficialRepo = true, FlatpakId = "tv.kodi.Kodi",
            AppxPackageFamilyPrefixes = new[] { "XBMCFoundation.Kodi" }
        },
        new()
        {
            SourceApp = "Plex Media Player", SourceLogoGlyph = "P", SourceLogoColor = Brush.Parse("#E5A00D"),
            TargetApp = "Plex Desktop", TargetLogoGlyph = "P", TargetLogoColor = Brush.Parse("#E5A00D"),
            Explanation = "Plex ships an official Linux client.",
            AurPackage = "plex-desktop", FlatpakId = "tv.plex.PlexDesktop"
        },
        new()
        {
            SourceApp = "Stremio", SourceLogoGlyph = "S", SourceLogoColor = Brush.Parse("#8054E7"),
            TargetApp = "Stremio", TargetLogoGlyph = "S", TargetLogoColor = Brush.Parse("#8054E7"),
            Explanation = "Stremio ships an official Linux client.",
            AurPackage = "stremio", FlatpakId = "com.stremio.Stremio"
        },

        // --- Gaming & Launchers ---
        new()
        {
            SourceApp = "Steam", SourceLogoGlyph = "S", SourceLogoColor = Brush.Parse("#1B2838"),
            TargetApp = "Steam", TargetLogoGlyph = "S", TargetLogoColor = Brush.Parse("#1B2838"),
            Explanation = "Steam ships an official Linux client with Proton built in.",
            AptPackage = "steam", DnfPackage = "steam", AurPackage = "steam", AurIsOfficialRepo = true,
            FlatpakId = "com.valvesoftware.Steam"
        },
        new()
        {
            SourceApp = "Epic Games Launcher", SourceLogoGlyph = "E", SourceLogoColor = Brush.Parse("#2A2A2A"),
            TargetApp = "Heroic Games Launcher", TargetLogoGlyph = "H", TargetLogoColor = Brush.Parse("#40C4B4"),
            Explanation = "Native Epic + GOG library support with built-in Proton/Wine integration. (Lutris also works, but Heroic is purpose-built for these two stores.)",
            AurPackage = "heroic-games-launcher-bin", FlatpakId = "com.heroicgameslauncher.hgl"
        },
        new()
        {
            SourceApp = "GOG Galaxy", SourceLogoGlyph = "G", SourceLogoColor = Brush.Parse("#8C46FF"),
            TargetApp = "Heroic Games Launcher", TargetLogoGlyph = "H", TargetLogoColor = Brush.Parse("#40C4B4"),
            Explanation = "Native GOG (and Epic) support, no DRM friction.",
            AurPackage = "heroic-games-launcher-bin", FlatpakId = "com.heroicgameslauncher.hgl"
        },
        new()
        {
            SourceApp = "EA App", SourceLogoGlyph = "EA", SourceLogoColor = Brush.Parse("#F34023"),
            TargetApp = "Lutris", TargetLogoGlyph = "L", TargetLogoColor = Brush.Parse("#FF9900"),
            Explanation = "Runs EA titles through Wine with community-maintained install scripts. (Heroic covers Epic/GOG instead.)",
            AptPackage = "lutris", DnfPackage = "lutris", AurPackage = "lutris", AurIsOfficialRepo = true, FlatpakId = "net.lutris.Lutris"
        },
        new()
        {
            SourceApp = "Ubisoft Connect", SourceLogoGlyph = "U", SourceLogoColor = Brush.Parse("#0070FF"),
            TargetApp = "Lutris", TargetLogoGlyph = "L", TargetLogoColor = Brush.Parse("#FF9900"),
            Explanation = "Community install scripts handle Ubisoft Connect and its games.",
            AptPackage = "lutris", DnfPackage = "lutris", AurPackage = "lutris", AurIsOfficialRepo = true, FlatpakId = "net.lutris.Lutris"
        },
        new()
        {
            SourceApp = "Battle.net", SourceLogoGlyph = "B", SourceLogoColor = Brush.Parse("#00AEFF"),
            TargetApp = "Lutris", TargetLogoGlyph = "L", TargetLogoColor = Brush.Parse("#FF9900"),
            Explanation = "Community install scripts handle Battle.net and Blizzard titles.",
            AptPackage = "lutris", DnfPackage = "lutris", AurPackage = "lutris", AurIsOfficialRepo = true, FlatpakId = "net.lutris.Lutris"
        },
        new()
        {
            SourceApp = "Minecraft", SourceLogoGlyph = "M", SourceLogoColor = Brush.Parse("#62B47A"),
            TargetApp = "Prism Launcher", TargetLogoGlyph = "P", TargetLogoColor = Brush.Parse("#5D8A41"),
            Explanation = "Modern, actively maintained Minecraft launcher with mod/modpack support.",
            AptPackage = "prismlauncher", AurPackage = "prismlauncher", AurIsOfficialRepo = true,
            FlatpakId = "org.prismlauncher.PrismLauncher",
            // Minecraft for Windows (Bedrock Edition, Store-installed) ships as this MSIX
            // package family. This won't match the separate Java Edition installer.
            AppxPackageFamilyPrefixes = new[] { "Microsoft.MinecraftUWP" }
        },
        new()
        {
            SourceApp = "Roblox", SourceLogoGlyph = "R", SourceLogoColor = Brush.Parse("#E2231A"),
            TargetApp = "Vinegar", TargetLogoGlyph = "V", TargetLogoColor = Brush.Parse("#00A2FF"),
            Explanation = "Wine wrapper purpose-built for running Roblox on Linux.",
            AurPackage = "vinegar", FlatpakId = "org.vinegarhq.Vinegar",
            AppxPackageFamilyPrefixes = new[] { "ROBLOXCORPORATION.ROBLOX" }
        },
        new()
        {
            SourceApp = "RetroArch", SourceLogoGlyph = "R", SourceLogoColor = Brush.Parse("#000000"),
            TargetApp = "RetroArch", TargetLogoGlyph = "R", TargetLogoColor = Brush.Parse("#000000"),
            Explanation = "RetroArch ships an official Linux client.",
            AptPackage = "retroarch", DnfPackage = "retroarch", AurPackage = "retroarch", AurIsOfficialRepo = true,
            FlatpakId = "org.libretro.RetroArch"
        },
        new()
        {
            SourceApp = "Dolphin Emulator", SourceLogoGlyph = "D", SourceLogoColor = Brush.Parse("#4A90D9"),
            TargetApp = "Dolphin", TargetLogoGlyph = "D", TargetLogoColor = Brush.Parse("#4A90D9"),
            Explanation = "Dolphin ships an official Linux client.",
            AptPackage = "dolphin-emu", DnfPackage = "dolphin-emu", AurPackage = "dolphin-emu", AurIsOfficialRepo = true,
            FlatpakId = "org.DolphinEmu.dolphin-emu"
        },
        new()
        {
            SourceApp = "PCSX2", SourceLogoGlyph = "P", SourceLogoColor = Brush.Parse("#1C3F94"),
            TargetApp = "PCSX2", TargetLogoGlyph = "P", TargetLogoColor = Brush.Parse("#1C3F94"),
            Explanation = "PCSX2 ships an official Linux client.",
            AptPackage = "pcsx2", AurPackage = "pcsx2", FlatpakId = "net.pcsx2.PCSX2"
        },
        new()
        {
            SourceApp = "RPCS3", SourceLogoGlyph = "R", SourceLogoColor = Brush.Parse("#000000"),
            TargetApp = "RPCS3", TargetLogoGlyph = "R", TargetLogoColor = Brush.Parse("#000000"),
            Explanation = "RPCS3 ships an official Linux client.",
            AurPackage = "rpcs3-bin", FlatpakId = "net.rpcs3.RPCS3"
        },

        // --- Creative & Design ---
        new()
        {
            SourceApp = "Adobe Photoshop", SourceLogoGlyph = "Ps", SourceLogoColor = Brush.Parse("#31A8FF"),
            TargetApp = "GIMP", TargetLogoGlyph = "G", TargetLogoColor = Brush.Parse("#5C5543"),
            Explanation = "Free, full-featured raster editor. Apply the open-source PhotoGIMP config overlay for a Photoshop-like layout.",
            AptPackage = "gimp", DnfPackage = "gimp", AurPackage = "gimp", AurIsOfficialRepo = true, FlatpakId = "org.gimp.GIMP"
        },
        new()
        {
            SourceApp = "Adobe Premiere Pro", SourceLogoGlyph = "Pr", SourceLogoColor = Brush.Parse("#9999FF"),
            TargetApp = "DaVinci Resolve", TargetLogoGlyph = "DR", TargetLogoColor = Brush.Parse("#233A51"),
            Explanation = "Professional-grade NLE with color grading tools that go beyond Premiere's. Installer requires a free Blackmagic account.",
            AurPackage = "davinci-resolve"
        },
        new()
        {
            SourceApp = "Sony Vegas Pro", SourceLogoGlyph = "V", SourceLogoColor = Brush.Parse("#00AEEF"),
            TargetApp = "DaVinci Resolve", TargetLogoGlyph = "DR", TargetLogoColor = Brush.Parse("#233A51"),
            Explanation = "Professional-grade NLE, actively developed (Vegas Pro has no Linux build). Installer requires a free Blackmagic account.",
            AurPackage = "davinci-resolve"
        },
        new()
        {
            SourceApp = "Adobe Illustrator", SourceLogoGlyph = "Ai", SourceLogoColor = Brush.Parse("#FF9A00"),
            TargetApp = "Inkscape", TargetLogoGlyph = "I", TargetLogoColor = Brush.Parse("#000000"),
            Explanation = "Vector editor with a comparable pen/path toolset.",
            AptPackage = "inkscape", DnfPackage = "inkscape", AurPackage = "inkscape", AurIsOfficialRepo = true,
            FlatpakId = "org.inkscape.Inkscape"
        },
        new()
        {
            SourceApp = "Adobe Lightroom", SourceLogoGlyph = "Lr", SourceLogoColor = Brush.Parse("#31A8FF"),
            TargetApp = "Darktable", TargetLogoGlyph = "D", TargetLogoColor = Brush.Parse("#EB8C00"),
            Explanation = "Non-destructive RAW workflow with a similar catalog/develop split.",
            AptPackage = "darktable", DnfPackage = "darktable", AurPackage = "darktable", AurIsOfficialRepo = true,
            FlatpakId = "org.darktable.Darktable",
            DetectionAliases = new[] { "Lightroom" }
        },
        new()
        {
            SourceApp = "Adobe After Effects", SourceLogoGlyph = "Ae", SourceLogoColor = Brush.Parse("#9999FF"),
            TargetApp = "Natron", TargetLogoGlyph = "N", TargetLogoColor = Brush.Parse("#000000"),
            Explanation = "Node-based compositor covering most motion-graphics workflows.",
            AurPackage = "natron", FlatpakId = "fr.natron.Natron",
            DetectionAliases = new[] { "After Effects" }
        },
        new()
        {
            SourceApp = "Figma", SourceLogoGlyph = "F", SourceLogoColor = Brush.Parse("#F24E1E"),
            TargetApp = "Figma-Linux", TargetLogoGlyph = "F", TargetLogoColor = Brush.Parse("#F24E1E"),
            Explanation = "Unofficial native wrapper around the real Figma web app, since Figma has no official Linux client.",
            AurPackage = "figma-linux-bin"
        },
        new()
        {
            SourceApp = "Blender", SourceLogoGlyph = "B", SourceLogoColor = Brush.Parse("#F5792A"),
            TargetApp = "Blender", TargetLogoGlyph = "B", TargetLogoColor = Brush.Parse("#F5792A"),
            Explanation = "Blender ships an official Linux client.",
            AptPackage = "blender", DnfPackage = "blender", AurPackage = "blender", AurIsOfficialRepo = true, FlatpakId = "org.blender.Blender",
            AppxPackageFamilyPrefixes = new[] { "BlenderFoundation.Blender" }
        },
        new()
        {
            SourceApp = "AutoCAD", SourceLogoGlyph = "A", SourceLogoColor = Brush.Parse("#E51050"),
            TargetApp = "FreeCAD", TargetLogoGlyph = "F", TargetLogoColor = Brush.Parse("#7A9CC6"),
            Explanation = "Parametric CAD covering most 2D/3D drafting workflows.",
            AptPackage = "freecad", AurPackage = "freecad", AurIsOfficialRepo = true, FlatpakId = "org.freecad.FreeCAD"
        },
        new()
        {
            SourceApp = "FL Studio", SourceLogoGlyph = "FL", SourceLogoColor = Brush.Parse("#FF6600"),
            TargetApp = "FL Studio (via Wine + Yabridge)", TargetLogoGlyph = "FL", TargetLogoColor = Brush.Parse("#FF6600"),
            Explanation = "Runs the real Windows FL Studio under Wine; Yabridge bridges Windows VST plugins so they load natively. Yabridge itself is AUR/build-from-source only.",
            AptPackage = "wine", DnfPackage = "wine", AurPackage = "wine yabridge-bin"
        },
        new()
        {
            SourceApp = "Ableton Live", SourceLogoGlyph = "A", SourceLogoColor = Brush.Parse("#000000"),
            TargetApp = "Bitwig Studio", TargetLogoGlyph = "B", TargetLogoColor = Brush.Parse("#FF5000"),
            Explanation = "Clip-launching DAW with a comparable session-view workflow, native Linux build.",
            AurPackage = "bitwig-studio", FlatpakId = "com.bitwig.BitwigStudio"
        },
        new()
        {
            SourceApp = "Audacity", SourceLogoGlyph = "A", SourceLogoColor = Brush.Parse("#0000CC"),
            TargetApp = "Audacity", TargetLogoGlyph = "A", TargetLogoColor = Brush.Parse("#0000CC"),
            Explanation = "Audacity ships an official Linux client.",
            AptPackage = "audacity", DnfPackage = "audacity", AurPackage = "audacity", AurIsOfficialRepo = true,
            FlatpakId = "org.audacityteam.Audacity"
        },
        new()
        {
            SourceApp = "OBS Studio", SourceLogoGlyph = "O", SourceLogoColor = Brush.Parse("#302E31"),
            TargetApp = "OBS Studio", TargetLogoGlyph = "O", TargetLogoColor = Brush.Parse("#302E31"),
            Explanation = "OBS Studio ships an official Linux client.",
            AptPackage = "obs-studio", DnfPackage = "obs-studio", AurPackage = "obs-studio", AurIsOfficialRepo = true,
            FlatpakId = "com.obsproject.Studio"
        },
        new()
        {
            SourceApp = "Paint.NET", SourceLogoGlyph = "P", SourceLogoColor = Brush.Parse("#2D72D9"),
            TargetApp = "Pinta", TargetLogoGlyph = "P", TargetLogoColor = Brush.Parse("#1F8A70"),
            Explanation = "Closest match for simple layer-based raster editing.",
            DnfPackage = "pinta", AurPackage = "pinta", FlatpakId = "com.github.PintaProject.Pinta",
            AppxPackageFamilyPrefixes = new[] { "dotPDNLLC.paint.net" }
        },
        new()
        {
            SourceApp = "CorelDRAW", SourceLogoGlyph = "C", SourceLogoColor = Brush.Parse("#00A651"),
            TargetApp = "Inkscape", TargetLogoGlyph = "I", TargetLogoColor = Brush.Parse("#000000"),
            Explanation = "Vector editor with a comparable pen/path toolset.",
            AptPackage = "inkscape", DnfPackage = "inkscape", AurPackage = "inkscape", AurIsOfficialRepo = true,
            FlatpakId = "org.inkscape.Inkscape",
            AppxPackageFamilyPrefixes = new[] { "CorelCorporation.CorelDRAW" }
        },
        new()
        {
            SourceApp = "SketchUp", SourceLogoGlyph = "S", SourceLogoColor = Brush.Parse("#005F9E"),
            TargetApp = "Sweet Home 3D", TargetLogoGlyph = "SH", TargetLogoColor = Brush.Parse("#4CAF50"),
            Explanation = "Closest match for simple architectural/interior 3D modeling.",
            AptPackage = "sweethome3d", AurPackage = "sweethome3d", AurIsOfficialRepo = true,
            FlatpakId = "com.sweethome3d.Sweethome3d"
        },

        // --- Development & IT ---
        new()
        {
            SourceApp = "Visual Studio Code", SourceLogoGlyph = "VS", SourceLogoColor = Brush.Parse("#007ACC"),
            TargetApp = "VS Code", TargetLogoGlyph = "VS", TargetLogoColor = Brush.Parse("#007ACC"),
            Explanation = "VS Code ships an official Linux build.",
            AptPackage = "code", DnfPackage = "code", AurPackage = "visual-studio-code-bin", FlatpakId = "com.visualstudio.code"
        },
        new()
        {
            SourceApp = "Visual Studio", SourceLogoGlyph = "VS", SourceLogoColor = Brush.Parse("#5C2D91"),
            TargetApp = "JetBrains Rider", TargetLogoGlyph = "R", TargetLogoColor = Brush.Parse("#FF3350"),
            Explanation = "Full-featured .NET IDE with debugging/refactoring on par with Visual Studio.",
            AurPackage = "rider", FlatpakId = "com.jetbrains.Rider",
            DetectionAliases = new[] { "Visual Studio Community", "Visual Studio Professional", "Visual Studio Enterprise" }
        },
        new()
        {
            SourceApp = "IntelliJ IDEA", SourceLogoGlyph = "IJ", SourceLogoColor = Brush.Parse("#FE315D"),
            TargetApp = "IntelliJ IDEA Community", TargetLogoGlyph = "IJ", TargetLogoColor = Brush.Parse("#FE315D"),
            Explanation = "IntelliJ IDEA ships an official Linux build.",
            AurPackage = "intellij-idea-community-edition", AurIsOfficialRepo = true,
            FlatpakId = "com.jetbrains.IntelliJ-IDEA-Community"
        },
        new()
        {
            SourceApp = "PyCharm", SourceLogoGlyph = "PC", SourceLogoColor = Brush.Parse("#21D789"),
            TargetApp = "PyCharm Community", TargetLogoGlyph = "PC", TargetLogoColor = Brush.Parse("#21D789"),
            Explanation = "PyCharm ships an official Linux build.",
            AurPackage = "pycharm-community-edition", AurIsOfficialRepo = true
        },
        new()
        {
            SourceApp = "Sublime Text", SourceLogoGlyph = "S", SourceLogoColor = Brush.Parse("#FF9800"),
            TargetApp = "Sublime Text", TargetLogoGlyph = "S", TargetLogoColor = Brush.Parse("#FF9800"),
            Explanation = "Sublime Text ships an official Linux build.",
            AptPackage = "sublime-text", DnfPackage = "sublime-text", AurPackage = "sublime-text-4",
            FlatpakId = "com.sublimehq.SublimeText"
        },
        new()
        {
            SourceApp = "GitHub Desktop", SourceLogoGlyph = "GH", SourceLogoColor = Brush.Parse("#24292E"),
            TargetApp = "GitHub Desktop Linux", TargetLogoGlyph = "GH", TargetLogoColor = Brush.Parse("#24292E"),
            Explanation = "Community-maintained Linux fork, kept in sync with upstream GitHub Desktop.",
            AptPackage = "github-desktop", DnfPackage = "github-desktop", AurPackage = "github-desktop-bin", FlatpakId = "io.github.shiftey.Desktop"
        },
        new()
        {
            SourceApp = "Postman", SourceLogoGlyph = "P", SourceLogoColor = Brush.Parse("#FF6C37"),
            TargetApp = "Postman", TargetLogoGlyph = "P", TargetLogoColor = Brush.Parse("#FF6C37"),
            Explanation = "Postman ships an official Linux client.",
            AurPackage = "postman-bin", FlatpakId = "com.getpostman.Postman"
        },
        new()
        {
            SourceApp = "Docker Desktop", SourceLogoGlyph = "D", SourceLogoColor = Brush.Parse("#2496ED"),
            TargetApp = "Docker Engine", TargetLogoGlyph = "D", TargetLogoColor = Brush.Parse("#2496ED"),
            Explanation = "Same Docker, no GUI wrapper needed - the daemon runs natively on Linux.",
            AptPackage = "docker-ce", DnfPackage = "docker-ce", AurPackage = "docker", AurIsOfficialRepo = true
        },
        new()
        {
            SourceApp = "PuTTY", SourceLogoGlyph = "P", SourceLogoColor = Brush.Parse("#5E5E5E"),
            TargetApp = "Remmina", TargetLogoGlyph = "R", TargetLogoColor = Brush.Parse("#4A90D9"),
            Explanation = "Covers SSH plus RDP/VNC in one client.",
            AptPackage = "remmina", DnfPackage = "remmina", AurPackage = "remmina", AurIsOfficialRepo = true,
            FlatpakId = "org.remmina.Remmina"
        },
        new()
        {
            SourceApp = "VMware Workstation", SourceLogoGlyph = "VM", SourceLogoColor = Brush.Parse("#717D85"),
            TargetApp = "Virt-Manager", TargetLogoGlyph = "V", TargetLogoColor = Brush.Parse("#B8312F"),
            Explanation = "GUI front-end for KVM/QEMU, the native Linux virtualization stack.",
            AptPackage = "virt-manager", DnfPackage = "virt-manager", AurPackage = "virt-manager", AurIsOfficialRepo = true
        },
        new()
        {
            SourceApp = "FileZilla", SourceLogoGlyph = "F", SourceLogoColor = Brush.Parse("#BF0000"),
            TargetApp = "FileZilla", TargetLogoGlyph = "F", TargetLogoColor = Brush.Parse("#BF0000"),
            Explanation = "FileZilla ships an official Linux client.",
            AptPackage = "filezilla", DnfPackage = "filezilla", AurPackage = "filezilla", AurIsOfficialRepo = true,
            FlatpakId = "org.filezillaproject.Filezilla",
            // Only FileZilla Pro (the commercial edition) has a real Store MSIX package -
            // the free FileZilla Client doesn't.
            AppxPackageFamilyPrefixes = new[] { "BusinessFollowssrl.FileZillaPro" }
        },
        new()
        {
            SourceApp = "WinSCP", SourceLogoGlyph = "W", SourceLogoColor = Brush.Parse("#00509D"),
            TargetApp = "FileZilla", TargetLogoGlyph = "F", TargetLogoColor = Brush.Parse("#BF0000"),
            Explanation = "Closest match for SFTP/FTP file transfer with a similar dual-pane feel.",
            AptPackage = "filezilla", DnfPackage = "filezilla", AurPackage = "filezilla", AurIsOfficialRepo = true,
            FlatpakId = "org.filezillaproject.Filezilla",
            // Real package name, verified against the live Store listing - it genuinely
            // has this typo (missing the "r" in Prikryl), not a transcription error here.
            AppxPackageFamilyPrefixes = new[] { "MartinPikryl.WinSCP" }
        },
        new()
        {
            SourceApp = "XAMPP", SourceLogoGlyph = "X", SourceLogoColor = Brush.Parse("#FB7A24"),
            TargetApp = "Docker", TargetLogoGlyph = "D", TargetLogoColor = Brush.Parse("#2496ED"),
            Explanation = "Run the same Apache/MySQL/PHP stack in containers instead of a bundled installer.",
            AptPackage = "docker-ce", DnfPackage = "docker-ce", AurPackage = "docker", AurIsOfficialRepo = true
        },
        new()
        {
            SourceApp = "DBeaver", SourceLogoGlyph = "DB", SourceLogoColor = Brush.Parse("#382923"),
            TargetApp = "DBeaver", TargetLogoGlyph = "DB", TargetLogoColor = Brush.Parse("#382923"),
            Explanation = "DBeaver ships an official Linux client.",
            AurPackage = "dbeaver", AurIsOfficialRepo = true, FlatpakId = "io.dbeaver.DBeaverCommunity",
            AppxPackageFamilyPrefixes = new[] { "DBeaverCorp.DBeaverCE" }
        },

        // --- System Utilities & Hardware ---
        new()
        {
            SourceApp = "WinRAR", SourceLogoGlyph = "R", SourceLogoColor = Brush.Parse("#8B0000"),
            TargetApp = "Ark", TargetLogoGlyph = "A", TargetLogoColor = Brush.Parse("#1D99F3"),
            Explanation = "Built-in KDE archive manager, supports .rar/.zip/.7z.",
            AptPackage = "ark", DnfPackage = "ark", AurPackage = "ark", AurIsOfficialRepo = true, FlatpakId = "org.kde.ark"
        },
        new()
        {
            SourceApp = "7-Zip", SourceLogoGlyph = "7z", SourceLogoColor = Brush.Parse("#000000"),
            TargetApp = "PeaZip", TargetLogoGlyph = "P", TargetLogoColor = Brush.Parse("#4E9A06"),
            Explanation = "Broad archive-format support with a similar dual-pane browser.",
            DnfPackage = "peazip-gtk3", AurPackage = "peazip", FlatpakId = "io.github.peazip.PeaZip"
        },
        new()
        {
            SourceApp = "Rufus", SourceLogoGlyph = "R", SourceLogoColor = Brush.Parse("#00539C"),
            TargetApp = "Ventoy", TargetLogoGlyph = "V", TargetLogoColor = Brush.Parse("#1E88E5"),
            Explanation = "Write once, then drag ISOs onto the drive - no reflashing per image.",
            AurPackage = "ventoy-bin",
            AppxPackageFamilyPrefixes = new[] { "19453.net.Rufus" }
        },
        new()
        {
            SourceApp = "MSI Afterburner", SourceLogoGlyph = "A", SourceLogoColor = Brush.Parse("#DC0714"),
            TargetApp = "GreenWithEnvy", TargetLogoGlyph = "G", TargetLogoColor = Brush.Parse("#4CAF50"),
            Explanation = "GPU overclocking, fan curves, and monitoring for NVIDIA cards.",
            DnfPackage = "gwe", AurPackage = "gwe"
        },
        new()
        {
            SourceApp = "HWMonitor", SourceLogoGlyph = "H", SourceLogoColor = Brush.Parse("#004A99"),
            TargetApp = "CPU-X", TargetLogoGlyph = "C", TargetLogoColor = Brush.Parse("#2C3E50"),
            Explanation = "System/hardware info panel covering CPU, GPU, memory, and sensors.",
            AptPackage = "cpu-x", DnfPackage = "cpu-x", AurPackage = "cpu-x", AurIsOfficialRepo = true,
            FlatpakId = "io.github.thetumultuousunicornofdarkness.cpu-x"
        },
        new()
        {
            SourceApp = "CCleaner", SourceLogoGlyph = "C", SourceLogoColor = Brush.Parse("#7ABA45"),
            TargetApp = "BleachBit", TargetLogoGlyph = "B", TargetLogoColor = Brush.Parse("#7CB342"),
            Explanation = "System cleaner covering caches, temp files, and free-space wiping.",
            AptPackage = "bleachbit", DnfPackage = "bleachbit", AurPackage = "bleachbit", AurIsOfficialRepo = true,
            FlatpakId = "org.bleachbit.BleachBit"
        },
        new()
        {
            SourceApp = "Everything Search", SourceLogoGlyph = "E", SourceLogoColor = Brush.Parse("#000000"),
            TargetApp = "FSearch", TargetLogoGlyph = "F", TargetLogoColor = Brush.Parse("#4A90D9"),
            Explanation = "Instant filename search with a near-identical query syntax.",
            AptPackage = "fsearch", AurPackage = "fsearch",
            DetectionAliases = new[] { "Everything" }
        },
        new()
        {
            SourceApp = "TeamViewer", SourceLogoGlyph = "TV", SourceLogoColor = Brush.Parse("#0E8EE9"),
            TargetApp = "RustDesk", TargetLogoGlyph = "R", TargetLogoColor = Brush.Parse("#1D91F1"),
            Explanation = "Open-source remote desktop, self-hostable server option.",
            AurPackage = "rustdesk-bin", FlatpakId = "com.rustdesk.RustDesk"
        },
        new()
        {
            SourceApp = "AnyDesk", SourceLogoGlyph = "AD", SourceLogoColor = Brush.Parse("#EF4B24"),
            TargetApp = "RustDesk", TargetLogoGlyph = "R", TargetLogoColor = Brush.Parse("#1D91F1"),
            Explanation = "Open-source remote desktop, self-hostable server option.",
            AurPackage = "rustdesk-bin", FlatpakId = "com.rustdesk.RustDesk"
        },
        new()
        {
            SourceApp = "Malwarebytes", SourceLogoGlyph = "M", SourceLogoColor = Brush.Parse("#0078D4"),
            TargetApp = "ClamTK", TargetLogoGlyph = "C", TargetLogoColor = Brush.Parse("#2E8B57"),
            Explanation = "GUI front-end for ClamAV, the standard open-source Linux scanner.",
            AptPackage = "clamtk", DnfPackage = "clamtk", AurPackage = "clamtk"
        },
        new()
        {
            SourceApp = "ShareX", SourceLogoGlyph = "S", SourceLogoColor = Brush.Parse("#2B2B2B"),
            TargetApp = "Flameshot", TargetLogoGlyph = "F", TargetLogoColor = Brush.Parse("#00C853"),
            Explanation = "Screenshot/annotation tool with a similar region-capture workflow.",
            AptPackage = "flameshot", DnfPackage = "flameshot", AurPackage = "flameshot", AurIsOfficialRepo = true,
            FlatpakId = "org.flameshot.Flameshot",
            AppxPackageFamilyPrefixes = new[] { "19568ShareX.ShareX" }
        },
        new()
        {
            SourceApp = "Wallpaper Engine", SourceLogoGlyph = "WE", SourceLogoColor = Brush.Parse("#1B2838"),
            TargetApp = "linux-wallpaperengine", TargetLogoGlyph = "WE", TargetLogoColor = Brush.Parse("#1B2838"),
            Explanation = "Reverse-engineered player for the same Workshop wallpapers.",
            AurPackage = "linux-wallpaperengine-git"
        },
        new()
        {
            SourceApp = "AutoHotkey", SourceLogoGlyph = "AH", SourceLogoColor = Brush.Parse("#6594B9"),
            TargetApp = "AutoKey", TargetLogoGlyph = "AK", TargetLogoColor = Brush.Parse("#4E7CA1"),
            Explanation = "Closest match for scripted text expansion and hotkeys.",
            AptPackage = "autokey-qt", DnfPackage = "autokey-qt", AurPackage = "autokey-qt"
        },
        new()
        {
            SourceApp = "PowerToys", SourceLogoGlyph = "PT", SourceLogoColor = Brush.Parse("#0078D7"),
            TargetApp = "KRunner", TargetLogoGlyph = "KR", TargetLogoColor = Brush.Parse("#1D99F3"),
            Explanation = "Covers PowerToys Run's quick-launcher role; window management and color picker come from separate KDE tools. Pulls in the full Plasma shell, not just the launcher, if not already on KDE.",
            AptPackage = "plasma-workspace", DnfPackage = "plasma-workspace", AurPackage = "plasma-workspace", AurIsOfficialRepo = true
        },
        new()
        {
            SourceApp = "WinDirStat", SourceLogoGlyph = "W", SourceLogoColor = Brush.Parse("#2E5C8A"),
            TargetApp = "Filelight", TargetLogoGlyph = "F", TargetLogoColor = Brush.Parse("#1D99F3"),
            Explanation = "Visual disk-usage breakdown, same radial/treemap style.",
            AptPackage = "filelight", DnfPackage = "filelight", AurPackage = "filelight", AurIsOfficialRepo = true,
            FlatpakId = "org.kde.filelight",
            AppxPackageFamilyPrefixes = new[] { "WinDirStatTeam.WinDirStat" }
        },
        new()
        {
            SourceApp = "CrystalDiskInfo", SourceLogoGlyph = "CD", SourceLogoColor = Brush.Parse("#1565C0"),
            TargetApp = "GSmartControl", TargetLogoGlyph = "G", TargetLogoColor = Brush.Parse("#616161"),
            Explanation = "S.M.A.R.T. drive health monitoring GUI.",
            AptPackage = "gsmartcontrol", DnfPackage = "gsmartcontrol", AurPackage = "gsmartcontrol", AurIsOfficialRepo = true
        },
        new()
        {
            SourceApp = "Corsair iCUE", SourceLogoGlyph = "iQ", SourceLogoColor = Brush.Parse("#FFC72C"),
            TargetApp = "ckb-next", TargetLogoGlyph = "ck", TargetLogoColor = Brush.Parse("#FFC72C"),
            Explanation = "RGB lighting and fan control for Corsair peripherals.",
            AptPackage = "ckb-next", DnfPackage = "ckb-next", AurPackage = "ckb-next", AurIsOfficialRepo = true,
            DetectionAliases = new[] { "iCUE" }
        },
        new()
        {
            SourceApp = "Razer Synapse", SourceLogoGlyph = "R", SourceLogoColor = Brush.Parse("#44D62C"),
            TargetApp = "Polychromatic", TargetLogoGlyph = "P", TargetLogoColor = Brush.Parse("#00BCD4"),
            Explanation = "RGB lighting and macro control built on OpenRazer.",
            DnfPackage = "polychromatic", AurPackage = "polychromatic", FlatpakId = "app.polychromatic.controller"
        },
        new()
        {
            SourceApp = "Logitech G Hub", SourceLogoGlyph = "G", SourceLogoColor = Brush.Parse("#00B8FC"),
            TargetApp = "Solaar", TargetLogoGlyph = "S", TargetLogoColor = Brush.Parse("#5E5E5E"),
            Explanation = "Battery status, DPI, and button config for Logitech peripherals.",
            AptPackage = "solaar", DnfPackage = "solaar", AurPackage = "solaar", AurIsOfficialRepo = true,
            DetectionAliases = new[] { "G HUB" }
        },
        new()
        {
            SourceApp = "Elgato Stream Deck", SourceLogoGlyph = "SD", SourceLogoColor = Brush.Parse("#101010"),
            TargetApp = "StreamController", TargetLogoGlyph = "SC", TargetLogoColor = Brush.Parse("#0FA3E0"),
            Explanation = "Button/macro configuration for Elgato Stream Deck hardware.",
            AurPackage = "streamcontroller", FlatpakId = "com.core447.StreamController",
            DetectionAliases = new[] { "Stream Deck" }
        },

        // --- Cloud Storage & Password Managers ---
        new()
        {
            SourceApp = "Google Drive", SourceLogoGlyph = "GD", SourceLogoColor = Brush.Parse("#4285F4"),
            TargetApp = "Rclone", TargetLogoGlyph = "R", TargetLogoColor = Brush.Parse("#3B3B3B"),
            Explanation = "Command-line sync/mount for Google Drive and most other cloud providers.",
            AptPackage = "rclone", DnfPackage = "rclone", AurPackage = "rclone", AurIsOfficialRepo = true
        },
        new()
        {
            SourceApp = "Microsoft OneDrive", SourceLogoGlyph = "OD", SourceLogoColor = Brush.Parse("#0078D4"),
            TargetApp = "OneDrive Free Client", TargetLogoGlyph = "OD", TargetLogoColor = Brush.Parse("#0078D4"),
            Explanation = "Unofficial but actively maintained OneDrive sync client for Linux.",
            AptPackage = "onedrive", DnfPackage = "onedrive", AurPackage = "onedrive-abraunegg"
        },
        new()
        {
            SourceApp = "Dropbox", SourceLogoGlyph = "D", SourceLogoColor = Brush.Parse("#0061FF"),
            TargetApp = "Dropbox", TargetLogoGlyph = "D", TargetLogoColor = Brush.Parse("#0061FF"),
            Explanation = "Dropbox ships an official Linux client.",
            AptPackage = "nautilus-dropbox", DnfPackage = "nautilus-dropbox", AurPackage = "dropbox", FlatpakId = "com.dropbox.Client",
            AppxPackageFamilyPrefixes = new[] { "DropboxInc.Dropbox" }
        },
        new()
        {
            SourceApp = "1Password", SourceLogoGlyph = "1P", SourceLogoColor = Brush.Parse("#0094F5"),
            TargetApp = "1Password", TargetLogoGlyph = "1P", TargetLogoColor = Brush.Parse("#0094F5"),
            Explanation = "1Password ships an official Linux client.",
            AptPackage = "1password", DnfPackage = "1password", AurPackage = "1password", FlatpakId = "com.onepassword.OnePassword",
            // Real package name, verified against the live Store listing - an
            // auto-generated numeric identity rather than a "1Password"-branded one.
            AppxPackageFamilyPrefixes = new[] { "DC5C6510.2032887045529" }
        },
        new()
        {
            SourceApp = "Bitwarden", SourceLogoGlyph = "B", SourceLogoColor = Brush.Parse("#175DDC"),
            TargetApp = "Bitwarden", TargetLogoGlyph = "B", TargetLogoColor = Brush.Parse("#175DDC"),
            Explanation = "Bitwarden ships an official Linux client.",
            AurPackage = "bitwarden-bin", FlatpakId = "com.bitwarden.desktop",
            AppxPackageFamilyPrefixes = new[] { "8bitSolutionsLLC.bitwardendesktop" }
        },
        new()
        {
            SourceApp = "LastPass", SourceLogoGlyph = "LP", SourceLogoColor = Brush.Parse("#D32D2D"),
            TargetApp = "KeePassXC", TargetLogoGlyph = "K", TargetLogoColor = Brush.Parse("#6CAC4D"),
            Explanation = "Local-first password manager, no cloud account required.",
            AptPackage = "keepassxc", DnfPackage = "keepassxc", AurPackage = "keepassxc", AurIsOfficialRepo = true,
            FlatpakId = "org.keepassxc.KeePassXC",
            AppxPackageFamilyPrefixes = new[] { "LastPass.LastPassforDesktop" }
        },
        new()
        {
            SourceApp = "NordVPN", SourceLogoGlyph = "N", SourceLogoColor = Brush.Parse("#4687C6"),
            TargetApp = "Mullvad VPN", TargetLogoGlyph = "M", TargetLogoColor = Brush.Parse("#294D73"),
            Explanation = "No-logs, independently audited VPN with native Linux support and no account email required - a stronger privacy posture than NordVPN.",
            AptPackage = "mullvad-vpn", DnfPackage = "mullvad-vpn", AurPackage = "mullvad-vpn-bin"
        },

        // --- Torrent & Downloading ---
        new()
        {
            SourceApp = "uTorrent", SourceLogoGlyph = "u", SourceLogoColor = Brush.Parse("#71C948"),
            TargetApp = "qBittorrent", TargetLogoGlyph = "q", TargetLogoColor = Brush.Parse("#2F67BA"),
            Explanation = "Ad-free, no bundled bloatware, similar interface.",
            AptPackage = "qbittorrent", DnfPackage = "qbittorrent", AurPackage = "qbittorrent", AurIsOfficialRepo = true,
            FlatpakId = "org.qbittorrent.qBittorrent"
        },
        new()
        {
            SourceApp = "BitTorrent", SourceLogoGlyph = "BT", SourceLogoColor = Brush.Parse("#0F9D3D"),
            TargetApp = "Transmission", TargetLogoGlyph = "T", TargetLogoColor = Brush.Parse("#AF3838"),
            Explanation = "Lightweight, native GTK/Qt torrent client.",
            AptPackage = "transmission", DnfPackage = "transmission", AurPackage = "transmission-gtk", AurIsOfficialRepo = true,
            FlatpakId = "com.transmissionbt.Transmission"
        },
        new()
        {
            SourceApp = "Internet Download Manager", SourceLogoGlyph = "IDM", SourceLogoColor = Brush.Parse("#004A99"),
            TargetApp = "Xtreme Download Manager", TargetLogoGlyph = "XDM", TargetLogoColor = Brush.Parse("#3B76D6"),
            Explanation = "Download accelerator and browser-integration client for Linux.",
            AurPackage = "xdman"
        },
        new()
        {
            SourceApp = "JDownloader", SourceLogoGlyph = "JD", SourceLogoColor = Brush.Parse("#3B76D6"),
            TargetApp = "JDownloader 2", TargetLogoGlyph = "JD", TargetLogoColor = Brush.Parse("#3B76D6"),
            Explanation = "JDownloader ships an official Linux client.",
            AurPackage = "jdownloader2", FlatpakId = "org.jdownloader.JDownloader"
        },
    };
}
