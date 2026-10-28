using Newtonsoft.Json;

namespace GamebananaAPI.Data;

public class PreviewImage
{
    [JsonProperty("_sBaseUrl")]
    public Uri? BaseUrl { get; set; }

    [JsonProperty("_sFile")]
    public string? File { get; set; }

    /// <summary>A 220 pixel wide thumbnail of <see cref="File"/>.</summary>
    [JsonProperty("_sFile220")]
    public string? File220 { get; set; }

    /// <summary>A safe for work version of <see cref="File220"/>, only on submissions with content ratings.</summary>
    [JsonProperty("_sFile220Sfw")]
    public string? File220Sfw { get; set; }

    /// <summary>A 530 pixel wide thumbnail of <see cref="File"/>.</summary>
    [JsonProperty("_sFile530")]
    public string? File530 { get; set; }
}
