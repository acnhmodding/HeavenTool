using Avalonia.Data;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeavenTool.Avalonia.Settings;
using HeavenTool.IO.FileFormats.BFRES;
using HeavenTool.Rendering.BFRES;
using HeavenTool.Shaders;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels;

public partial class BfresViewModel : ObservableObject, IDisposable
{
    private readonly BfresRenderer renderer;
    private readonly IStorageProvider storage;
    private CancellationTokenSource? loading;
    private bool disposed;
    // Game materials load with each model, from the romfs in Settings or the model's own.
    private SwitchShaderArchive? shaderArchive;
    private string? shaderArchivePath;
    private IReadOnlyList<BfresAsset> assets = [];
    private string sourceName = "";
    private string[] modelWarnings = [], shaderWarnings = [];
    private (int Native, int Fallback) drawCounts;
    private readonly Dictionary<BfresBone, BfresNode> boneNodes = new(ReferenceEqualityComparer.Instance);
    private IReadOnlySet<(string Name, int Component)>? materialUsage;

    public ObservableCollection<BfresNode> Roots { get; } = [];
    public ObservableCollection<BfresMaterialValue> Properties { get; } = [];
    public string[] LightingPresets { get; } = ["Day", "Night"];
    [ObservableProperty] private string title = "BFRES Viewer — Heaven Tool";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isLoaded;
    [ObservableProperty] private string status = "Open a model to begin.";
    [ObservableProperty] private string? error;
    [ObservableProperty] private string? renderError;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasWarnings), nameof(WarningSummary))] private string? warning;
    [ObservableProperty] private bool wireframe;
    [ObservableProperty] private bool showBones;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsDay), nameof(IsNight))] private string lighting = "Day";
    [ObservableProperty] private bool skyFill = true;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ExposureText))] private double exposure;
    [ObservableProperty] private bool showScenePanel = true;
    [ObservableProperty] private bool showMaterialPanel = true;
    [ObservableProperty] private BfresNode? selectedNode;
    [ObservableProperty] private string materialName = "No mesh selected";
    [ObservableProperty] private string propertyFilter = "";
    [ObservableProperty] private bool showAllProperties;
    [ObservableProperty] private BfresMaterialValue? selectedProperty;
    [ObservableProperty] private string propertySummary = "";
    [ObservableProperty] private string? materialError;
    [ObservableProperty] private bool hasMaterial;
    private BfresMaterial? material;

    // Radio menu items set these; unchecking one leaves the choice to its sibling.
    public bool IsDay { get => Lighting == "Day"; set { if (value) Lighting = "Day"; } }
    public bool IsNight { get => Lighting == "Night"; set { if (value) Lighting = "Night"; } }

    public string ExposureText => Exposure == 0 ? "0 EV" : Exposure.ToString("+0.0;-0.0", CultureInfo.InvariantCulture) + " EV";
    public bool HasWarnings => !string.IsNullOrEmpty(Warning);
    public string WarningSummary => (modelWarnings.Length + shaderWarnings.Length + renderer.NativeShaderWarnings.Count) switch { 1 => "1 warning", var n => $"{n} warnings" };

    public BfresViewModel(BfresRenderer renderer, IStorageProvider storage)
    {
        this.renderer = renderer;
        this.storage = storage;
        renderer.DrawStatusChanged += OnRendered;
        renderer.BonePicked += OnBonePicked;
        renderer.NativeShaderWarningsChanged += UpdateWarning;
        ApplyLighting();
    }

    partial void OnWireframeChanged(bool value) => renderer.Wireframe = value;
    partial void OnShowBonesChanged(bool value) => renderer.ShowBones = value;
    partial void OnLightingChanging(string value)
    {
        if (value is not ("Day" or "Night")) throw new ArgumentException("Unknown lighting preset.", nameof(value));
    }
    partial void OnLightingChanged(string value) { ApplyLighting(); UpdateStatus(); RefreshMaterial(); }
    partial void OnSkyFillChanged(bool value) => ApplyLighting();
    partial void OnExposureChanged(double value) => renderer.SetExposure(.06f * MathF.Pow(2, (float)value));
    partial void OnPropertyFilterChanged(string value) => PopulateProperties();
    partial void OnShowAllPropertiesChanged(bool value) => PopulateProperties();
    partial void OnSelectedNodeChanged(BfresNode? value)
    {
        renderer.SelectBone(value?.Source as BfresBone);
        renderer.SelectMesh(value?.Source as BfresMesh);
        if (value?.Source is BfresBone) ShowBones = true;
        RefreshMaterial();
    }

    private void ApplyLighting()
    {
        var preset = Lighting == "Night" ? AcnhLightingPreset.Night : AcnhLightingPreset.Day;
        if (!SkyFill) preset = preset with { PreviewIndirectLight = System.Numerics.Vector3.Zero };
        renderer.SetLighting(preset);
    }

    [RelayCommand] private void FrameAll() => renderer.FrameScene();
    [RelayCommand] private void FrameSelection() => renderer.FrameSelection();
    [RelayCommand] private void ToggleBones() => ShowBones = !ShowBones;
    [RelayCommand] private void ResetExposure() => Exposure = 0;
    [RelayCommand] private void ChangeExposure(string steps) => Exposure = Math.Clamp(Exposure + double.Parse(steps, CultureInfo.InvariantCulture), -4, 4);
    [RelayCommand] private void SetLighting(string preset) => Lighting = preset;
    [RelayCommand] private void DismissError() => Error = null;

    private static readonly string[] ModelPatterns = ["*.bfres", "*.sbfres", "*.zs", "*.szs", "*.sarc"];

    // Drops accept the same file types as the Open dialog.
    public static bool IsModelFile(IStorageItem item) =>
        item is IStorageFile && ModelPatterns.Any(p => item.Name.EndsWith(p[1..], StringComparison.OrdinalIgnoreCase));

    [RelayCommand]
    private async Task Open()
    {
        try
        {
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open a Switch model",
                FileTypeFilter = [new("Switch models") { Patterns = ModelPatterns }, FilePickerFileTypes.All]
            });
            if (files.Count > 0) await LoadFileAsync(files[0]);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    public Task LoadFileAsync(IStorageFile file) => LoadAsync(async token =>
    {
        await using var stream = await file.OpenReadAsync();
        using var data = new MemoryStream();
        await stream.CopyToAsync(data, token);
        return data.ToArray();
    }, file.Name, file.TryGetLocalPath());

    public Task LoadFileAsync(string path) => LoadAsync(token => File.ReadAllBytesAsync(path, token), Path.GetFileName(path), path);
    public Task LoadBytesAsync(byte[] bytes, string name) => LoadAsync(_ => Task.FromResult(bytes), name, null);

    private async Task LoadAsync(Func<CancellationToken, Task<byte[]>> read, string name, string? path)
    {
        var request = BeginRequest(ref loading, $"Loading {name}...");
        try
        {
            var bytes = await read(request.Token);
            var scene = await Task.Run(() =>
            {
                using var stream = new MemoryStream(bytes, false);
                return BfresScene.Load(stream, request.Token);
            }, request.Token);
            request.Token.ThrowIfCancellationRequested();
            assets = scene.Assets;
            sourceName = name;
            SelectedNode = null;
            renderer.SetScene(scene.Geometry);
            renderer.SetSkeletons(assets.SelectMany(a => a.Document.Models).Select(m => m.Bones));
            BuildTree();
            IsLoaded = true;
            modelWarnings = assets.SelectMany(a => a.Document.Warnings).Distinct().ToArray();
            shaderWarnings = [];
            UpdateWarning();
            UpdateTitle();
            // The model is already showing, so a broken material archive must not fail the load.
            try
            {
                // Without an archive for this model, the last one found keeps applying.
                if (FindShaderArchive(path) is { } shaderPath && shaderPath != shaderArchivePath)
                {
                    Status = "Loading game materials...";
                    var archive = await Task.Run(() => SwitchShaderArchive.Load(shaderPath), request.Token);
                    request.Token.ThrowIfCancellationRequested();
                    (shaderArchive, shaderArchivePath) = (archive, shaderPath);
                }
                await PrepareShadersAsync(request.Token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (!request.IsCancellationRequested) Error = $"Could not load game materials: {ex.Message}";
            }
            request.Token.ThrowIfCancellationRequested();
            UpdateStatus();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!request.IsCancellationRequested) { Error = ex.Message; Status = $"Could not load {name}."; } }
        finally { EndRequest(ref loading, request); }
    }

    // The romfs from Settings comes first, then the romfs the model was opened from.
    private static string? FindShaderArchive(string? modelPath) =>
        (GameFiles.FindRomfs(AppSettings.Current.RomfsFolder) is { } romfs ? BfresScene.FindRomfsShaderArchive(romfs) : null)
        ?? (modelPath != null ? BfresScene.FindShaderArchive(modelPath) : null);

    private async Task PrepareShadersAsync(CancellationToken cancellation)
    {
        if (shaderArchive == null || assets.Count == 0) return;
        Status = "Preparing game materials...";
        var snapshot = assets;
        var archive = shaderArchive;
        var prepared = await Task.Run(() => BfresScene.PrepareShaders(snapshot, archive, cancellation), cancellation);
        cancellation.ThrowIfCancellationRequested();
        // A model opened in the meantime prepares its own materials.
        if (disposed || !ReferenceEquals(snapshot, assets) || !ReferenceEquals(archive, shaderArchive)) return;
        renderer.SetNativeShaders(prepared.Programs);
        shaderWarnings = prepared.Errors.Select(p => $"{p.Key.Name}: {p.Value}").ToArray();
        UpdateWarning();
    }

    private void UpdateWarning() => Warning = string.Join(Environment.NewLine, modelWarnings.Concat(shaderWarnings).Concat(renderer.NativeShaderWarnings));

    private CancellationTokenSource BeginRequest(ref CancellationTokenSource? slot, string status)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        slot?.Cancel();
        var request = new CancellationTokenSource();
        slot = request;
        IsBusy = true;
        Error = null;
        Status = status;
        return request;
    }

    private void EndRequest(ref CancellationTokenSource? slot, CancellationTokenSource request)
    {
        if (ReferenceEquals(slot, request)) slot = null;
        IsBusy = loading != null;
        request.Dispose();
    }

    private void BuildTree()
    {
        Roots.Clear();
        boneNodes.Clear();
        // Models start expanded; skeletons stay collapsed until a bone is picked.
        foreach (var model in assets.SelectMany(asset => asset.Document.Models))
        {
            var node = new BfresNode(model.Name, null) { IsExpanded = true };
            Roots.Add(node);
            if (model.Bones.Count > 0)
            {
                var skeleton = node.Add(new($"Skeleton ({model.Bones.Count} bones)", null));
                var nodes = model.Bones.Select(b => new BfresNode(b.Name, b)).ToArray();
                foreach (var bone in model.Bones)
                {
                    boneNodes.Add(bone, nodes[bone.Index]);
                    (bone.ParentIndex < 0 ? skeleton : nodes[bone.ParentIndex]).Add(nodes[bone.Index]);
                }
            }
            foreach (var mesh in model.Meshes) node.Add(new(mesh.Name, mesh));
        }
    }

    private void OnBonePicked(BfresBone bone)
    {
        if (!boneNodes.TryGetValue(bone, out var node)) return;
        for (var parent = node.Parent; parent != null; parent = parent.Parent) parent.IsExpanded = true;
        SelectedNode = node;
    }

    // Runs after every frame, so the status only changes with the draw counts and keeps edit messages visible.
    private void OnRendered()
    {
        var counts = (renderer.NativeDrawCount, renderer.FallbackDrawCount);
        if (counts != drawCounts)
        {
            drawCounts = counts;
            if (!IsBusy && Error == null && RenderError == null) UpdateStatus();
        }
        RefreshMaterial();
    }

    private void UpdateStatus()
    {
        if (!IsLoaded) return;
        // Without a 3D view there are no draw counts to report.
        if (RenderError != null) { Status = "3D view unavailable."; return; }
        int count = renderer.NativeDrawCount + renderer.FallbackDrawCount;
        Status = $"{count:N0} meshes · {Lighting}" + (renderer.FallbackDrawCount > 0 ? " · Some materials use simplified shading" : "");
    }

    private void UpdateTitle() => Title = $"BFRES Viewer — {sourceName}" +
        (assets.SelectMany(a => a.Document.Models).SelectMany(m => m.Materials).Any(m => m.HasChanges) ? " *" : "");

    private void RefreshMaterial()
    {
        var mesh = SelectedNode?.Source as BfresMesh;
        var usage = mesh == null ? null : renderer.GetMaterialVisibility(mesh);
        if (ReferenceEquals(material, mesh?.Material) && ReferenceEquals(materialUsage, usage)) return;
        material = mesh?.Material;
        materialUsage = usage;
        HasMaterial = material != null;
        MaterialName = material?.Name ?? "No mesh selected";
        PopulateProperties();
    }

    private void PopulateProperties()
    {
        List<BfresMaterialProperty> rows = material == null ? [] : BfresMaterialProperty.Enumerate(material, materialUsage, ShowAllProperties, PropertyFilter).ToList();
        PropertySummary = material == null ? "" : rows.Count switch
        {
            0 => "No matching properties.",
            1 => "1 property",
            _ => $"{rows.Count} properties"
        };
        // When only the active state changed (shaders finished, lighting switched), update the rows in place
        // so a value being edited is not thrown away.
        if (rows.Count > 0 && rows.Count == Properties.Count && rows.Zip(Properties).All(p => p.Second.Matches(p.First)))
        {
            for (int i = 0; i < rows.Count; i++) Properties[i].Update(rows[i]);
            return;
        }
        Properties.Clear();
        SelectedProperty = null;
        foreach (var property in rows) Properties.Add(new(property, MaterialEdited));
    }

    private void MaterialEdited(BfresMaterial edited)
    {
        renderer.RefreshMaterial(edited);
        UpdateTitle();
        Status = edited.HasChanges ? $"Material edited: {edited.Name}" : $"Material reset: {edited.Name}";
    }

    // The grid checks typed values before committing them, as the WinForms viewer does.
    public bool CheckValue(string? text)
    {
        MaterialError = BfresMaterialProperty.TryParse(text, out _) ? null : "Enter a finite number, for example 0.5.";
        return MaterialError == null;
    }

    [RelayCommand] private void ResetValue() => SelectedProperty?.Reset();
    [RelayCommand]
    private void ResetMaterial()
    {
        if (material == null) return;
        material.ResetParameters();
        MaterialEdited(material);
        foreach (var row in Properties) row.Refresh();
    }

    public void ReportRenderFailure(Exception error) { RenderError = error.Message; Status = "3D view unavailable."; }

    public void Dispose()
    {
        disposed = true;
        loading?.Cancel();
        renderer.DrawStatusChanged -= OnRendered;
        renderer.BonePicked -= OnBonePicked;
        renderer.NativeShaderWarningsChanged -= UpdateWarning;
    }
}

