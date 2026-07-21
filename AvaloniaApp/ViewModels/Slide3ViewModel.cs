using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using AvaloniaApp.Models;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;

namespace AvaloniaApp.ViewModels;

public partial class Slide3ViewModel : ViewModelBase
{
    private readonly MigrationState _state;
    private readonly GameCompatModalViewModel _gameCompat;

    public SlideHeaderViewModel Header { get; } = new(
        MaterialIconKind.ClipboardCheck,
        "Ready for Deployment",
        "Review your settings and finalize the migration.");

    public ObservableCollection<ISelectableOption> BackupOptions { get; }
    public IReadOnlyList<OptionRow> BackupOptionRows { get; }
    public SelectableOption<BackupMethod> SkipOption { get; }

    public IRelayCommand OpenAppDictionaryCommand { get; }
    public IRelayCommand OpenGameCompatCommand { get; }

    [ObservableProperty]
    private string _gameCompatSummaryText = string.Empty;

    [ObservableProperty]
    private string _gpuStatusText = "Checking your hardware…";

    [ObservableProperty]
    private MaterialIconKind _gpuStatusIconKind = MaterialIconKind.ProgressClock;

    [ObservableProperty]
    private IBrush _gpuStatusColor = Brush.Parse("#64748B");

    public Slide3ViewModel(MigrationState state, GameCompatModalViewModel gameCompat, Action openAppDictionary, Action openGameCompat)
    {
        _state = state;
        _gameCompat = gameCompat;
        OpenAppDictionaryCommand = new RelayCommand(openAppDictionary);
        OpenGameCompatCommand = new RelayCommand(openGameCompat);
        UpdateGameCompatSummary();

        SkipOption = new SelectableOption<BackupMethod>(Select)
        {
            Value = BackupMethod.None,
            Title = "Skip for Now",
            Description = "Continue without transferring any files.",
            IconKind = MaterialIconKind.CloseCircleOutline,
        };

        BackupOptions = new ObservableCollection<ISelectableOption>
        {
            new SelectableOption<BackupMethod>(Select)
            {
                Value = BackupMethod.Rclone,
                Title = "Cloud Provider",
                Description = "Transfer your files over Rclone. Fully automated.",
                IconKind = MaterialIconKind.Cloud,
            },
            new SelectableOption<BackupMethod>(Select)
            {
                Value = BackupMethod.ExternalDrive,
                Title = "External Drive",
                Description = "Transfer your files locally. Might require large amounts of free space.",
                IconKind = MaterialIconKind.Harddisk,
            },
            SkipOption,
        };
        BackupOptionRows = OptionRow.Chunk(BackupOptions.Take(2).ToList());
        SyncBackupSelection();

        _state.PropertyChanged += OnStateChanged;
        _gameCompat.PropertyChanged += OnGameCompatChanged;
        UpdateGpuStatus();
    }

    private void OnGameCompatChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The ProtonDB lookups finish after construction; refresh the summary when
        // they land (or when we learn they never will, e.g. no network).
        if (e.PropertyName is nameof(GameCompatModalViewModel.IsLoading)
            or nameof(GameCompatModalViewModel.LookupUnavailable))
        {
            UpdateGameCompatSummary();
        }
    }

    private void UpdateGameCompatSummary()
    {
        if (_gameCompat.IsLoading)
        {
            GameCompatSummaryText = "Checking ProtonDB compatibility…";
            return;
        }

        if (_gameCompat.LookupUnavailable)
        {
            GameCompatSummaryText = "Couldn't reach ProtonDB - compatibility unknown.";
            return;
        }

        var runningWell = _gameCompat.RunsPerfectly.Count + _gameCompat.RunsFine.Count;
        var total = runningWell + _gameCompat.WontRun.Count + _gameCompat.UnknownTier.Count;

        if (total == 0)
        {
            GameCompatSummaryText = "No Steam library was detected on this PC.";
            return;
        }

        var percent = (int)Math.Round(runningWell * 100.0 / total);
        GameCompatSummaryText = $"Your game library is {percent}% compatible.";
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

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MigrationState.DetectedGpu)
            or nameof(MigrationState.GpuDetectionError)
            or nameof(MigrationState.IsDiagnosticsComplete))
        {
            UpdateGpuStatus();
        }
    }

    private void UpdateGpuStatus()
    {
        if (!_state.IsDiagnosticsComplete)
        {
            GpuStatusText = "Checking your hardware…";
            GpuStatusIconKind = MaterialIconKind.ProgressClock;
            GpuStatusColor = Brush.Parse("#64748B");
            return;
        }

        if (_state.GpuDetectionError != null)
        {
            GpuStatusText = _state.GpuDetectionError;
            GpuStatusIconKind = MaterialIconKind.AlertCircle;
            GpuStatusColor = Brush.Parse("#B45309");
            return;
        }

        if (_state.DetectedGpu == GpuManufacturer.Unknown)
        {
            GpuStatusText = "We couldn't detect your GPU automatically. You can still continue.";
            GpuStatusIconKind = MaterialIconKind.InformationOutline;
            GpuStatusColor = Brush.Parse("#64748B");
            return;
        }

        GpuStatusText = _state.DetectedGpu switch
        {
            GpuManufacturer.Nvidia => "Your NVIDIA GPU is supported.",
            GpuManufacturer.AMD => "Your AMD GPU is supported.",
            GpuManufacturer.Intel => "Your Intel GPU is supported.",
            _ => "Your GPU is supported.",
        };
        GpuStatusIconKind = MaterialIconKind.CheckCircle;
        GpuStatusColor = Brush.Parse("#16A34A");
    }
}
