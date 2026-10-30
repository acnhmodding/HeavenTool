using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeavenTool.ModManager.Villagers;
using HeavenTool.Rendering.BFRES;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels.VillagerCreator;

/// <summary>
/// The inside of the house: wallpaper, flooring, music and the items in the room, with a 3D preview, and the vanilla villagers whose
/// room file and Gmo file the new villager starts from.
/// </summary>
public partial class RoomStepViewModel : ObservableObject, IDisposable
{
    private readonly VillagerCreatorViewModel creator;
    private VillagerProject? project;
    private BfresRenderer? renderer;
    private CancellationTokenSource? previewLoad;
    private VillagerRoom? shown;
    private bool previewVisible, framed, loading;

    public RoomStepViewModel(VillagerCreatorViewModel creator)
    {
        this.creator = creator;
        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ItemCountText));
    }

    [ObservableProperty] private IReadOnlyList<VillagerInfo> villagers = [];
    [ObservableProperty] private VillagerInfo? roomFrom;
    [ObservableProperty] private VillagerInfo? gmoFrom;
    [ObservableProperty] private string? gmoWarning;
    [ObservableProperty] private ItemField? wallpaper;
    [ObservableProperty] private ItemField? flooring;
    [ObservableProperty] private ItemField? music;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasSelection)), NotifyCanExecuteChangedFor(nameof(RemoveItemCommand))]
    private RoomItemEntry? selectedItem;
    [ObservableProperty] private bool isPreviewLoading;
    [ObservableProperty] private string? previewWarning;
    [ObservableProperty] private string? renderError;

    public ObservableCollection<RoomItemEntry> Items { get; } = [];
    public bool HasSelection => SelectedItem != null;
    public string ItemCountText => Items.Count == 1 ? "1 item" : $"{Items.Count} items";

    internal void Initialize()
    {
        Villagers = creator.Data.Villagers;
        Wallpaper = Surface("Wallpaper", new("wallpapers", "RoomWall"));
        Flooring = Surface("Flooring", new("floorings", "RoomFloor"));
        Music = Surface("Music", new("songs", "Music"));
    }

    private ItemField Surface(string title, ItemKinds kinds)
    {
        var field = new ItemField(title, "", creator.Data, false, kinds, creator.PickItem);
        field.Changed += _ => Commit();
        return field;
    }

    /// <summary>Connects the viewport the preview draws in.</summary>
    public void Attach(BfresRenderer value)
    {
        renderer = value;
        // The room has its own floor, which the viewport's ground would cut through.
        renderer.ShowGround = false;
    }

    public void ReportRenderFailure(Exception error) => RenderError = error.Message;

    internal void Load(VillagerProject value)
    {
        project = null;
        RoomFrom = Find(value.RoomFrom);
        GmoFrom = Find(value.GmoFrom);
        project = value;
        Check();
        Show(value.Room ?? Read(value.RoomFrom));
    }

    partial void OnRoomFromChanged(VillagerInfo? value)
    {
        if (project == null || value == null) return;
        // Copying another villager's room starts over from its items.
        project.RoomFrom = value.Label;
        project.Room = Read(value.Label);
        Show(project.Room);
    }

    partial void OnGmoFromChanged(VillagerInfo? value)
    {
        if (project == null || value == null) return;
        project.GmoFrom = value.Label;
        Check();
    }

    /// <summary>Checks the Gmo file against the villager's species, which the label can change on another step.</summary>
    internal void Check()
    {
        if (project == null || GmoFrom == null) return;
        var species = VillagerLabel.Species(project.Label);
        if (VillagerLabel.Species(GmoFrom.Label) == species)
        {
            GmoWarning = null;
            return;
        }
        var name = Villagers.FirstOrDefault(villager => VillagerLabel.Species(villager.Label) == species)?.Species.ToLowerInvariant() ?? species;
        GmoWarning = $"{GmoFrom.Name} is a {GmoFrom.Species.ToLowerInvariant()}. A Gmo file belongs to the model, so a {name} fits a {species} label best.";
    }

    private VillagerRoom? Read(string label) =>
        creator.Data.ReadParam(VillagerGenerator.RoomPath(label)) is { } file ? VillagerRoom.Read(file) : null;

    private void Show(VillagerRoom? room)
    {
        loading = true;
        try
        {
            Wallpaper?.Show(room?.Wallpaper);
            Flooring?.Show(room?.Flooring);
            Music?.Show(room?.Music);
            Items.Clear();
            foreach (var item in room?.Items ?? []) Items.Add(Entry(item));
            SelectedItem = null;
        }
        finally { loading = false; }
        RefreshPreview();
    }

    private RoomItemEntry Entry(RoomItem item) => new(item, creator.Data, creator.PickItem, Commit);

    [RelayCommand]
    private async Task AddItem()
    {
        var picker = new ItemPickerViewModel(creator.Data, "Add an item", RoomItemEntry.Furniture, null);
        if (!await creator.PickItem(picker) || picker.Selected is not { } choice) return;
        // A new item goes in the middle of the room, or of the back wall, to be moved from there.
        var size = creator.Data.ItemSize(choice.Item.Id);
        const int middle = (VillagerRoom.FirstCell + VillagerRoom.LastCell + 1) / 2;
        int z = size.Layer == RoomLayer.BackWall ? VillagerRoom.TopRow + 2 : middle - size.Z / 2;
        var entry = Entry(new RoomItem(choice.Item.Id, middle - size.X / 2, z, 0, size.Layer, 0));
        Items.Add(entry);
        SelectedItem = entry;
        Commit();
    }

    private bool CanRemoveItem() => SelectedItem != null;

    [RelayCommand(CanExecute = nameof(CanRemoveItem))]
    private void RemoveItem()
    {
        if (SelectedItem is not { } entry) return;
        int index = Items.IndexOf(entry);
        Items.Remove(entry);
        SelectedItem = Items.Count > 0 ? Items[Math.Min(index, Items.Count - 1)] : null;
        Commit();
    }

    private void Commit()
    {
        if (loading || project == null) return;
        project.Room = new VillagerRoom((int)(Wallpaper?.Value ?? 0), (int)(Flooring?.Value ?? 0), (int)(Music?.Value ?? 0),
            [.. Items.Select(entry => entry.ToRoomItem())]);
        RefreshPreview();
    }

    /// <summary>Starts drawing the preview, the first time the step is shown.</summary>
    internal void ShowPreview()
    {
        previewVisible = true;
        RefreshPreview();
    }

    private async void RefreshPreview()
    {
        if (!previewVisible || renderer == null || project == null) return;
        var room = project.Room ?? Read(project.RoomFrom);
        if (room == null || Same(room, shown)) return;

        previewLoad?.Cancel();
        var request = previewLoad = new CancellationTokenSource();
        IsPreviewLoading = true;
        try
        {
            var built = await creator.RoomPreview.BuildAsync(room, request.Token);
            if (built == null || request.IsCancellationRequested) return;
            // The camera starts looking in through the missing front wall, and stays where the user puts it after an edit.
            renderer.SetScene(built.Geometry, keepCamera: framed);
            if (!framed)
            {
                renderer.SetOrbit(0, .55f);
                // Framing fits the whole room's bounds; the room shows better a little closer.
                renderer.Zoom(2);
            }
            framed = true;
            renderer.SetNativeShaders(built.Shaders);
            PreviewWarning = built.Warnings.Count > 0 ? string.Join(Environment.NewLine, built.Warnings) : null;
            shown = room;
        }
        catch (Exception ex)
        {
            if (!request.IsCancellationRequested) PreviewWarning = $"Couldn't show the room: {ex.Message}";
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

    // Records compare their item lists by reference, so the items are compared one by one. Music doesn't show.
    private static bool Same(VillagerRoom room, VillagerRoom? other) => other != null && room.Wallpaper == other.Wallpaper
        && room.Flooring == other.Flooring && room.Items.SequenceEqual(other.Items);

    private VillagerInfo? Find(string label) => Villagers.FirstOrDefault(villager => villager.Label == label);

    public void Dispose() => previewLoad?.Cancel();
}
