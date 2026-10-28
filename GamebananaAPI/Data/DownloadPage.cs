using Newtonsoft.Json;

namespace GamebananaAPI.Data;

/// <summary>The files a submission offers for download.</summary>
public class DownloadPage
{
    [JsonProperty("_aFiles")]
    public GamebananaFile[] Files { get; set; } = [];

    public static DownloadPage? FromJson(string json) => JsonConvert.DeserializeObject<DownloadPage>(json, GamebananaJsonConverter.Settings);
}

public class GamebananaFile
{
    [JsonProperty("_idRow")]
    public int IdRow { get; set; }

    [JsonProperty("_sFile")]
    public string FileName { get; set; } = "";

    [JsonProperty("_nFilesize")]
    public long Size { get; set; }

    [JsonProperty("_tsDateAdded")]
    public long DateAdded { get; set; }

    [JsonProperty("_nDownloadCount")]
    public long DownloadCount { get; set; }

    [JsonProperty("_sDownloadUrl")]
    public Uri? DownloadUrl { get; set; }

    [JsonProperty("_sMd5Checksum")]
    public string? Md5Checksum { get; set; }

    [JsonProperty("_sVersion")]
    public string? Version { get; set; }

    [JsonProperty("_sDescription")]
    public string? Description { get; set; }

    /// <summary>GameBanana's virus scan result: "clean" once the file passed.</summary>
    [JsonProperty("_sAvResult")]
    public string? AvResult { get; set; }
}
