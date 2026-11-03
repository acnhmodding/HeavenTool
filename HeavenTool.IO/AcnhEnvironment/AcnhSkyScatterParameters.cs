#nullable enable
using AeonSake.NintendoTools.FileFormats.Byml;
using System.Numerics;
using static HeavenTool.IO.AcnhEnvironment.AcnhEnvironmentParameters;

namespace HeavenTool.IO.AcnhEnvironment;

/// <summary>Static sky inputs; SceneEnvMgr replaces the sun color, intensity and scattering coefficients from the palette.</summary>
public sealed record AcnhSkyScatterParameters(Vector2 FramebufferSize, uint MaximumMipLevel, float VirtualCameraHeight,
    float EarthColorIntensity, float SunEraserPower, float SunHorizonOffset)
{
    internal static AcnhSkyScatterParameters Read(Func<string, IBymlNode?> read)
    {
        var p = Dictionary(HashedField(Dictionary(read("Param/Gfx/SkyScatter.byml")), "f5ab6869"));
        // StaticParam stores this u32-named parameter in a signed BYML integer node.
        uint levels = Field(p, "mMaxMipLevel", "u32") switch
        {
            BymlUInt32Value value when value.Value > 0 => value.Value,
            BymlInt32Value value when value.Value > 0 => (uint)value.Value,
            _ => throw new InvalidDataException("Invalid sky scattering mip count.")
        };
        var size = Position2(p, "mFbSize");
        if (size.X < 1 || size.Y < 1 || size.X != MathF.Truncate(size.X) || size.Y != MathF.Truncate(size.Y))
            throw new InvalidDataException("Invalid sky scattering framebuffer size.");
        return new(size, levels, Number(p, "mVirtualCameraHeight"),
            // This misspelling is part of the game's parameter name and therefore its CRC32 key.
            Number(p, "mEirthColorIntensity"), Number(p, "mSunEraserPower"), Number(p, "mSunHorizonOffset"));
    }
}
