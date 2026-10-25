using Newtonsoft.Json;

namespace GamebananaAPI.Data;

public class Category
{
    [JsonProperty("_sName")]
    public string? Name { get; set; }

    [JsonProperty("_sProfileUrl")]
    public Uri ProfileUrl { get; set; }

    [JsonProperty("_sIconUrl")]
    public string? IconUrl { get; set; }
}
