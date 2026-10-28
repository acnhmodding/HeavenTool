using GamebananaAPI.Data;
using GamebananaAPI.Data.Subfeed;
using System.Text.RegularExpressions;

namespace GamebananaAPI;

public partial class GamebananaClient
{
    private readonly HttpClient _httpClient;
    private const string BASE_URL = "https://gamebanana.com/apiv11/";

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
    /// Get all files that will be downloaded from a specific file (zip)
    /// </summary>
    /// <param name="fileId"></param>
    /// <returns></returns>
    public async Task<string[]> GetRawFileList(uint fileId)
    {
        var text = await _httpClient.GetStringAsync($"File/{fileId}/RawFileList");
        var result = NewLineRegex().Split(text);
        return result;
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

    [GeneratedRegex("\r\n|\r|\n")]
    private static partial Regex NewLineRegex();
}