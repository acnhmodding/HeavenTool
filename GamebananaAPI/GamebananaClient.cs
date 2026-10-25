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

    public async Task<Subfeed?> GetSubfeed(int gameId, int page = 1, SortType sortMode = SortType.New, Category category = Category.Mod, string query = "")
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

        var response = await _httpClient.GetStringAsync(url);

        return Subfeed.FromJson(response);
    }

    [GeneratedRegex("\r\n|\r|\n")]
    private static partial Regex NewLineRegex();
}