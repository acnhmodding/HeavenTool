using HeavenTool.IO.AcnhEnvironment;
using HeavenTool.IO.FileFormats.BFRES;
using System.Buffers.Binary;
using System.Numerics;

namespace HeavenTool.Shaders;

public sealed record AcnhLightingPreset(string Name, Vector4 DirectColor, float DirectIntensity, Vector3 DirectDirection,
    Vector4 SkyColor, Vector4 GroundColor, float HemisphereIntensity, float EnvironmentTintIntensity)
{
    // Captured from the running game on the island at noon. The game's hemisphere light is zero by day;
    // its sky lighting comes from scene probes the viewer lacks, which PreviewIndirectLight stands in for.
    public static AcnhLightingPreset Day { get; } = new("Day", new(1, .9f, .5f, 1), 14, new(.021964f, -.70608f, -.70779f), new(1, .9f, .7f, 1), new(.45f, .6f, .45f, 1), 0, 1)
    {
        PreviewIndirectLight = new(3, 3, 3),
        User0 = new Dictionary<int, Vector4>
        {
            [10] = new(.030124f, -.88006f, -.4739f, 0), [17] = new(0, 2.5f, .6f, .94837f), [18] = new(.39891f, 0, .5f, 14),
            [21] = new(.10977f, .25271f, .99457f, .1f), [52] = new(.19891f, 0, 0, 0), [55] = new(0, .33836f, 0, 0),
            [61] = new(.96258f, 0, .27099f, 1.4436f), [72] = new(.021719f, .021696f, .021602f, .021719f),
            [73] = new(.30109f, .30109f, .30109f, 1), [75] = new(-.0011585f, .37181f, -.92831f, 0), [76] = new(1, 9, 12, 6),
        },
        Environment = new Dictionary<int, Vector4>
            { [10] = new(.10869f, .75163f, .99457f, .99457f), [11] = new(0, 0, -1, -1.3726f), [12] = new(.005668f, 1, 0, 0) },
    };
    // Captured from the title screen at night.
    public static AcnhLightingPreset Night { get; } = new("Night", new(.15f, .2f, 1, 1), 7.5043f, new(-.61047f, -.79183f, .01836f), new(.1f, .4f, 1, 1), new(.15f, .3f, .5f, 1), .37038f, 1)
    {
        User0 = new Dictionary<int, Vector4>
        {
            [10] = new(-.42079f, -.11845f, .89939f, 0), [17] = new(0, 2.5f, .6f, .43906f), [18] = new(.6f, 0, .5f, 8.5105f),
            [21] = new(.19034f, .26423f, .35622f, .1f), [52] = new(.2f, 0, 0, 0), [55] = new(0, .3137f, 0, 0),
            [61] = new(-.88268f, 0, .46998f, .96774f), [72] = new(1.2464f, 1.8764f, 3.4511f, 0),
            [73] = new(.05f, .05f, .1823f, 1), [75] = new(.25946f, -.38414f, -.88607f, 0), [76] = new(1, 9, 200, 6),
        },
        Environment = new Dictionary<int, Vector4>
            { [10] = new(0, .01f, .037229f, .99907f), [11] = new(0, 0, -1, -.8174f), [12] = new(.0060504f, 1, 0, 0) },
    };

    // Captured from the running game inside a villager's house at 4:51 PM, with the room's middle moved to the origin. The
    // room is lit by a light over its middle and the sun coming in from the side, both local lights. The house shades the
    // sun itself out, at the room's shadow strength in [17].w, which the viewer has no house to do. Without an environment
    // probe, the fill stands in for the room's indirect light. The world AO strength in [4].x is the game's .27563 raised
    // to .6, as the fill brightens the room past what the game's .28 darkens enough to show.
    public static AcnhLightingPreset Room { get; } = new("Room", new(1, .37058f, .19748f, 1), 20 * (1 - .97731f), new(.81884f, -.57403f, -.00006f), new(.92689f, .52184f, .35713f, 1), new(.36345f, .1f, .087814f, 1), 1.75632f, 1)
    {
        PreviewIndirectLight = new(3, 3, 3),
        User0 = new Dictionary<int, Vector4>
        {
            [4] = new(.6f, 0, 0, 0), [5] = new(1, .3f, 0, 0), [10] = new(.81884f, -.57403f, -.00006f, 0), [17] = new(0, 1, 1, .97731f),
            [18] = new(.6f, 1, .5f, 20), [21] = default, [52] = new(.35126f, 0, 0, 0), [55] = new(1, .34932f, 0, 0),
            [61] = new(.90428f, 0, .42693f, 0), [72] = default, [73] = new(1, 1, 1, 1), [75] = new(0, 0, -1, 0), [76] = new(1, 1, 250, 0),
        },
        Environment = new Dictionary<int, Vector4> { [10] = default, [11] = new(0, 0, -1, -1), [12] = new(.01f, 1, 0, 0) },
        LocalLights =
        [
            new(new(0, 28, 0), new(6, 3, 1.2f), 30, 20),
            new(new(-44.623f, 32.151f, 0), new(27.995f, 18.858f, 18.782f), 105.13f, 64.114f) { Direction = new(.75623f, -.6543f, 0), Cone = .91078f },
        ],
    };
    // Viewer approximation for missing sky-derived indirect illumination; not a game uniform value.
    public Vector3 PreviewIndirectLight { get; init; }
    // Evaluated sky rows: [6] atmosphere, [8] sun radiance, [9] scattered sun RGB, [10] sky direction, [24].xy sun controls.
    // Other time-of-day gsys_user0 rows: [17].w shadow strength, [21] fog colour, and
    // the rim light's colour [72], direction [75] and exponents/intensity [76], and field GI override colour [73].
    public IReadOnlyDictionary<int, Vector4> User0 { get; init; } = new Dictionary<int, Vector4>();
    // Time-of-day gsys_environment rows that only the vertex programs read.
    public IReadOnlyDictionary<int, Vector4> Environment { get; init; } = new Dictionary<int, Vector4>();
    // The gsys_user1 light list, at most seven lights, all within the light grid around the origin.
    public IReadOnlyList<AcnhLocalLight> LocalLights { get; init; } = [];
    // The color-grading volume ToneMapping samples; none leaves the tone-mapped color as is.
    public AcnhColorGrading? ColorGrading { get; init; }
    // The environment cube the game captures and turns into gsys_cube_map's panorama; none keeps the studio panorama.
    public AcnhEnvironmentProbe? EnvironmentProbe { get; init; }
    // agl's screen-space AO passes and the stage's settings for them; none leaves gsys_ssao white.
    public AcnhScreenOcclusion? ScreenOcclusion { get; init; }
    // The shade gsys_projection0 casts over direct light; none leaves it white.
    public AcnhProjectionShadow? ProjectionShadow { get; init; }
}

