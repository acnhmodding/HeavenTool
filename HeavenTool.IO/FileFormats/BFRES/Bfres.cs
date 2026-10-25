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
}

// Mips holds the deswizzled mip chain, largest level first.
public sealed record BntxTexture(string Name, int Width, int Height, uint Format, IReadOnlyList<ReadOnlyMemory<byte>> Mips, byte[] Channels);

public sealed record BfresSampler(string Name, BntxTexture? Texture, byte WrapU, byte WrapV, ushort FilterFlags, byte BorderColor,
    byte MaxAnisotropy, float MinLod, float MaxLod, float LodBias);

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
