using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeavenTool.ModManager.Villagers;
using System.Linq;

namespace HeavenTool.Avalonia.ViewModels.VillagerCreator;

/// <summary>Whether the villager gets an amiibo, and the IDs it uses.</summary>
public partial class AmiiboStepViewModel(VillagerCreatorViewModel creator) : ObservableObject
{
    private VillagerProject? project;

    [ObservableProperty] private bool isEnabled;
    // Kept while the amiibo is turned off, so turning it back on gives the same IDs.
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CharacterId), nameof(NumberingId), nameof(GameCharacterId))]
    private AmiiboIds? ids;
    [ObservableProperty] private int freeSlots;
    [ObservableProperty] private string? error;

    public string CharacterId => Ids is { } value ? $"0x{value.CharacterId:X8}" : "—";
    public string NumberingId => Ids is { } value ? $"{value.NumberingId}" : "—";
    public string GameCharacterId => Ids is { } value ? $"0x{value.GameCharacterId:X4}" : "—";

    internal void Load(VillagerProject value)
    {
        project = null;
        FreeSlots = AmiiboGenerator.FreeSlots(creator.Data.AmiiboData).Count();
        IsEnabled = value.Amiibo != null;
        Ids = value.Amiibo ?? AmiiboGenerator.PickFree(creator.Data.AmiiboData);
        Error = Ids == null ? "Every amiibo slot after the vanilla villagers is taken." : null;
        project = value;
    }

    partial void OnIsEnabledChanged(bool value) => Apply();

    partial void OnIdsChanged(AmiiboIds? value) => Apply();

    private void Apply()
    {
        if (project != null) project.Amiibo = IsEnabled ? Ids : null;
    }

    /// <summary>Picks another free slot, for when the IDs clash with another modder's villager.</summary>
    [RelayCommand]
    private void PickOther()
    {
        var free = AmiiboGenerator.FreeSlots(creator.Data.AmiiboData).Where(slot => slot != Ids).ToList();
        if (free.Count > 0) Ids = free[System.Random.Shared.Next(free.Count)];
    }
}
