using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;

namespace HeavenTool.IO.FileFormats.BFRES;

public sealed record BfresAsset(string EntryName, BfresDocument Document);

public sealed record BfresDocument(IReadOnlyList<BfresModel> Models, IReadOnlyList<string> Warnings);

public sealed record BfresModel(string Name, IReadOnlyList<BfresMesh> Meshes, IReadOnlyList<BfresMaterial> Materials, IReadOnlyList<BfresBone> Bones);

public sealed record BfresBone(int Index, string Name, int ParentIndex, Matrix4x4 WorldTransform)
{
    public Vector3 Position => WorldTransform.Translation;
}

public sealed class BfresMesh
{
    public required string Name { get; init; }
    public required BfresMaterial Material { get; init; }
    public int VertexSkinCount { get; init; }
    public ReadOnlyMemory<Vector3> Positions { get; init; }
    public ReadOnlyMemory<Vector3> Normals { get; init; }
    public ReadOnlyMemory<uint> Indices { get; init; }
    public ReadOnlyMemory<Vector2> TexCoords { get; init; }
    public required IReadOnlyDictionary<string, ReadOnlyMemory<Vector4>> NativeAttributes { get; init; }
    public ReadOnlyMemory<Matrix4x4> BonePalette { get; init; }
    public Matrix4x4 ShapeTransform { get; init; } = Matrix4x4.Identity;

    /// <summary>A copy moved by <paramref name="transform"/>, such as a part placed on a bone of another model.</summary>
    public BfresMesh Transformed(Matrix4x4 transform)
    {
        var positions = new Vector3[Positions.Length];
        for (int i = 0; i < positions.Length; i++) positions[i] = Vector3.Transform(Positions.Span[i], transform);
        var normalTransform = Matrix4x4.Invert(transform, out var inverse) ? Matrix4x4.Transpose(inverse) : transform;
        var normals = new Vector3[Normals.Length];
        for (int i = 0; i < normals.Length; i++)
        {
            var normal = Vector3.TransformNormal(Normals.Span[i], normalTransform);
            normals[i] = normal.LengthSquared() > 1e-12f ? Vector3.Normalize(normal) : normal;
        }
        var palette = new Matrix4x4[BonePalette.Length];
        for (int i = 0; i < palette.Length; i++) palette[i] = BonePalette.Span[i] * transform;

        // Native shaders read the raw vertices with the shape and bone matrices, so those move too.
        return new BfresMesh
        {
            Name = Name, Material = Material, VertexSkinCount = VertexSkinCount, Positions = positions, Normals = normals,
            Indices = Indices, TexCoords = TexCoords, NativeAttributes = NativeAttributes, BonePalette = palette,
            ShapeTransform = ShapeTransform * transform,
        };
    }

    /// <summary>A copy drawn with another material, such as one with textures from another file.</summary>
    public BfresMesh WithMaterial(BfresMaterial material) => new()
    {
        Name = Name, Material = material, VertexSkinCount = VertexSkinCount, Positions = Positions, Normals = Normals,
        Indices = Indices, TexCoords = TexCoords, NativeAttributes = NativeAttributes, BonePalette = BonePalette,
        ShapeTransform = ShapeTransform,
    };
}

// Mips holds the deswizzled mip chain, largest level first.
public sealed record BntxTexture(string Name, int Width, int Height, uint Format, IReadOnlyList<ReadOnlyMemory<byte>> Mips, byte[] Channels);

// TextureName is the texture the material asks for, which another file may hold when Texture is null.
public sealed record BfresSampler(string Name, BntxTexture? Texture, byte WrapU, byte WrapV, ushort FilterFlags, byte BorderColor,
    byte MaxAnisotropy, float MinLod, float MaxLod, float LodBias)
{
    public string TextureName { get; init; } = "";
}

public sealed record BfresRenderInfo(IReadOnlyList<string> Strings, IReadOnlyList<float> Floats, IReadOnlyList<int> Integers);

