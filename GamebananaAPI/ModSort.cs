namespace GamebananaAPI;

/// <summary>The orders Mod/Index can list mods in.</summary>
public enum ModSort
{
    /// <summary>GameBanana's own order when none is asked for, newest first.</summary>
    Default,
    Newest,
    LatestUpdated,
    MostLiked,
    MostDownloaded,
    MostViewed,
    Alphabetically,
}
