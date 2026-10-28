using GamebananaAPI.Data;
using GamebananaAPI.Data.Subfeed;
using System.Text.RegularExpressions;

namespace GamebananaAPI;

public partial class GamebananaClient
{
    private readonly HttpClient _httpClient;
    private const string BASE_URL = "https://gamebanana.com/apiv13/";

    public GamebananaClient()
    {
        _httpClient = new HttpClient()
        {
            BaseAddress = new Uri(BASE_URL)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HeavenTool");
    }

    /// <summary>
    /// Get the files a submission offers for download
    /// </summary>
    public async Task<DownloadPage?> GetDownloadPage(int modId, CancellationToken cancellation = default)
    {
        var response = await _httpClient.GetStringAsync($"Mod/{modId}/DownloadPage", cancellation);
        return DownloadPage.FromJson(response);
    }

    /// <summary>
    /// Download a file, reporting the bytes received so far
    /// </summary>
    public async Task DownloadFile(GamebananaFile file, Stream destination, IProgress<long>? progress = null, CancellationToken cancellation = default)
    {
        if (file.DownloadUrl == null) throw new InvalidOperationException($"{file.FileName} has no download link.");
        using var response = await _httpClient.GetAsync(file.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellation);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellation);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellation)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellation);
            total += read;
            progress?.Report(total);
        }
    }

    /// <summary>
    /// Get the paths of the files inside a downloadable archive, without downloading it
    /// </summary>
    public async Task<string[]> GetRawFileList(int fileId, CancellationToken cancellation = default)
    {
        var text = await _httpClient.GetStringAsync($"File/{fileId}/RawFileList", cancellation);
        // A missing file can come back as a 200 with a JSON error instead of a 404.
        if (text.StartsWith('{') && text.Contains("\"_sErrorCode\""))
            throw new HttpRequestException($"GameBanana couldn't list the files of {fileId}: {text}", null, System.Net.HttpStatusCode.NotFound);
        return NewLineRegex().Split(text).Where(line => line.Length > 0).ToArray();
    }

    public async Task<Subfeed?> GetSubfeed(int gameId, int page = 1, SortType sortMode = SortType.New, Category category = Category.Mod, string query = "", CancellationToken cancellation = default)
    {
        var queryString = System.Web.HttpUtility.ParseQueryString(string.Empty);
        var sortString = sortMode.ToString().ToLower();

        queryString.Add("_nPage", page.ToString());
        queryString.Add("_sSort", sortString);

        if (category != Category.Any)
            queryString.Add("_csvModelInclusions", category.ToString());

        if (!string.IsNullOrEmpty(query))
            queryString.Add("_sName", query);
       

        var url = $"Game/{gameId}/Subfeed?{queryString}";

        var response = await _httpClient.GetStringAsync(url, cancellation);

        return Subfeed.FromJson(response);
    }

    /// <summary>
    /// List a game's mods narrowed by a filter. Unlike the subfeed, Mod/Index honors filters and more sorts
    /// </summary>
    public async Task<Subfeed?> GetModIndex(int gameId, ModFilter filter, int page = 1, int perPage = 15, CancellationToken cancellation = default)
    {
        var queryString = System.Web.HttpUtility.ParseQueryString(string.Empty);
        queryString.Add("_nPage", page.ToString());
        queryString.Add("_nPerpage", perPage.ToString());
        queryString.Add("_aFilters[Generic_Game]", gameId.ToString());

        if (!string.IsNullOrWhiteSpace(filter.Name))
            queryString.Add("_aFilters[Generic_Name]", "contains," + filter.Name.Trim());
        if (filter.CategoryId is { } categoryId)
            queryString.Add("_aFilters[Generic_Category]", categoryId.ToString());
        if (filter.SubmitterId is { } submitterId)
            queryString.Add("_aFilters[Generic_Submitter]", submitterId.ToString());
        if (filter.FeaturedOnly)
            queryString.Add("_aFilters[Generic_WasFeatured]", "true");
        if (filter.Sort != ModSort.Default)
            queryString.Add("_sSort", "Generic_" + filter.Sort);

        var response = await _httpClient.GetStringAsync($"Mod/Index?{queryString}", cancellation);
        return Subfeed.FromJson(response);
    }

    /// <summary>
    /// Get a game's top-level mod categories, A to Z
    /// </summary>
    public async Task<ModCategory[]> GetModCategories(int gameId, CancellationToken cancellation = default)
    {
        var response = await _httpClient.GetStringAsync($"Mod/Categories?_idGameRow={gameId}&_sSort=a_to_z&_bShowEmpty=true", cancellation);
        return ModCategory.ListFromJson(response);
    }

    /// <summary>
    /// Get the subcategories of a mod category, A to Z
    /// </summary>
    public async Task<ModCategory[]> GetModSubcategories(int categoryId, CancellationToken cancellation = default)
    {
        var response = await _httpClient.GetStringAsync($"Mod/Categories?_idCategoryRow={categoryId}&_sSort=a_to_z&_bShowEmpty=true", cancellation);
        return ModCategory.ListFromJson(response);
    }

    /// <summary>
    /// Search GameBanana's members by name
    /// </summary>
    public async Task<SubfeedRecord[]> FindMembers(string name, CancellationToken cancellation = default)
    {
        var queryString = System.Web.HttpUtility.ParseQueryString(string.Empty);
        queryString.Add("_sModelName", "Member");
        queryString.Add("_sSearchString", name);
        queryString.Add("_nPage", "1");

        var response = await _httpClient.GetStringAsync($"Util/Search/Results?{queryString}", cancellation);
        return Subfeed.FromJson(response)?.Records ?? [];
    }

    /// <summary>
    /// Get a mod's tags and categories
    /// </summary>
    public async Task<ModProfile?> GetProfilePage(int modId, CancellationToken cancellation = default)
    {
        var response = await _httpClient.GetStringAsync($"Mod/{modId}/ProfilePage", cancellation);
        return ModProfile.FromJson(response);
    }

    [GeneratedRegex("\r\n|\r|\n")]
    private static partial Regex NewLineRegex();
}