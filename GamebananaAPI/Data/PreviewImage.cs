using Newtonsoft.Json;

namespace GamebananaAPI.Data;

public class PreviewContent
{
    /// <summary>The submission's first screenshot.</summary>
    [JsonProperty("screenshot")]
    public PreviewImage? Screenshot { get; set; }
}

public class PreviewImage
{
    [JsonProperty("_sBaseUrl")]
    public Uri? BaseUrl { get; set; }

    [JsonProperty("_sFile")]
    public string? File { get; set; }

    /// <summary>A 220 pixel wide thumbnail of <see cref="File"/>.</summary>
    [JsonProperty("_sFile220")]
    public string? File220 { get; set; }

    /// <summary>A 530 pixel wide thumbnail of <see cref="File"/>.</summary>
    [JsonProperty("_sFile530")]
    public string? File530 { get; set; }
}
