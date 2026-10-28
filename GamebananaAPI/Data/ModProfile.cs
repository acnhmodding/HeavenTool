using Newtonsoft.Json;

namespace GamebananaAPI.Data;

/// <summary>The parts of a mod's profile page that the subfeed leaves out.</summary>
public class ModProfile
{
    [JsonProperty("_aTags", NullValueHandling = NullValueHandling.Ignore)]
    public ModTag[] Tags { get; set; } = [];

    /// <summary>The mod's own category, a subcategory when <see cref="SuperCategory"/> is set.</summary>
    [JsonProperty("_aCategory")]
    public ModCategory? Category { get; set; }

    /// <summary>The root category, absent when <see cref="Category"/> is one.</summary>
    [JsonProperty("_aSuperCategory")]
    public ModCategory? SuperCategory { get; set; }

    /// <summary>The files offered for download, the same as the download page's.</summary>
    [JsonProperty("_aFiles", NullValueHandling = NullValueHandling.Ignore)]
    public GamebananaFile[] Files { get; set; } = [];

    /// <summary>The mod's current version, as its author wrote it.</summary>
    [JsonProperty("_sVersion")]
    public string? Version { get; set; }
}

public class ModTag
{
    [JsonProperty("_sTitle")]
    public string? Title { get; set; }

    [JsonProperty("_sValue")]
    public string Value { get; set; } = "";

    public override string ToString() => string.IsNullOrEmpty(Title) ? Value : $"{Title}: {Value}";
}
