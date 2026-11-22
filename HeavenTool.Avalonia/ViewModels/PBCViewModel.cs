using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeavenTool.Avalonia.FileTypes.PBC;
using HeavenTool.Avalonia.Settings;
using HeavenTool.IO.AcnhEnvironment;
using HeavenTool.IO.FileFormats.BFRES;
using HeavenTool.Rendering.BFRES;
using HeavenTool.Shaders;
using HeavenTool.IO.FileFormats.PBC;
using HeavenTool.IO.UndoSystem;
using HeavenTool.Rendering.PBC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels;

/// <summary>What the PBC view model asks of its window.</summary>
public interface IPBCView
{
    IStorageProvider StorageProvider { get; }

    /// <summary>Asks whether to save first: true to save, false to discard, null to cancel.</summary>
    Task<bool?> ConfirmSaveChangesAsync();
}

/// <summary>One PBC of the open file, with its own edit history.</summary>
public sealed class PbcEntry(string name, PBCFileReader file)
{
    public string Name { get; } = name;
    public PBCFileReader File { get; } = file;
    public UndoManager History { get; } = new();
    public override string ToString() => Name;
}

public sealed record PbcMaterialItem(TileType Type, string Name, byte Id, IBrush Swatch)
{
    public string IdText => Id.ToString();
}

public partial class PBCViewModel : ObservableObject
{
    private readonly IPBCView view;
    private readonly UndoManager noHistory = new();
    private PbcContainer? container;
    private UndoManager undoManager => SelectedEntry?.History ?? noHistory;
    private IStorageFile? sourceFile;
    private string? localPath;
    private string fileName = "PBC Editor";
    // Set while the inspector is filled from the file, so its edits aren't recorded again.
    private bool refreshingInspector;

    public PBCViewModel(IPBCView view)
    {
        this.view = view;
        Materials = [.. Enum.GetValues<TileType>().Select(type => new PbcMaterialItem(type, PbcGeometry.MaterialName(type), (byte)type,
            new SolidColorBrush(Color.FromUInt32(PbcGeometry.MaterialColor(type)))))];
        SelectedMaterial = Materials.FirstOrDefault(item => item.Type == TileType.Grass);
    }

    public IReadOnlyList<PbcMaterialItem> Materials { get; }

    /// <summary>The PBCs of the open file: one for a .pbc, every .pbc entry for a SARC archive.</summary>
    [ObservableProperty] public partial IReadOnlyList<PbcEntry> Entries { get; private set; } = [];
    [ObservableProperty] public partial PbcEntry? SelectedEntry { get; set; }
    public bool HasMultipleEntries => Entries.Count > 1;

    partial void OnEntriesChanged(IReadOnlyList<PbcEntry> value) => OnPropertyChanged(nameof(HasMultipleEntries));

