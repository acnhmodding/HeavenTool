using Newtonsoft.Json;

namespace GamebananaAPI.Data.Subfeed;

public class Subfeed
{
    [JsonProperty("_aMetadata")]
    public SubfeedMetadata? Metadata { get; set; }

    [JsonProperty("_aRecords")]
    public SubfeedRecord[]? Records { get; set; }

    public static Subfeed? FromJson(string json) => JsonConvert.DeserializeObject<Subfeed>(json, GamebananaJsonConverter.Settings);
    public string ToJson() => JsonConvert.SerializeObject(this, GamebananaJsonConverter.Settings);
}
