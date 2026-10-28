using Newtonsoft.Json;

namespace GamebananaAPI.Data;

public class PreviewImage
{
    [JsonProperty("_sType")]
    public string? Type { get; set; }

    [JsonProperty("_sBaseUrl")]
    public Uri? BaseUrl { get; set; }

    [JsonProperty("_sFile")]
    public string? File { get; set; }

    /// <summary>A 220 pixel wide thumbnail of <see cref="File"/>.</summary>
    [JsonProperty("_sFile220")]
    public string? File220 { get; set; }
}