    partial void OnSelectedEntryChanged(PbcEntry? value)
    {
        Selection = null;
        File = value?.File;
        RefreshInspector();
        RefreshCommands();
        FileReplaced?.Invoke();
        if (value != null)
            StatusText = $"{(Entries.Count > 1 ? value.Name + " · " : "")}{value.File.Width} × {value.File.Height} tiles · offset {value.File.OffsetX}, {value.File.OffsetZ}";
        _ = LoadModelAsync(value == null ? null : PbcModelLocator.Find(value.Name, fileName, localPath),
            value == null ? null : PbcModelLocator.ModelName(value.Name));
    }

    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        HasUnsavedChanges = Entries.Any(entry => !entry.History.IsAtSavePoint);
        RefreshCommands();
    }

    /// <summary>Raised when the file's tiles change in any way (edits, undo, resize, load).</summary>
    public event Action? TilesChanged;
    /// <summary>Raised when a different file is loaded or the file is closed.</summary>
    public event Action? FileReplaced;

    [ObservableProperty] public partial PBCFileReader? File { get; private set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial bool HasUnsavedChanges { get; set; }
    [ObservableProperty] public partial string Title { get; set; } = "PBC Editor — Heaven Tool";
    [ObservableProperty] public partial string StatusText { get; set; } = "Open a PBC file to begin.";
    [ObservableProperty] public partial string HoverText { get; set; } = "";
    [ObservableProperty] public partial string? ErrorMessage { get; set; }

    [ObservableProperty] public partial PbcTool Tool { get; set; } = PbcTool.Select;
    /// <summary>The paint and height tools' reach: 1 is a single triangle or point, larger sizes add a circle of (size - 1) half tiles.</summary>
    [ObservableProperty] public partial int BrushSize { get; set; } = 1;
    /// <summary>How far one raise or lower stroke moves each height.</summary>
    [ObservableProperty] public partial decimal? HeightStep { get; set; } = 1;
    /// <summary>Whether the height tools move whole triangles instead of map points.</summary>
    [ObservableProperty] public partial bool HeightFaces { get; set; }
    [ObservableProperty] public partial PbcMaterialItem? SelectedMaterial { get; set; }
    [ObservableProperty] public partial TilePosition? Selection { get; set; }

    // Inspector
    [ObservableProperty] public partial decimal? MapWidth { get; set; }
    [ObservableProperty] public partial decimal? MapHeight { get; set; }
    [ObservableProperty] public partial PbcMaterialItem? TriangleMaterial { get; set; }
    [ObservableProperty] public partial decimal? CenterHeight { get; set; }
    [ObservableProperty] public partial decimal? Corner1Height { get; set; }
    [ObservableProperty] public partial decimal? Corner2Height { get; set; }
    [ObservableProperty] public partial string Corner1Label { get; set; } = "Corner 1";
    [ObservableProperty] public partial string Corner2Label { get; set; } = "Corner 2";
    [ObservableProperty] public partial string SelectionText { get; set; } = "Nothing selected";

    public bool IsFileLoaded => File != null;
    public bool HasSelection => Selection != null && File != null;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public string OffsetText => File is { } file ? $"{file.OffsetX}, {file.OffsetZ}" : "";
    public TileType Brush => SelectedMaterial?.Type ?? TileType.Grass;

    public bool IsSelectTool { get => Tool == PbcTool.Select; set { if (value) Tool = PbcTool.Select; else OnPropertyChanged(); } }
    public bool IsPaintTool { get => Tool == PbcTool.Paint; set { if (value) Tool = PbcTool.Paint; else OnPropertyChanged(); } }
    public bool IsFillTool { get => Tool == PbcTool.Fill; set { if (value) Tool = PbcTool.Fill; else OnPropertyChanged(); } }

    public bool IsRaiseTool { get => Tool == PbcTool.Raise; set { if (value) Tool = PbcTool.Raise; else OnPropertyChanged(); } }
    public bool IsLowerTool { get => Tool == PbcTool.Lower; set { if (value) Tool = PbcTool.Lower; else OnPropertyChanged(); } }
    public bool IsFlattenTool { get => Tool == PbcTool.Flatten; set { if (value) Tool = PbcTool.Flatten; else OnPropertyChanged(); } }
    public bool UsesBrushSize => Tool is PbcTool.Paint or PbcTool.Raise or PbcTool.Lower or PbcTool.Flatten;
    public bool UsesHeightStep => Tool is PbcTool.Raise or PbcTool.Lower;
    public bool UsesHeightMode => Tool is PbcTool.Raise or PbcTool.Lower or PbcTool.Flatten;
    public bool IsPointMode { get => !HeightFaces; set { if (value) HeightFaces = false; else OnPropertyChanged(); } }
    public bool IsFaceMode { get => HeightFaces; set { if (value) HeightFaces = true; else OnPropertyChanged(); } }

    partial void OnHeightFacesChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPointMode));
        OnPropertyChanged(nameof(IsFaceMode));
    }

    partial void OnFileChanged(PBCFileReader? value)
    {
        OnPropertyChanged(nameof(IsFileLoaded));
        OnPropertyChanged(nameof(OffsetText));
        RefreshCommands();
    }

    partial void OnToolChanged(PbcTool value)
    {
        OnPropertyChanged(nameof(IsSelectTool));
        OnPropertyChanged(nameof(IsPaintTool));
        OnPropertyChanged(nameof(IsFillTool));
        OnPropertyChanged(nameof(IsRaiseTool));
        OnPropertyChanged(nameof(IsLowerTool));
        OnPropertyChanged(nameof(IsFlattenTool));
        OnPropertyChanged(nameof(UsesBrushSize));
        OnPropertyChanged(nameof(UsesHeightStep));
        OnPropertyChanged(nameof(UsesHeightMode));
    }

    partial void OnSelectedMaterialChanged(PbcMaterialItem? value) => OnPropertyChanged(nameof(Brush));
    partial void OnSelectionChanged(TilePosition? value) => RefreshInspector();
    partial void OnHasUnsavedChangesChanged(bool value) => UpdateTitle();
    partial void OnIsBusyChanged(bool value) => RefreshCommands();
    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    private void UpdateTitle() => Title = $"{fileName}{(HasUnsavedChanges ? " *" : "")} — Heaven Tool";
    private bool CanOpen() => !IsBusy;
    private bool CanEdit() => IsFileLoaded && !IsBusy;
    private bool CanSaveFile() => CanEdit() && HasUnsavedChanges;
    private bool CanUndo() => CanEdit() && undoManager.CanUndo;
    private bool CanRedo() => CanEdit() && undoManager.CanRedo;

    private void RefreshCommands()
    {
        OpenFileCommand.NotifyCanExecuteChanged();
        SaveFileCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
        CloseFileCommand.NotifyCanExecuteChanged();
        ChooseModelCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    // ----- Editing -----

    /// <summary>Records a paint stroke or fill the 3D view already applied.</summary>
    public void RecordStroke(IReadOnlyList<TileChange> changes)
    {
        if (File == null || changes.Count == 0) return;
        undoManager.Execute(new TileEditCommand(File, changes), skipFirstRedo: true);
        StatusText = changes.Count == 1 ? $"Painted 1 triangle with {Brush}" : $"Painted {changes.Count:N0} triangles with {Brush}";
        RefreshInspector();
        TilesChanged?.Invoke();
    }

    /// <summary>Records a raise, lower or flatten stroke the 3D view already applied.</summary>
    public void RecordHeights(IReadOnlyList<HeightChange> changes)
    {
        if (File == null || changes.Count == 0) return;
        undoManager.Execute(new HeightEditCommand(File, changes), skipFirstRedo: true);
        string verb = Tool switch { PbcTool.Raise => "Raised", PbcTool.Lower => "Lowered", _ => "Flattened" };
        StatusText = changes.Count == 1 ? $"{verb} 1 height" : $"{verb} {changes.Count:N0} heights";
        RefreshInspector();
        TilesChanged?.Invoke();
    }

    private void Execute(IUndoCommand command, string status)
    {
        undoManager.Execute(command);
        StatusText = status;
        RefreshInspector();
        TilesChanged?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => StepHistory(undo: true);

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => StepHistory(undo: false);

    private void StepHistory(bool undo)
    {
        if ((undo ? undoManager.Undo() : undoManager.Redo()) == null) return;
        StatusText = undo ? "Undone" : "Redone";
        // A resize may have cut the selected tile away.
        if (Selection is { } selected && (selected.TileX >= File!.Width || selected.TileZ >= File.Height)) Selection = null;
        RefreshInspector();
        TilesChanged?.Invoke();
    }

    partial void OnTriangleMaterialChanged(PbcMaterialItem? value)
    {
        if (refreshingInspector || value == null || File == null || Selection is not { } position) return;
        var old = File[position.TileZ, position.TileX].Materials[(int)position.Triangle];
        if (old == value.Type) return;
        Execute(new TileEditCommand(File, [new TileChange(position, old, value.Type)]), $"Set material to {value.Name}");
    }

    partial void OnCenterHeightChanged(decimal? value) => SetHeight(0, value);
    partial void OnCorner1HeightChanged(decimal? value) => SetHeight(1, value);
    partial void OnCorner2HeightChanged(decimal? value) => SetHeight(2, value);

    private void SetHeight(int corner, decimal? value)
    {
        if (refreshingInspector || value == null || File == null || Selection is not { } position) return;
        var heights = File[position.TileZ, position.TileX].HeightMap.Triangles[(int)position.Triangle];
        float old = corner switch { 0 => heights.CenterHeight, 1 => heights.Corner1Height, _ => heights.Corner2Height };
        float now = (float)value.Value;
        if (old == now) return;
        string name = corner == 0 ? "center" : PbcGeometry.CornerName(position.Triangle, corner);
        Execute(new TriangleHeightCommand(File, position, corner, old, now), $"Set {name} height to {now:0.###}");
    }

    partial void OnMapWidthChanged(decimal? value) => Resize((int)(value ?? 0), File?.Height ?? 0);
    partial void OnMapHeightChanged(decimal? value) => Resize(File?.Width ?? 0, (int)(value ?? 0));

    private void Resize(int width, int height)
    {
        if (refreshingInspector || File is not { } file || width < 1 || height < 1 || width == file.Width && height == file.Height) return;
        var oldTiles = file.Tiles;
        var newTiles = new PBCFileReader.Tile[height, width];
        for (int z = 0; z < height; z++)
            for (int x = 0; x < width; x++)
                newTiles[z, x] = z < file.Height && x < file.Width ? oldTiles[z, x] : new PBCFileReader.Tile();
        if (Selection is { } selected && (selected.TileX >= width || selected.TileZ >= height)) Selection = null;
        Execute(new ResizeCommand(file, oldTiles, newTiles), $"Resized to {width} × {height}");
    }

    public void RefreshInspector()
    {
        refreshingInspector = true;
        try
        {
            MapWidth = File?.Width;
            MapHeight = File?.Height;
            if (File is { } file && Selection is { } position && position.TileX < file.Width && position.TileZ < file.Height)
            {
                var tile = file[position.TileZ, position.TileX];
                var heights = tile.HeightMap.Triangles[(int)position.Triangle];
                TriangleMaterial = Materials.FirstOrDefault(item => item.Type == tile.Materials[(int)position.Triangle]);
                CenterHeight = (decimal)heights.CenterHeight;
                Corner1Height = (decimal)heights.Corner1Height;
                Corner2Height = (decimal)heights.Corner2Height;
                Corner1Label = $"Corner 1 ({PbcGeometry.CornerName(position.Triangle, 1)})";
                Corner2Label = $"Corner 2 ({PbcGeometry.CornerName(position.Triangle, 2)})";
                SelectionText = $"Tile {position.TileX}, {position.TileZ} · {position.Triangle} triangle"
                    + (PbcGeometry.IsValid(heights) ? "" : " · void");
            }
            else
            {
                TriangleMaterial = null;
                CenterHeight = Corner1Height = Corner2Height = null;
                Corner1Label = "Corner 1";
                Corner2Label = "Corner 2";
                SelectionText = "Nothing selected";
            }
        }
        finally { refreshingInspector = false; }
        OnPropertyChanged(nameof(HasSelection));
    }

    public void SetHover(TilePosition? position)
    {
        if (File is not { } file || position is not { } at) { HoverText = ""; return; }
        var tile = file[at.TileZ, at.TileX];
        var heights = tile.HeightMap.Triangles[(int)at.Triangle];
        var material = tile.Materials[(int)at.Triangle];
        string height = PbcGeometry.IsValid(heights)
            ? $"{(heights.CenterHeight + heights.Corner1Height + heights.Corner2Height) / 3:0.##}" : "void";
        HoverText = $"{at.TileX}, {at.TileZ} · {at.Triangle} · {PbcGeometry.MaterialName(material)} ({(byte)material}) · height {height}";
    }

    // ----- Files -----

    private static readonly string[] Extensions = [".pbc", ".zs", ".szs", ".sarc"];
    private static FilePickerFileType AnyFileType { get; } = new("PBC or PBC archive") { Patterns = [.. Extensions.Select(extension => "*" + extension)] };
    private static FilePickerFileType PbcFileType { get; } = new("PBC") { Patterns = ["*.pbc"] };
    private static FilePickerFileType ZstdArchiveType { get; } = new("Zstd SARC archive") { Patterns = ["*.zs"] };
    private static FilePickerFileType Yaz0ArchiveType { get; } = new("Yaz0 SARC archive") { Patterns = ["*.szs"] };
    private static FilePickerFileType SarcType { get; } = new("SARC archive") { Patterns = ["*.sarc"] };

    public static bool IsPbcFile(IStorageItem item) =>
        item is IStorageFile && Extensions.Contains(Path.GetExtension(item.Name), StringComparer.OrdinalIgnoreCase);

    [RelayCommand(CanExecute = nameof(CanOpen))]
    private async Task OpenFile()
    {
        try
        {
            if (!await ConfirmChangesAsync()) return;
            var files = await view.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false, FileTypeFilter = [AnyFileType], Title = "Open a PBC file or archive..."
            });
            if (files.Count > 0) await LoadAsync(files[0]);
        }
        catch (Exception ex) { ErrorMessage = $"Could not open file: {ex.Message}"; }
    }

    /// <summary>Opens a file dropped on the window, asking about unsaved changes first.</summary>
    public async Task OpenFileAsync(IStorageFile file)
    {
        if (IsBusy) return;
        try
        {
            if (await ConfirmChangesAsync()) await LoadAsync(file);
        }
        catch (Exception ex) { ErrorMessage = $"Could not open file: {ex.Message}"; }
    }

    private async Task LoadAsync(IStorageFile source)
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusText = $"Loading {source.Name}…";
        try
        {
            byte[] bytes;
            await using (var stream = await source.OpenReadAsync())
            using (var buffer = new MemoryStream())
            {
                await stream.CopyToAsync(buffer);
                bytes = buffer.ToArray();
            }
            var loaded = await Task.Run(() => PbcContainer.Read(bytes, source.Name));
            foreach (var entry in Entries) entry.History.Changed -= OnHistoryChanged;
            var entries = loaded.Entries.Select(entry => new PbcEntry(entry.Name, entry.File)).ToList();
            foreach (var entry in entries) entry.History.Changed += OnHistoryChanged;
            container = loaded;
            sourceFile = source;
            localPath = source.TryGetLocalPath();
            fileName = source.Name;
            HasUnsavedChanges = false;
            UpdateTitle();
            Entries = entries;
            SelectedEntry = null;
            SelectedEntry = entries[0];
            if (entries.Count > 1) StatusText = $"{entries.Count} PBC files in {source.Name} · showing {entries[0].Name}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load {source.Name}: {ex.Message}";
            StatusText = "Loading failed.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanSaveFile))]
    private async Task SaveFile()
    {
        if (localPath == null && sourceFile == null) await SaveAs();
        else await SaveAsync(localPath, sourceFile);
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task SaveAs()
    {
        try
        {
            var destination = await view.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save PBC as...", SuggestedFileName = fileName,
                // The file keeps its container and compression, so only that kind is offered.
                DefaultExtension = SaveType.Patterns![0][2..], FileTypeChoices = [SaveType], ShowOverwritePrompt = true
            });
            if (destination != null) await SaveAsync(destination.TryGetLocalPath(), destination);
        }
        catch (Exception ex) { ErrorMessage = $"Could not save file: {ex.Message}"; }
    }

    private FilePickerFileType SaveType => container is not { } open ? PbcFileType : open.Compression switch
    {
        PbcCompression.Zstd => ZstdArchiveType,
        PbcCompression.Yaz0 => Yaz0ArchiveType,
        _ => open.IsArchive ? SarcType : PbcFileType
    };

    private async Task SaveAsync(string? path, IStorageFile? destination)
    {
        if (!CanEdit()) return;
        string name = path != null ? Path.GetFileName(path) : destination!.Name;
        IsBusy = true;
        ErrorMessage = null;
        StatusText = $"Saving {name}…";
        try
        {
            var bytes = await Task.Run(container!.Save);
            if (path != null)
            {
                path = Path.GetFullPath(path);
                // Through a temporary copy, so a failed write leaves the old file intact.
                string temporary = Path.Combine(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
                try
                {
                    await System.IO.File.WriteAllBytesAsync(temporary, bytes);
                    System.IO.File.Move(temporary, path, overwrite: true);
                }
                finally
                {
                    if (System.IO.File.Exists(temporary)) System.IO.File.Delete(temporary);
                }
            }
            else
            {
                await using var output = await destination!.OpenWriteAsync();
                output.SetLength(0);
                await output.WriteAsync(bytes);
            }
            sourceFile = destination ?? sourceFile;
            localPath = path;
            fileName = name;
            foreach (var entry in Entries) entry.History.MarkSaved();
            HasUnsavedChanges = false;
            UpdateTitle();
            StatusText = $"Saved {name}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not save {name}: {ex.Message}";
            StatusText = "Saving failed; your edits are still open.";
        }
        finally { IsBusy = false; }
    }

    public async Task<bool> ConfirmChangesAsync()
    {
        if (IsBusy) return false;
        if (!HasUnsavedChanges) return true;
        bool? choice = await view.ConfirmSaveChangesAsync();
        if (choice == null) return false;
        if (!choice.Value) return true;
        await SaveFile();
        return !HasUnsavedChanges;
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task CloseFile()
    {
        if (!await ConfirmChangesAsync()) return;
        foreach (var entry in Entries) entry.History.Changed -= OnHistoryChanged;
        container = null;
        SelectedEntry = null;
        Entries = [];
        sourceFile = null;
        localPath = null;
        fileName = "PBC Editor";
        HasUnsavedChanges = false;
        UpdateTitle();
        StatusText = "Open a PBC file to begin.";
        ErrorMessage = null;
    }

    [RelayCommand]
    private void DismissError() => ErrorMessage = null;

    // ----- Model -----

    // Shared by every PBC window: the material archive and the lighting are large and the same for every model.
    private static (string Path, SwitchShaderArchive Archive)? shaderArchive;
    private static (string Romfs, AcnhEnvironmentResources Resources)? environment;
    private CancellationTokenSource? modelLoad;

    /// <summary>The model drawn under the PBC, or null when none was found.</summary>
    [ObservableProperty] public partial BfresRenderer? ModelRenderer { get; private set; }
    [ObservableProperty] public partial string? ModelName { get; private set; }
    [ObservableProperty] public partial bool ShowModel { get; set; } = true;
    /// <summary>How opaque the PBC surface is over the model, in percent.</summary>
    [ObservableProperty] public partial double SurfaceOpacity { get; set; } = 50;
    public bool HasModel => ModelRenderer != null;
    public string ModelTip => ModelName is { } name ? $"Show the model ({name})" : "No matching model found next to the file or in the romfs";

    partial void OnModelRendererChanged(BfresRenderer? value) => OnPropertyChanged(nameof(HasModel));
    partial void OnModelNameChanged(string? value) => OnPropertyChanged(nameof(ModelTip));

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task ChooseModel()
    {
        var files = await view.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false, Title = "Choose the model shown under the PBC...",
            FileTypeFilter = [new FilePickerFileType("BFRES model") { Patterns = ["*.zs", "*.bfres", "*.sbfres", "*.szs"] }]
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path) await LoadModelAsync(path, SelectedEntry is { } entry ? PbcModelLocator.ModelName(entry.Name) : null);
    }

    /// <summary>
    /// Shows a model archive under the PBC. An archive with a model named <paramref name="modelName"/> shows only that
    /// one: e.g. BridgeBricks holds a model per PBC of BridgeBricks_pbc, each named like its PBC.
    /// </summary>
    private async Task LoadModelAsync(string? path, string? modelName)
    {
        modelLoad?.Cancel();
        var request = modelLoad = new CancellationTokenSource();
        var token = request.Token;
        ModelRenderer = null;
        ModelName = null;
        if (path == null) return;
        string? sourcePath = localPath;
        try
        {
            var (meshes, geometry, shown, textureWarning) = await Task.Run(() =>
            {
                using var stream = System.IO.File.OpenRead(path);
                var models = BfresFileReader.Load(stream, token).SelectMany(asset => asset.Document.Models).ToList();
                var named = models.Where(model => string.Equals(model.Name, modelName, StringComparison.OrdinalIgnoreCase)).ToList();
                var chosen = named.Count > 0 ? named : models;
                var (meshes, textureWarning) = WithModelTextures(chosen.SelectMany(model => model.Meshes).ToArray(), path, sourcePath, token);
                return (meshes, BfresRenderer.PrepareScene(meshes, token), named.Count > 0 ? named[0].Name : null, textureWarning);
            }, token);
            token.ThrowIfCancellationRequested();
            var renderer = new BfresRenderer();
            renderer.SetScene(geometry);
            ModelRenderer = renderer;
            ModelName = Path.GetFileName(path) + (shown != null ? $" · {shown}" : "");
            if (textureWarning != null) StatusText = textureWarning;
            // The model already shows with simple shading; the game's materials and lighting follow when they load.
            try
            {
                string? romfs = GameFiles.FindRomfs(AppSettings.Current.RomfsFolder);
                string? archivePath = (romfs != null ? BfresScene.FindRomfsShaderArchive(romfs) : null) ?? BfresScene.FindShaderArchive(path);
                if (archivePath != null && shaderArchive?.Path != archivePath)
                    shaderArchive = (archivePath, await Task.Run(() => SwitchShaderArchive.Load(archivePath), token));
                token.ThrowIfCancellationRequested();
                string? lightingRomfs = archivePath != null ? Path.GetDirectoryName(Path.GetDirectoryName(archivePath)) : romfs;
                if (lightingRomfs != null && environment?.Romfs != lightingRomfs)
                    environment = (lightingRomfs, await Task.Run(() => AcnhEnvironmentResources.Load(lightingRomfs), token));
                token.ThrowIfCancellationRequested();
                var (lighting, inside) = ModelLighting(PbcModelLocator.ModelFileName(path), environment?.Resources);
                // Rooms get the game's ambient occlusion, as the villager room preview does.
                renderer.AmbientOcclusion = inside;
                renderer.SetLighting(lighting);
                if (shaderArchive is { } archive)
                {
                    var prepared = await Task.Run(() => BfresScene.PrepareShaders(meshes, archive.Archive, token), token);
                    token.ThrowIfCancellationRequested();
                    renderer.SetNativeShaders(prepared.Programs);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                StatusText = $"Model shown without game materials: {ex.Message}";
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested) ErrorMessage = $"Could not load the model {Path.GetFileName(path)}: {ex.Message}";
        }
    }

    /// <summary>
    /// Noon lighting for a model by where the game places it. A building interior (Idr) takes its EnvPaletteIdrSelector entry; a
    /// villager room piece or rug, which only go inside houses, the villager room's; everything else is outside, as the model
    /// viewer opens it without the sky fill.
    /// </summary>
    private static (AcnhLightingPreset Lighting, bool Inside) ModelLighting(string model, AcnhEnvironmentResources? resources)
    {
        if (model.StartsWith("Idr", StringComparison.Ordinal))
            return (resources != null ? AcnhSceneLighting.EvaluateIdr(resources, 12, AcnhIdrEnvironment.IdrName(model) ?? model) : AcnhLightingPreset.Room, true);
        if (model.StartsWith("Room", StringComparison.Ordinal) || model.StartsWith("Rug", StringComparison.Ordinal))
            return (resources != null ? AcnhSceneLighting.Evaluate(resources, 12, inside: true) : AcnhLightingPreset.Room, true);
        var outside = resources != null ? AcnhSceneLighting.Evaluate(resources, 12) : AcnhLightingPreset.Day;
        return (outside with { PreviewIndirectLight = System.Numerics.Vector3.Zero }, false);
    }

    /// <summary>
    /// The meshes with the textures the game binds to them from other files: the model's own _Tex_ files, the field texture pool
    /// and the ShareTexture files, found where the PBC is first, then the romfs. Returns a warning naming the textures not found.
    /// </summary>
    private static (BfresMesh[] Meshes, string? Warning) WithModelTextures(BfresMesh[] meshes, string modelPath, string? sourcePath, CancellationToken token)
    {
        var wanted = meshes.SelectMany(mesh => mesh.Material.Samplers).Where(sampler => sampler.Texture == null && sampler.TextureName.Length > 0)
            .Select(sampler => sampler.TextureName).Distinct().ToList();
        if (wanted.Count == 0) return (meshes, null);
        string model = PbcModelLocator.ModelFileName(modelPath);
        string? localized = PbcModelLocator.LocalizeTable() is { } table ? AcnhLocalizedNames.Load(table).TextureArchive(model) : null;
        var (textures, missing) = AcnhModelTextures.Resolve(model, PbcModelLocator.TextureFolders(modelPath, sourcePath), wanted, token, localized);
        if (textures.Count > 0) meshes = [.. meshes.Select(mesh => mesh.WithMaterial(mesh.Material.WithTextures(textures)))];
        return (meshes, missing.Count > 0 ? $"No texture file found for {string.Join(", ", missing)}" : null);
    }

    public void ReportModelFailure(Exception error)
    {
        ModelRenderer = null;
        ErrorMessage = $"The model could not be drawn and was hidden: {error.Message}";
    }

    [ObservableProperty] public partial string? RenderError { get; set; }
    [ObservableProperty] public partial bool Show3DGrid { get; set; } = true;

    public void ReportRenderFailure(Exception error) => RenderError = error.Message;
}
