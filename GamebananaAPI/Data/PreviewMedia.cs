using Newtonsoft.Json;

namespace GamebananaAPI.Data;

public class PreviewMedia
{
    [JsonProperty("_aImages", NullValueHandling = NullValueHandling.Ignore)]
    public PreviewImage[]? Data { get; set; }

    [JsonProperty("_aMetadata", NullValueHandling = NullValueHandling.Ignore)]
    public PreviewMediaMetadata? Metadata { get; set; }
}
