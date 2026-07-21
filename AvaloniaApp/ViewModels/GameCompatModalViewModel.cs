using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AvaloniaApp.Models;
using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;

namespace AvaloniaApp.ViewModels;

public partial class GameCompatTierViewModel : ViewModelBase
{
    private const int CollapsedLimit = 7;
    private readonly IReadOnlyList<GameCompatEntry> _all;

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

    public GameCompatTierViewModel(string title, IReadOnlyList<GameCompatEntry> entries)
    {
        TierTitle = title;
        _all = entries;
        ToggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        Refresh();
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
    public GameCompatTierViewModel UnknownTier { get; }
    public IRelayCommand CloseCommand { get; }

    [ObservableProperty]
    private bool _hasNoGamesDetected;

    public GameCompatModalViewModel(SteamLibraryService steamLibrary, IProtonDbClient protonDb, Action onClose)
    {
        CloseCommand = new RelayCommand(onClose);

        var entries = steamLibrary.DetectInstalledApps()
            .Select(app =>
            {
                var (tier, note) = protonDb.Lookup(app.AppId, app.Name);
                return new GameCompatEntry(app.AppId, app.Name, tier, note);
            })
            .ToList();

        HasNoGamesDetected = entries.Count == 0;

        RunsPerfectly = new GameCompatTierViewModel("Runs perfectly", entries.Where(e => e.Tier == CompatTier.RunsPerfectly).ToList());
        RunsFine = new GameCompatTierViewModel("Runs fine", entries.Where(e => e.Tier == CompatTier.RunsFine).ToList());
        UnknownTier = new GameCompatTierViewModel("Unknown", entries.Where(e => e.Tier == CompatTier.Unknown).ToList());
    }
}
