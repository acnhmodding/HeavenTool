#nullable enable
using HeavenTool.IO.FileFormats.BFRES;
using System.Numerics;
using static HeavenTool.IO.AcnhEnvironment.AcnhColorCorrection;

namespace HeavenTool.IO.AcnhEnvironment;

/// <summary>
/// The house's projected shade (env_inside.bgsdw's projection_shadow_0): <paramref name="Texture"/> cast down from
/// envinside.baglenv's ProjectionShadow projector, repeating. <paramref name="TextureProjection"/> takes a world position
/// (row-vector convention) to (u·w, v·w, 0, w), what gsys_context rows 35–37 hold for the game shaders' projective lookup;
/// <paramref name="Density"/> is how much of the direct light its dark texels take away.
/// </summary>
public sealed record AcnhProjectionShadow(Matrix4x4 TextureProjection, BntxTexture Texture, float Density)
{
    // Unconfirmed: nothing in the files names the texture, and collect.genvres holds only this and sample_cloud.
    private const string TextureName = "treeshade";

    public static AcnhProjectionShadow? LoadInside(string romfs)
    {
        var gameEnv = OpenArchive(Entry(OpenArchive(File.ReadAllBytes(Path.Combine(romfs, "Env", "GameEnv.szs"))), "GameEnv.genvb"));
        using var resources = new MemoryStream(Entry(gameEnv, "collect.genvres"), false);
        var texture = BfresFileReader.LoadTextures(resources).SingleOrDefault(t => t.Name == TextureName)
            ?? throw new InvalidDataException($"collect.genvres is missing {TextureName}.");
        return Read(Entry(gameEnv, "env_inside.bgsdw"), Entry(gameEnv, "envinside.baglenv"), texture);
    }

    public static AcnhProjectionShadow? Read(byte[] shadow, byte[] environment, BntxTexture texture)
    {
        var settings = new Aamp(shadow);
        var p = settings.Parameters(settings.Objects(0x30 + settings.Int(0x14)).GetValueOrDefault("projection_shadow_0".ToCRC32(), -1));
        string projector = settings.String(p, "proj_name");
        if (projector.Length == 0) return null;

        var env = new Aamp(environment);
        int root = 0x30 + env.Int(0x14);
        // The projector list (0xd968d421) holds one object per projector, matched by name.
        var list = env.Lists(root).GetValueOrDefault(0xd968d421, -1);
        if (list < 0) throw new InvalidDataException("envinside.baglenv has no projectors.");
        var light = env.Objects(list).Values.Select(env.Parameters).FirstOrDefault(o => env.String(o, "name") == projector)
            ?? throw new InvalidDataException($"envinside.baglenv has no {projector} projector.");
        if (!env.Bool(light, "enable")) return null;

        var view = Matrix4x4.CreateLookAt(env.Vector3(light, "view_pos"), env.Vector3(light, "view_at"), env.Vector3(light, "view_up"));
        float near = env.Float(light, "near"), far = env.Float(light, "far"), height = env.Float(light, "height"), aspect = env.Float(light, "aspect");
        var clip = env.Int(light, "proj_type") switch
        {
            // Assumed sead's order, perspective then orthographic; the two differ by a few percent over a room.
            0 => Matrix4x4.CreatePerspectiveFieldOfView(env.Float(light, "fovy") * MathF.PI / 180, aspect, near, far),
            1 => Matrix4x4.CreateOrthographic(height * aspect, height, near, far),
            var type => throw new NotSupportedException($"Projector type {type} is not recovered."),
        };

        // The texture matrix FUN_71039b6820 builds from bias_scale/rotate/trans, here taking the clip-space
        // position's xy to texture space. The swing animation (a few thousandths of a repeat) is left out.
        var scale = settings.Vector2(p, "bias_scale");
        var trans = settings.Vector2(p, "bias_trans");
        float rotate = settings.Float(p, "bias_rotate"), cos = MathF.Cos(rotate), sin = MathF.Sin(rotate);
        float sx = .5f / scale.X, sy = -.5f / scale.Y;
        var texture2d = new Matrix4x4(
            cos * sx, sin * sx, 0, 0,
            -sin * sy, cos * sy, 0, 0,
            0, 0, 0, 0,
            trans.X + .5f, trans.Y + .5f, 0, 1);
        return new(view * clip * texture2d, texture, settings.Float(p, "density"));
    }
}
