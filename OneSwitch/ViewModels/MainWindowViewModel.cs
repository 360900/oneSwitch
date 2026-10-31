using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OneSwitch.Models;
using OneSwitch.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OneSwitch.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const int TotalSteps = 3;
    private const string ScriptFileName = "oneswitch.sh";

    public MigrationState State { get; }

    // Interim output location until §6 Payload Staging consumes the builder output directly.
    public string OutputDirectory { get; set; } = DefaultOutputDirectory();

    public IReadOnlyList<ViewModelBase> Slides { get; }

    public AppTranslationModalViewModel AppTranslationModal { get; }
    public GameCompatModalViewModel GameCompatModal { get; }
    public MigrationResultViewModel Result { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(IsLastStep))]
    [NotifyPropertyChangedFor(nameof(IsStep1))]
    [NotifyPropertyChangedFor(nameof(IsStep2))]
    [NotifyPropertyChangedFor(nameof(IsStep3))]
    private int _currentStep = 1;

    [ObservableProperty]
    private ViewModelBase _currentSlide;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDialogOpen))]
    private bool _isAppTranslationModalVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDialogOpen))]
    private bool _isGameCompatModalVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDialogOpen))]
    private bool _isResultVisible;

    [ObservableProperty]
    private bool _isNavigatingBack;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartMigrationCommand))]
    private bool _isBusy;

    public bool CanGoBack => CurrentStep > 1;
    public bool IsLastStep => CurrentStep == TotalSteps;
    public bool IsStep1 => CurrentStep >= 1;
    public bool IsStep2 => CurrentStep >= 2;
    public bool IsStep3 => CurrentStep >= 3;
    public bool IsDialogOpen => IsAppTranslationModalVisible || IsGameCompatModalVisible || IsResultVisible;

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
                AppTranslationModal,
                GameCompatModal,
                openAppDictionary: () => IsAppTranslationModalVisible = true,
                openGameCompat: () => IsGameCompatModalVisible = true),
        };

        _currentSlide = Slides[0];

        _ = InitializeDiagnosticsAsync(diagnostics);
    }

    private static string DefaultOutputDirectory()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var root = !string.IsNullOrEmpty(desktop) && Directory.Exists(desktop)
            ? desktop
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(root, "oneSwitch");
    }

    private async Task InitializeDiagnosticsAsync(DiagnosticsService diagnostics)
    {
        var result = await Task.Run(diagnostics.DetectGpu);

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

    private bool CanStartMigration() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanStartMigration))]
    private async Task StartMigration()
    {
        IsBusy = true;
        try
        {
            var entries = AppTranslationModal.Entries.ToList();
            var outputDirectory = OutputDirectory;
            var distro = DistroResolver.ResolveTargetDistro(State);

            var script = FirstBootPayloadBuilder.Build(State, entries);
            var unattended = UnattendedScriptFactory.Build(State, script);

            var written = new List<string>();
            string? error = null;

            await Task.Run(() =>
            {
                try
                {
                    Directory.CreateDirectory(outputDirectory);

                    var scriptPath = Path.Combine(outputDirectory, ScriptFileName);
                    File.WriteAllText(scriptPath, script);
                    written.Add(scriptPath);

                    if (unattended.Content is not null)
                    {
                        var unattendedPath = Path.Combine(outputDirectory, unattended.FileName);
                        File.WriteAllText(unattendedPath, unattended.Content);
                        written.Add(unattendedPath);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    error = ex.Message;
                }
            });

            var notes = new List<string>();
            if (error is null)
            {
                notes.Add(UsageNote(distro, unattended));

                var needsAur = distro == TargetDistro.Arch && entries.Any(e =>
                    e.IsIncluded && e.AurPackage is not null && !e.AurIsOfficialRepo);
                if (needsAur)
                    notes.Add("Some apps come from the AUR. After your first login, install the ones listed in /var/lib/oneswitch/aur-pending.txt with yay or paru.");
            }

            Result.Show(outputDirectory, written, notes, error);
            IsResultVisible = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string UsageNote(TargetDistro distro, UnattendedScriptFactory.UnattendedInstallScript unattended)
    {
        if (unattended.Content is null)
            return $"{unattended.UnsupportedReason} After installing {DistroResolver.DisplayName(distro)}, run {ScriptFileName} once with sudo.";

        return distro switch
        {
            TargetDistro.Debian => $"Boot the Debian installer with {unattended.FileName} as its preseed file. {ScriptFileName} runs once on first boot.",
            TargetDistro.Fedora => $"Boot the Fedora installer with inst.ks pointing to {unattended.FileName}. {ScriptFileName} runs once on first boot.",
            TargetDistro.Arch => $"From the Arch ISO, run archinstall --config {unattended.FileName}, then pick a disk and create your account.",
            _ => $"Use {unattended.FileName} with your installer. {ScriptFileName} runs once on first boot.",
        };
    }

    [RelayCommand]
    private void CloseResult() => IsResultVisible = false;

    [RelayCommand]
    private void OpenOutputFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Result.OutputDirectory) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }

    // Esc closes whichever dialog is on top.
    public void CloseTopDialog()
    {
        if (IsResultVisible) IsResultVisible = false;
        else if (IsAppTranslationModalVisible) IsAppTranslationModalVisible = false;
        else if (IsGameCompatModalVisible) IsGameCompatModalVisible = false;
    }
}
