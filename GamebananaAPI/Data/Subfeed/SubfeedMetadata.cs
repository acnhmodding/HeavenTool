using Newtonsoft.Json;

namespace GamebananaAPI.Data.Subfeed;

public class SubfeedMetadata
{
    [JsonProperty("_nRecordCount")]
    public int RecordCount { get; set; }

    [JsonProperty("_nPerpage")]
    public int PerPage { get; set; }

    [JsonProperty("_bIsComplete")]
    public bool IsComplete { get; set; }
}
