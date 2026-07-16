# KARGO: Master Architectural Reference Sheet

## 1. Branding & Design Language

* **Logo:** Isometric 3D box (shipping crate/payload metaphor).
* **Color Palette:** "Enterprise Trust." Dark Navy box on a clean, cool Off-White/Light Slate Gray background (`#F8FAFC`). Soft, diffuse navy drop-shadow for depth.
* **UI Theme:** Light Mode default. Professional, clean, zero "hacker/terminal" vibes. Custom-drawn title bar (small branded strip, drag-to-move, minimize/close only, no maximize since it would break the locked window aspect ratio) rather than native OS chrome.

## 2. Frontend Architecture

* **Framework:** C# Avalonia (XAML-based), MVVM via CommunityToolkit.Mvvm. Each slide and modal has a ViewModel; a shared `MigrationState` observable model carries the user's choices across the wizard and back-navigation.
* **Layout Structure:** `TransitioningContentControl` with a `PageSlide` animation to create a linear, frictionless 3-step sliding page index; direction (forward/back) is tracked so Back visibly reverses the slide instead of replaying the forward animation. The whole UI scales proportionally (fonts, icons, cards) with window size via a `LayoutTransformControl`, and the window is locked to its baseline aspect ratio on resize so that scaling stays uniform.
* **Top-Level Nav:** A simple progress bar anchored at the top. Left/Back arrows hidden on Step 1.

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
  * The Daily Use distro depends on the Slide 1 layout choice: Mint's Cinnamon desktop is Windows-like by default (bottom taskbar, start menu), so it needs no reconfiguration. Fedora is the reference GNOME distro (no Snap, no Amazon-lens history, reliable release model) but achieving the macOS-style centered dock on it requires first-boot provisioning to install and configure a Dash-to-Dock-style extension, not yet implemented (see §7).

* **Slide 3: Confirmation & Execution**
  * *UI:* Dynamic diagnostic readouts ("Your Nvidia GPU is supported").
  * *Interactions:* "See which ones >" links for apps/games. Toggle card for Cloud Provider (Rclone) vs. External Drive vs. skipping data migration entirely.
  * *Action:* Prominent blue "Start Migration >" button.

## 4. UX Modals (Data Translation)

* **App Translation View:** Maps Windows `.exe` to Linux package. Format: `[Old App Logo] -> [New App Logo]`. Includes brief explanation text (e.g., *why* OnlyOffice is used) and a pre-install opt-out checkbox. Implemented, fed by real detected installed programs (§5); the Windows-app -> Linux-equivalent mapping itself comes from a curated table (`AppTranslationCatalog`, ~100 entries) by nature, not something to detect. Each entry carries real per-package-manager identifiers (apt/dnf/AUR/Flatpak); the modal shows whichever one matches the distro actually chosen in Slide 2 (`DistroResolver`), falling back to another manager if the app isn't packaged for the preferred one. Shows an empty-state message if no familiar applications were found.
* **Game Compatibility View:** Categorized by Steam Deck tiers ("Runs perfectly", "Runs fine", "Unknown"). Capped at 7 visible items per list (expandable). Includes ProtonDB community notes under titles. Implemented, fed by real detected Steam library data (§5); the ProtonDB notes themselves come from a placeholder lookup table pending the real API integration.

## 5. Backend Diagnostics & Data Extraction

* **Hardware Polling:** C# GPU detection is implemented and wired into Slide 3's readout (`wmic` on Windows, `lspci` on Linux). Flags payload for `nvidia-open` modules on NVIDIA hardware.
* **Library Parsing:** Implemented. `SteamLibraryService` parses `libraryfolders.vdf` and `appmanifest_*.acf` (Valve's VDF/KeyValues format) to find real Steam library paths and installed games, via the Windows registry or the standard Linux Steam directories. Pinging the real ProtonDB/AreWeAntiCheatYet APIs is not yet implemented; `ProtonDbClient` currently returns a small hardcoded lookup table as a placeholder.
* **App Dictionary:** Translates detected Windows workflows to APT, DNF, AUR, or Flatpak equivalents. `InstalledProgramScanner` combines four real sources to find installed programs: the registry Uninstall keys (`HKLM`/`HKLM\WOW6432Node`/`HKCU`, same source Control Panel's "Programs and Features" uses), the AppX package repository (`HKCU\...\AppModel\Repository\Packages`, since Microsoft Store/MSIX apps never write an Uninstall key entry), App Paths (`HKLM`/`HKLM\WOW6432Node\...\App Paths`, the Win+R exe-resolution registry), and Start Menu `.lnk` shortcuts (per-user and all-users Programs folders, catching portable/manually-copied apps with no registry trace at all). `AppTranslationCatalog` matches the combined, deduplicated list against its curated table. See §4.
* **User Data Staging:** Extracts active Windows wallpaper from AppData. Uses Rclone to authenticate (intercepting `127.0.0.1` OAuth) and queue `/home` directory transfers. Not yet implemented.

## 6. Payload Staging (The Deployment Engine)

* **ISO Acquisition:** MonoTorrent embedded to pull Linux ISOs in the background via magnet links.
* **USB Formatting:** Silent execution of `Ventoy2Disk.exe` targeting the user-selected drive.
* **Bootloader Config:** Injects custom Kargo GRUB themes. Auto-generates `ventoy.json` with `VTOY_DEFAULT_IMAGE` set to the ISO and `VTOY_MENU_TIMEOUT` set to `0` for an invisible, zero-click boot.

## 7. OS Provisioning & Post-Install

* **Unattended Script Factory:** Generates YAML/Config files based on target OS: Ubuntu (`autoinstall.yaml`), Fedora (`.ks`), Debian (`preseed.cfg`). Injects the execution hook for the final payload.
* **Kargo.sh (The First-Boot Payload):** 
  * Detects environment (`apt` vs `dnf` vs `pacman`).
  * Silently installs the translated application array.
  * Executes Rclone pull to restore the user's cloud files into `/home`.
  * Applies the extracted Windows wallpaper using DE-specific CLI commands (e.g., `plasma-apply-wallpaperimage`).
