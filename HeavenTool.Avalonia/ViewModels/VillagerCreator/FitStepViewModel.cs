using CommunityToolkit.Mvvm.ComponentModel;
using HeavenTool.ModManager.Villagers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading;
using HeavenTool.Rendering.BFRES;

namespace HeavenTool.Avalonia.ViewModels.VillagerCreator;

/// <summary>A Gmo file the villager can start from: a villager's, or a species' default.</summary>
public sealed record GmoSource(string Label, string Text)
{
    public override string ToString() => Text;
}

public sealed record GmoChoice(string Value, string Text)
{
    public override string ToString() => Text;
}

/// <summary>Three numbers, such as a position. <paramref name="factor"/> turns the stored value into the shown one.</summary>
public sealed partial class VectorField(string title, string? description, Action changed, double factor = 1, string[]? labels = null)
    : ObservableObject
{
    private bool loading;

    public string Title { get; } = title;
    public string? Description { get; } = description;
    public string XLabel { get; } = labels?[0] ?? "X";
    public string YLabel { get; } = labels?[1] ?? "Y";
    public string ZLabel { get; } = labels?[2] ?? "Z";

    [ObservableProperty]
    public partial decimal? X { get; set; }
    [ObservableProperty]
    public partial decimal? Y { get; set; }
    [ObservableProperty]
    public partial decimal? Z { get; set; }

    public void Show(GmoVector value)
    {
        loading = true;
        try
        {
            X = Shown(value.X);
            Y = Shown(value.Y);
            Z = Shown(value.Z);
        }
        finally { loading = false; }
    }

    public GmoVector Value => new(Stored(X), Stored(Y), Stored(Z));

    // Rounded so a value shows as typed; the files keep three or four decimals.
    private decimal Shown(float value) => Math.Round((decimal)(value * factor), 4);
    private float Stored(decimal? value) => (float)((double)(value ?? 0) / factor);

    partial void OnXChanged(decimal? value) => Changed();
    partial void OnYChanged(decimal? value) => Changed();
    partial void OnZChanged(decimal? value) => Changed();

    private void Changed()
    {
        if (!loading) changed();
    }
}

public sealed partial class ToolScaleEntry(string name, string text, Action changed) : ObservableObject
{
    private bool loading;

    public string Name { get; } = name;
    public string Text { get; } = text;

    [ObservableProperty]
    public partial decimal? Scale { get; set; }

    public void Show(float value)
    {
        loading = true;
        try { Scale = Math.Round((decimal)value, 4); }
        finally { loading = false; }
    }

    partial void OnScaleChanged(decimal? value)
    {
        if (!loading) changed();
    }
}

public sealed record HeldChoice(string Text, ToolScaleEntry? Tool);

public sealed record StanceChoice(FitStance Stance, string Text);

/// <summary>The Fit step: the villager's Gmo file, starting from the donor's.</summary>
public partial class FitStepViewModel : ObservableObject, IDisposable
{
    private static readonly IReadOnlyDictionary<string, string> FootPrintNames = new Dictionary<string, string>
    {
        ["None"] = "None",
        ["ShoesSocks"] = "Socks",
        ["ShoesSoft"] = "Soft shoes",
        ["ShoesHard"] = "Hard shoes",
        ["ShoesGeta"] = "Geta",
        ["ShoesPumps"] = "Pumps",
        ["ShoeslessHuman"] = "Bare feet, human",
        ["ShoeslessNikukyu"] = "Bare feet, paw pads",
        ["ShoeslessMaru"] = "Bare feet, round",
        ["ShoeslessNagaMaru"] = "Bare feet, long round",
        ["ShoeslessHosonagaMaru"] = "Bare feet, slim long round",
        ["ShoeslessBird"] = "Bare feet, bird",
        ["ShoeslessBird2"] = "Bare feet, bird 2",
        ["ShoeslessHidume"] = "Bare feet, hooves",
    };

