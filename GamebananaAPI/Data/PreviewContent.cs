using Newtonsoft.Json;

namespace GamebananaAPI.Data;

public class PreviewContent
{
    /// <summary>The submission's first screenshot.</summary>
    [JsonProperty("screenshot", NullValueHandling = NullValueHandling.Ignore)]
    public PreviewImage? Screenshot { get; set; }
}
