//using Newtonsoft.Json.Linq;
//using Newtonsoft.Json;
//using GamebananaAPI.Data;

//namespace GamebananaAPI.Converters;

//internal class BulkItemDataConverter(ItemData[] data) : JsonConverter<ItemData[]>
//{
//    public override ItemData[]? ReadJson(JsonReader reader, Type objectType, ItemData[]? existingValue, bool hasExistingValue, JsonSerializer serializer)
//    {
//        var result = new List<ItemData>();
//        var array = JArray.Load(reader);
//        if (array == null || data == null) return null;

//        foreach (var (item, index) in array.Select((x, i) => (x, i)))
//        {
//            var type = GamebananaAPIClient.GetItemDataForType(data[index].Type);
//            if (type == null) continue;

//            ItemData? add = (ItemData?) item.ToObject(type);
//            if (add != null)
//                result.Add(add);
//        }

//        return [.. result];
//    }

//    public override void WriteJson(JsonWriter writer, ItemData[]? value, JsonSerializer serializer)
//    {
//        throw new NotImplementedException();
//    }
//}
