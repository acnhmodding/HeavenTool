#nullable enable
using AeonSake.NintendoTools.FileFormats.Byml;
using System.Numerics;
using static HeavenTool.IO.AcnhEnvironment.AcnhEnvironmentParameters;

namespace HeavenTool.IO.AcnhEnvironment;

/// <summary>
/// A PointLightParam (constructed by 71014ed5a0). 71014eb250 packs it as color × intensity at <see cref="Position"/>, at full
/// strength out to <see cref="Size"/> and gone at <see cref="Radius"/>.
/// </summary>
/// <param name="BindModel">mBindModelName; 71014edd30 binds a scene light to that model when its first character isn't 0.</param>
public sealed record AcnhPointLightParameters(bool Enabled, Vector4 Color, float Intensity, Vector3 Position, float Size, float Radius,
    string BindModel)
{
    /// <summary>
    /// The light 710148cfe0 blends from a dual light's two settings: 7101525a50 interpolates only its f32, vector and color
    /// fields, so the rest stay as <paramref name="a"/>'s.
    /// </summary>
    public static AcnhPointLightParameters Blend(AcnhPointLightParameters a, AcnhPointLightParameters b, float t) => a with
    {
        Color = Vector4.Lerp(a.Color, b.Color, t),
        Intensity = float.Lerp(a.Intensity, b.Intensity, t),
        Position = Vector3.Lerp(a.Position, b.Position, t),
        Size = float.Lerp(a.Size, b.Size, t),
        Radius = float.Lerp(a.Radius, b.Radius, t),
    };
}

/// <summary>
/// A SpotLightParam. 71014eb250 packs it as color × intensity / (<see cref="Fovy"/> / 2π) at <see cref="Position"/>, with half
/// of <see cref="Fovy"/> as its cone, at full strength out to <see cref="Size"/> and gone at <see cref="Far"/>.
/// </summary>
public sealed record AcnhSpotLightParameters(bool Enabled, Vector4 Color, float Intensity, Vector3 Position, float Size,
    Vector3 Direction, float Far, float Fovy, string BindModel)
{
    /// <inheritdoc cref="AcnhPointLightParameters.Blend"/>
    public static AcnhSpotLightParameters Blend(AcnhSpotLightParameters a, AcnhSpotLightParameters b, float t) => a with
    {
        Color = Vector4.Lerp(a.Color, b.Color, t),
        Intensity = float.Lerp(a.Intensity, b.Intensity, t),
        Position = Vector3.Lerp(a.Position, b.Position, t),
        Size = float.Lerp(a.Size, b.Size, t),
        Direction = Vector3.Lerp(a.Direction, b.Direction, t),
        Far = float.Lerp(a.Far, b.Far, t),
        Fovy = float.Lerp(a.Fovy, b.Fovy, t),
    };
}

/// <summary>An IdrLightMgr light: its daytime setting and the night one it blends to.</summary>
public sealed record AcnhDualLight<T>(T Day, T Night);

/// <summary>
/// A scene's own lights, read from Param/Gfx/&lt;scene&gt;/, the folder 71015267c0 names after the scene: DynamicLightMgr_0's
/// DynamicLightSceneParam (loaded by 71014edd30) and IdrLightMgr_0's IdrLightMgrParam (loaded by 710148c590). Positions are in
/// the scene's world space.
/// </summary>
public sealed class AcnhSceneLights
{
    public static AcnhSceneLights Empty { get; } = new([], [], [], []);

    /// <summary>mPointParamArray, every entry, enabled or not.</summary>
    public IReadOnlyList<AcnhPointLightParameters> Points { get; }
    public IReadOnlyList<AcnhSpotLightParameters> Spots { get; }
    /// <summary>mPointLightParam's mDaulPointLight groups, each with its two mParam settings.</summary>
    public IReadOnlyList<AcnhDualLight<AcnhPointLightParameters>> DualPoints { get; }
    public IReadOnlyList<AcnhDualLight<AcnhSpotLightParameters>> DualSpots { get; }

    private AcnhSceneLights(IReadOnlyList<AcnhPointLightParameters> points, IReadOnlyList<AcnhSpotLightParameters> spots,
        IReadOnlyList<AcnhDualLight<AcnhPointLightParameters>> dualPoints, IReadOnlyList<AcnhDualLight<AcnhSpotLightParameters>> dualSpots)
    {
        Points = points;
        Spots = spots;
        DualPoints = dualPoints;
        DualSpots = dualSpots;
    }

    /// <summary>The scene's lights; a scene without either file has none from it, as the managers skip missing files.</summary>
    public static AcnhSceneLights Read(Func<string, IBymlNode?> read, string scene)
    {
        List<AcnhPointLightParameters> points = [];
        List<AcnhSpotLightParameters> spots = [];
        if (read($"Param/Gfx/{scene}/DynamicLightMgr_0.byml") is BymlDictionary dynamic && dynamic.TryGetValue(Hash("DynamicLightSceneParam"), out var sceneNode))
        {
            var body = Dictionary(sceneNode);
            points.AddRange(Entries(body, "mPointParamArray[]").Select(ReadPoint));
            spots.AddRange(Entries(body, "mSpotParamArray[]").Select(ReadSpot));
        }

        List<AcnhDualLight<AcnhPointLightParameters>> dualPoints = [];
        List<AcnhDualLight<AcnhSpotLightParameters>> dualSpots = [];
        if (read($"Param/Gfx/{scene}/IdrLightMgr_0.byml") is BymlDictionary idr)
        {
            var body = Dictionary(Field(idr, "IdrLightMgrParam"));
            foreach (var group in Entries(body, "mPointLightParam[]"))
            {
                var pair = Pair(Dictionary(group));
                dualPoints.Add(new(ReadPoint(pair[0]), ReadPoint(pair[1])));
            }
            foreach (var group in Entries(body, "mSpotLightParam[]"))
            {
                var pair = Pair(Dictionary(group));
                dualSpots.Add(new(ReadSpot(pair[0]), ReadSpot(pair[1])));
            }
        }
        return new(points, spots, dualPoints, dualSpots);
    }

