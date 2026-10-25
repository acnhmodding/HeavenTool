using Newtonsoft.Json;

namespace GamebananaAPI.Data;

public class Submitter
{
    [JsonProperty("_idRow")]
    public long IdRow { get; set; }

    [JsonProperty("_sName")]
    public string? Name { get; set; }

    [JsonProperty("_bIsOnline")]
    public bool IsOnline { get; set; }

    [JsonProperty("_bHasRipe")]
    public bool HasRipe { get; set; }

    [JsonProperty("_sProfileUrl")]
    public Uri? ProfileUrl { get; set; }

    [JsonProperty("_sAvatarUrl")]
    public Uri? AvatarUrl { get; set; }

    [JsonProperty("_sHdAvatarUrl", NullValueHandling = NullValueHandling.Ignore)]
    public Uri? AvatarUrlHD { get; set; }

    [JsonProperty("_aClearanceLevels", NullValueHandling = NullValueHandling.Ignore)]
    public string[]? ClearanceLevels { get; set; }

    [JsonProperty("_sUpicUrl", NullValueHandling = NullValueHandling.Ignore)]
    public Uri? UpicUrl { get; set; }

    [JsonProperty("_aSubjectShaper", NullValueHandling = NullValueHandling.Ignore)]
    public SubjectShaper? SubjectShaper { get; set; }

    [JsonProperty("_sSubjectShaperCssCode", NullValueHandling = NullValueHandling.Ignore)]
    public string? SubjectShaperCssCode { get; set; }
}
