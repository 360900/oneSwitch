using System.Collections.Generic;
using System.Threading.Tasks;
using AvaloniaApp.Models;
using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const int TotalSteps = 3;

    public MigrationState State { get; }

    public IReadOnlyList<ViewModelBase> Slides { get; }

    public AppTranslationModalViewModel AppTranslationModal { get; }
    public GameCompatModalViewModel GameCompatModal { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(IsLastStep))]
    private int _currentStep = 1;

    [ObservableProperty]
    private ViewModelBase _currentSlide;

    [ObservableProperty]
    private bool _isAppTranslationModalVisible;

    [ObservableProperty]
    private bool _isGameCompatModalVisible;

    [ObservableProperty]
    private bool _isNavigatingBack;

    public bool CanGoBack => CurrentStep > 1;
    public bool CanGoNext => CurrentStep < TotalSteps;
    public bool IsLastStep => CurrentStep == TotalSteps;

    public MainWindowViewModel()
    {
        State = new MigrationState();

        var diagnostics = new DiagnosticsService();
        var steamLibrary = new SteamLibraryService();
        var protonDb = new ProtonDbClient();
        var appCatalog = new AppTranslationCatalog();
        var programScanner = new InstalledProgramScanner();

        AppTranslationModal = new AppTranslationModalViewModel(appCatalog, programScanner, () => IsAppTranslationModalVisible = false);
        GameCompatModal = new GameCompatModalViewModel(steamLibrary, protonDb, () => IsGameCompatModalVisible = false);

        Slides = new List<ViewModelBase>
        {
            new Slide1ViewModel(State),
            new Slide2ViewModel(State),
            new Slide3ViewModel(
                State,
                GameCompatModal,
                openAppDictionary: () => IsAppTranslationModalVisible = true,
                openGameCompat: () => IsGameCompatModalVisible = true),
        };

        _currentSlide = Slides[0];

        _ = InitializeDiagnosticsAsync(diagnostics);
    }

    private async Task InitializeDiagnosticsAsync(DiagnosticsService diagnostics)
    {
        var result = await Task.Run(diagnostics.DetectGpu);

        // Constructed on the UI thread, so this continuation resumes there too,
        // safe to set observable properties directly, no Dispatcher hop needed.
        State.DetectedGpu = result.Manufacturer;
        State.GpuDetails = result.Details;
        State.GpuDetectionError = result.Error;
        State.IsDiagnosticsComplete = true;
    }

    [RelayCommand]
    private void Next()
    {
        if (CurrentStep >= TotalSteps) return;
        IsNavigatingBack = false;
        CurrentStep++;
        CurrentSlide = Slides[CurrentStep - 1];
    }

    [RelayCommand]
    private void Back()
    {
        if (CurrentStep <= 1) return;
        IsNavigatingBack = true;
        CurrentStep--;
        CurrentSlide = Slides[CurrentStep - 1];
    }

    [RelayCommand]
    private void StartMigration()
    {
        // TODO: hand off to the payload-staging engine (Architecture.md §6-7) once it exists.
    }
}
