using Newtonsoft.Json;

namespace GamebananaAPI.Data;

/// <summary>The files a submission offers for download.</summary>
public class DownloadPage
{
    [JsonProperty("_aFiles")]
    public GamebananaFile[] Files { get; set; } = [];
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

    /// <summary>Whether the author moved the file to the profile's archived files, usually because a newer one replaces it.</summary>
    [JsonProperty("_bIsArchived")]
    public bool IsArchived { get; set; }
}
