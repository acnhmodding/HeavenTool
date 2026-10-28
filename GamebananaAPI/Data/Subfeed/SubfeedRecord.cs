using Newtonsoft.Json;

namespace GamebananaAPI.Data.Subfeed;

public class SubfeedRecord
{
    [JsonProperty("_idRow")]
    public int? IdRow { get; set; }

    [JsonProperty("_sModelName")]
    public string? ModelName { get; set; }

    [JsonProperty("_sSingularTitle")]
    public string? SingularTitle { get; set; }

    [JsonProperty("_sIconClasses")]
    public string? IconClasses { get; set; }

    [JsonProperty("_sName")]
    public string? Name { get; set; }

    [JsonProperty("_sProfileUrl")]
    public Uri? ProfileUrl { get; set; }

    [JsonProperty("_tsDateAdded")]
    public long DateAdded { get; set; }

    [JsonProperty("_tsDateModified")]
    public long DateModified { get; set; }

    [JsonProperty("_bHasFiles")]
    public bool HasFiles { get; set; }

    [JsonProperty("_aTags")]
    public dynamic[]? Tags { get; set; }

    [JsonProperty("_sPayType")]
    public string? PayType { get; set; } // free

    [JsonProperty("_aPreviewContent")]
    public PreviewContent? PreviewContent { get; set; }

    [JsonProperty("_aSubmitter")]
    public Submitter? Submitter { get; set; }

    [JsonProperty("_aRootCategory")]
    public Category? Category { get; set; }

    [JsonProperty("_aSubCategory", NullValueHandling = NullValueHandling.Ignore)]
    public Category? SubCategory { get; set; }

    [JsonProperty("_sVersion", NullValueHandling = NullValueHandling.Ignore)]
    public string? Version { get; set; }

    [JsonProperty("_bIsObsolete", NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsObsolete { get; set; }

    [JsonProperty("_sInitialVisibility")]
    public string? InitialVisibility { get; set; }

    [JsonProperty("_bHasContentRatings")]
    public bool? BHasContentRatings { get; set; }

    [JsonProperty("_nLikeCount", NullValueHandling = NullValueHandling.Ignore)]
    public int LikeCount { get; set; } = 0;

    [JsonProperty("_nPostCount", NullValueHandling = NullValueHandling.Ignore)]
    public long PostCount { get; set; } = 0;

    [JsonProperty("_bWasFeatured", NullValueHandling = NullValueHandling.Ignore)]
    public bool? WasFeatured { get; set; }

    [JsonProperty("_nViewCount")]
    public long? ViewCount { get; set; }

    [JsonProperty("_bIsOwnedByAccessor")]
    public bool IsOwnedByAccessor { get; set; }

    [JsonProperty("_tsDeadline", NullValueHandling = NullValueHandling.Ignore)]
    public long? TsDeadline { get; set; }

    [JsonProperty("_sResolution", NullValueHandling = NullValueHandling.Ignore)]
    public string Resolution { get; set; }

    [JsonProperty("_akResolution", NullValueHandling = NullValueHandling.Ignore)]
    public string AkResolution { get; set; }

    [JsonProperty("_sResolutionMessage", NullValueHandling = NullValueHandling.Ignore)]
    public string SResolutionMessage { get; set; }

    [JsonProperty("_aAdditionalRewards")]
    public dynamic? AdditionalRewards { get; set; }

    [JsonProperty("_nBounty", NullValueHandling = NullValueHandling.Ignore)]
    public long? Bounty { get; set; }

    [JsonProperty("_tsLastBoostDate", NullValueHandling = NullValueHandling.Ignore)]
    public long? TsLastBoostDate { get; set; }

    [JsonProperty("_iCompletionPercentage", NullValueHandling = NullValueHandling.Ignore)]
    public long? ICompletionPercentage { get; set; }

    [JsonProperty("_sFinishedSubmissionUrl", NullValueHandling = NullValueHandling.Ignore)]
    public string FinishedSubmissionUrl { get; set; }

    [JsonProperty("_akDevelopmentState", NullValueHandling = NullValueHandling.Ignore)]
    public string? DevelopmentStateTag { get; set; } // indev

    [JsonProperty("_sDevelopmentState", NullValueHandling = NullValueHandling.Ignore)]
    public string? DevelopmentState { get; set; } // In Development

    [JsonProperty("_nWipCount", NullValueHandling = NullValueHandling.Ignore)]
    public long? WipCount { get; set; }

    [JsonProperty("_bIsStuck", NullValueHandling = NullValueHandling.Ignore)]
    public bool? BIsStuck { get; set; }

    [JsonProperty("_akState", NullValueHandling = NullValueHandling.Ignore)]
    public string AkState { get; set; }

    [JsonProperty("_sState", NullValueHandling = NullValueHandling.Ignore)]
    public string SState { get; set; }

    [JsonProperty("_sStateMessage", NullValueHandling = NullValueHandling.Ignore)]
    public string SStateMessage { get; set; }

    [JsonProperty("_nEntryCount", NullValueHandling = NullValueHandling.Ignore)]
    public long? NEntryCount { get; set; }

    [JsonProperty("_tsDateUpdated", NullValueHandling = NullValueHandling.Ignore)]
    public long? TsDateUpdated { get; set; }
}
