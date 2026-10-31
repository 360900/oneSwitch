# oneSwitch: Master Architectural Reference Sheet

## 1. Branding & Design Language

* **Logo:** Accent-blue rounded tile with a white toggle switch (`oneswitch_icon.svg`).
* **Design language:** oneDesign "One". Calm opaque content, one accent (blue) driving all tints, boxed lists, concentric radii, hairline strokes with soft shadows. Light/dark follows the OS.
* **Tokens:** `Theme/OneTheme.cs` resolves the One OKLCH tokens into light and dark brush dictionaries (`One.*` resource keys). `Theme/OneStyles.axaml` holds the control styles. Icons are Adwaita symbolic paths (`Theme/Icons.axaml`).
* **Window chrome:** Custom headerbar with a centered title and a round close button (drag to move, double-click to maximize).

## 2. Frontend Architecture

* **Framework:** C# Avalonia (XAML-based), MVVM via CommunityToolkit.Mvvm. Each slide and modal has a ViewModel; a shared `MigrationState` observable model carries the user's choices across the wizard and back-navigation.
* **Layout Structure:** `TransitioningContentControl` with a slide + fade transition for the 3-step wizard; direction (forward/back) is tracked so Back reverses the slide. Layout is responsive: fixed type sizes, content centered with a max width, freely resizable window.
* **Navigation:** Footer with Back, step indicator and the suggested Continue / "Create migration files" button. Dialogs (apps, games, result) sit on a scrim and close with Esc or a click outside.

## 3. Wizard Flow (The 3 Slides)

* **Slide 1: Desktop Paradigm**
  * *UI:* Selectable cards (same pattern as Slide 2, for visual consistency across the wizard).
  * *Options:* Windows-style (Bottom Taskbar) vs. macOS-style (Centered Dock/Top Bar).

