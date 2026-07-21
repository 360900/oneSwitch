using System;
using System.Collections.ObjectModel;
using System.Linq;
using AvaloniaApp.Models;
using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class AppTranslationModalViewModel : ViewModelBase
{
    public ObservableCollection<AppTranslationEntry> Entries { get; }
    public IRelayCommand CloseCommand { get; }

    [ObservableProperty]
    private bool _hasNoEntriesDetected;

    public AppTranslationModalViewModel(AppTranslationCatalog catalog, InstalledProgramScanner scanner, MigrationState state, Action onClose)
    {
        var installedPrograms = scanner.DetectInstalledPrograms();
        var suggestions = catalog.GetSuggestions(installedPrograms);
        var packageManager = DistroResolver.ResolvePackageManager(state);

        foreach (var entry in suggestions)
        {
            entry.ResolvedPackageDisplay = entry.ResolvePackageDisplay(packageManager);
        }

        // Edge alongside a real browser choice is almost always the Windows-bundled
        // default nobody opted into, not an intentional pick - leave it suggested but
        // unchecked rather than defaulting to install it.
        var hasOtherBrowser = suggestions.Any(e => e.IsBrowser && e.SourceApp != "Microsoft Edge");
        if (hasOtherBrowser)
        {
            foreach (var edge in suggestions.Where(e => e.SourceApp == "Microsoft Edge"))
                edge.IsIncluded = false;
        }

        Entries = new ObservableCollection<AppTranslationEntry>(suggestions);
        HasNoEntriesDetected = Entries.Count == 0;
        CloseCommand = new RelayCommand(onClose);
    }
}
