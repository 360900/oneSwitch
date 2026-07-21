using AvaloniaApp.Models;
using Material.Icons;

namespace AvaloniaApp.ViewModels;

public class Slide1ViewModel : ViewModelBase
{
    private readonly MigrationState _state;

    public SlideHeaderViewModel Header { get; } = new(
        MaterialIconKind.Monitor,
        "Choose your desktop layout",
        "How do you prefer to navigate your system?");

    public SelectableOption<DesktopParadigm> WindowsOption { get; }
    public SelectableOption<DesktopParadigm> MacOsOption { get; }

    public Slide1ViewModel(MigrationState state)
    {
        _state = state;

        WindowsOption = new SelectableOption<DesktopParadigm>(Select)
        {
            Value = DesktopParadigm.Windows,
            Title = "Bottom Taskbar",
            Description = "Windows-style layout",
            IconKind = MaterialIconKind.PageLayoutFooter,
        };
        MacOsOption = new SelectableOption<DesktopParadigm>(Select)
        {
            Value = DesktopParadigm.MacOS,
            Title = "Centered Dock",
            Description = "macOS-style layout with bottom dock and top bar",
            IconKind = MaterialIconKind.PageLayoutHeaderFooter,
        };

        SyncSelection();
    }

    private void Select(DesktopParadigm value)
    {
        _state.SelectedParadigm = value;
        SyncSelection();
    }

    private void SyncSelection()
    {
        WindowsOption.IsSelected = WindowsOption.Value == _state.SelectedParadigm;
        MacOsOption.IsSelected = MacOsOption.Value == _state.SelectedParadigm;
    }
}