    private static readonly IReadOnlyDictionary<string, string> SizeNames = new Dictionary<string, string>
    {
        ["cSmall"] = "Small", ["cNormal"] = "Normal", ["cLarge"] = "Large",
    };

    private readonly VillagerCreatorViewModel creator;
    private readonly Dictionary<string, GmoAccessory> accessories = [];
    private VillagerProject? project;
    private VillagerGmo? source;
    private bool loading;
    private BfresRenderer? renderer;
    private bool previewVisible;
    private CancellationTokenSource? previewLoad;
    private string? shownModel;
    private IReadOnlyList<string> sceneWarnings = [];
    private Matrix4x4? shownFeel;

    public FitStepViewModel(VillagerCreatorViewModel creator)
    {
        this.creator = creator;
        HandItemOffset = new("Handing over", "Where the villager holds out an item when giving it.", Commit);
        SitOffset = new("Sitting on a stool", "Stools and seats without a backrest.", () => OffsetChanged(FitStance.Stool));
        SitSbOffset = new("Sitting on a chair", "Chairs with a backrest.", () => OffsetChanged(FitStance.Chair));
        BedOffset = new("Lying in bed", null, () => OffsetChanged(FitStance.Bed));
        Stance = Stances[0];
        EyeOffset = new("Look from", null, Commit);
        // The third value of the ranges isn't known; vanilla files mostly have 5.1 there.
        EyeHorizon = new("Look left and right", null, Commit, labels: ["Min", "Max", "3rd"]);
        EyeVertical = new("Look up and down", null, Commit, labels: ["Min", "Max", "3rd"]);
        LeftEye = new("Left eye", null, EmotionPointChanged);
        RightEye = new("Right eye", null, EmotionPointChanged);
        Mouth = new("Mouth", null, EmotionPointChanged);
        AccessoryPosition = new("Position", null, CommitAccessory);
        AccessoryRotation = new("Rotation", "In degrees.", CommitAccessory, 180 / Math.PI);
        AccessoryScale = new("Scale", null, CommitAccessory);
    }

    public IReadOnlyList<GmoChoice> FootPrints { get; } =
        [.. VillagerGmo.FootPrints.Select(value => new GmoChoice(value, FootPrintNames.GetValueOrDefault(value, value)))];

    public IReadOnlyList<GmoChoice> Sizes { get; } =
        [.. VillagerGmo.CharacterSizes.Select(value => new GmoChoice(value, SizeNames.GetValueOrDefault(value, value)))];

    [ObservableProperty]
    public partial IReadOnlyList<GmoSource> Sources { get; set; } = [];

    [ObservableProperty]
    public partial GmoSource? Source { get; set; }

    [ObservableProperty]
    public partial GmoChoice? FootPrint { get; set; }

    [ObservableProperty]
    public partial decimal? FootScale { get; set; }

    [ObservableProperty]
    public partial GmoChoice? Size { get; set; }

    [ObservableProperty]
    public partial decimal? EyeRotateOffset { get; set; }

    public IReadOnlyList<StanceChoice> Stances { get; } =
    [
        new(FitStance.Standing, "Standing"), new(FitStance.Chair, "On a chair"), new(FitStance.Stool, "On a stool"), new(FitStance.Bed, "In bed"),
    ];

    [ObservableProperty]
    public partial StanceChoice Stance { get; set; }

    public VectorField HandItemOffset { get; }
    public VectorField SitOffset { get; }
    public VectorField SitSbOffset { get; }
    public VectorField BedOffset { get; }
    public VectorField EyeOffset { get; }
    public VectorField EyeHorizon { get; }
    public VectorField EyeVertical { get; }
    public VectorField LeftEye { get; }
    public VectorField RightEye { get; }
    public VectorField Mouth { get; }

    [ObservableProperty]
    public partial IReadOnlyList<GmoChoice> Accessories { get; set; } = [];

