using CommunityToolkit.Mvvm.ComponentModel;
using HeavenTool.ModManager.Villagers;
using System.Collections.Generic;
using System.Linq;

namespace HeavenTool.Avalonia.ViewModels.VillagerCreator;

/// <summary>The vanilla villagers whose room and Gmo file the new villager copies.</summary>
public partial class RoomStepViewModel(VillagerCreatorViewModel creator) : ObservableObject
{
    private VillagerProject? project;

    [ObservableProperty] private IReadOnlyList<VillagerInfo> villagers = [];
    [ObservableProperty] private VillagerInfo? roomFrom;
    [ObservableProperty] private VillagerInfo? gmoFrom;
    [ObservableProperty] private string? gmoWarning;

    internal void Initialize() => Villagers = creator.Data.Villagers;

    internal void Load(VillagerProject value)
    {
        project = null;
        RoomFrom = Find(value.RoomFrom);
        GmoFrom = Find(value.GmoFrom);
        project = value;
        Check();
    }

    partial void OnRoomFromChanged(VillagerInfo? value)
    {
        if (project != null && value != null) project.RoomFrom = value.Label;
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

    private VillagerInfo? Find(string label) => Villagers.FirstOrDefault(villager => villager.Label == label);
}