public partial class BfresNode(string name, object? source) : ObservableObject
{
    public string Name { get; } = name;
    public object? Source { get; } = source;
    public ObservableCollection<BfresNode> Children { get; } = [];
    public BfresNode? Parent { get; private set; }
    [ObservableProperty] private bool isExpanded;

    public BfresNode Add(BfresNode child)
    {
        child.Parent = this;
        Children.Add(child);
        return child;
    }


}

public sealed class BfresMaterialValue(BfresMaterialProperty property, Action<BfresMaterial> changed) : ObservableObject
{
    public string Label => property.Label;
    public string Name => property.Name;
    public bool IsActive => property.IsActive;
    public bool IsChanged => property.IsChanged;
    public string Activity => IsActive ? "Active in this preview." : "Inactive in this preview.";
    public string Value
    {
        get => property.Value.ToString("G", CultureInfo.InvariantCulture);
        set
        {
            if (!BfresMaterialProperty.TryParse(value, out float number))
                throw new DataValidationException("Enter a finite number, for example 0.5.");
            property.SetValue(number);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsChanged));
            changed(property.Material);
        }
    }
    public void Reset() => Value = property.OriginalValue.ToString("G", CultureInfo.InvariantCulture);

    public bool Matches(BfresMaterialProperty other) =>
        ReferenceEquals(property.Material, other.Material) && property.Name == other.Name && property.Component == other.Component;

    public void Refresh()
    {
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(IsChanged));
    }

    public void Update(BfresMaterialProperty other)
    {
        if (property.IsActive == other.IsActive) return;
        property = other;
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(Activity));
    }
}
