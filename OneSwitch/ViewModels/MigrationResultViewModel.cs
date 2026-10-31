using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneSwitch.ViewModels;

public partial class MigrationResultViewModel : ViewModelBase
{
    public ObservableCollection<string> Files { get; } = new();
    public ObservableCollection<string> Notes { get; } = new();

    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _isError;

    public void Show(string outputDirectory, IReadOnlyList<string> files, IReadOnlyList<string> notes, string? error)
    {
        OutputDirectory = outputDirectory;
        IsError = error is not null;
        Title = IsError ? "Couldn't save files" : "Migration files created";
        Message = error ?? $"Saved to {outputDirectory}";

        Files.Clear();
        foreach (var file in files) Files.Add(Path.GetFileName(file));

        Notes.Clear();
        foreach (var note in notes) Notes.Add(note);
    }
}
