using GamebananaAPI.Data;
using Newtonsoft.Json;
using System.Globalization;

namespace GamebananaAPI;

public class GamebananaClient
{
    // Dates come as Unix timestamps; nothing should be read as a date.
    private static readonly JsonSerializerSettings JsonSettings = new() { DateParseHandling = DateParseHandling.None };

    private readonly HttpClient _httpClient = new() { BaseAddress = new Uri("https://gamebanana.com/apiv13/") };

    public GamebananaClient()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HeavenTool");
    }

    /// <summary>
    /// Get the files a submission offers for download
    /// </summary>
    public Task<DownloadPage?> GetDownloadPage(int modId, CancellationToken cancellation = default) =>
        GetAsync<DownloadPage>($"Mod/{modId}/DownloadPage", cancellation);

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
        return text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// List a game's mods the way its GameBanana page does. The subfeed ignores filters; see <see cref="GetModIndex"/>
    /// </summary>
    public Task<RecordPage<ModRecord>?> GetSubfeed(int gameId, SubfeedSort sort = SubfeedSort.New, int page = 1, string? query = null, CancellationToken cancellation = default) =>
        GetAsync<RecordPage<ModRecord>>($"Game/{gameId}/Subfeed" + Query(
            ("_nPage", page),
            ("_sSort", sort.ToString().ToLowerInvariant()),
            ("_csvModelInclusions", "Mod"),
            ("_sName", query)), cancellation);

    /// <summary>
    /// List a game's mods narrowed by a filter. Unlike the subfeed, Mod/Index honors filters and more sorts
    /// </summary>
    public Task<RecordPage<ModRecord>?> GetModIndex(int gameId, ModFilter filter, int page = 1, int perPage = 15, CancellationToken cancellation = default) =>
        GetAsync<RecordPage<ModRecord>>("Mod/Index" + Query(
            ("_nPage", page),
            ("_nPerpage", perPage),
            ("_aFilters[Generic_Game]", gameId),
            ("_aFilters[Generic_Name]", string.IsNullOrWhiteSpace(filter.Name) ? null : "contains," + filter.Name.Trim()),
            ("_aFilters[Generic_Category]", filter.CategoryId),
            ("_aFilters[Generic_Submitter]", filter.SubmitterId),
            ("_aFilters[Generic_WasFeatured]", filter.FeaturedOnly ? "true" : null),
            ("_sSort", filter.Sort != ModSort.Default ? "Generic_" + filter.Sort : null)), cancellation);

    /// <summary>
    /// Get a game's top-level mod categories, A to Z
    /// </summary>
    public Task<ModCategory[]> GetModCategories(int gameId, CancellationToken cancellation = default) =>
        GetCategories(("_idGameRow", gameId), cancellation);

    /// <summary>
    /// Get the subcategories of a mod category, A to Z
    /// </summary>
    public Task<ModCategory[]> GetModSubcategories(int categoryId, CancellationToken cancellation = default) =>
        GetCategories(("_idCategoryRow", categoryId), cancellation);

    private async Task<ModCategory[]> GetCategories((string, object?) parent, CancellationToken cancellation) =>
        await GetAsync<ModCategory[]>("Mod/Categories" + Query(parent, ("_sSort", "a_to_z"), ("_bShowEmpty", "true")), cancellation) ?? [];

    /// <summary>
    /// Search GameBanana's members by name. The search matches any word of the name, so the results can be loose
    /// </summary>
    public async Task<Member[]> FindMembers(string name, CancellationToken cancellation = default) =>
        (await GetAsync<RecordPage<Member>>("Util/Search/Results" + Query(
            ("_sModelName", "Member"),
            ("_sSearchString", name),
            ("_nPage", 1)), cancellation))?.Records ?? [];

    /// <summary>
    /// Get a mod's tags, categories, files and version
    /// </summary>
    public Task<ModProfile?> GetProfilePage(int modId, CancellationToken cancellation = default) =>
        GetAsync<ModProfile>($"Mod/{modId}/ProfilePage", cancellation);

    private async Task<T?> GetAsync<T>(string url, CancellationToken cancellation) =>
        JsonConvert.DeserializeObject<T>(await _httpClient.GetStringAsync(url, cancellation), JsonSettings);

    // Parameters without a value are left out.
    private static string Query(params (string Name, object? Value)[] parameters) =>
        "?" + string.Join("&", parameters
            .Select(parameter => (parameter.Name, Value: Convert.ToString(parameter.Value, CultureInfo.InvariantCulture)))
            .Where(parameter => !string.IsNullOrEmpty(parameter.Value))
            .Select(parameter => $"{Uri.EscapeDataString(parameter.Name)}={Uri.EscapeDataString(parameter.Value!)}"));
}
