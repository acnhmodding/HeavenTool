using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GamebananaAPI;

internal static class GamebananaJsonConverter
{
    public static readonly JsonSerializerSettings Settings = new()
    {
        DefaultValueHandling = DefaultValueHandling.Ignore,
        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
        DateParseHandling = DateParseHandling.None,
        Converters =
        {
            new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
        },
    };
}
