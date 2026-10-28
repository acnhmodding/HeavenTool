using Newtonsoft.Json;

namespace GamebananaAPI.Data;

/// <summary>A mod as a listing shows it.</summary>
public class ModRecord
{
    [JsonProperty("_idRow")]
    public int? IdRow { get; set; }

    [JsonProperty("_sName")]
    public string? Name { get; set; }

    [JsonProperty("_sProfileUrl")]
    public Uri? ProfileUrl { get; set; }

    [JsonProperty("_tsDateAdded")]
    public long DateAdded { get; set; }

    [JsonProperty("_tsDateModified")]
    public long DateModified { get; set; }

    [JsonProperty("_aPreviewContent")]
    public PreviewContent? PreviewContent { get; set; }

    [JsonProperty("_aSubmitter")]
    public Member? Submitter { get; set; }

    /// <summary>The root category. Its <see cref="ModCategory.IdRow"/> isn't listed.</summary>
    [JsonProperty("_aRootCategory")]
    public ModCategory? Category { get; set; }

    [JsonProperty("_aSubCategory")]
    public ModCategory? SubCategory { get; set; }

    /// <summary>Whether the mod has content ratings, like nudity, that hide it from some users.</summary>
    [JsonProperty("_bHasContentRatings", NullValueHandling = NullValueHandling.Ignore)]
    public bool HasContentRatings { get; set; }

    [JsonProperty("_nLikeCount", NullValueHandling = NullValueHandling.Ignore)]
    public int LikeCount { get; set; }

    [JsonProperty("_nViewCount")]
    public long? ViewCount { get; set; }
}
