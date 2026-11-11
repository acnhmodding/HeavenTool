#nullable enable
using AeonSake.NintendoTools.FileFormats.Byml;
using HeavenTool.IO.FileFormats.BFRES;
using static HeavenTool.IO.AcnhEnvironment.AcnhEnvironmentParameters;

namespace HeavenTool.IO.AcnhEnvironment;

/// <summary>
/// The panorama the game turns its environment cube into (71015224b0), with the Park_Proc programs that draw and filter it:
/// DrawEnvMap writes level 0 from the cube, DrawEnvBlurLow, DrawEnvBlur and DrawEnvBlurHi blur each coarser level from the
/// one above it, and DrawEnvBlur then smooths the middle levels back up from the one below.
/// </summary>
public sealed record AcnhPanoramaEnvironment(int Width, int Height, int Levels, SwitchProcessShaderProgram DrawEnvMap,
    SwitchProcessShaderProgram BlurLow, SwitchProcessShaderProgram Blur, SwitchProcessShaderProgram BlurHigh)
{
    internal static AcnhPanoramaEnvironment Read(Func<string, IBymlNode?> read, string romfs)
    {
        var p = Dictionary(Field(Dictionary(read("Param/Gfx/PanoramaEnvMap.byml")), "PanoramaEnv"));
        var size = Position2(p, "mFbSize");
        // The mip count is only known by its hash; 71015224b0 reads it from +0x684.
        int levels = Field(p, "mMaxMipLevel", "u32") switch
        {
            BymlInt32Value value => value.Value,
            BymlUInt32Value value => (int)value.Value,
            _ => throw new InvalidDataException("Invalid panorama mip count.")
        };
        if (size.X < 1 || size.Y < 1 || size.X != MathF.Truncate(size.X) || size.Y != MathF.Truncate(size.Y) || levels < 1
            || levels > 1 + (int)MathF.Log2(Math.Max(size.X, size.Y)))
            throw new InvalidDataException("Invalid panorama size.");

        var programs = SwitchProcessShaderArchive.Load(Path.Combine(romfs, "Shader", "Park_Proc.Nin_NX_NVN.release.szs")).Programs;
        SwitchProcessShaderProgram Program(string name) =>
            programs.SingleOrDefault(program => program.Name == name) ?? throw new InvalidDataException($"Park_Proc is missing {name}.");
        return new((int)size.X, (int)size.Y, levels, Program("DrawEnvMap"), Program("DrawEnvBlurLow"), Program("DrawEnvBlur"), Program("DrawEnvBlurHi"));
    }
}
