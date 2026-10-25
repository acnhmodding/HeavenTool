using Newtonsoft.Json;

namespace GamebananaAPI.Data;

public class PreviewMediaMetadata
{
    [JsonProperty("_nBounty", NullValueHandling = NullValueHandling.Ignore)]
    public long? Bounty { get; set; }

    [JsonProperty("_sSnippet")]
    public string? Snippet { get; set; }

    [JsonProperty("_sState", NullValueHandling = NullValueHandling.Ignore)]
    public string? State { get; set; }

    [JsonProperty("_nPostCount", NullValueHandling = NullValueHandling.Ignore)]
    public long? PostCount { get; set; }

    [JsonProperty("_secTimeLeft", NullValueHandling = NullValueHandling.Ignore)]
    public long? TimeLeftInSeconds { get; set; }
}
