using CommunityToolkit.Mvvm.ComponentModel;
using HeavenTool.ModManager.Villagers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HeavenTool.Avalonia.ViewModels.VillagerCreator;

/// <summary>The first step: the vanilla villager the new one starts as a copy of.</summary>
public partial class DonorStepViewModel(VillagerCreatorViewModel creator) : ObservableObject
{
    private IReadOnlyList<VillagerInfo> villagers = [];

    [ObservableProperty] private string search = "";
    [ObservableProperty] private IReadOnlyList<VillagerInfo> results = [];
    [ObservableProperty] private VillagerInfo? selected;
    [ObservableProperty] private string? error;

    internal void Initialize()
    {
        villagers = creator.Data.Villagers;
        Filter();
    }

    internal void Load(VillagerProject project)
    {
        Search = "";
        Selected = villagers.FirstOrDefault(villager => villager.Label == project.Donor);
    }

    partial void OnSearchChanged(string value) => Filter();

    private void Filter()
    {
        var text = Search.Trim();
        Results = text.Length == 0
            ? villagers
            : [.. villagers.Where(villager => villager.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase)
                || villager.Label.Contains(text, StringComparison.OrdinalIgnoreCase))];
    }

    partial void OnSelectedChanged(VillagerInfo? value)
    {
        // Picking the villager the project is already based on keeps its edits.
        if (value == null || creator.Project?.Donor == value.Label) return;
        Error = null;
        creator.Open(VillagerProject.FromDonor(creator.Data, value), null);
    }
}
