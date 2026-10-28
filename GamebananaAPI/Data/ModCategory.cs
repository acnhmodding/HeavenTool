using Newtonsoft.Json;

namespace GamebananaAPI.Data;

/// <summary>A mod category of a game, as Mod/Categories and a profile page list it.</summary>
public class ModCategory
{
    [JsonProperty("_idRow")]
    public int IdRow { get; set; }

    [JsonProperty("_sName")]
    public string Name { get; set; } = "";

    /// <summary>Mods directly in this category, not in its subcategories.</summary>
    [JsonProperty("_nItemCount")]
    public int ItemCount { get; set; }

    [JsonProperty("_nCategoryCount")]
    public int CategoryCount { get; set; }

    [JsonProperty("_sIconUrl")]
    public string? IconUrl { get; set; }

    public static ModCategory[] ListFromJson(string json) => JsonConvert.DeserializeObject<ModCategory[]>(json, GamebananaJsonConverter.Settings) ?? [];
}