    [ObservableProperty]
    public partial GmoChoice? Accessory { get; set; }

    public VectorField AccessoryPosition { get; }
    public VectorField AccessoryRotation { get; }
    public VectorField AccessoryScale { get; }

    [ObservableProperty]
    public partial int EarSet { get; set; }

    public ObservableCollection<ToolScaleEntry> Tools { get; } = [];

    /// <summary>The held items that have a model to show, after Nothing.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<HeldChoice> HeldChoices { get; set; } = [];

    [ObservableProperty]
    public partial HeldChoice? Held { get; set; }

    /// <summary>Vanilla hats or glasses that go in the selected slot, to see it with.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ItemInfo> TryOns { get; set; } = [];

    [ObservableProperty]
    public partial ItemInfo? TryOn { get; set; }

    [ObservableProperty]
    public partial bool ShowEmotionPoints { get; set; }

    [ObservableProperty]
    public partial bool IsPreviewLoading { get; set; }

    [ObservableProperty]
    public partial string? PreviewWarning { get; set; }

    [ObservableProperty]
    public partial string? RenderError { get; set; }

    /// <summary>The ear settings a hat can have. Vanilla files use 0 to 3 on hats only.</summary>
    // Bit 2 hides the ears (mCapVis), bit 1 stops their physics (FUN_7101f15c30).
    public IReadOnlyList<string> EarSets { get; } = ["Shown", "Shown, held still", "Hidden", "Hidden, held still"];

