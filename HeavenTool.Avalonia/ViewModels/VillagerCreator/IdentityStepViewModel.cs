using CommunityToolkit.Mvvm.ComponentModel;
using HeavenTool.ModManager.NookKit;
using HeavenTool.ModManager.Villagers;
using System.Collections.Generic;
using System.Linq;

namespace HeavenTool.Avalonia.ViewModels.VillagerCreator;

/// <summary>A language's name, catchphrase and motto. Empty text falls back to the default.</summary>
public sealed partial class TranslationViewModel(string language, string displayName, VillagerProject project) : ObservableObject
{
    public string Language { get; } = language;
    public string DisplayName { get; } = displayName;

    [ObservableProperty] private string name = Text(project.Name, language);
    [ObservableProperty] private string catchphrase = Text(project.Catchphrase, language);
    [ObservableProperty] private string motto = Text(project.Motto, language);

    partial void OnNameChanged(string value) => Set(project.Name, value);
    partial void OnCatchphraseChanged(string value) => Set(project.Catchphrase, value);
    partial void OnMottoChanged(string value) => Set(project.Motto, value);

    private void Set(Dictionary<string, string> text, string value)
    {
        if (value.Length > 0) text[Language] = value;
        else text.Remove(Language);
    }

    private static string Text(Dictionary<string, string> text, string language) => text.GetValueOrDefault(language) ?? "";
}

/// <summary>The villager's label, UniqueID, name, catchphrase and motto.</summary>
public partial class IdentityStepViewModel(VillagerCreatorViewModel creator) : ObservableObject
{
    private static readonly Dictionary<string, string> LanguageNames = new()
    {
        ["USen"] = "English (US)", ["USes"] = "Spanish (US)", ["USfr"] = "French (Canada)", ["EUen"] = "English (Europe)",
        ["EUde"] = "German", ["EUes"] = "Spanish (Europe)", ["EUfr"] = "French (Europe)", ["EUit"] = "Italian", ["EUnl"] = "Dutch",
        ["EUru"] = "Russian", ["JPja"] = "Japanese", ["KRko"] = "Korean", ["CNzh"] = "Chinese (Simplified)", ["TWzh"] = "Chinese (Traditional)",
    };

    private VillagerProject? project;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(ModelPath))] private string label = "";
    [ObservableProperty] private string? labelError;
    [ObservableProperty] private decimal? uniqueId;
    [ObservableProperty] private string? uniqueIdError;
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string catchphrase = "";
    [ObservableProperty] private string motto = "";
    [ObservableProperty] private IReadOnlyList<TranslationViewModel> translations = [];

    /// <summary>Where the game looks for the villager's model, which the creator doesn't make.</summary>
    public string ModelPath => $"romfs/Model/{VillagerLabel.ModelName(Label)}.Nin_NX_NVN.zs";

    internal void Load(VillagerProject value)
    {
        project = null;
        Label = value.Label;
        UniqueId = RowValues.Integer(value.Row, RowValues.Key(creator.Data.NmlNpcParam, VillagerColumns.UniqueId));
        Name = value.Name.GetValueOrDefault(MessageEntry.DefaultLanguage) ?? "";
        Catchphrase = value.Catchphrase.GetValueOrDefault(MessageEntry.DefaultLanguage) ?? "";
        Motto = value.Motto.GetValueOrDefault(MessageEntry.DefaultLanguage) ?? "";
        Translations = [.. VillagerGameData.Languages.Select(language => new TranslationViewModel(language, LanguageNames[language], value))];
        project = value;
        CheckLabel();
        CheckUniqueId();
    }

    partial void OnLabelChanged(string value)
    {
        if (project == null) return;
        project.Label = value;
        project.Row[RowValues.Key(creator.Data.NmlNpcParam, VillagerColumns.Label)] = value;
        project.Row[RowValues.Key(creator.Data.NmlNpcParam, VillagerColumns.ResName)] = VillagerLabel.ModelName(value);
        CheckLabel();
        creator.UpdateTitle();
    }

    partial void OnUniqueIdChanged(decimal? value)
    {
        if (project == null || value is not { } id) return;
        project.Row[RowValues.Key(creator.Data.NmlNpcParam, VillagerColumns.UniqueId)] = (long)id;
        project.Row[RowValues.Key(creator.Data.NmlNpcParam, VillagerColumns.UniqueIdCopy)] = (long)id;
        CheckUniqueId();
    }

    partial void OnNameChanged(string value)
    {
        if (project == null) return;
        project.Name[MessageEntry.DefaultLanguage] = value;
        creator.UpdateTitle();
    }

    partial void OnCatchphraseChanged(string value)
    {
        if (project != null) project.Catchphrase[MessageEntry.DefaultLanguage] = value;
    }

    partial void OnMottoChanged(string value)
    {
        if (project != null) project.Motto[MessageEntry.DefaultLanguage] = value;
    }

    private void CheckLabel()
    {
        var owner = creator.Data.Villagers.FirstOrDefault(villager => villager.Label == Label);
        LabelError = !VillagerLabel.IsValid(Label) ? "Use a three-letter species code and two digits, such as wol13."
            : owner != null ? $"{owner.Name} already uses {Label}."
            : null;
    }

    private void CheckUniqueId()
    {
        var table = creator.Data.NmlNpcParam;
        var column = table.ColumnIndex(VillagerColumns.UniqueId);
        var owner = creator.Data.Villagers.FirstOrDefault(villager => (ushort)table.Entries[villager.Row][column] == UniqueId);
        UniqueIdError = owner != null ? $"{owner.Name} already uses {UniqueId}." : null;
    }
}
