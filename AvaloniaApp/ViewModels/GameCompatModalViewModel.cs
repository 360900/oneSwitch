using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AvaloniaApp.Models;
using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;

namespace AvaloniaApp.ViewModels;

public partial class GameCompatTierViewModel : ViewModelBase
{
    private const int CollapsedLimit = 7;
    private IReadOnlyList<GameCompatEntry> _all = Array.Empty<GameCompatEntry>();

    public string TierTitle { get; }
    public int Count => _all.Count;
    public bool IsEmpty => _all.Count == 0;
    public ObservableCollection<GameCompatEntry> VisibleEntries { get; } = new();
    public bool HasMore => _all.Count > CollapsedLimit;
    public string ExpandButtonText => IsExpanded ? "Show less" : $"Show all {_all.Count}";
    public MaterialIconKind ExpandIconKind => IsExpanded ? MaterialIconKind.ChevronUp : MaterialIconKind.ChevronDown;

    [ObservableProperty]
    private bool _isExpanded;

    public IRelayCommand ToggleExpandCommand { get; }

    public GameCompatTierViewModel(string title)
    {
        TierTitle = title;
        ToggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
    }

    // Entries arrive after the async ProtonDB lookups finish, not at construction time.
    public void SetEntries(IReadOnlyList<GameCompatEntry> entries)
    {
        _all = entries;
        IsExpanded = false;
        Refresh();
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasMore));
        OnPropertyChanged(nameof(ExpandButtonText));
    }

    partial void OnIsExpandedChanged(bool value)
    {
        Refresh();
        OnPropertyChanged(nameof(ExpandButtonText));
        OnPropertyChanged(nameof(ExpandIconKind));
    }

    private void Refresh()
    {
        VisibleEntries.Clear();
        foreach (var entry in IsExpanded ? _all : _all.Take(CollapsedLimit))
            VisibleEntries.Add(entry);
    }
}

public partial class GameCompatModalViewModel : ViewModelBase
{
    public GameCompatTierViewModel RunsPerfectly { get; }
    public GameCompatTierViewModel RunsFine { get; }
    public GameCompatTierViewModel WontRun { get; }
    public GameCompatTierViewModel UnknownTier { get; }
    public IRelayCommand CloseCommand { get; }

    [ObservableProperty]
    private bool _hasNoGamesDetected;

    // True while ProtonDB lookups are in flight. Steam detection itself is local and
    // synchronous, so the game list is known instantly - only the tiers are async.
    [ObservableProperty]
    private bool _isLoading;

    // True when every lookup failed (e.g. no network): tiers would be all-Unknown,
    // which reads as "we checked" - this flag lets the UI say "we couldn't check".
    [ObservableProperty]
    private bool _lookupUnavailable;

    public GameCompatModalViewModel(SteamLibraryService steamLibrary, IProtonDbClient protonDb, Action onClose)
    {
        CloseCommand = new RelayCommand(onClose);

        RunsPerfectly = new GameCompatTierViewModel("Runs perfectly");
        RunsFine = new GameCompatTierViewModel("Runs fine");
        WontRun = new GameCompatTierViewModel("Won't run");
        UnknownTier = new GameCompatTierViewModel("Unknown");

        var apps = steamLibrary.DetectInstalledApps();
        HasNoGamesDetected = apps.Count == 0;

        if (apps.Count > 0)
        {
            IsLoading = true;
            _ = LoadAsync(protonDb, apps);
        }
    }

    private async Task LoadAsync(IProtonDbClient protonDb, IReadOnlyList<InstalledSteamApp> apps)
    {
        IReadOnlyList<ProtonDbLookupResult> results;
        try
        {
            results = await protonDb.LookupAsync(apps);
        }
        catch
        {
            // LookupAsync is documented not to throw per-app failures; this guards the
            // batch level (e.g. cancellation) so the modal never wedges in IsLoading.
            results = apps.Select(_ => new ProtonDbLookupResult(CompatTier.Unknown, null, Succeeded: false)).ToList();
        }

        LookupUnavailable = results.Count > 0 && results.All(r => !r.Succeeded);

        // Result order matches input order (client contract), so zip by position.
        var entries = apps
            .Zip(results, (app, r) => new GameCompatEntry(app.AppId, app.Name, r.Tier, r.Note))
            .ToList();

        RunsPerfectly.SetEntries(entries.Where(e => e.Tier == CompatTier.RunsPerfectly).ToList());
        RunsFine.SetEntries(entries.Where(e => e.Tier == CompatTier.RunsFine).ToList());
        WontRun.SetEntries(entries.Where(e => e.Tier == CompatTier.WontRun).ToList());
        UnknownTier.SetEntries(entries.Where(e => e.Tier == CompatTier.Unknown).ToList());

        IsLoading = false;
    }
}