    internal void Initialize()
    {
        var data = creator.Data;
        var villagers = data.Villagers.ToDictionary(villager => villager.Label);
        const string prefix = "Param/Gmo/Gmo_";
        var labels = data.StaticParam.Files.Select(file => file.Name)
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(".byml", StringComparison.Ordinal))
            .Select(name => name[prefix.Length..^".byml".Length]);
        // Villagers by name, then the species' defaults, then the other characters.
        var species = data.Villagers.GroupBy(villager => VillagerLabel.Species(villager.Label))
            .ToDictionary(group => group.Key, group => group.First().Species);
        Sources = [.. labels
            .Select(label => villagers.TryGetValue(label, out var villager)
                ? (Order: 0, Source: new GmoSource(label, $"{villager.Name} ({label})"))
                : species.TryGetValue(label, out var name)
                    ? (Order: 1, Source: new GmoSource(label, $"Every {name} ({label})"))
                    : (Order: 2, Source: new GmoSource(label, label)))
            .OrderBy(entry => entry.Order).ThenBy(entry => entry.Source.Text, StringComparer.CurrentCultureIgnoreCase)
            .Select(entry => entry.Source)];
    }

    internal void Load(VillagerProject value)
    {
        project = value;
        loading = true;
        try { Source = Sources.FirstOrDefault(entry => entry.Label == value.GmoFrom); }
        finally { loading = false; }
        Show(value.Gmo ?? Read(value.GmoFrom));
        RefreshPreview();
    }

    private VillagerGmo? Read(string label) =>
        creator.Data.ReadParam(VillagerGenerator.GmoPath(label)) is { } file ? VillagerGmo.Read(file) : null;

    partial void OnSourceChanged(GmoSource? value)
    {
        if (loading || project == null || value == null) return;
        // Another file to start from replaces the edits, as the values belong to its model.
        project.GmoFrom = value.Label;
        project.Gmo = null;
        Show(Read(value.Label));
        RefreshPreview();
    }

    private void Show(VillagerGmo? gmo)
    {
        source = gmo;
        loading = true;
        try
        {
            FootPrint = gmo == null ? null : Choice(FootPrints, gmo.FootPrint);
            FootScale = gmo == null ? null : Math.Round((decimal)gmo.FootScale, 4);
            Size = gmo == null ? null : Choice(Sizes, gmo.CharacterSize);
            EyeRotateOffset = gmo == null ? null : Math.Round((decimal)gmo.EyeRotateOffset, 4);
            HandItemOffset.Show(gmo?.HandItemOffset ?? default);
            SitOffset.Show(gmo?.SitOffset ?? default);
            SitSbOffset.Show(gmo?.SitSbOffset ?? default);
            BedOffset.Show(gmo?.BedOffset ?? default);
            EyeOffset.Show(gmo?.EyeOffset ?? default);
            EyeHorizon.Show(gmo?.EyeHorizon ?? default);
            EyeVertical.Show(gmo?.EyeVertical ?? default);
            LeftEye.Show(gmo?.Emoticons.GetValueOrDefault("EyeL") ?? default);
            RightEye.Show(gmo?.Emoticons.GetValueOrDefault("EyeR") ?? default);
            Mouth.Show(gmo?.Emoticons.GetValueOrDefault("Mouth") ?? default);

            accessories.Clear();
            foreach (var (name, accessory) in gmo?.Accessories ?? new Dictionary<string, GmoAccessory>()) accessories[name] = accessory;
            var selected = Accessory?.Value;
            Accessories = [.. accessories.Keys.OrderBy(AccessoryOrder).ThenBy(name => name, StringComparer.Ordinal)
                .Select(name => new GmoChoice(name, AccessoryText(name)))];
            Accessory = Accessories.FirstOrDefault(choice => choice.Value == selected) ?? Accessories.FirstOrDefault();
            ShowAccessory();

            Tools.Clear();
            foreach (var (name, scale) in (gmo?.Tools ?? new Dictionary<string, float>()).OrderBy(tool => ToolText(tool.Key), StringComparer.CurrentCultureIgnoreCase))
            {
                ToolScaleEntry? entry = null;
                entry = new ToolScaleEntry(name, ToolText(name), () =>
                {
                    Commit();
                    if (Held?.Tool == entry) RefreshPreview();
                });
                entry.Show(scale);
                Tools.Add(entry);
            }
            // Keep holding the same kind of item when the values change.
            var held = Held?.Tool?.Name;
            HeldChoices = [new HeldChoice("Nothing", null),
                .. Tools.Where(tool => creator.FitPreview.HeldModelPath(tool.Name) != null).Select(tool => new HeldChoice(tool.Text, tool))];
            Held = HeldChoices.FirstOrDefault(choice => choice.Tool?.Name == held) ?? HeldChoices[0];
        }
        finally { loading = false; }
    }

    private static GmoChoice Choice(IReadOnlyList<GmoChoice> choices, string value) =>
        // A modded file may hold a value the game's list doesn't; keep it as it is.
        choices.FirstOrDefault(choice => choice.Value == value) ?? new GmoChoice(value, $"Custom ({value})");

    partial void OnAccessoryChanged(GmoChoice? value)
    {
        var was = loading;
        loading = true;
        try { ShowAccessory(); }
        finally { loading = was; }
        // Keep the item on show when the new slot fits it too.
        var tryOn = TryOn;
        TryOns = value == null ? [] : creator.Data.ItemsFitting(value.Value);
        TryOn = TryOns.FirstOrDefault(item => item.Id == tryOn?.Id) ?? TryOns.FirstOrDefault();
        RefreshPreview();
    }

    partial void OnTryOnChanged(ItemInfo? value) { if (value != null) RefreshPreview(); }

    partial void OnHeldChanged(HeldChoice? value) { if (!loading) RefreshPreview(); }

    partial void OnStanceChanged(StanceChoice value) => RefreshPreview();

    partial void OnShowEmotionPointsChanged(bool value) => UpdateMarkers();

    private void EmotionPointChanged()
    {
        Commit();
        UpdateMarkers();
    }

    // Left eye blue, right eye green, mouth orange.
    private void UpdateMarkers()
    {
        if (renderer == null) return;
        if (!ShowEmotionPoints || shownFeel is not { } feel)
        {
            renderer.SetMarkers([]);
            return;
        }
        renderer.SetMarkers(
        [
            new(FitPreview.EmotionPoint(LeftEye.Value, feel), new(.35f, .8f, .95f)),
            new(FitPreview.EmotionPoint(RightEye.Value, feel), new(.45f, .95f, .4f)),
            new(FitPreview.EmotionPoint(Mouth.Value, feel), new(1, .55f, .05f)),
        ]);
    }

    private void OffsetChanged(FitStance stance)
    {
        Commit();
        if (Stance.Stance == stance) RefreshPreview();
    }

    // The game's sit states take SitSbOffset for a chair, SitOffset for a stool and BedOffset in bed (FUN_7101d5883c).
    private GmoVector StanceOffset() => Stance.Stance switch
    {
        FitStance.Chair => SitSbOffset.Value,
        FitStance.Stool => SitOffset.Value,
        FitStance.Bed => BedOffset.Value,
        _ => default,
    };

    public void Attach(BfresRenderer value)
    {
        if (renderer != null) renderer.NativeShaderWarningsChanged -= UpdatePreviewWarning;
        renderer = value;
        renderer.NativeShaderWarningsChanged += UpdatePreviewWarning;
    }

    private void UpdatePreviewWarning()
    {
        var warnings = sceneWarnings.Concat(renderer?.NativeShaderWarnings ?? []).ToArray();
        PreviewWarning = warnings.Length > 0 ? string.Join(Environment.NewLine, warnings) : null;
    }

    public void ReportRenderFailure(Exception error) => RenderError = error.Message;

    /// <summary>Starts drawing the preview, the first time the step is shown.</summary>
    internal void ShowPreview()
    {
        previewVisible = true;
        RefreshPreview();
    }

    // The model the values belong to.
    private string ModelName() =>
        VillagerLabel.ModelName(project?.GmoFrom is { } label && creator.Data.Villagers.Any(villager => villager.Label == label)
            ? label : project?.Donor ?? "");

    private async void RefreshPreview()
    {
        if (!previewVisible || renderer == null || project == null) return;
        var model = ModelName();
        var item = TryOn == null ? null : creator.Data.ItemModelPath(TryOn.Id);
        var fit = Accessory == null ? null : accessories.GetValueOrDefault(Accessory.Value);
        var held = Held?.Tool is { } tool && creator.FitPreview.HeldModelPath(tool.Name) is { } path
            ? new FitHeldItem(tool.Name, path, (float)(tool.Scale ?? 1), Size?.Value ?? source?.CharacterSize ?? "cNormal") : null;

        previewLoad?.Cancel();
        var request = previewLoad = new CancellationTokenSource();
        IsPreviewLoading = true;
        try
        {
            var built = await creator.FitPreview.BuildAsync(model, item, fit,
                Accessory?.Value.StartsWith("C_", StringComparison.Ordinal) == true, held, Stance.Stance, StanceOffset(), request.Token);
            if (built == null || request.IsCancellationRequested) return;
            sceneWarnings = built.Warnings;
            // Editing a value keeps the camera where it is; another villager is framed again.
            renderer.SetScene(built.Geometry, keepCamera: shownModel == model);
            if (built.Lighting != null) renderer.SetLighting(built.Lighting);
            renderer.SetNativeShaders(built.Shaders);
            shownFeel = built.Feel;
            UpdateMarkers();
            UpdatePreviewWarning();
            shownModel = model;
        }
        catch (Exception ex)
        {
            if (!request.IsCancellationRequested) PreviewWarning = $"Couldn't show the villager: {ex.Message}";
        }
        finally
        {
            if (previewLoad == request)
            {
                previewLoad = null;
                IsPreviewLoading = false;
            }
            request.Dispose();
        }
    }

    public void Dispose()
    {
        if (renderer != null) renderer.NativeShaderWarningsChanged -= UpdatePreviewWarning;
        previewLoad?.Cancel();
        GC.SuppressFinalize(this);
    }

    private void ShowAccessory()
    {
        var accessory = Accessory != null ? accessories.GetValueOrDefault(Accessory.Value) : null;
        var was = loading;
        loading = true;
        try
        {
            AccessoryPosition.Show(accessory?.Position ?? default);
            AccessoryRotation.Show(accessory?.Rotation ?? default);
            AccessoryScale.Show(accessory?.Scale ?? default);
            EarSet = Math.Clamp(accessory?.EarSet ?? 0, 0, 3);
        }
        finally { loading = was; }
    }

    partial void OnFootPrintChanged(GmoChoice? value) => Commit();
    partial void OnFootScaleChanged(decimal? value) => Commit();
    partial void OnSizeChanged(GmoChoice? value)
    {
        Commit();
        if (!loading && Held?.Tool != null) RefreshPreview();
    }
    partial void OnEyeRotateOffsetChanged(decimal? value) => Commit();
    partial void OnEarSetChanged(int value) => CommitAccessory();

    private void CommitAccessory()
    {
        if (loading || Accessory == null) return;
        accessories[Accessory.Value] = new GmoAccessory(AccessoryPosition.Value, AccessoryRotation.Value, AccessoryScale.Value, EarSet);
        Commit();
        RefreshPreview();
    }

    private void Commit()
    {
        if (loading || project == null || source == null) return;
        var emoticons = new Dictionary<string, GmoVector>(source.Emoticons)
        {
            ["EyeL"] = LeftEye.Value,
            ["EyeR"] = RightEye.Value,
            ["Mouth"] = Mouth.Value,
        };
        project.Gmo = new VillagerGmo(
            FootPrint?.Value ?? source.FootPrint, (float)(FootScale ?? (decimal)source.FootScale), Size?.Value ?? source.CharacterSize,
            HandItemOffset.Value, SitOffset.Value, SitSbOffset.Value, BedOffset.Value,
            EyeOffset.Value, (float)(EyeRotateOffset ?? (decimal)source.EyeRotateOffset), EyeHorizon.Value, EyeVertical.Value,
            emoticons, new Dictionary<string, GmoAccessory>(accessories),
            Tools.ToDictionary(tool => tool.Name, tool => (float)(tool.Scale ?? 1)));
    }

    private static int AccessoryOrder(string name) => name.StartsWith("C_", StringComparison.Ordinal) ? 0 : name.StartsWith("G_", StringComparison.Ordinal) ? 1 : 2;

    private static string AccessoryText(string name) => name switch
    {
        _ when name.StartsWith("C_", StringComparison.Ordinal) => $"Hat: {Spaced(name[2..])}",
        _ when name.StartsWith("G_", StringComparison.Ordinal) => $"Glasses: {Spaced(name[2..])}",
        "None" => "None",
        _ => $"Unknown ({name})",
    };

    // Readable names for the game's short item names whose item is clear.
    private static string ToolText(string name) => name switch
    {
        "Trwl" => "Trowel",
        "Fpan" => "Frying pan",
        "Pitc" => "Pitcher",
        "Cop" => "Cup",
        "FAndK" => "Fork and knife",
        "Mcas" => "Maracas",
        "Cnut" => "Coconut",
        "SwRing" => "Swim ring",
        _ => Spaced(name),
    };

    // FishingRod as "Fishing rod". Short names, such as Trwl, stay as the game has them.
    private static string Spaced(string name)
    {
        if (name.Length > 0 && name.All(char.IsUpper)) return name[..1] + name[1..].ToLower(CultureInfo.InvariantCulture);
        var words = CamelCase().Split(name).Where(word => word.Length > 0).ToArray();
        return string.Join(" ", words.Select((word, index) => index == 0 ? word : word.ToLower(CultureInfo.InvariantCulture)));
    }

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])")]
    private static partial Regex CamelCase();
}
