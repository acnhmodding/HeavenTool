using Newtonsoft.Json;

namespace GamebananaAPI.Data;

public class SubjectShaper
{
    [JsonProperty("_sBorderStyle")]
    public string? BorderStyle { get; set; }

    [JsonProperty("_sFont")]
    public string? Font { get; set; }

    [JsonProperty("_sTextColor")]
    public string? TextColor { get; set; }

    [JsonProperty("_sTextHoverColor")]
    public string? TextHoverColor { get; set; }
}
