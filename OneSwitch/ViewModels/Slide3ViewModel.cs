using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using OneSwitch.Models;
using OneSwitch.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OneSwitch.ViewModels;

public partial class Slide3ViewModel : ViewModelBase
{
    private readonly MigrationState _state;
    private readonly AppTranslationModalViewModel _apps;
    private readonly GameCompatModalViewModel _gameCompat;

    public SlideHeaderViewModel Header { get; } = new(
        "Ready to switch",
        "Review your setup, then create your migration files.");

    public ObservableCollection<ISelectableOption> BackupOptions { get; }

    public IRelayCommand OpenAppDictionaryCommand { get; }
    public IRelayCommand OpenGameCompatCommand { get; }

    [ObservableProperty]
    private string _systemTitle = string.Empty;

    [ObservableProperty]
    private string _systemSummary = string.Empty;

    [ObservableProperty]
    private string _appsSummaryText = string.Empty;

    [ObservableProperty]
    private string _gameCompatSummaryText = string.Empty;

    [ObservableProperty]
    private string _gpuStatusText = "Checking your hardware…";

    [ObservableProperty]
    private string _gpuIconKey = "Icon.Display";

    [ObservableProperty]
    private bool _isGpuSupported;

    [ObservableProperty]
    private bool _isGpuWarning;

    public Slide3ViewModel(MigrationState state, AppTranslationModalViewModel apps, GameCompatModalViewModel gameCompat,
        Action openAppDictionary, Action openGameCompat)
    {
        _state = state;
        _apps = apps;
        _gameCompat = gameCompat;
        OpenAppDictionaryCommand = new RelayCommand(openAppDictionary);
        OpenGameCompatCommand = new RelayCommand(openGameCompat);

        BackupOptions = new ObservableCollection<ISelectableOption>
        {
            new SelectableOption<BackupMethod>(Select)
            {
                Value = BackupMethod.Rclone,
                Title = "Cloud provider",
                Description = "Transfer your files over Rclone. Fully automated.",
                IconKey = "Icon.Cloud",
            },
            new SelectableOption<BackupMethod>(Select)
            {
                Value = BackupMethod.ExternalDrive,
                Title = "External drive",
                Description = "Transfer your files locally. Needs enough free space.",
                IconKey = "Icon.Drive",
            },
            new SelectableOption<BackupMethod>(Select)
            {
                Value = BackupMethod.None,
                Title = "Skip for now",
                Description = "Continue without transferring any files.",
                IconKey = "Icon.Unavailable",
            },
        };
        SyncBackupSelection();

        _state.PropertyChanged += OnStateChanged;
        _apps.PropertyChanged += (_, _) => UpdateAppsSummary();
        _gameCompat.PropertyChanged += OnGameCompatChanged;
        UpdateSystemSummary();
        UpdateAppsSummary();
        UpdateGameCompatSummary();
        UpdateGpuStatus();
    }

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MigrationState.SelectedRole) or nameof(MigrationState.SelectedParadigm):
                UpdateSystemSummary();
                break;
            case nameof(MigrationState.DetectedGpu) or nameof(MigrationState.GpuDetectionError)
                or nameof(MigrationState.IsDiagnosticsComplete):
                UpdateGpuStatus();
                break;
        }
    }

    private void OnGameCompatChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GameCompatModalViewModel.IsLoading)
            or nameof(GameCompatModalViewModel.LookupUnavailable))
        {
            UpdateGameCompatSummary();
        }
    }

    private void UpdateSystemSummary()
    {
        var distro = DistroResolver.ResolveTargetDistro(_state);
        var desktop = DistroResolver.ResolveDesktopEnvironment(_state);
        var layout = _state.SelectedParadigm == DesktopParadigm.MacOS ? "centered dock" : "bottom taskbar";
        SystemTitle = DistroResolver.DisplayName(distro);
        SystemSummary = $"{DistroResolver.DisplayName(desktop)} with a {layout}";
    }

    private void UpdateAppsSummary()
    {
        AppsSummaryText = _apps.IsLoading ? "Looking for installed apps…"
            : _apps.Entries.Count == 0 ? "No familiar apps were found on this PC."
            : $"{_apps.IncludedCount} of {_apps.Entries.Count} apps will be installed.";
    }

    private void UpdateGameCompatSummary()
    {
        if (_gameCompat.IsLoading)
        {
            GameCompatSummaryText = "Checking game compatibility…";
            return;
        }

        if (_gameCompat.LookupUnavailable)
        {
            GameCompatSummaryText = "Couldn't reach ProtonDB. Compatibility unknown.";
            return;
        }

        var runningWell = _gameCompat.RunsPerfectly.Count + _gameCompat.RunsFine.Count;
        var total = runningWell + _gameCompat.WontRun.Count + _gameCompat.UnknownTier.Count;

        if (total == 0)
        {
            GameCompatSummaryText = "No Steam library was found on this PC.";
            return;
        }

        var percent = (int)Math.Round(runningWell * 100.0 / total);
        GameCompatSummaryText = $"{percent}% of your {total} games run well on Linux.";
    }

    private void Select(BackupMethod value)
    {
        _state.SelectedBackupMethod = value;
        SyncBackupSelection();
    }

    private void SyncBackupSelection()
    {
        foreach (var option in BackupOptions.Cast<SelectableOption<BackupMethod>>())
            option.IsSelected = option.Value == _state.SelectedBackupMethod;
    }

    private void UpdateGpuStatus()
    {
        IsGpuSupported = false;
        IsGpuWarning = false;
        GpuIconKey = "Icon.Display";

        if (!_state.IsDiagnosticsComplete)
        {
            GpuStatusText = "Checking your hardware…";
            return;
        }

        if (_state.GpuDetectionError != null)
        {
            GpuStatusText = _state.GpuDetectionError;
            GpuIconKey = "Icon.Warning";
            IsGpuWarning = true;
            return;
        }

        if (_state.DetectedGpu == GpuManufacturer.Unknown)
        {
            GpuStatusText = "We couldn't detect your GPU. You can still continue.";
            GpuIconKey = "Icon.Info";
            return;
        }

        GpuStatusText = _state.DetectedGpu switch
        {
            GpuManufacturer.Nvidia => "Your NVIDIA GPU is supported.",
            GpuManufacturer.AMD => "Your AMD GPU is supported.",
            GpuManufacturer.Intel => "Your Intel GPU is supported.",
            _ => "Your GPU is supported.",
        };
        GpuIconKey = "Icon.Check";
        IsGpuSupported = true;
    }
}
