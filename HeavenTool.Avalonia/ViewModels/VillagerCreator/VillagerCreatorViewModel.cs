using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeavenTool.Avalonia.Settings;
using HeavenTool.Avalonia.Views;
using HeavenTool.ModManager.Villagers;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels.VillagerCreator;

/// <summary>
/// The villager creator: a wizard that starts from a vanilla villager and ends with NookKit patches that add a new one.
/// Each step edits the shared <see cref="Project"/>.
/// </summary>
public partial class VillagerCreatorViewModel : ObservableObject, IDisposable
{
    // The steps, in the order of the window's step list.
    public const int DonorStep = 0, IdentityStep = 1, TraitsStep = 2, HouseStep = 3, RoomStep = 4, AmiiboStep = 5, GenerateStep = 6;
    private const int StepCount = 7;

    private readonly IStorageProvider storage;

    public VillagerCreatorViewModel(IStorageProvider storage, ILauncher launcher)
    {
        this.storage = storage;
        Launcher = launcher;
        Donor = new(this);
        Identity = new(this);
        Traits = new(this);
        House = new(this);
        Room = new(this);
        Amiibo = new(this);
        Generate = new(this);
        AppSettings.Saved += OnSettingsSaved;
    }

    public DonorStepViewModel Donor { get; }
    public IdentityStepViewModel Identity { get; }
    public TraitsStepViewModel Traits { get; }
    public HouseStepViewModel House { get; }
    public RoomStepViewModel Room { get; }
    public AmiiboStepViewModel Amiibo { get; }
    public GenerateStepViewModel Generate { get; }

    internal IStorageProvider Storage => storage;
    internal ILauncher Launcher { get; }
    internal VillagerGameData Data { get; private set; } = null!;
    internal HouseCatalog Catalog { get; private set; } = null!;
    internal HousePreview Preview { get; private set; } = null!;
    internal RoomPreview RoomPreview { get; private set; } = null!;

    /// <summary>Shows an item picker as a dialog; true when an item was chosen. The window sets it.</summary>
    internal Func<ItemPickerViewModel, Task<bool>> PickItem { get; set; } = _ => Task.FromResult(false);

    /// <summary>The folder of the project file the villager was opened from, if any.</summary>
    internal string? ProjectFolder { get; private set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasProject)), NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private VillagerProject? project;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(StepText), nameof(IsLastStep))]
    [NotifyCanExecuteChangedFor(nameof(BackCommand), nameof(NextCommand))]
    private int stepIndex;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsReady))] private bool isLoading = true;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsReady))] private string? loadError;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsReady))] private bool needsRomfs;
    [ObservableProperty] private string title = "Villager Creator";

    public bool HasProject => Project != null;
    public bool IsReady => !IsLoading && !NeedsRomfs && LoadError == null;
    public bool IsLastStep => StepIndex == StepCount - 1;
    public string StepText => $"Step {StepIndex + 1} of {StepCount}";

    /// <summary>Reads the game files from the romfs in Settings.</summary>
    public async Task LoadAsync()
    {
        LoadError = null;
        var romfs = GameFiles.FindRomfs(AppSettings.Current.RomfsFolder);
        NeedsRomfs = romfs == null;
        if (romfs == null)
        {
            IsLoading = false;
            return;
        }

        IsLoading = true;
        try
        {
            // Warm what every step lists, so the steps open without a pause.
            var (data, catalog) = await Task.Run(() =>
            {
                var data = VillagerGameData.Open(romfs);
                _ = data.Villagers;
                _ = data.ItemNames;
                _ = data.AmiiboData;
                return (data, HouseCatalog.Load(data));
            });
            Data = data;
            Catalog = catalog;
            Preview = new HousePreview(romfs);
            RoomPreview = new RoomPreview(data);
            Donor.Initialize();
            Traits.Initialize();
            House.Initialize();
            Room.Initialize();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            LoadError = $"Couldn't read the game files: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OnSettingsSaved()
    {
        // A romfs set while the window waits for one is picked up straight away.
        if (NeedsRomfs && GameFiles.FindRomfs(AppSettings.Current.RomfsFolder) != null) _ = LoadAsync();
    }

    /// <summary>Shows a villager in every step, after one is started from a donor or opened from a file.</summary>
    internal void Open(VillagerProject value, string? folder)
    {
        ProjectFolder = folder;
        Project = value;
        Donor.Load(value);
        Identity.Load(value);
        Traits.Load(value);
        House.Load(value);
        Room.Load(value);
        Amiibo.Load(value);
        Generate.Load(value);
        UpdateTitle();
    }

    internal void UpdateTitle() =>
        Title = Project is { } value && VillagerGenerator.DefaultText(value.Name) is { Length: > 0 } name
            ? $"{name} ({value.Label}) — Villager Creator"
            : "Villager Creator";

    partial void OnStepIndexChanged(int value)
    {
        if (value == HouseStep) House.ShowPreview();
        if (value == RoomStep)
        {
            Room.Check();
            Room.ShowPreview();
        }
        if (value == GenerateStep) Generate.Check();
    }

    private bool CanGoBack() => StepIndex > 0;
    private bool CanGoNext() => HasProject && StepIndex < StepCount - 1;

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Back() => StepIndex--;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next() => StepIndex++;

    [RelayCommand]
    private async Task OpenProject()
    {
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open a villager",
            FileTypeFilter = [new("Villager Creator projects") { Patterns = ["*" + VillagerProject.Extension] }],
        });
        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path) return;
        try
        {
            var value = VillagerProject.Load(path);
            if (!Data.Villagers.Any(villager => villager.Label == value.Donor))
                throw new InvalidDataException($"It's based on {value.Donor}, which isn't in this romfs.");
            Open(value, Path.GetDirectoryName(path));
            StepIndex = IdentityStep;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            Donor.Error = $"Couldn't open {Path.GetFileName(path)}: {ex.Message}";
        }
    }

    [RelayCommand]
    private static void OpenSettings() => SettingsWindow.ShowSingle();

    [RelayCommand]
    private Task Retry() => LoadAsync();

    public void Dispose()
    {
        AppSettings.Saved -= OnSettingsSaved;
        House.Dispose();
        Room.Dispose();
    }
}
