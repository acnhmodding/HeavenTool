//using Newtonsoft.Json.Linq;
//using Newtonsoft.Json;
//using GamebananaAPI.Data;

//namespace GamebananaAPI.Converters;

//public class ItemDataConverter : JsonConverter<ItemData[]>
//{
//    public override ItemData[]? ReadJson(JsonReader reader, Type objectType, ItemData[]? existingValue, bool hasExistingValue, JsonSerializer serializer)
//    {
//        var result = new List<ItemData>();
//        var array = JArray.Load(reader);

//        foreach (var item in array)
//        {
//            var innerArray = (JArray)item;
            
//            var type = Enum.Parse<DataType>(innerArray[0].ToString());
//            result.Add(new ItemData(type, innerArray[1].Value<int>()));
//        }

//        return [.. result];
//    }

//    public override void WriteJson(JsonWriter writer, ItemData[]? value, JsonSerializer serializer)
//    {
//        throw new NotImplementedException();
//    }
//}
