using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeavenTool.ModManager.Villagers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels.VillagerCreator;

/// <summary>The last step: checks the project and writes its NookKit patches, amiibo and project file.</summary>
public partial class GenerateStepViewModel(VillagerCreatorViewModel creator) : ObservableObject
{
    private VillagerProject? project;
    private string? defaultFolder;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    public partial string OutputFolder { get; set; } = "";

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsValid)), NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    public partial IReadOnlyList<string> Problems { get; set; } = [];

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    public partial bool IsGenerating { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasFiles))]
    public partial IReadOnlyList<GeneratedFile> Files { get; set; } = [];

    [ObservableProperty]
    public partial string? Error { get; set; }

    [ObservableProperty]
    public partial string ModelPath { get; set; } = "";

    public bool IsValid => Problems.Count == 0;
    public bool HasFiles => Files.Count > 0;

    internal void Load(VillagerProject value)
    {
        project = value;
        defaultFolder = DefaultFolder(value.Label);
        OutputFolder = creator.ProjectFolder ?? defaultFolder;
        Files = [];
        Error = null;
    }

    /// <summary>Checks the project again, since every step before this one can change it.</summary>
    internal void Check()
    {
        if (project == null) return;
        Problems = VillagerGenerator.Validate(creator.Data, project);
        ModelPath = $"romfs/Model/{VillagerLabel.ModelName(project.Label)}.Nin_NX_NVN.zs";
        // The default folder is named after the label, so it follows a label changed since; a chosen folder stays.
        if (OutputFolder == defaultFolder) OutputFolder = defaultFolder = DefaultFolder(project.Label);
    }

    private static string DefaultFolder(string label) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Heaven Tool", "Villagers", label);

    [RelayCommand]
    private async Task Browse()
    {
        try
        {
            var start = Directory.Exists(OutputFolder) ? await creator.Storage.TryGetFolderFromPathAsync(OutputFolder) : null;
            var folders = await creator.Storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose where to write the villager",
                SuggestedStartLocation = start,
            });
            if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path) OutputFolder = path;
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    private bool CanGenerate() => !IsGenerating && IsValid && OutputFolder.Length > 0;

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task Generate()
    {
        if (project == null) return;
        IsGenerating = true;
        Error = null;
        try
        {
            var (data, value, output) = (creator.Data, project, OutputFolder);
            Files = await Task.Run(() => VillagerGenerator.Generate(data, value, output));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException)
        {
            Error = $"Couldn't write the villager: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task OpenFolder()
    {
        if (!Directory.Exists(OutputFolder)) return;
        try { await creator.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(OutputFolder)); }
        catch (Exception ex) { Error = $"Couldn't open {OutputFolder}: {ex.Message}"; }
    }
}
