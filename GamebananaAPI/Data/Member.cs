using Newtonsoft.Json;

namespace GamebananaAPI.Data;

/// <summary>A GameBanana member, like the one who submitted a mod.</summary>
public class Member
{
    [JsonProperty("_idRow")]
    public long IdRow { get; set; }

    [JsonProperty("_sName")]
    public string? Name { get; set; }

    [JsonProperty("_sProfileUrl")]
    public Uri? ProfileUrl { get; set; }
}