/// <summary>
/// A light of the game's local light list, at full strength out to <paramref name="Start"/> and gone <paramref name="Length"/>
/// further on.
/// </summary>
public sealed record AcnhLocalLight(Vector3 Position, Vector3 Color, float Start, float Length)
{
    // A spot light's direction, and the half-angle of its cone in radians. A light with no cone shines all round.
    public Vector3 Direction { get; init; }
    public float Cone { get; init; }
}

/// <summary>Where the environment cube is captured from, its clip range, and the panorama it is filtered into.</summary>
public sealed record AcnhEnvironmentProbe(Vector3 Position, float Near, float Far, AcnhPanoramaEnvironment Panorama);

/// <summary>agl's color_correction_map program and the filter block it bakes into the grading volume.</summary>
public sealed record AcnhColorGrading(SwitchProcessShaderProgram Map, byte[] Filters);

/// <summary>agl's SSAO programs, and the stage's ssao_parameter they are drawn with.</summary>
public sealed record AcnhScreenOcclusion(AcnhAmbientOcclusion Passes, AcnhSsaoParameters Settings);

public static class ParkUniforms
{
    public const int BufferSize = 65536;
    // Bytes of gsys_context that carry data; the rest of the 64 KiB block stays zero.
    public const int ContextSize = 2336;