    // A dual light's two settings; 710148cfe0 reads entries 0 and 1.
    private static IBymlNode[] Pair(BymlDictionary group)
    {
        var settings = Entries(group, "mParam[]").ToArray();
        return settings.Length == 2 ? settings : throw new InvalidDataException("A dual light needs exactly two settings.");
    }

    // An empty array is stored as null.
    private static IEnumerable<IBymlNode> Entries(BymlDictionary p, string name) =>
        p.TryGetValue(Hash(name), out var node) && node is BymlArray array ? array : [];

    private static AcnhPointLightParameters ReadPoint(IBymlNode entry)
    {
        var p = Dictionary(entry);
        return new(Flag(p, "mIsEnable"), Color(p, "mColor"), Number(p, "mIntensity"), Position3(p, "mPos"), Number(p, "mSize"),
            Number(p, "mRadius"), Text(p, "mBindModelName"));
    }

    private static AcnhSpotLightParameters ReadSpot(IBymlNode entry)
    {
        var p = Dictionary(entry);
        return new(Flag(p, "mIsEnable"), Color(p, "mColor"), Number(p, "mIntensity"), Position3(p, "mPos"), Number(p, "mSize"),
            Position3(p, "mDir"), Number(p, "mFar"), Number(p, "mFovy"), Text(p, "mBindModelName"));
    }
}

/// <summary>
/// DynamicLightParam (71014ecb00): the light grid's cell counts and cell size. A stage without the file keeps the constructor's
/// 16 × 1 × 16 cells of 10 × 100 × 10.
/// </summary>
public sealed record AcnhLightGridParameters(int CountX, int CountY, int CountZ, Vector3 CellSize)
{
    public static AcnhLightGridParameters Default { get; } = new(16, 1, 16, new(10, 100, 10));

    /// <summary>The grid of <paramref name="stage"/>'s Param/Gfx/&lt;stage&gt;/DynamicLightMgr_0.byml, such as Stage_Idr's.</summary>
    public static AcnhLightGridParameters Read(Func<string, IBymlNode?> read, string stage)
    {
        if (read($"Param/Gfx/{stage}/DynamicLightMgr_0.byml") is not BymlDictionary root || !root.TryGetValue(Hash("DynamicLightParam"), out var node))
            return Default;
        var p = Dictionary(node);
        // Fields the file leaves out keep their defaults.
        int[] counts = p.TryGetValue(Hash("mClusterSize.V3i"), out var clusters) ? Integers(clusters, 3) : [Default.CountX, Default.CountY, Default.CountZ];
        var size = p.TryGetValue(Hash("mAABBSize.V3f"), out _) ? Position3(p, "mAABBSize") : Default.CellSize;
        if (counts.Any(count => count < 1 || count > 100) || size.X <= 0 || size.Y <= 0 || size.Z <= 0)
            throw new InvalidDataException($"Invalid light grid in {stage}.");
        return new(counts[0], counts[1], counts[2], size);
    }

    private static int[] Integers(IBymlNode node, int count)
    {
        var values = Array(node);
        if (values.Count != count) throw new InvalidDataException("Invalid integer vector.");
        return [.. values.Select(value => value is BymlInt32Value number ? number.Value : throw new InvalidDataException("Invalid integer component."))];
    }
}

/// <summary>
/// SceneEnvMgr.byml's EnvParam sunrise and sunset times (constructed by 71015357e0), from which 710153b670 works out how far
/// into the night the clock is.
/// </summary>
public sealed record AcnhDayNight(float SunriseStart, float SunriseEnd, float SunsetStart, float SunsetEnd)
{
    public static AcnhDayNight Read(Func<string, IBymlNode?> read)
    {
        var p = Dictionary(Field(Dictionary(read("Param/Gfx/SceneEnvMgr.byml")), "EnvParam"));
        var times = new AcnhDayNight(Number(p, "mSunriseStartTime"), Number(p, "mSunriseEndTime"), Number(p, "mSunsetStartTime"), Number(p, "mSunsetEndTime"));
        if (!(times.SunriseStart < times.SunriseEnd && times.SunriseEnd <= times.SunsetStart && times.SunsetStart < times.SunsetEnd))
            throw new InvalidDataException("Invalid sunrise and sunset times.");
        return times;
    }

    /// <summary>
    /// SceneEnvMgr +0x104 at the effective (seasonally remapped) <paramref name="hour"/>, as 710153b670 computes it: 1 through
    /// the night, falling to 0 across sunrise and rising back to 1 across sunset.
    /// </summary>
    public float NightRatio(float hour)
    {
        if (hour <= SunriseStart) return 1;
        if (hour < SunriseEnd) return 1 - (hour - SunriseStart) / (SunriseEnd - SunriseStart);
        if (hour <= SunsetStart) return 0;
        return hour < SunsetEnd ? (hour - SunsetStart) / (SunsetEnd - SunsetStart) : 1;
    }

    /// <summary>
    /// The blend 710148cfe0 moves IdrLightMgr's lights by: clamp(night + 2·max(darkness − .5, 0), 0, 1), where darkness is the
    /// palette's +0x1c58, which 710153b670 takes from 7103f58980 by weather. That table gives the clear-weather row 0.
    /// </summary>
    public float IndoorLightBlend(float hour, float darkness = 0) =>
        Math.Clamp(NightRatio(hour) + Math.Max(darkness - .5f, 0) * 2, 0, 1);
}
