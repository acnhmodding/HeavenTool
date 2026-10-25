//using GamebananaAPI.Converters;
//using GamebananaAPI.Data;
//using GamebananaAPI.Data.Details;
//using Newtonsoft.Json;
//using System.Reflection;

//namespace GamebananaAPI;

//public class GamebananaAPIClient : IDisposable
//{
//    private readonly HttpClient _httpClient;
//    private const string BaseUrl = "https://api.gamebanana.com/Core/";

//    public GamebananaAPIClient()
//    {
//        _httpClient = new HttpClient()
//        {
//            BaseAddress = new Uri(BaseUrl)
//        };
//    }



//    /// <summary>
//    /// List <see cref="ItemData"/> for the the specified <paramref name="gameId"/>
//    /// </summary>
//    /// <param name="gameId">Game page ID</param>
//    /// <param name="type">Mod, Request, etc...</param>
//    /// <param name="page">Pagination starting at 1</param>
//    /// <param name="includeUpdated">Include recently updated entries</param>
//    /// <returns></returns>
//    public async Task<ItemData[]?> ListItemData(int gameId, DataType type = DataType.All, int page = 1, bool includeUpdated = true)
//    {
//        var url = $"List/New?page={page}&gameid={gameId}&include_updated={includeUpdated}";

//        if (type != DataType.All)
//            url += $"&itemtype={type}";

//        var response = await _httpClient.GetStringAsync(url);

//        return JsonConvert.DeserializeObject<ItemData[]>(response, new ItemDataConverter());
//    }

//    public static Type? GetItemDataForType(DataType dataType)
//    {
//        return dataType switch
//        {
//            DataType.Mod => typeof(ModData),
//            _ => null
//        };
//    }

//    private string GetJsonFields(Type type) 
//    {
//        var fields = new List<string>();
//        foreach (var property in type.GetProperties())
//        {
//            var prop = property.GetCustomAttribute<JsonPropertyAttribute>();
//            if (prop != null && !string.IsNullOrEmpty(prop.PropertyName) && !fields.Contains(prop.PropertyName))
//                fields.Add(prop.PropertyName);
//        }
//        return string.Join(",", fields);
//    }

//    public async Task<ItemData[]?> LoadDataAsync(ItemData[] items)
//    {
//        if (items == null || items.Length == 0) return [];

//        List<string> entries = [];

//        var validItems = items.Where(x => GetItemDataForType(x.Type) != null).ToArray();

//        for (int i = 0; i < validItems.Length; i++) 
//        {
//            var obj = validItems[i];
//            var fields = GetJsonFields(GetItemDataForType(obj.Type)!);
//            entries.Add($"itemtype[{i}]={obj.Type}&itemid[{i}]={obj.Id}&fields[{i}]={fields}");
//        }

//        var url = $"Item/Data?return_keys=1&" + string.Join("&", entries);
//        var response = await _httpClient.GetStringAsync(url);

//        var reader = JsonConvert.DeserializeObject<ItemData[]>(response, new BulkItemDataConverter(validItems));

//        return reader;
//    }

//    public void Dispose()
//    {
//        _httpClient?.Dispose();
//    }
//}
