#nullable enable

using AeonSake.NintendoTools.FileFormats.Byml;
using HeavenTool.IO;
using HeavenTool.ModManager.NookKit;

namespace HeavenTool.ModManager.Villagers;

/// <summary>Three numbers, as a Gmo file stores a position, a rotation in radians, a scale or a range.</summary>
public readonly record struct GmoVector(float X, float Y, float Z);

/// <summary>Where a hat or glasses sit on the head bone, rotation in radians. <paramref name="EarSet"/> is 0 to 3.</summary>
public sealed record GmoAccessory(GmoVector Position, GmoVector Rotation, GmoVector Scale, int EarSet);

/// <summary>
/// <c>Param/Gmo/Gmo_&lt;label&gt;.byml</c>, which falls back to the species' file, such as <c>Gmo_cat</c>. Lists are keyed by
/// entry name, or by hash when the name isn't known.
/// </summary>
public sealed record VillagerGmo(string FootPrint, float FootScale, string CharacterSize, GmoVector HandItemOffset,
    GmoVector SitOffset, GmoVector SitSbOffset, GmoVector BedOffset, GmoVector EyeOffset, float EyeRotateOffset,
    GmoVector EyeHorizon, GmoVector EyeVertical, IReadOnlyDictionary<string, GmoVector> Emoticons,
    IReadOnlyDictionary<string, GmoAccessory> Accessories, IReadOnlyDictionary<string, float> Tools)
{
    /// <summary>The footprints the game knows, in its order.</summary>
    public static readonly IReadOnlyList<string> FootPrints =
    [
        "None",
        "ShoesSocks",
        "ShoesSoft",
        "ShoesHard",
        "ShoeslessHuman",
        "ShoeslessNikukyu",
        "ShoeslessMaru",
        "ShoeslessNagaMaru",
        "ShoeslessHosonagaMaru",
        "ShoeslessBird",
        "ShoeslessBird2",
        "ShoeslessHidume",
        "ShoesGeta",
        "ShoesPumps",
    ];

    /// <summary>The size classes the game knows. Vanilla villagers of one species all have the same.</summary>
    public static readonly IReadOnlyList<string> CharacterSizes = ["cSmall", "cNormal", "cLarge"];

    // The param names hash to these keys: the CRC32 of the name and its type, as in mFootScale.f32 or mSitOffset.V3f.
    private static readonly string EmoticonsKey = Key("mEmoticonNodeParamsArray");
    private static readonly string AccessoriesKey = Key("mAccParamsArray");
    private static readonly string ToolsKey = Key("mToolParamsArray");
    private static readonly string FootPrintKey = Key("mFootPrint.Enum");
    private static readonly string FootScaleKey = Key("mFootScale.f32");
    private static readonly string CharacterSizeKey = Key("mCharacterSize.Enum");
    private static readonly string HandItemOffsetKey = Key("mHandItemOffset.V3f");
    private static readonly string SitOffsetKey = Key("mSitOffset.V3f");
    private static readonly string SitSbOffsetKey = Key("mSitSbOffset.V3f");
    private static readonly string BedOffsetKey = Key("mBedOffset.V3f");
    private static readonly string EyeOffsetKey = Key("mEyeCtrlOffset.V3f");
    private static readonly string EyeRotateOffsetKey = Key("mEyeCtrlRotateOffset.f32");
    private static readonly string EyeHorizonKey = Key("mEyeCtrlParamHorizon.V3f");
    private static readonly string EyeVerticalKey = Key("mEyeCtrlParamVertical.V3f");
    private static readonly string TransKey = Key("mTrans.V3f");
    private static readonly string RotateKey = Key("mRotate.V3f");
    private static readonly string ScaleKey = Key("mScale.V3f");
    private static readonly string EarSetKey = Key("mEarSetType.s32");
    private static readonly string ToolScaleKey = Key("mScale.f32");

    // The names of the list entries, which the lists are keyed by the hash of.
    private static readonly IReadOnlyDictionary<string, string> EntryNames = new[]
    {
        "EyeL", "EyeR", "Mouth",
        "C_Normal", "C_Brim", "C_BrimUnder", "C_Knit", "C_Bandana", "C_Crown", "C_Forehead", "C_Mask", "C_Top", "C_Left", "C_LeftBig",
        "C_Right", "G_Normal", "G_Fit", "G_FitLittle", "G_Left", "G_LeftBig", "G_Down", "G_UP", "G_Beard", "None",
        "Trwl", "Fpan", "Tr", "PaperFan", "Candy", "Pitc", "Cop", "HandLens", "GlDr", "Coffee", "FruitBasket", "Book", "Sandwich",
        "FishingRod", "Dumbbell", "LightDumbbell", "Watering", "Sprayer", "Windmill", "Doughnut", "FAndK", "Dh", "Duster",
        "CuteBagHand", "CoolBagHand", "HighBagHand", "LowBagHand", "Smoothie", "Firewood", "SoapBubble", "SmallBook", "SmartPhone",
        "Mcas", "Broom", "Branch", "Cracker", "Tambourine", "Net", "DStk", "SpnR", "Hammer", "MChart", "Umbrella", "Firework", "Ice",
        "StickLight", "SwRing", "Canister", "Cnut",
    }.ToDictionary(Key);

    // What the game uses for a value a file leaves out, as its param class sets them up.
    private static readonly GmoVector DefaultHandItemOffset = new(0, -4, 0);
    private static readonly GmoVector DefaultEyeHorizon = new(-27.5f, 27.5f, 8);
    private static readonly GmoVector DefaultEyeVertical = new(-22.5f, 22.5f, 6);
    private static readonly GmoVector One = new(1, 1, 1);

    private static string Key(string name) => name.ToCRC32().ToString("x");

    private static string Name(string key) => EntryNames.TryGetValue(key, out var name) ? name : key;

    // A name the file had only the hash of is kept as that hash.
    private static string EntryKey(string name) => EntryNames.Values.Contains(name) ? Key(name) : name;

    public static VillagerGmo Read(IBymlNode root)
    {
        var gmo = HouseExterior.Body(root);
        return new VillagerGmo(
            String(gmo, FootPrintKey, "None"), Float(gmo, FootScaleKey, 1), String(gmo, CharacterSizeKey, "cNormal"),
            Vector(gmo, HandItemOffsetKey, DefaultHandItemOffset), Vector(gmo, SitOffsetKey), Vector(gmo, SitSbOffsetKey),
            Vector(gmo, BedOffsetKey), Vector(gmo, EyeOffsetKey), Float(gmo, EyeRotateOffsetKey, 0),
            Vector(gmo, EyeHorizonKey, DefaultEyeHorizon), Vector(gmo, EyeVerticalKey, DefaultEyeVertical),
            Entries(gmo, EmoticonsKey, entry => Vector(entry, TransKey)),
            Entries(gmo, AccessoriesKey, entry => new GmoAccessory(Vector(entry, TransKey), Vector(entry, RotateKey),
                Vector(entry, ScaleKey, One), entry.TryGetValue(EarSetKey, out var value) && value is BymlInt32Value number ? number.Value : 0)),
            Entries(gmo, ToolsKey, entry => Float(entry, ToolScaleKey, 1)));
    }

    /// <summary>A Gmo file: a copy of <paramref name="template"/> with these values.</summary>
    public BymlDictionary Write(IBymlNode template)
    {
        var root = (BymlDictionary)BymlJson.FromJson(BymlJson.ToJson(template));
        var gmo = HouseExterior.Body(root);
        Set(gmo, FootPrintKey, FootPrint, "None", value => new BymlStringValue(value));
        Set(gmo, FootScaleKey, FootScale, 1, value => new BymlSingleValue(value));
        Set(gmo, CharacterSizeKey, CharacterSize, "cNormal", value => new BymlStringValue(value));
        Set(gmo, HandItemOffsetKey, HandItemOffset, DefaultHandItemOffset, Node);
        Set(gmo, SitOffsetKey, SitOffset, default, Node);
        Set(gmo, SitSbOffsetKey, SitSbOffset, default, Node);
        Set(gmo, BedOffsetKey, BedOffset, default, Node);
        Set(gmo, EyeOffsetKey, EyeOffset, default, Node);
        Set(gmo, EyeRotateOffsetKey, EyeRotateOffset, 0, value => new BymlSingleValue(value));
        Set(gmo, EyeHorizonKey, EyeHorizon, DefaultEyeHorizon, Node);
        Set(gmo, EyeVerticalKey, EyeVertical, DefaultEyeVertical, Node);
        WriteEntries(gmo, EmoticonsKey, Emoticons, (entry, position) => Set(entry, TransKey, position, default, Node));
        WriteEntries(gmo, AccessoriesKey, Accessories, (entry, accessory) =>
        {
            Set(entry, EarSetKey, accessory.EarSet, 0, value => new BymlInt32Value(value));
            Set(entry, RotateKey, accessory.Rotation, default, Node);
            Set(entry, ScaleKey, accessory.Scale, One, Node);
            Set(entry, TransKey, accessory.Position, default, Node);
        });
        WriteEntries(gmo, ToolsKey, Tools, (entry, scale) => Set(entry, ToolScaleKey, scale, 1, value => new BymlSingleValue(value)));
        return root;
    }

    // Some files, such as the player's, leave out values the game has a default for. One left out stays out while it's that.
    private static void Set<T>(BymlDictionary dictionary, string key, T value, T fallback, Func<T, IBymlNode> node)
    {
        if (dictionary.ContainsKey(key) || !EqualityComparer<T>.Default.Equals(value, fallback)) dictionary[key] = node(value);
    }

    private static Dictionary<string, T> Entries<T>(BymlDictionary gmo, string key, Func<BymlDictionary, T> read)
    {
        var result = new Dictionary<string, T>();
        if (gmo.TryGetValue(key, out var value) && value is BymlDictionary list)
            foreach (var (entryKey, entry) in list)
                if (entry is BymlDictionary fields) result[Name(entryKey)] = read(fields);
        return result;
    }

    private static void WriteEntries<T>(BymlDictionary gmo, string key, IReadOnlyDictionary<string, T> entries, Action<BymlDictionary, T> write)
    {
        if (!gmo.TryGetValue(key, out var value) || value is not BymlDictionary list) gmo[key] = list = [];
        foreach (var (name, entry) in entries)
        {
            var entryKey = EntryKey(name);
            if (!list.TryGetValue(entryKey, out var node) || node is not BymlDictionary fields) list[entryKey] = fields = [];
            write(fields, entry);
        }
    }

    private static string String(BymlDictionary dictionary, string key, string fallback) =>
        dictionary.TryGetValue(key, out var value) && value is BymlStringValue text ? text.Value : fallback;

    private static float Float(BymlDictionary dictionary, string key, float fallback) =>
        dictionary.TryGetValue(key, out var value) ? Number(value) ?? fallback : fallback;

    private static GmoVector Vector(BymlDictionary dictionary, string key, GmoVector fallback = default) =>
        dictionary.TryGetValue(key, out var value) && value is BymlArray { Count: 3 } array
            ? new GmoVector(Number(array[0]) ?? 0, Number(array[1]) ?? 0, Number(array[2]) ?? 0)
            : fallback;

    private static float? Number(IBymlNode node) => node switch
    {
        BymlSingleValue number => number.Value,
        BymlInt32Value number => number.Value,
        _ => null,
    };

    private static BymlArray Node(GmoVector vector) => [new BymlSingleValue(vector.X), new BymlSingleValue(vector.Y), new BymlSingleValue(vector.Z)];
}