public sealed record BfresShaderParameter(byte Type, byte[] Data)
{
    public float Float(int component, float fallback)
    {
        if (Type is not (>= 12 and <= 15) || component < 0 || component * 4 > Data.Length - 4) return fallback;
        float value = BinaryPrimitives.ReadSingleLittleEndian(Data.AsSpan(component * 4));
        return float.IsFinite(value) ? value : fallback;
    }
}

public sealed class BfresMaterial
{
    public required string Name { get; init; }
    public required string ShaderArchive { get; init; }
    public required string ShadingModel { get; init; }
    public required IReadOnlyDictionary<string, string> AttributeAssignments { get; init; }
    public required IReadOnlyDictionary<string, string> SamplerAssignments { get; init; }
    public required IReadOnlyDictionary<string, string> ShaderOptions { get; init; }
    // Values as loaded; previewer edits only replace entries in Parameters and are never saved.
    public required IReadOnlyDictionary<string, BfresShaderParameter> OriginalParameters { get; init; }
    public required IReadOnlyDictionary<string, BfresRenderInfo> RenderInfos { get; init; }
    public required IReadOnlyList<BfresSampler> Samplers { get; init; }

    private Dictionary<string, BfresShaderParameter>? edited;
    public IReadOnlyDictionary<string, BfresShaderParameter> Parameters => edited ?? OriginalParameters;
    public bool HasChanges => edited != null && edited.Any(p => !p.Value.Data.AsSpan().SequenceEqual(OriginalParameters[p.Key].Data));

    public void SetParameterComponent(string name, int component, float value)
    {
        var parameter = Parameters[name];
        byte[] data = [.. parameter.Data];
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(component * 4), value);
        (edited ??= new(OriginalParameters))[name] = parameter with { Data = data };
    }

    public void ResetParameters() => edited = null;

    /// <summary>
    /// A copy whose samplers take their texture from <paramref name="textures"/>, by the texture name they ask for, when it has
    /// one. Game files keep some textures apart from the model, such as furniture colors and room wallpapers.
    /// </summary>
    public BfresMaterial WithTextures(IReadOnlyDictionary<string, BntxTexture> textures) => new()
    {
        Name = Name, ShaderArchive = ShaderArchive, ShadingModel = ShadingModel, AttributeAssignments = AttributeAssignments,
        SamplerAssignments = SamplerAssignments, ShaderOptions = ShaderOptions, OriginalParameters = OriginalParameters,
        RenderInfos = RenderInfos,
        Samplers = [.. Samplers.Select(sampler => textures.TryGetValue(sampler.TextureName, out var texture) ? sampler with { Texture = texture } : sampler)],
    };

    public bool IsParkUber => ShaderArchive is "Park_UBER" or "Park_UBER_All" && ShadingModel == "park_uber";

    public BfresSampler? Sampler(string shaderInput) =>
        SamplerAssignments.TryGetValue(shaderInput, out string? name) ? Samplers.FirstOrDefault(x => x.Name == name) : null;

    public int Option(string name, int fallback = 0) => ShaderOptions.TryGetValue(name, out var value)
        && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : fallback;

    public float Parameter(string name, float fallback) => Parameters.TryGetValue(name, out var parameter) ? parameter.Float(0, fallback) : fallback;

    public Vector4 Color(string name, Vector4 fallback) => Parameters.TryGetValue(name, out var parameter)
        ? new(parameter.Float(0, fallback.X), parameter.Float(1, fallback.Y), parameter.Float(2, fallback.Z), parameter.Float(3, fallback.W)) : fallback;

    public string Info(string name, string fallback) => RenderInfos.TryGetValue(name, out var info) && info.Strings.Count > 0 ? info.Strings[0] : fallback;

    public float InfoFloat(string name, float fallback) => RenderInfos.TryGetValue(name, out var info) && info.Floats.Count > 0 ? info.Floats[0] : fallback;
}
