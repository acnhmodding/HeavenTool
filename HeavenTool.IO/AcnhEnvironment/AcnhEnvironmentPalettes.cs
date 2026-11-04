#nullable enable
using System.Collections.Frozen;
using System.Numerics;

namespace HeavenTool.IO.AcnhEnvironment;

/// <summary>Read-only StaticParam palette tables. No lighting numbers are calibrated or copied from a running game.</summary>
public sealed partial class AcnhEnvironmentPalettes
{
    private const int HoursPerDay = 24;

    private readonly FrozenDictionary<string, AcnhEnvironmentPalette> palettes;
    private readonly FrozenDictionary<string, string[][]> sets;

    public IEnumerable<string> PaletteSets => sets.Keys;
    public int PaletteCount => palettes.Count;

    private AcnhEnvironmentPalettes(Dictionary<string, AcnhEnvironmentPalette> palettes, Dictionary<string, string[][]> sets)
    {
        this.palettes = palettes.ToFrozenDictionary(StringComparer.Ordinal);
        this.sets = sets.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>Evaluates an effective palette hour, after seasonal clock remapping. Row 0 is the clear-weather preview.</summary>
    public AcnhPaletteSample Evaluate(string set, float effectiveHour, int timetableRow = 0)
    {
        if (!float.IsFinite(effectiveHour)) throw new ArgumentOutOfRangeException(nameof(effectiveHour));
        if (!sets.TryGetValue(set, out var rows)) throw new ArgumentException($"Unknown environment set {set}.", nameof(set));
        if ((uint)timetableRow >= rows.Length) throw new ArgumentOutOfRangeException(nameof(timetableRow));

        // The timetable only names some hours; the palette blends between the nearest named hours around this one.
        string[] hours = rows[timetableRow];
        bool IsKey(int hour) => hours[hour].Length > 0 && palettes.ContainsKey(hours[hour]);

        effectiveHour = AcnhEnvironmentTime.WrapHour(effectiveHour);
        int current = (int)effectiveHour;
        int previous = Enumerable.Range(0, HoursPerDay).Select(distance => (current - distance + HoursPerDay) % HoursPerDay).FirstOrDefault(IsKey, -1);
        int next = Enumerable.Range(1, HoursPerDay).Select(distance => (current + distance) % HoursPerDay).FirstOrDefault(IsKey, -1);
        if (previous < 0 || next < 0) throw new InvalidDataException($"Environment set {set}, row {timetableRow} has no palette keys.");

        int length = (next - previous + HoursPerDay) % HoursPerDay;
        float weight = length == 0 ? 0 : AcnhEnvironmentTime.WrapHour(effectiveHour - previous) / length;
        return new(set, hours[previous], hours[next], weight, Blend(palettes[hours[previous]], palettes[hours[next]], weight));
    }

    public static string IndoorSet(bool leftWindow, bool rightWindow, bool leftCurtainClosed = false, bool rightCurtainClosed = false) =>
        (leftWindow, rightWindow) switch
        {
            (false, false) => "WindowNothingInside",
            (true, false) => leftCurtainClosed ? "WindowLeftOnlyInsideCurtainClose" : "WindowLeftOnlyInside",
            (false, true) => rightCurtainClosed ? "WindowRightOnlyInsideCurtainClose" : "WindowRightOnlyInside",
            _ => (leftCurtainClosed, rightCurtainClosed) switch
            {
                (true, true) => "NormalInsideCurtainClose",
                (true, false) => "NormalInsideLeftCurtainClose",
                (false, true) => "NormalInsideRightCurtainClose",
                _ => "NormalInside"
            }
        };

    private static AcnhEnvironmentPalette Blend(AcnhEnvironmentPalette a, AcnhEnvironmentPalette b, float t) => new(
        DirectColor: Lerp(a.DirectColor, b.DirectColor, t),
        DirectIntensity: Lerp(a.DirectIntensity, b.DirectIntensity, t),
        Longitude: Lerp(a.Longitude, b.Longitude, t),
        Latitude: Lerp(a.Latitude, b.Latitude, t),
        SkyColor: Lerp(a.SkyColor, b.SkyColor, t),
        GroundColor: Lerp(a.GroundColor, b.GroundColor, t),
        HemisphereIntensity: Lerp(a.HemisphereIntensity, b.HemisphereIntensity, t),
        EnvironmentMapTintIntensity: Lerp(a.EnvironmentMapTintIntensity, b.EnvironmentMapTintIntensity, t),
        WorldAoIntensity: Lerp(a.WorldAoIntensity, b.WorldAoIntensity, t),
        ShadowIntensity: Lerp(a.ShadowIntensity, b.ShadowIntensity, t),
        FogColor: Lerp(a.FogColor, b.FogColor, t),
        FogIntensity: Lerp(a.FogIntensity, b.FogIntensity, t),
        EdgeColor: Lerp(a.EdgeColor, b.EdgeColor, t),
        EdgeLongitude: Lerp(a.EdgeLongitude, b.EdgeLongitude, t),
        EdgeLatitude: Lerp(a.EdgeLatitude, b.EdgeLatitude, t),
        EdgeIntensity: Lerp(a.EdgeIntensity, b.EdgeIntensity, t),
        EdgeDirectionBlend: Lerp(a.EdgeDirectionBlend, b.EdgeDirectionBlend, t),
        RoomPointIntensity: Lerp(a.RoomPointIntensity, b.RoomPointIntensity, t),
        RoomSpot: Blend(a.RoomSpot, b.RoomSpot, t))
    {
        FieldGIOverrideColor = Lerp(a.FieldGIOverrideColor, b.FieldGIOverrideColor, t),
        SunColor = Lerp(a.SunColor, b.SunColor, t),
        SkyLightIntensity = Lerp(a.SkyLightIntensity, b.SkyLightIntensity, t),
        RayleighIntensity = Lerp(a.RayleighIntensity, b.RayleighIntensity, t),
        MieIntensity = Lerp(a.MieIntensity, b.MieIntensity, t),
        BloomThreshold = Lerp(a.BloomThreshold, b.BloomThreshold, t),
        BloomBlendColor0 = Lerp(a.BloomBlendColor0, b.BloomBlendColor0, t),
        BloomBlendColor1 = Lerp(a.BloomBlendColor1, b.BloomBlendColor1, t),
        BloomBlendColor2 = Lerp(a.BloomBlendColor2, b.BloomBlendColor2, t),
        BloomBlendColor3 = Lerp(a.BloomBlendColor3, b.BloomBlendColor3, t)
    };

    private static AcnhRoomSpotPalette Blend(AcnhRoomSpotPalette a, AcnhRoomSpotPalette b, float t) => new(
        Color: Lerp(a.Color, b.Color, t),
        Intensity: Lerp(a.Intensity, b.Intensity, t),
        Position: Lerp(a.Position, b.Position, t),
        Size: Lerp(a.Size, b.Size, t),
        Direction: Lerp(a.Direction, b.Direction, t),
        Far: Lerp(a.Far, b.Far, t),
        Fovy: Lerp(a.Fovy, b.Fovy, t));

    // Spelled out because the library Lerps round differently, which would shift the palette values.
    private static float Lerp(float a, float b, float t) => (1 - t) * a + t * b;
    private static Vector2 Lerp(Vector2 a, Vector2 b, float t) => (1 - t) * a + t * b;
    private static Vector3 Lerp(Vector3 a, Vector3 b, float t) => (1 - t) * a + t * b;
    private static Vector4 Lerp(Vector4 a, Vector4 b, float t) => (1 - t) * a + t * b;
}
