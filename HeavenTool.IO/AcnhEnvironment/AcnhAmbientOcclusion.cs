#nullable enable
using HeavenTool.IO.FileFormats.BFRES;
using static HeavenTool.IO.AcnhEnvironment.AcnhColorCorrection;

namespace HeavenTool.IO.AcnhEnvironment;

/// <summary>
/// One stage's agl ssao_parameter (env.bgsdw outside, env_inside.bgsdw in a house), for the alchemy AO the game draws:
/// <paramref name="Radius"/> the world reach around a point, <paramref name="MaxRadius"/> its cap as a fraction of the
/// frame's width, <paramref name="Bias"/> what a sample must stand above the surface by, <paramref name="Density"/> how
/// quickly one sample darkens fully, and <paramref name="Intensity"/> how dark all of them make it together.
/// </summary>
public sealed record AcnhSsaoParameters(float Radius, float MaxRadius, float Bias, float Density, float Intensity, int Samples, int BlurCount);

/// <summary>
/// agl's screen-space AO chain: gsys's render_buffer_depth turns the camera depth into view depth, alchemy_ao_buffer
/// darkens it, and ssao_blur softens the result down and then across.
/// </summary>
public sealed record AcnhAmbientOcclusion(SwitchProcessShaderProgram Depth, SwitchProcessShaderProgram Ao, SwitchProcessShaderProgram Blur,
    AcnhSsaoParameters Outside, AcnhSsaoParameters Inside)
{
    public static AcnhAmbientOcclusion Load(string romfs)
    {
        var agl = OpenArchive(File.ReadAllBytes(Path.Combine(romfs, "System", "Agl", "agl_resource.Nin_NX_NVN.release.szs")));
        var gsys = OpenArchive(File.ReadAllBytes(Path.Combine(romfs, "System", "GSys", "gsys_resource.Nin_NX_NVN.release.szs")));
        var shadow = Programs(Entry(agl, "agl_technique_shdw.sharcb"));
        var common = Programs(Entry(gsys, "common.sharcb"));
        SwitchProcessShaderProgram Program(IReadOnlyList<SwitchProcessShaderProgram> programs, string name) =>
            programs.SingleOrDefault(program => program.Name == name) ?? throw new InvalidDataException($"Missing the {name} shader.");

        var gameEnv = OpenArchive(Entry(OpenArchive(File.ReadAllBytes(Path.Combine(romfs, "Env", "GameEnv.szs"))), "GameEnv.genvb"));
        return new(Program(common, "render_buffer_depth"), Program(shadow, "alchemy_ao_buffer"), Program(shadow, "ssao_blur"),
            ReadParameters(Entry(gameEnv, "env.bgsdw")), ReadParameters(Entry(gameEnv, "env_inside.bgsdw")));
    }

    public static AcnhSsaoParameters ReadParameters(byte[] data)
    {
        var file = new Aamp(data);
        var objects = file.Objects(0x30 + file.Int(0x14));
        var p = file.Parameters(objects.GetValueOrDefault("ssao_parameter".ToCRC32(), -1));
        // Names recovered by their CRC32. The intensity (0xee6902aa) and sample count (0xd76914d2) are only known by
        // hash; the count is the alchemy program's NUM_SAMPLES choice, five pairs either side as the game draws it.
        return new(file.Float(p, "alchemy_radius"), file.Float(p, "alchemy_max_radius"), file.Float(p, "alchemy_bias"),
            file.Float(p, "alchemy_density"), file.Float(p, 0xee6902aa), file.Int(p, 0xd76914d2), file.Int(p, "blur_num"));
    }

    private static IReadOnlyList<SwitchProcessShaderProgram> Programs(byte[] archive)
    {
        using var stream = new MemoryStream(archive, false);
        return SwitchProcessShaderArchive.Read(stream).Programs;
    }
}
