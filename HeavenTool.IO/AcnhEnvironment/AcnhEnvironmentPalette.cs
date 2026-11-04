#nullable enable
using System.Numerics;

namespace HeavenTool.IO.AcnhEnvironment;

/// <summary>The recovered lighting fields of an ACNH environment palette. Angles in BYML are already radians.</summary>
public sealed record AcnhEnvironmentPalette(
    Vector4 DirectColor, float DirectIntensity, float Longitude, float Latitude,
    Vector4 SkyColor, Vector4 GroundColor, float HemisphereIntensity, float EnvironmentMapTintIntensity,
    float WorldAoIntensity, float ShadowIntensity,
    Vector4 FogColor, float FogIntensity,
    Vector4 EdgeColor, float EdgeLongitude, float EdgeLatitude, float EdgeIntensity, float EdgeDirectionBlend,
    float RoomPointIntensity, AcnhRoomSpotPalette RoomSpot)
{
    public Vector4 FieldGIOverrideColor { get; init; }
    public Vector4 SunColor { get; init; }
    public float SkyLightIntensity { get; init; }
    public float RayleighIntensity { get; init; }
    public float MieIntensity { get; init; }
    // The bloom threshold and the four compose colors the palette sets on agl's bloom.
    public float BloomThreshold { get; init; }
    public Vector4 BloomBlendColor0 { get; init; }
    public Vector4 BloomBlendColor1 { get; init; }
    public Vector4 BloomBlendColor2 { get; init; }
    public Vector4 BloomBlendColor3 { get; init; }
}

public sealed record AcnhRoomSpotPalette(Vector4 Color, float Intensity, Vector2 Position, float Size, Vector3 Direction, float Far, float Fovy);

public sealed record AcnhPaletteSample(string Set, string Previous, string Next, float Weight, AcnhEnvironmentPalette Values);
