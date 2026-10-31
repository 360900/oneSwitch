using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using OneSwitch.Models;
using OneSwitch.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OneSwitch.ViewModels;

public partial class AppTranslationModalViewModel : ViewModelBase
{
    private readonly MigrationState _state;

    public ObservableCollection<AppTranslationEntry> Entries { get; } = new();
    public IRelayCommand CloseCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoEntriesDetected))]
    [NotifyPropertyChangedFor(nameof(HasEntries))]
    private bool _isLoading = true;

    [ObservableProperty]
    private int _includedCount;

    public bool HasNoEntriesDetected => !IsLoading && Entries.Count == 0;
    public bool HasEntries => !IsLoading && Entries.Count > 0;

    public AppTranslationModalViewModel(AppTranslationCatalog catalog, InstalledProgramScanner scanner, MigrationState state, Action onClose)
    {
        _state = state;
        CloseCommand = new RelayCommand(onClose);
        _state.PropertyChanged += OnStateChanged;
        _ = LoadAsync(catalog, scanner);
    }

    // The registry and Start Menu scan takes seconds on a full Windows install, so it runs
    // off the UI thread. Constructed on the UI thread, so the continuation resumes there.
    private async Task LoadAsync(AppTranslationCatalog catalog, InstalledProgramScanner scanner)
    {
        var suggestions = await Task.Run(() => catalog.GetSuggestions(scanner.DetectInstalledPrograms()));

        // Edge alongside a real browser choice is almost always the Windows-bundled
        // default nobody opted into, so leave it suggested but unchecked.
        var hasOtherBrowser = suggestions.Any(e => e.IsBrowser && e.SourceApp != "Microsoft Edge");
        if (hasOtherBrowser)
        {
            foreach (var edge in suggestions.Where(e => e.SourceApp == "Microsoft Edge"))
                edge.IsIncluded = false;
        }

        foreach (var entry in suggestions)
        {
            entry.PropertyChanged += OnEntryChanged;
            Entries.Add(entry);
        }

        RefreshPackages();
        RefreshIncludedCount();
        IsLoading = false;
    }

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The target distro (and so the package manager) depends on both choices.
        if (e.PropertyName is nameof(MigrationState.SelectedRole) or nameof(MigrationState.SelectedParadigm))
            RefreshPackages();
    }

    private void OnEntryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppTranslationEntry.IsIncluded))
            RefreshIncludedCount();
    }

    private void RefreshPackages()
    {
        var packageManager = DistroResolver.ResolvePackageManager(_state);
        foreach (var entry in Entries)
            entry.ResolvedPackageDisplay = entry.ResolvePackageDisplay(packageManager);
    }

    private void RefreshIncludedCount() => IncludedCount = Entries.Count(e => e.IsIncluded);
}
