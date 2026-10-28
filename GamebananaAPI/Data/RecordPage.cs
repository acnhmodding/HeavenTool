using Newtonsoft.Json;

namespace GamebananaAPI.Data;

/// <summary>A page of a listing, like a game's subfeed or a search.</summary>
public class RecordPage<T>
{
    [JsonProperty("_aMetadata")]
    public PageMetadata? Metadata { get; set; }

    [JsonProperty("_aRecords")]
    public T[] Records { get; set; } = [];
}

public class PageMetadata
{
    [JsonProperty("_nRecordCount")]
    public int RecordCount { get; set; }

    [JsonProperty("_nPerpage")]
    public int PerPage { get; set; }

    /// <summary>Whether this is the last page.</summary>
    [JsonProperty("_bIsComplete")]
    public bool IsComplete { get; set; }
}
