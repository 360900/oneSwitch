using System;
using System.Collections.ObjectModel;
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

    public AppTranslationModalViewModel(AppTranslationCatalog catalog, InstalledProgramScanner scanner, Action onClose)
    {
        var installedPrograms = scanner.DetectInstalledPrograms();
        var suggestions = catalog.GetSuggestions(installedPrograms);

        Entries = new ObservableCollection<AppTranslationEntry>(suggestions);
        HasNoEntriesDetected = Entries.Count == 0;
        CloseCommand = new RelayCommand(onClose);
    }
}
