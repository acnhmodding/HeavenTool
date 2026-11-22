#nullable enable
using System.Numerics;
using static HeavenTool.IO.AcnhEnvironment.AcnhColorCorrection;

namespace HeavenTool.IO.AcnhEnvironment;

/// <summary>
/// agl::pfx::Bloom settings (GameEnv's one Env.aglblm), as the game feeds them to the bloom passes:
/// bloom_mask keeps <c>clamp((L - Threshold) / Range, 0, 1)</c> of each pixel, with L its luminance and the
/// result limited to <paramref name="ClampedLuminance"/> when set; the reduced levels are blurred and summed
/// with <paramref name="LevelWeights"/>, and ToneMapping adds the sum to the HDR color before exposure.
/// The environment palette replaces <paramref name="Threshold"/> and tints each level (bloom_compose's color).
/// </summary>
public sealed record AcnhBloom(float Threshold, float Range, float Intensity, float? ClampedLuminance, Vector4 LevelWeights)
{
    public IReadOnlyList<Vector4> LevelColors { get; init; } = [Vector4.One, Vector4.One, Vector4.One, Vector4.One];

    /// <summary>The palette's mBloomThreshold and mBloomBlendColor0–3 in place of the file's.</summary>
    public AcnhBloom WithPalette(AcnhEnvironmentPalette palette) => this with
    {
        Threshold = palette.BloomThreshold,
        LevelColors = [palette.BloomBlendColor0, palette.BloomBlendColor1, palette.BloomBlendColor2, palette.BloomBlendColor3],
    };

    public static AcnhBloom? Load(string romfs)
    {
        var gameEnv = OpenArchive(Entry(OpenArchive(File.ReadAllBytes(Path.Combine(romfs, "Env", "GameEnv.szs"))), "GameEnv.genvb"));
        return Read(Entry(gameEnv, "env.baglblm"));
    }

    public static AcnhBloom? Read(byte[] data)
    {
        var file = new Aamp(data);
        var p = file.Parameters(file.Objects(0x30 + file.Int(0x14)).GetValueOrDefault("bloom".ToCRC32(), -1));
        if (!file.Bool(p, "enable")) return null;
        // finalblend 0 takes the authored color1–4 instead; nothing ships that, so it is not recovered.
        if (file.Int(p, "finalblend") != 1) throw new NotSupportedException("Bloom finalblend modes other than expand are not recovered.");
        return new(file.Float(p, "threshhold"), file.Float(p, "threshold_range"), file.Float(p, "intensity"),
            file.Bool(p, "enable_clamped_luminance") ? file.Float(p, "clamped_luminance") : null, ExpandWeights(file.Float(p, "expand")));
    }

    /// <summary>
    /// The compose weights the game derives from expand: (1, a, b, 0) normalized, with a = min(3·expand, 1)
    /// and b = 3·expand − 1 past a third, so a larger expand moves weight to the wider levels.
    /// </summary>
    public static Vector4 ExpandWeights(float expand)
    {
        float wide = expand >= 1 / 3f ? 1 : expand * 3, wider = expand >= 1 / 3f ? expand * 3 - 1 : 0;
        float scale = 1 / (1 + wide + wider);
        return new(scale, wide * scale, wider * scale, 0);
    }
}