* **Slide 2: Primary Role**
  * *Options:* 
    1. Pure Gaming (Bazzite)
    2. Balanced Daily Use (Mint if Slide 1's Windows-style layout was picked, Fedora if macOS-style was picked)
    3. Productivity & Dev (Debian)
    4. Enthusiast (Arch)
  * Update cadence (rock-solid vs. bleeding edge) is derived from this choice rather than asked as a separate step. Enthusiast implies bleeding-edge, everything else defaults to LTS.
  * The Daily Use distro depends on the Slide 1 layout choice: Mint's Cinnamon desktop is Windows-like by default (bottom taskbar, start menu), so it needs no reconfiguration. Fedora is the reference GNOME distro (no Snap, no Amazon-lens history, reliable release model); achieving the macOS-style centered dock on it (and on Debian, the Productivity pick, which is also GNOME by default, and on Arch when the Enthusiast pick is paired with the macOS paradigm) is handled by `oneswitch.sh` at first boot (see §7): installs the Dash to Dock extension (`dash-to-dock@micxgx.gmail.com`) via the distro's own package manager, then writes it system-wide-enabled and pre-configured (bottom position, fixed, dynamic transparency) through `/etc/dconf/db/local.d` defaults rather than a first-login script - this avoids the Wayland failure mode where an extension enabled by a one-shot script won't actually load until the next re-login, and leaves every setting user-overridable since no dconf locks are written. The resolved DE itself comes from `DistroResolver.ResolveDesktopEnvironment` - Mint/Cinnamon, Fedora/GNOME, Debian/GNOME are fixed; Bazzite and Arch follow the paradigm (macOS → GNOME, Windows → KDE).

* **Slide 3: Confirmation & Execution**
  * *UI:* Summary list: target system, GPU readout ("Your NVIDIA GPU is supported."), apps and games rows that open their dialogs.
  * *Interactions:* Radio list for Cloud Provider (Rclone) vs. External Drive vs. skipping data migration entirely.
  * *Action:* "Create migration files" writes the files to `Desktop/oneSwitch` and shows a result dialog with the written files, install instructions for the chosen distro, and any error.

## 4. UX Modals (Data Translation)

* **App Translation View:** Maps Windows `.exe` to Linux package. Format: `[Old App Logo] -> [New App Logo]`. Includes brief explanation text (e.g., *why* OnlyOffice is used) and a pre-install opt-out checkbox. Implemented, fed by real detected installed programs (§5); the Windows-app -> Linux-equivalent mapping itself comes from a curated table (`AppTranslationCatalog`, ~100 entries) by nature, not something to detect. Each entry carries real per-package-manager identifiers (apt/dnf/AUR/Flatpak); the modal shows whichever one matches the distro actually chosen in Slide 2 (`DistroResolver`), falling back to Flatpak if the app isn't packaged natively, and updating live when the Slide 2 choice changes. Program scanning runs off the UI thread. Shows an empty-state message if no familiar applications were found.
* **Game Compatibility View:** Categorized by ProtonDB verdict ("Runs perfectly" = platinum, "Runs fine" = gold/silver, "Won't run" = bronze/borked, "Unknown" = pending/no data). Capped at 7 visible items per list (expandable). Includes a synthesized ProtonDB note under titles (tier, report count, confidence). Implemented, fed by real detected Steam library data (§5) and the live ProtonDB summaries API; loads asynchronously with a "Checking ProtonDB…" state, and degrades to an explicit "Couldn't reach ProtonDB" message (instead of a misleading all-Unknown list) when every lookup fails.

## 5. Backend Diagnostics & Data Extraction

* **Hardware Polling:** C# GPU detection is implemented and wired into Slide 3's readout (display adapter class key `DriverDesc` in the registry on Windows, since `wmic` is gone from current Windows 11; `lspci` display-class lines only on Linux). Flags payload for `nvidia-open` modules on NVIDIA hardware.
* **Library Parsing:** Implemented. `SteamLibraryService` parses `libraryfolders.vdf` and `appmanifest_*.acf` (Valve's VDF/KeyValues format) to find real Steam library paths and installed games (skipping Proton, Steam Linux Runtime and Steamworks redistributables), via the Windows registry or the standard Linux Steam directories. ProtonDB integration is implemented in `ProtonDbClient` against the community summaries API (`protondb.com/api/v1/reports/summaries/{appId}.json`), throttled to 4 concurrent requests with an in-memory per-session cache; per-game failures degrade to Unknown and never throw. AreWeAntiCheatYet is still not integrated - anti-cheat caveats surface only insofar as ProtonDB's tier already reflects them.
* **App Dictionary:** Translates detected Windows workflows to APT, DNF, AUR, or Flatpak equivalents. `InstalledProgramScanner` combines four real sources to find installed programs: the registry Uninstall keys (`HKLM`/`HKLM\WOW6432Node`/`HKCU`, same source Control Panel's "Programs and Features" uses), the AppX package repository (`HKCU\...\AppModel\Repository\Packages`, since Microsoft Store/MSIX apps never write an Uninstall key entry), App Paths (`HKLM`/`HKLM\WOW6432Node\...\App Paths`, the Win+R exe-resolution registry), and Start Menu `.lnk` shortcuts (per-user and all-users Programs folders, catching portable/manually-copied apps with no registry trace at all). `AppTranslationCatalog` matches the combined, deduplicated list against its curated table. See §4.
* **User Data Staging:** Extracts active Windows wallpaper from AppData. Uses Rclone to authenticate (intercepting `127.0.0.1` OAuth) and queue `/home` directory transfers. Not yet implemented.

## 6. Payload Staging (The Deployment Engine)

* **ISO Acquisition:** MonoTorrent embedded to pull Linux ISOs in the background via magnet links.
* **USB Formatting:** Silent execution of `Ventoy2Disk.exe` targeting the user-selected drive.
* **Bootloader Config:** Injects custom oneSwitch GRUB themes. Auto-generates `ventoy.json` with `VTOY_DEFAULT_IMAGE` set to the ISO and `VTOY_MENU_TIMEOUT` set to `0` for an invisible, zero-click boot.

## 7. OS Provisioning & Post-Install

* **Unattended Script Factory:** Implemented for Debian, Fedora, and Arch via `UnattendedScriptFactory` - generates `oneswitch-preseed.cfg` (Debian, GNOME task selected), `oneswitch.ks` (Fedora), and `user_configuration.json` (Arch, archinstall 4.x schema: GRUB, NetworkManager, PipeWire, GNOME or KDE Plasma profile, NVIDIA open driver when detected) written next to `oneswitch.sh` in `Desktop/oneSwitch`. The Arch config deliberately omits disks and users: a silent disk layout needs the exact target device and byte-exact partitions, so it is meant for `archinstall --config user_configuration.json`, where the user picks the disk and creates the account in the preloaded menu. All three builders share one first-boot delivery mechanism: the generated `oneswitch.sh` is base64-embedded into the target filesystem (preseed runs it through `in-target sh -c` so the redirect lands in `/target`) along with a self-disabling systemd oneshot unit (`oneswitch-first-boot.service`, after `network-online.target`, `ExecStart=-` so a failing script still lets it clean up) that runs it on the first real boot, then disables and deletes itself in `ExecStartPost`. Base64 is used specifically because `oneswitch.sh` itself contains heredocs (the dconf blocks), and a heredoc-in-heredoc approach would need fragile unique-marker bookkeeping across three different outer syntaxes (preseed `in-target` / kickstart `%post` / archinstall JSON string array) - the base64 alphabet (A-Za-z0-9+/=) needs no escaping in any of them. None of the three configs create a local user account or set a password - each installer already falls back to prompting for just that one screen when those directives are absent, which is the same normal "create your account" screen a manual install would show, and avoids shipping a hardcoded placeholder account. Bazzite and Mint are explicitly listed as not yet supported: Bazzite's kickstart is dynamically generated by its own installer hook script with no documented custom-`%post` path, and Mint has no first-party unattended-install mechanism (only community reverse-engineered preseed + custom boot-media edits, too fragile/version-sensitive to ship without dedicated VM verification). Same documented-gap honesty pattern as the ProtonDB API, Rclone capture, and wallpaper staging already used in §5.
* **oneswitch.sh (The First-Boot Payload):** Implemented via `FirstBootPayloadBuilder`, wired to Slide 3's "Create migration files" button - writes the generated script to disk for inspection until §6 Payload Staging exists to consume it directly. Detects `apt`/`dnf`/`pacman`, the distro id, and the desktop environment from the live system at boot time (not baked in at generation time), then:
  * Refreshes package metadata, then installs each included `AppTranslationModal` entry's resolved package, best-effort (`|| true`) per app so one failure never blocks the rest. Flatpak is installed if missing and the Flathub remote added first. Genuine AUR packages (not yet graduated to Arch's official repo) are listed in `/var/lib/oneswitch/aur-pending.txt`, since AUR helpers refuse to run as root and no AUR helper is installed by this script. Known gaps needing a vendor repo added first (undone here): Brave, Docker, 1Password, VS Code, Sublime Text, Mullvad.
  * Gated on macOS paradigm + GNOME + Fedora/Debian/Arch: installs the dash-to-dock extension - `dnf install gnome-shell-extension-dash-to-dock` on Fedora, `apt-get install gnome-shell-extension-dashtodock` on Debian (both real distro packages; Debian does **not** need a GitHub-release download, despite that being the original assumption during planning) - then applies it via system-wide dconf defaults. The dconf user profile is only created if missing, otherwise `system-db:local` is appended, so distro databases are kept. See §3 for why.
  * Applies the wallpaper on GNOME through the same dconf system database (the first-boot service has no user session, so `gsettings` would not work) from a fixed staging path (`/usr/share/backgrounds/oneswitch-wallpaper.jpg`) - a documented no-op seam until §5's wallpaper extraction actually stages that file. KDE is not handled yet.
  * Rclone `/home` restore is a commented stub - §5's Rclone remote-config capture doesn't exist yet, so there's nothing real to pull.
