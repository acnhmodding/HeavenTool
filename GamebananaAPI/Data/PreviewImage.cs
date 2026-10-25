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

    // Todo: Convert this to ImageData
    // with URL, Width, Height
    // use some regex stuff or JsonConverters
    // _sFile220 | _hFile220 | _wFile220
}
