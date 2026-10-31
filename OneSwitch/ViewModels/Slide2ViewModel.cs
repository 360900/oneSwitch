using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using OneSwitch.Models;

namespace OneSwitch.ViewModels;

public class Slide2ViewModel : ViewModelBase
{
    private readonly MigrationState _state;

    public SlideHeaderViewModel Header { get; } = new(
        "What will you use this PC for?",
        "Select the best match for your daily workflow.");

    public ObservableCollection<ISelectableOption> Options { get; }
    public SelectableOption<PrimaryRole> DailyUseOption { get; }

    public Slide2ViewModel(MigrationState state)
    {
        _state = state;

        DailyUseOption = new SelectableOption<PrimaryRole>(Select)
        {
            Value = PrimaryRole.DailyUse,
            Title = "Balanced Daily Use",
            Description = "Web, media, everyday productivity, and casual gaming.",
            IconKey = "Icon.Home",
        };

        Options = new ObservableCollection<ISelectableOption>
        {
            new SelectableOption<PrimaryRole>(Select)
            {
                Value = PrimaryRole.Gaming,
                Title = "Pure Gaming",
                Description = "Optimized for a console-like experience, with smooth performance and vivid visuals.",
                Caption = "Bazzite",
                IconKey = "Icon.Gaming",
            },
            DailyUseOption,
            new SelectableOption<PrimaryRole>(Select)
            {
                Value = PrimaryRole.Productivity,
                Title = "Productivity & Dev",
                Description = "Coding, design, illustration, and photo editing.",
                Caption = "Debian",
                IconKey = "Icon.Terminal",
            },
            new SelectableOption<PrimaryRole>(Select)
            {
                Value = PrimaryRole.Enthusiast,
                Title = "Enthusiast",
                Description = "Bleeding-edge software and full control over your system. Requires hands-on maintenance.",
                Caption = "Arch Linux",
                IconKey = "Icon.Engineering",
            },
        };

        _state.PropertyChanged += OnStateChanged;
        UpdateDailyUseOption();
        SyncSelection();
    }

    private void Select(PrimaryRole value)
    {
        _state.SelectedRole = value;
        // Update cadence is implied by role rather than asked separately.
        // Enthusiast wants bleeding-edge, everyone else gets the rock-solid default.
        _state.SelectedStance = value == PrimaryRole.Enthusiast ? UpdateStance.BleedingEdge : UpdateStance.LTS;
        SyncSelection();
    }

    private void SyncSelection()
    {
        foreach (var option in Options.Cast<SelectableOption<PrimaryRole>>())
            option.IsSelected = option.Value == _state.SelectedRole;
    }

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MigrationState.SelectedParadigm))
            UpdateDailyUseOption();
    }

    // Mint's Cinnamon desktop is Windows-like out of the box (bottom taskbar,
    // start menu). Fedora is the reference GNOME distro, which needs a
    // Dash-to-Dock-style extension for a macOS-like centered dock, but is
    // GNOME-default and doesn't carry Ubuntu's Snap/Firefox-snap baggage.
    private void UpdateDailyUseOption()
    {
        if (_state.SelectedParadigm == DesktopParadigm.MacOS)
        {
            DailyUseOption.Caption = "Fedora";
        }
        else
        {
            DailyUseOption.Caption = "Linux Mint";
        }
    }
}
