#nullable enable
using HeavenTool.IO.FileFormats.BFRES;
using System.Collections.Frozen;
using System.Numerics;

namespace HeavenTool.IO.AcnhEnvironment;

/// <summary>Selects and samples the shipped SunOrbit tracks using the shared BFRES animation API.</summary>
public sealed class AcnhSunOrbit
{
    private const string BoneName = "SunOrbit";
    private const string ModelTrack = "SunOrbitModel", LightTrack = "SunOrbitLight", InsideTrack = "SunOrbitInside";
    // The tracks start at 6:00, at ten frames an hour.
    private const float FirstHour = 6, FramesPerHour = 10;

    private readonly FrozenDictionary<string, BfresAnimationCurve[]> tracks;

    private AcnhSunOrbit(Dictionary<string, BfresAnimationCurve[]> tracks) => this.tracks = tracks.ToFrozenDictionary(StringComparer.Ordinal);

    public static AcnhSunOrbit Load(string romfs)
    {
        using var stream = File.OpenRead(Path.Combine(romfs, "Model", "SysSunOrbit.Nin_NX_NVN.zs"));
        var assets = BfresFileReader.Load(stream);
        if (assets.Count != 1) throw new InvalidDataException("Expected one SunOrbit BFRES.");
        return FromDocument(assets[0].Document);
    }

    public static AcnhSunOrbit FromDocument(BfresDocument document)
    {
        if (document.Models.Count != 1 || document.Models[0].Bones is not { Count: 1 } bones
            || bones[0].ParentIndex != -1 || bones[0].Name != BoneName)
            throw new NotSupportedException("SunOrbit must have one root bone named SunOrbit.");

        var tracks = new Dictionary<string, BfresAnimationCurve[]>(StringComparer.Ordinal);
        foreach (string name in new[] { ModelTrack, LightTrack, InsideTrack }) tracks.Add(name, ReadTranslation(document, name));
        return new(tracks);
    }

    private static BfresAnimationCurve[] ReadTranslation(BfresDocument document, string name)
    {
        var animation = document.SkeletalAnimations.SingleOrDefault(animation => animation.Name == name)
            ?? throw new NotSupportedException($"Missing readable {name} animation. {string.Join(" ", document.Warnings)}");
        if (animation.Bones is not { Count: 1 } bones || bones[0].Name != BoneName)
            throw new NotSupportedException($"Unexpected bone target for {name}.");

        BfresAnimationCurve Curve(BfresAnimationChannel channel) =>
            bones[0].Curves.SingleOrDefault(curve => curve.Channel == channel)
            ?? throw new NotSupportedException($"{name} is missing its {channel} curve.");

        return [Curve(BfresAnimationChannel.TranslationX), Curve(BfresAnimationChannel.TranslationY), Curve(BfresAnimationChannel.TranslationZ)];
    }

    /// <summary>The sky vector uses SunOrbitModel outdoors, SunOrbitInside in a room.</summary>
    public Vector3 SkyDirection(float effectiveHour, bool inside) => Sample(effectiveHour, inside ? InsideTrack : ModelTrack);

    /// <summary>The direct light uses SunOrbitLight outdoors, then the stage's authored minimum elevation.</summary>
    public Vector3 DirectDirection(float effectiveHour, bool inside, float minimumAngle)
    {
        if (!float.IsFinite(minimumAngle) || minimumAngle is < 0 or >= MathF.PI / 2) throw new ArgumentOutOfRangeException(nameof(minimumAngle));

        var position = -Sample(effectiveHour, inside ? InsideTrack : LightTrack);
        float angle = Math.Clamp(MathF.Acos(Math.Clamp(position.Y, -1, 1)), 0, MathF.PI / 2 - minimumAngle);
        float azimuth = MathF.Atan2(position.Z, position.X);
        float horizontal = MathF.Sin(angle);
        return new(-horizontal * MathF.Cos(azimuth), -MathF.Cos(angle), -horizontal * MathF.Sin(azimuth));
    }

    private Vector3 Sample(float effectiveHour, string track)
    {
        if (!float.IsFinite(effectiveHour)) throw new ArgumentOutOfRangeException(nameof(effectiveHour));

        float frame = AcnhEnvironmentTime.WrapHour(effectiveHour - FirstHour) * FramesPerHour;
        var curves = tracks[track];
        var position = new Vector3(curves[0].Evaluate(frame), curves[1].Evaluate(frame), curves[2].Evaluate(frame));
        return position.LengthSquared() > 0 ? -Vector3.Normalize(position) : throw new InvalidDataException("Zero SunOrbit direction.");
    }
}