    public static byte[] Constants(SwitchShaderStage stage)
    {
        var result = new byte[BufferSize];
        if (stage.Control.Length < 1800) return result;
        var control = stage.Control.Span;
        int length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(control[1788..]));
        int start = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(control[1792..]));
        if (length > BufferSize || start < 0 || start > stage.Binary.Length - length) throw new InvalidDataException("Native shader constant range is invalid.");
        stage.Binary.Span.Slice(start, length).CopyTo(result);
        return result;
    }

    public static byte[] Material(BfresMaterial material, ShaderUniformBlock block)
    {
        var result = new byte[BufferSize];
        block.Defaults.Span.CopyTo(result);
        foreach (var uniform in block.Uniforms)
        {
            if (uniform.Offset < 0 || !material.Parameters.TryGetValue(uniform.Name, out var parameter)) continue;
            byte[] packed = parameter.Type switch { 30 => TextureSrt(parameter, false), 31 => TextureSrt(parameter, true), _ => parameter.Data };
            if (uniform.Offset > block.Size - packed.Length) throw new InvalidDataException($"Material parameter {uniform.Name} exceeds the native uniform block.");
            packed.CopyTo(result, uniform.Offset);
        }
        return result;
    }

    public static byte[] Shape(BfresMesh mesh)
    {
        var result = new byte[BufferSize];
        Matrix3x4(result, 0, mesh.ShapeTransform);
        Integer(result, 48, mesh.VertexSkinCount);
        return result;
    }

    public static byte[] Skeleton(BfresMesh mesh)
    {
        var matrices = mesh.BonePalette;
        if (matrices.Length > BufferSize / 48) throw new NotSupportedException("Skeleton exceeds the native uniform buffer.");
        var result = new byte[BufferSize];
        for (int i = 0; i < matrices.Length; i++) Matrix3x4(result, i * 48, matrices.Span[i]);
        return result;
    }

    // shadow maps world space into the depth shadow map's [0, 1] texture and depth range; texel is its PCF tap step.
    // projected is the shade gsys_projection0 is bound to, if any.
    public static byte[] Context(Matrix4x4 view, Matrix4x4 projection, float near, float far, Vector2 screen, Matrix4x4 shadow, float texel,
        AcnhProjectionShadow? projected = null)
    {
        var result = new byte[ContextSize];
        float aspect = screen.X / screen.Y;
        Matrix3x4(result, 0, view); Matrix4(result, 48, view * projection); Matrix4(result, 112, projection);
        Matrix4x4.Invert(view, out var inverse); Matrix3x4(result, 176, inverse);
        Vector(result, 224, new(near, far, far / near, 1 - near / far));
        Vector(result, 240, new(1 / (far - near), near / (far - near), aspect, 1 / aspect));
        Vector(result, 256, new(far - near, 0, 0, 0));
        // The target's size; screen-space maps such as the SSAO are looked up by pixel position times its inverse.
        Vector(result, 288, new(screen.X, screen.Y, 1 / screen.X, 1 / screen.Y));
        // The shade's strength, which the shaders take max() with the depth shadow's; .55 was captured outside, where
        // the viewer has no shade bound.
        Float(result, 656, projected?.Density ?? .55f); Integer(result, 660, 1);
        for (int offset = 664; offset < ContextSize; offset += 4) Float(result, offset, 1);
        // Projected-shadow texture rows, read as texture(gsys_projection0, xy / z); without one, only a non-zero w
        // is needed, since the viewer binds a white texture there.
        var p = projected?.TextureProjection ?? new Matrix4x4 { M44 = 1 };
        Vector(result, 560, new(p.M11, p.M21, p.M31, p.M41)); Vector(result, 576, new(p.M12, p.M22, p.M32, p.M42));
        Vector(result, 592, new(p.M14, p.M24, p.M34, p.M44));
        Matrix4(result, 672, shadow);
        Float(result, 944, texel); Float(result, 948, texel);
        Vector(result, 1024, new(512, 4, 1, 1));
        return result;
    }

    public static byte[] StudioBlock(string name, AcnhLightingPreset? lighting = null)
    {
        var result = new byte[BufferSize];
        if (name == "gsys_user0")
        {
            // Captured from the running game; the time-of-day rows come from the preset. Rows holding world
            // positions or scene projections stay zero, since an isolated model has no island around it.
            Vector(result, 4 * 16, new(.4f, 0, 0, 0));
            Vector(result, 5 * 16, new(.5f, .2f, 0, 0));
            Vector(result, 14 * 16, new(100000, 1, 100000, 1));
            Vector(result, 16 * 16, new(0, 20, 0, 1));
            Vector(result, 20 * 16, new(1, 101.88f, 145.96f, 0)); // Fog length and start distance; [21] is its colour.
            // y scales the final additive atmosphere term, which needs game scene/depth inputs.
            Vector(result, 23 * 16, new(.3f, 0, 1, 4));
            Vector(result, 29 * 16, new(250, 0, .025f, .025f));
            Vector(result, 33 * 16, new(-.37461f, -.92718f, .016667f, .015385f));
            Vector(result, 34 * 16, new(7.5219f, 22.233f, .065622f, 3.5f));
            Vector(result, 35 * 16, new(-.25882f, -.96593f, .02f, .014286f));
            Vector(result, 36 * 16, new(54.57f, 16.814f, .02406f, 5));
            Vector(result, 37 * 16, new(1, 0, 0, 0));
            Vector(result, 39 * 16, new(0, .02f, .3f, 0));
            Vector(result, 40 * 16, new(.05f, 0, 0, .5f));
            Vector(result, 41 * 16, new(0, 0, .05f, .5f));
            Vector(result, 45 * 16, new(.028f, 1.1f, .018f, 1.1f));
            // Drum angle, offset and intensity; the drum bends the island, so it stays off. A zero
            // angle is a division-by-zero in the native vertex program.
            Vector(result, 46 * 16, new(20, 0, 0, 0));
            Vector(result, 51 * 16, new(7.2f, 1.05f, 1, 1.4e8f));
            Vector(result, 60 * 16, new(.02f, .02f, 1, 0));
            Vector(result, 65 * 16, new(0, 1, 0, 0));
            // x: where local lights start fading out across the light grid, the game's 32 in a room, past its walls.
            // y/z: start and length of the shadow distance fade around [87]; start it out of reach.
            Vector(result, 66 * 16, new(32, 100000, 1, 0));
            Vector(result, 77 * 16, new(.75f, .3f, 240, .15f));
            Vector(result, 81 * 16, new(1, .8f, 0, 0));
            Vector(result, 89 * 16, new(1, 0, 0, 0));
            foreach (var (row, value) in (lighting ?? AcnhLightingPreset.Day).User0) Vector(result, row * 16, value);
            if (lighting?.LocalLights.Count > 0)
            {
                // The light grid: its first corner, far corner and middle, then the cell size and the cell counts, as integers.
                Vector(result, 11 * 16, new(-LightGridHalf, -1, -LightGridHalf, 0));
                Vector(result, 12 * 16, new(LightGridHalf, 99, LightGridHalf, 0));
                Vector(result, 13 * 16, new(0, -1, 0, 0));
                Vector(result, 14 * 16, new(LightGridCell, 100, LightGridCell, 0));
                foreach (var (at, count) in new[] { (0, LightGridCells), (4, 1), (8, LightGridCells), (12, LightGridCells) })
                    Integer(result, 15 * 16 + at, count);
            }
        }
        if (name == "gsys_user1" && lighting?.LocalLights is { Count: > 0 } lights) LocalLights(result, lights);
        if (name == "gsys_environment")
        {
            Vector(result, 16, new(.2f, .22f, .26f, 1));
            Vector(result, 32, new(.08f, .09f, .1f, 1));
            Vector(result, 80, new(.4f, .4f, .4f, 1));
            Vector(result, 368, new(-.4f, -.8f, -.6f, 0));
            foreach (var (row, value) in (lighting ?? AcnhLightingPreset.Day).Environment) Vector(result, row * 16, value);
        }
        if (lighting != null && name == "gsys_environment")
        {
            // Isolated models have no scene sky/GI. Keep this separately switchable preview fill.
            var fill = new Vector4(lighting.PreviewIndirectLight, 0);
            Vector(result, 16, lighting.SkyColor * lighting.HemisphereIntensity + fill);
            Vector(result, 32, lighting.GroundColor * lighting.HemisphereIntensity + fill);
            Vector(result, 80, lighting.DirectColor * lighting.DirectIntensity);
            Vector(result, 368, new(lighting.DirectDirection, 0));
        }
        if (lighting != null && name == "gsys_user0") Float(result, 16 * 16 + 12, lighting.EnvironmentTintIntensity);
        return result;
    }

    // The game's local light grid: 16 by 16 cells of one tile, as it lays it out around a house's room.
    private const int LightGridCells = 16;
    private const float LightGridCell = 10, LightGridHalf = LightGridCells * LightGridCell / 2;

    // gsys_user1: each grid cell's light count and up to seven light indices, then, per light, its color, its position,
    // its spot direction and its falloff start, each in a table of its own.
    private static void LocalLights(byte[] target, IReadOnlyList<AcnhLocalLight> lights)
    {
        int count = Math.Min(lights.Count, 7);
        // The lights fade out by themselves, so every cell lists them all.
        for (int cell = 0; cell < LightGridCells * LightGridCells; cell++)
        {
            Integer(target, cell * 32, count);
            for (int i = 0; i < count; i++) Integer(target, cell * 32 + 4 + i * 4, i);
        }
        for (int i = 0; i < count; i++)
        {
            var light = lights[i];
            Vector(target, 0x2000 + i * 16, new(light.Color, light.Cone > 0 ? 1 : 0));
            Vector(target, 0x4000 + i * 16, new(light.Position, 1 / light.Length));
            Vector(target, 0x6000 + i * 16, new(light.Direction, light.Cone));
            Vector(target, 0x8000 + i * 16, new(light.Start));
        }
    }

    public static void Float(byte[] target, int at, float value) => BinaryPrimitives.WriteSingleLittleEndian(target.AsSpan(at, 4), value);
    public static void Integer(byte[] target, int at, int value) => BinaryPrimitives.WriteInt32LittleEndian(target.AsSpan(at, 4), value);
    public static void Vector(byte[] target, int at, Vector4 v) { Float(target, at, v.X); Float(target, at + 4, v.Y); Float(target, at + 8, v.Z); Float(target, at + 12, v.W); }
    
    private static void Matrix3x4(byte[] b, int at, Matrix4x4 m)
    {
        Vector(b, at, new(m.M11, m.M21, m.M31, m.M41));
        Vector(b, at + 16, new(m.M12, m.M22, m.M32, m.M42));
        Vector(b, at + 32, new(m.M13, m.M23, m.M33, m.M43));
    }

    private static void Matrix4(byte[] b, int at, Matrix4x4 m)
    { 
        Matrix3x4(b, at, m); 
        Vector(b, at + 48, new(m.M14, m.M24, m.M34, m.M44)); 
    }

    private static byte[] TextureSrt(BfresShaderParameter parameter, bool extended)
    {
        ReadOnlySpan<byte> b = parameter.Data;
        if (b.Length < 24) 
            throw new InvalidDataException("Truncated texture SRT.");

        int mode = BinaryPrimitives.ReadInt32LittleEndian(b);
        float sx = BinaryPrimitives.ReadSingleLittleEndian(b[4..]), sy = BinaryPrimitives.ReadSingleLittleEndian(b[8..]);
        float r = BinaryPrimitives.ReadSingleLittleEndian(b[12..]), tx = BinaryPrimitives.ReadSingleLittleEndian(b[16..]), ty = BinaryPrimitives.ReadSingleLittleEndian(b[20..]);
        float xc = sx * MathF.Cos(r), xs = sx * MathF.Sin(r), yc = sy * MathF.Cos(r), ys = sy * MathF.Sin(r);
        float[] values = mode switch
        {
            0 => [xc, -ys, xs, yc, -.5f * (xc + xs - sx) - sx * tx, -.5f * (yc - ys + sy) + sy * ty + 1, 0, 0],
            1 => [xc, -ys, xs, yc, -xc * (tx + .5f) + xs * (ty - .5f) + .5f, ys * (tx + .5f) + yc * (ty - .5f) + .5f, 0, 0],
            2 => [xc, ys, -xs, yc, xs - xc * tx - xs * ty, -yc - ys * tx + yc * ty + 1, 0, 0],
            _ => throw new NotSupportedException($"Texture SRT mode {mode} is unsupported.")
        };

        if (extended) 
            values = [values[0], values[2], values[4], 0, values[1], values[3], values[5], 0, 0, 0, 1, 0];

        var result = new byte[values.Length * 4];
        for (int i = 0; i < values.Length; i++) 
            Float(result, i * 4, values[i]);

        return result;
    }
}
