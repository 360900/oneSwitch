using System;
using System.Collections.Generic;
using System.IO;
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

    // Where StartMigration writes the generated kargo.sh for inspection/testing. §6
    // Payload Staging (not yet implemented) will consume the same FirstBootPayloadBuilder
    // output directly instead of a file on disk - this path exists for the interim.
    public string KargoScriptOutputPath { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "kargo.sh");

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

        AppTranslationModal = new AppTranslationModalViewModel(appCatalog, programScanner, State, () => IsAppTranslationModalVisible = false);
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
        var script = FirstBootPayloadBuilder.Build(State, AppTranslationModal.Entries);

        try
        {
            File.WriteAllText(KargoScriptOutputPath, script);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Inspection-only output path unwritable (e.g. no Desktop folder on this dev
            // box) - not fatal, §6 Payload Staging will consume the builder output
            // directly once it exists instead of relying on this file.
        }

        // Unattended install-config (preseed/kickstart/archinstall) is generated next to
        // kargo.sh when supported. Bazzite/Mint return Content=null with a reason; the
        // UnsupportedReason has no UI surface today (Slide 3 doesn't have a place for
        // it), so unsupported cases just skip writing - deliberate rather than inventing
        // new UI beyond what was asked.
        var unattended = UnattendedScriptFactory.Build(State, script);
        if (unattended.Content is not null)
        {
            var unattendedPath = Path.Combine(
                Path.GetDirectoryName(KargoScriptOutputPath) ?? string.Empty,
                unattended.FileName);
            try
            {
                File.WriteAllText(unattendedPath, unattended.Content);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Same best-effort semantics as the kargo.sh write above.
            }
        }
    }
}
