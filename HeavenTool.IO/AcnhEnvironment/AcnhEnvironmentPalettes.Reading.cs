#nullable enable
using AeonSake.NintendoTools.FileFormats.Byml;
using static HeavenTool.IO.AcnhEnvironment.AcnhEnvironmentParameters;

namespace HeavenTool.IO.AcnhEnvironment;

public sealed partial class AcnhEnvironmentPalettes
{
    public static AcnhEnvironmentPalettes Load(string romfs) => Read(OpenStaticParam(romfs));

    /// <summary>Also accepts the application's cached StaticParam reader.</summary>
    public static AcnhEnvironmentPalettes Read(Func<string, IBymlNode?> read)
    {
        var parameters = ReadParameters(read);
        ApplyOverrides(parameters, read);
        var palettes = parameters.ToDictionary(pair => pair.Key, pair => ReadPalette(pair.Value), StringComparer.Ordinal);
        return new(palettes, ReadSets(read));
    }

    // Container hashes are verified in the 3.0.3 resources; leaf hashes use the game's typed-name CRC32 schema.
    private static BymlArray Entries(IBymlNode? root, string rootHash)
    {
        var body = Dictionary(HashedField(Dictionary(root), rootHash));
        return Array(HashedField(body, "eb658d9a"));
    }

    private static Dictionary<string, BymlDictionary> ReadParameters(Func<string, IBymlNode?> read)
    {
        var parameters = new Dictionary<string, BymlDictionary>(StringComparer.Ordinal);
        foreach (var node in Entries(read("Param/Gfx/EnvPaletteList.byml"), "53fcb74c"))
        {
            var entry = Dictionary(node);
            parameters.Add(Text(entry, "mPaletteName"), Clone(entry));
        }
        return parameters;
    }

    // A false per-leaf flag inherits the base; true retains the authored child value. A missing base falls back to self.
    private static void ApplyOverrides(Dictionary<string, BymlDictionary> parameters, Func<string, IBymlNode?> read)
    {
        foreach (var node in Entries(read("Param/Gfx/OverrideEnvPaletteList.byml"), "a898aa5a"))
        {
            var entry = Dictionary(node);
            if (!parameters.TryGetValue(Text(entry, "mPaletteName"), out var child)
                || !parameters.TryGetValue(Text(entry, "mOverridePaletteName"), out var parent)) continue;

            foreach (var item in Array(HashedField(entry, "2c75a2a8")))
            {
                var flag = Dictionary(item);
                if (Field(flag, "mFlag", "bool") is not BymlBoolValue keepsChild) throw new InvalidDataException("Invalid palette override flag.");
                if (!keepsChild.Value) Inherit(child, parent, Text(flag, "mName").Split('/'));
            }
        }
    }

    private static BymlDictionary Clone(BymlDictionary source)
    {
        var result = new BymlDictionary();
        foreach (var (key, value) in source) result[key] = value is BymlDictionary nested ? Clone(nested) : value;
        return result;
    }

    private static void Inherit(BymlDictionary child, BymlDictionary parent, string[] path)
    {
        foreach (string segment in path[..^1])
        {
            string key = Hash(segment);
            if (!child.TryGetValue(key, out var childNode) || !parent.TryGetValue(key, out var parentNode)) return;
            child = Dictionary(childNode);
            parent = Dictionary(parentNode);
        }

        // The flag names the leaf without its type, which the key's hash includes.
        string name = path[^1];
        foreach (string type in new[] { "f32", "C4f", "V2f", "V3f", "V4f", "Str", "bool", "s32", "u32", "" })
        {
            string key = Hash(type.Length == 0 ? name : $"{name}.{type}");
            if (parent.TryGetValue(key, out var value) && child.ContainsKey(key))
            {
                child[key] = value;
                return;
            }
        }
    }

    private static AcnhEnvironmentPalette ReadPalette(BymlDictionary p)
    {
        var roomLights = Dictionary(Field(p, "mRoomLightPalette"));
        return new(
            DirectColor: Color(p, "mDirectLightColor"),
            DirectIntensity: Number(p, "mDirectLightIntensity"),
            Longitude: Number(p, "mDirectLightDirLongi"),
            Latitude: Number(p, "mDirectLightDirLati"),
            SkyColor: Color(p, "mHemiSkyColor"),
            GroundColor: Color(p, "mHemiGroundColor"),
            HemisphereIntensity: Number(p, "mHemiLightIntensity"),
            EnvironmentMapTintIntensity: Number(p, "mEnvMapTintIntensity"),
            WorldAoIntensity: Number(p, "mWorldAoIntensity"),
            ShadowIntensity: Number(p, "mShadowIntensity"),
            FogColor: Color(p, "mFogColor"),
            FogIntensity: Number(p, "mFogIntensity"),
            EdgeColor: Color(p, "mMoonlightEdgeLightColor"),
            EdgeLongitude: Number(p, "mMoonlightEdgeLightLongi"),
            EdgeLatitude: Number(p, "mMoonlightEdgeLightLati"),
            EdgeIntensity: Number(p, "mMoonlightEdgeLightIntensity"),
            EdgeDirectionBlend: Number(p, "mMoonlightEdgeLightDirBlendRate"),
            RoomPointIntensity: Number(Dictionary(Field(roomLights, "mRoomPointLight")), "mIntensity"),
            RoomSpot: ReadSpot(Dictionary(Field(roomLights, "mAlternateLight"))))
        {
            FieldGIOverrideColor = Color(p, "mFieldGIOverrideColor"),
            SunColor = Color(p, "mSunColor"),
            SkyLightIntensity = Number(p, "mSkyLightIntensity"),
            RayleighIntensity = Number(p, "mRayleighIntensity"),
            MieIntensity = Number(p, "mMieIntensity"),
            BloomThreshold = Number(p, "mBloomThreshold"),
            BloomBlendColor0 = Color(p, "mBloomBlendColor0"),
            BloomBlendColor1 = Color(p, "mBloomBlendColor1"),
            BloomBlendColor2 = Color(p, "mBloomBlendColor2"),
            BloomBlendColor3 = Color(p, "mBloomBlendColor3")
        };
    }

    private static AcnhRoomSpotPalette ReadSpot(BymlDictionary p) => new(
        Color: Color(p, "mColor"),
        Intensity: Number(p, "mIntensity"),
        Position: Position2(p, "mPos"),
        Size: Number(p, "mSize"),
        Direction: Position3(p, "mDir"),
        Far: Number(p, "mFar"),
        Fovy: Number(p, "mFovy"));

    private static Dictionary<string, string[][]> ReadSets(Func<string, IBymlNode?> read)
    {
        var sets = new Dictionary<string, string[][]>(StringComparer.Ordinal);
        foreach (var node in Entries(read("Param/Gfx/EnvPaletteSetList.byml"), "ecd96410"))
        {
            var entry = Dictionary(node);
            string[][] rows = [.. Array(HashedField(entry, "3279c433")).Select(ReadTimetable)];
            if (rows.Length == 0) throw new InvalidDataException("An environment set has no timetables.");
            sets.Add(Text(entry, "mPaletteSetName"), rows);
        }
        return sets;
    }

    private static string[] ReadTimetable(IBymlNode row)
    {
        var hours = Array(HashedField(Dictionary(row), "4fe43bb1"));
        if (hours.Count != HoursPerDay) throw new InvalidDataException("An environment timetable must have 24 hours.");
        return [.. hours.Select(hour => hour is BymlStringValue palette ? palette.Value : throw new InvalidDataException("Invalid palette reference."))];
    }
}
