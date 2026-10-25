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

    // Viewer approximation for missing sky-derived indirect illumination; not a game uniform value.
    public Vector3 PreviewIndirectLight { get; init; }
    // Time-of-day gsys_user0 rows: [10] a sky direction, [17].w shadow strength, [21] fog colour, and
    // the rim light's colour [72], direction [75] and exponents/intensity [76].
    public IReadOnlyDictionary<int, Vector4> User0 { get; init; } = new Dictionary<int, Vector4>();
    // Time-of-day gsys_environment rows that only the vertex programs read.
    public IReadOnlyDictionary<int, Vector4> Environment { get; init; } = new Dictionary<int, Vector4>();
}

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
    public static byte[] Context(Matrix4x4 view, Matrix4x4 projection, float near, float far, float aspect, Matrix4x4 shadow, float texel)
    {
        var result = new byte[ContextSize];
        Matrix3x4(result, 0, view); Matrix4(result, 48, view * projection); Matrix4(result, 112, projection);
        Matrix4x4.Invert(view, out var inverse); Matrix3x4(result, 176, inverse);
        Vector(result, 224, new(near, far, far / near, 1 - near / far));
        Vector(result, 240, new(1 / (far - near), near / (far - near), aspect, 1 / aspect));
        Vector(result, 256, new(far - near, 0, 0, 0));
        Float(result, 656, .55f); Integer(result, 660, 1);
        for (int offset = 664; offset < ContextSize; offset += 4) Float(result, offset, 1);
        // Projected-shadow texture matrix; it only needs a non-zero w, since the viewer binds a white texture there.
        Vector(result, 560, default); Vector(result, 576, default); Vector(result, 592, new(0, 0, 0, 1));
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
            // y/z: start and length of the shadow distance fade around [87]; start it out of reach.
            Vector(result, 66 * 16, new(-1, 100000, 1, 0));
            Vector(result, 77 * 16, new(.75f, .3f, 240, .15f));
            Vector(result, 81 * 16, new(1, .8f, 0, 0));
            Vector(result, 89 * 16, new(1, 0, 0, 0));
            foreach (var (row, value) in (lighting ?? AcnhLightingPreset.Day).User0) Vector(result, row * 16, value);
        }
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