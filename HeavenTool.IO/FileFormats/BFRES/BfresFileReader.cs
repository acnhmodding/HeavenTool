using AeonSake.NintendoTools.Compression.Yaz0;
using AeonSake.NintendoTools.Compression.Zstd;
using AeonSake.NintendoTools.FileFormats.Sarc;
using HeavenTool.IO.Compression;
using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;

namespace HeavenTool.IO.FileFormats.BFRES;

// Reads the models, materials, skeletons and embedded textures of ACNH's Switch BFRES v9 files for previewing.
public sealed partial class BfresFileReader
{
    private const uint ZstdMagic = 0xfd2fb528;
    private const ushort LittleEndianMark = 0xfeff;

    // The sizes of the file's array entries, in bytes.
    private const int ModelSize = 120, ShapeSize = 96, MaterialSize = 168, BoneSize = 96;
    private const int VertexBufferSize = 16, VertexAttributeSize = 16, ExternalFileSize = 16;
    private const int SamplerSize = 32, RenderInfoSize = 24, ShaderParameterSize = 32;
    private const int PointerSize = 8;

    private readonly BinaryReader reader;
    private readonly CancellationToken cancellation;
    private readonly List<string> warnings = [];
    private readonly Dictionary<string, BntxTexture> textures = [];
    private static readonly ZstdDecompressor zstdDecompressor = new();
    private static readonly Yaz0Decompressor yaz0Decompressor = new();
    private long gpuBase;

    private BfresFileReader(BinaryReader reader, CancellationToken cancellation)
    {
        this.reader = reader;
        this.cancellation = cancellation;
    }

    // Loads a BFRES, or every BFRES inside a SARC, optionally Zstd or Yaz0 compressed.
    public static IReadOnlyList<BfresAsset> Load(Stream input, CancellationToken cancellation = default)
    {
        var files = Unpack(input, "FRES"u8, "BFRES");
        cancellation.ThrowIfCancellationRequested();
        if (files.Count == 0) throw new InvalidDataException("Expected BFRES or a SARC model archive.");

        return [.. files.Select(file => new BfresAsset(file.Name, Read(file.Data, cancellation)))];
    }

    // Loads only the embedded textures of a BFRES, or of every BFRES inside a SARC such as a layout icon, optionally Zstd or
    // Yaz0 compressed. Textures in unsupported formats are left out.
    public static IReadOnlyList<BntxTexture> LoadTextures(Stream input, CancellationToken cancellation = default)
    {
        var textures = new List<BntxTexture>();
        foreach (var (_, data) in Unpack(input, "FRES"u8, "BFRES"))
        {
            using var reader = Open(data, 0, data.Length);
            var loader = new BfresFileReader(reader, cancellation);
            loader.ReadEmbeddedTextures(data);
            textures.AddRange(loader.textures.Values);
        }
        return textures;
    }

    // Decompresses the input and returns it when it starts with magic, or else every SARC entry that does.
    internal static List<(string Name, byte[] Data)> Unpack(Stream input, ReadOnlySpan<byte> magic, string name)
    {
        byte[] data = Decompress(input);
        if (data.AsSpan().StartsWith(magic)) 
            return [(name, data)];

        if (!data.AsSpan().StartsWith("SARC"u8)) 
            return [];

        using var archive = new MemoryStream(data);
        var files = new List<(string, byte[])>();
        foreach (var file in new SarcFileReader().Read(archive).Files)
            if (file.Data.AsSpan().StartsWith(magic)) 
                files.Add((file.Name, file.Data));
        
        return files;
    }

    private static byte[] Decompress(Stream input)
    {
        Span<byte> header = stackalloc byte[4];
        long start = input.Position;
        input.ReadExactly(header);
        input.Position = start;

        using var content = new MemoryStream();
        if (BinaryPrimitives.ReadUInt32LittleEndian(header) == ZstdMagic)
            zstdDecompressor.Decompress(input, content);
        else if (header.SequenceEqual("Yaz0"u8))
            yaz0Decompressor.Decompress(input, content);
        else
            input.CopyTo(content);
        
        return content.ToArray();
    }

    private static BfresDocument Read(byte[] data, CancellationToken cancellation)
    {
        using var reader = Open(data, 0, data.Length);
        return new BfresFileReader(reader, cancellation).Parse(data);
    }

    private static BinaryReader Open(byte[] data, int offset, int length) =>
        new(new MemoryStream(data, offset, length, false), false) { Encoding = Encoding.UTF8 };

    private BfresDocument Parse(byte[] data)
    {
        ReadEmbeddedTextures(data);

        long bufferInfo = reader.ReadPointerAt(176);
        gpuBase = bufferInfo == 0 ? 0 : reader.ReadPointerAt(bufferInfo + 8);

        long modelArray = reader.ReadPointerAt(40);
        int modelCount = reader.ReadUInt16At(220);
        var models = new List<BfresModel>();
        for (int i = 0; i < modelCount; i++) models.Add(ReadModel(modelArray + i * ModelSize));

        var animations = ReadSkeletalAnimations();
        if (!models.Any(model => model.Meshes.Count > 0))
            warnings.Add("This BFRES contains no previewable model geometry (it may be a texture or animation resource).");

        return new BfresDocument(models, warnings) { SkeletalAnimations = animations };
    }

    private Vector3 ReadVector3(long at) => new(reader.ReadSingleAt(at), reader.ReadSingleAt(at + 4), reader.ReadSingleAt(at + 8));

    #region Models

    private sealed record VertexData(Vector3[] Positions, Vector3[] Normals, Vector4[] BoneIndices, Vector4[] Weights, Vector2[] TexCoords,
        Dictionary<string, ReadOnlyMemory<Vector4>> Attributes);

    private BfresModel ReadModel(long at)
    {
        cancellation.ThrowIfCancellationRequested();
        string name = reader.ReadNameAt(at + 8);
        var skeleton = ReadSkeleton(reader.ReadPointerAt(at + 24));

        long materialArray = reader.ReadPointerAt(at + 56);
        var materials = new BfresMaterial[reader.ReadUInt16At(at + 108)];
        for (int i = 0; i < materials.Length; i++) materials[i] = ReadMaterial(materialArray + i * MaterialSize);

        long shapeArray = reader.ReadPointerAt(at + 40);
        int shapeCount = reader.ReadUInt16At(at + 106);
        var meshes = new List<BfresMesh>();
        // Several shapes can share one FVTX, so decoded vertices are cached by offset.
        var vertexCache = new Dictionary<long, VertexData>();
        for (int i = 0; i < shapeCount; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            long shape = shapeArray + i * ShapeSize;
            string shapeName = reader.ReadNameAt(shape + 8);
            try
            {
                if (ReadMesh(shape, shapeName, materials, skeleton, vertexCache) is { } mesh) meshes.Add(mesh);
            }
            catch (NotSupportedException ex)
            {
                warnings.Add($"{name}/{shapeName}: {ex.Message}");
            }
        }
        return new BfresModel(name, meshes, materials, skeleton.Bones);
    }

    // The mesh of a shape, or null when the shape has no geometry to draw.
    private BfresMesh? ReadMesh(long shape, string name, BfresMaterial[] materials, SkeletonData skeleton, Dictionary<long, VertexData> vertexCache)
    {
        const uint MeshletFlag = 16;
        if ((reader.ReadUInt32At(shape + 4) & MeshletFlag) != 0) throw new NotSupportedException("Meshlet geometry is unsupported.");

        long vertexBuffer = reader.ReadPointerAt(shape + 16);
        if (!vertexCache.TryGetValue(vertexBuffer, out var vertices))
        {
            vertices = ReadVertices(vertexBuffer);
            vertexCache.Add(vertexBuffer, vertices);
        }

        int meshCount = reader.ReadByteAt(shape + 91);
        if (meshCount == 0) return null;

        int skinCount = reader.ReadByteAt(shape + 90);
        bool missingBones = skinCount > 0 && vertices.BoneIndices.Length == 0;
        bool missingWeights = skinCount > 1 && vertices.Weights.Length == 0;
        if (skinCount > 4 || missingBones || missingWeights)
            throw new NotSupportedException("Unsupported skin weights or missing skin attributes.");

        var shapeTransform = skinCount == 0 && skeleton.World.Length > 0 ? skeleton.World[reader.ReadUInt16At(shape + 84)] : Matrix4x4.Identity;
        var indices = ReadIndices(reader.ReadPointerAt(shape + 24));
        var (positions, normals) = PoseVertices(vertices, skinCount, shapeTransform, skeleton.Palette);
        if (vertices.Normals.Length == 0) normals = GenerateNormals(positions, indices);

        return new BfresMesh
        {
            Name = name,
            Material = materials[reader.ReadUInt16At(shape + 82)],
            VertexSkinCount = skinCount,
            Positions = positions,
            Normals = normals,
            Indices = indices,
            TexCoords = vertices.TexCoords,
            NativeAttributes = vertices.Attributes,
            BonePalette = skeleton.Palette,
            ShapeTransform = shapeTransform
        };
    }

    // The vertices moved from their bind pose into the model's space, by their shape or their bones.
    private static (Vector3[] Positions, Vector3[] Normals) PoseVertices(VertexData vertices, int skinCount, Matrix4x4 shapeTransform, Matrix4x4[] palette)
    {
        var positions = new Vector3[vertices.Positions.Length];
        var normals = new Vector3[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            var transform = skinCount > 0 ? SkinTransform(vertices, i, skinCount, palette) : shapeTransform;
            positions[i] = Vector3.Transform(vertices.Positions[i], transform);
            if (vertices.Normals.Length == 0) continue;

            var normal = Matrix4x4.Invert(transform, out var inverse)
                ? Vector3.TransformNormal(vertices.Normals[i], Matrix4x4.Transpose(inverse))
                : vertices.Normals[i];
            normals[i] = normal.LengthSquared() > 1e-12f ? Vector3.Normalize(normal) : normal;
        }
        return (positions, normals);
    }

    // The bones' matrices blended by the vertex's weights. A vertex with a single bone has no weight attribute.
    private static Matrix4x4 SkinTransform(VertexData vertices, int vertex, int skinCount, Matrix4x4[] palette)
    {
        Matrix4x4 transform = default;
        for (int i = 0; i < skinCount; i++)
        {
            float weight = skinCount == 1 ? 1 : vertices.Weights[vertex][i];
            transform += palette[(int)vertices.BoneIndices[vertex][i]] * weight;
        }
        return transform;
    }

    private VertexData ReadVertices(long at)
    {
        var buffers = ReadVertexBuffers(at);
        long attributeArray = reader.ReadPointerAt(at + 8);
        int attributeCount = reader.ReadByteAt(at + 76);
        int vertexCount = reader.ReadInt32At(at + 80);

        var decoded = new Dictionary<string, Vector4[]>();
        for (int i = 0; i < attributeCount; i++)
        {
            long attribute = attributeArray + i * VertexAttributeSize;
            string name = reader.ReadNameAt(attribute);
            if (IsDecodedAttribute(name)) decoded.Add(name, ReadVertexAttribute(attribute, buffers, vertexCount));
        }
        if (!decoded.TryGetValue("_p0", out var positions)) throw new NotSupportedException("No position attribute (_p0).");

        Vector4[] Values(string name) => decoded.TryGetValue(name, out var values) ? values : [];
        static Vector3 ToVector3(Vector4 value) => new(value.X, value.Y, value.Z);
        static Vector2 ToVector2(Vector4 value) => new(value.X, value.Y);

        return new VertexData(
            Positions: Array.ConvertAll(positions, ToVector3),
            Normals: Array.ConvertAll(Values("_n0"), ToVector3),
            BoneIndices: Values("_i0"),
            Weights: Values("_w0"),
            TexCoords: Array.ConvertAll(Values("_u0"), ToVector2),
            Attributes: decoded.ToDictionary(pair => pair.Key, pair => (ReadOnlyMemory<Vector4>)pair.Value));
    }

    // Vertex buffers are packed one after another, each aligned to 8 bytes.
    private (long Offset, int Stride)[] ReadVertexBuffers(long at)
    {
        long sizes = reader.ReadPointerAt(at + 48);
        long strides = reader.ReadPointerAt(at + 56);
        var buffers = new (long Offset, int Stride)[reader.ReadByteAt(at + 77)];
        long offset = gpuBase + reader.ReadUInt32At(at + 72);
        for (int i = 0; i < buffers.Length; i++)
        {
            offset = (offset + 7) & ~7L;
            buffers[i] = (offset, reader.ReadInt32At(strides + i * VertexBufferSize));
            offset += reader.ReadUInt32At(sizes + i * VertexBufferSize);
        }
        return buffers;
    }

    // Attributes are named _ and a kind, then a digit: position, normal, bone index, weight, uv, tangent, bitangent or color.
    private static bool IsDecodedAttribute(string name) => name.Length == 3 && name[0] == '_' && "pniwutbc".Contains(name[1]);

    private Vector4[] ReadVertexAttribute(long attribute, (long Offset, int Stride)[] buffers, int vertexCount)
    {
        // The attribute format is the only big endian field.
        ushort format = BinaryPrimitives.ReverseEndianness(reader.ReadUInt16At(attribute + 8));
        var (bufferOffset, stride) = buffers[reader.ReadByteAt(attribute + 14)];
        long first = bufferOffset + reader.ReadUInt16At(attribute + 12);

        var values = new Vector4[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            if ((i & 4095) == 0) cancellation.ThrowIfCancellationRequested();
            values[i] = DecodeVertexValue(first + (long)i * stride, format);
        }
        return values;
    }

    private Vector4 DecodeVertexValue(long at, ushort format)
    {
        const ushort PackedSigned10102 = 0x020e;
        if (format == PackedSigned10102) return DecodePackedSigned10102(reader.ReadUInt32At(at));

        int layout = format & 255;
        var (bits, count) = layout switch
        {
            0x02 => (8, 1),
            0x09 => (8, 2),
            0x0b => (8, 4),
            0x0a => (16, 1),
            0x12 => (16, 2),
            0x15 => (16, 4),
            0x16 => (32, 1),
            0x17 => (32, 2),
            0x18 => (32, 3),
            0x19 => (32, 4),
            _ => throw new NotSupportedException($"Vertex format 0x{format:X4} is unsupported.")
        };

        int kind = format >> 8;
        // Nintendo's scalar 16-bit UNorm and SNorm values swap their kind codes.
        if (layout == 0x0a && kind is 2 or 3) kind ^= 1;

        Vector4 value = default;
        for (int i = 0; i < count; i++)
        {
            long component = at + i * bits / 8;
            uint raw = bits switch
            {
                8 => reader.ReadByteAt(component),
                16 => reader.ReadUInt16At(component),
                _ => reader.ReadUInt32At(component)
            };
            int signed = bits switch
            {
                8 => (sbyte)raw,
                16 => (short)raw,
                _ => (int)raw
            };
            value[i] = kind switch
            {
                1 => raw / (bits == 8 ? 255f : bits == 16 ? 65535f : uint.MaxValue),
                2 => Math.Max(signed / (bits == 8 ? 127f : bits == 16 ? 32767f : int.MaxValue), -1),
                3 or 8 => raw,
                4 or 10 => signed,
                5 when bits == 16 => (float)BitConverter.UInt16BitsToHalf((ushort)raw),
                5 when bits == 32 => BitConverter.UInt32BitsToSingle(raw),
                _ => throw new NotSupportedException($"Vertex format 0x{format:X4} is unsupported.")
            };
        }
        return value;
    }

    private static Vector4 DecodePackedSigned10102(uint packed)
    {
        static float Signed(uint value) => Math.Max(((int)(value << 22) >> 22) / 511f, -1);

        return new(Signed(packed & 1023), Signed((packed >> 10) & 1023), Signed((packed >> 20) & 1023), (packed >> 30) / 3f);
    }

    private uint[] ReadIndices(long at)
    {
        const int TriangleList = 3, TriangleStrip = 4;

        long offset = gpuBase + reader.ReadUInt32At(at + 32);
        int topology = reader.ReadInt32At(at + 36);
        int count = reader.ReadInt32At(at + 44);
        uint firstVertex = reader.ReadUInt32At(at + 48);
        var (indices, restart) = reader.ReadInt32At(at + 40) switch
        {
            0 => (Array.ConvertAll(reader.ReadByteArrayAt(offset, count), index => (uint)index), byte.MaxValue),
            1 => (Array.ConvertAll(reader.ReadUInt16ArrayAt(offset, count), index => (uint)index), ushort.MaxValue),
            2 => (reader.ReadUInt32ArrayAt(offset, count), uint.MaxValue),
            _ => throw new NotSupportedException("Unsupported index format.")
        };

        for (int i = 0; i < indices.Length; i++)
            indices[i] = topology == TriangleStrip && indices[i] == restart ? uint.MaxValue : indices[i] + firstVertex;

        return topology switch
        {
            TriangleList => indices,
            TriangleStrip => TriangulateStrip(indices),
            _ => throw new NotSupportedException($"Primitive topology {topology} is unsupported.")
        };
    }

    // Converts a triangle strip, restarting at uint.MaxValue, into a list, dropping degenerate triangles.
    private static uint[] TriangulateStrip(uint[] strip)
    {
        var triangles = new List<uint>();
        int start = 0;
        for (int i = 0; i < strip.Length; i++)
        {
            if (strip[i] == uint.MaxValue)
            {
                start = i + 1;
                continue;
            }
            if (i - start < 2) continue;

            uint a = strip[i - 2], b = strip[i - 1], c = strip[i];
            if (a == b || b == c || a == c) continue;

            // Every other triangle of a strip is wound the other way.
            if (((i - start) & 1) != 0) (a, b) = (b, a);
            triangles.AddRange(a, b, c);
        }
        return [.. triangles];
    }

    private static Vector3[] GenerateNormals(Vector3[] positions, uint[] indices)
    {
        var normals = new Vector3[positions.Length];
        for (int i = 0; i < indices.Length; i += 3)
        {
            uint a = indices[i], b = indices[i + 1], c = indices[i + 2];
            var normal = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
            normals[a] += normal;
            normals[b] += normal;
            normals[c] += normal;
        }
        for (int i = 0; i < normals.Length; i++)
            normals[i] = normals[i].LengthSquared() > 1e-12f ? Vector3.Normalize(normals[i]) : Vector3.UnitY;
        return normals;
    }

    #endregion

    #region Skeleton

    private const int MayaScaling = 2, SoftimageScaling = 3;
    private const int BoneParentOffset = 42, BoneScaleOffset = 56;

    private sealed record SkeletonData(BfresBone[] Bones, Matrix4x4[] World, Matrix4x4[] Palette);

    private enum BoneState : byte { Pending, Visiting, Done }

    private SkeletonData ReadSkeleton(long at)
    {
        if (at == 0) return new([], [], []);

        long boneArray = reader.ReadPointerAt(at + 16);
        int boneCount = reader.ReadUInt16At(at + 56);
        int scalingMode = (int)((reader.ReadUInt32At(at + 4) >> 8) & 3);
        if (scalingMode == SoftimageScaling) warnings.Add("Softimage skeleton scaling uses standard inheritance; its compensation rules are not implemented.");

        var world = new Matrix4x4[boneCount];
        var state = new BoneState[boneCount];
        Matrix4x4 World(int index)
        {
            if (state[index] == BoneState.Done) return world[index];
            if (state[index] == BoneState.Visiting) throw new InvalidDataException("Cycle in bone hierarchy.");
            state[index] = BoneState.Visiting;

            int parent = reader.ReadInt16At(boneArray + index * BoneSize + BoneParentOffset);
            var local = ReadBoneTransform(boneArray, index, parent, scalingMode);
            world[index] = parent < 0 ? local : local * World(parent);
            state[index] = BoneState.Done;
            return world[index];
        }

        var bones = new BfresBone[boneCount];
        for (int i = 0; i < boneCount; i++)
        {
            long bone = boneArray + i * BoneSize;
            string name = reader.ReadNameAt(bone);
            bones[i] = new(i, name.Length == 0 ? $"Bone {i}" : name, reader.ReadInt16At(bone + BoneParentOffset), World(i));
        }
        return new(bones, world, ReadSkinPalette(at, world));
    }

    private Matrix4x4 ReadBoneTransform(long boneArray, int index, int parent, int scalingMode)
    {
        const uint EulerRotationFlag = 0x1000;

        long bone = boneArray + index * BoneSize;
        float[] r = reader.ReadSingleArrayAt(bone + 68, 4);
        var rotation = (reader.ReadUInt32At(bone + 52) & EulerRotationFlag) != 0
            ? Matrix4x4.CreateRotationX(r[0]) * Matrix4x4.CreateRotationY(r[1]) * Matrix4x4.CreateRotationZ(r[2])
            : Matrix4x4.CreateFromQuaternion(new(r[0], r[1], r[2], r[3]));

        var transform = Matrix4x4.CreateScale(ReadVector3(bone + BoneScaleOffset)) * rotation;
        // Maya row-vector order: S * R * inverseParentScale * T.
        if (parent >= 0 && scalingMode == MayaScaling)
            transform *= Matrix4x4.CreateScale(Vector3.One / ReadVector3(boneArray + parent * BoneSize + BoneScaleOffset));
        return transform * Matrix4x4.CreateTranslation(ReadVector3(bone + 84));
    }

    // Smooth skinning matrices bind through the inverse bind pose; rigid ones use the bone directly.
    private Matrix4x4[] ReadSkinPalette(long skeleton, Matrix4x4[] world)
    {
        long paletteArray = reader.ReadPointerAt(skeleton + 24);
        long inverseArray = reader.ReadPointerAt(skeleton + 32);
        int smoothCount = reader.ReadUInt16At(skeleton + 58);
        int rigidCount = reader.ReadUInt16At(skeleton + 60);

        var palette = new Matrix4x4[smoothCount + rigidCount];
        for (int i = 0; i < palette.Length; i++)
        {
            var boneWorld = world[reader.ReadUInt16At(paletteArray + i * 2)];
            palette[i] = i < smoothCount ? ReadInverseBind(inverseArray + i * 48) * boneWorld : boneWorld;
        }
        return palette;
    }

    // File matrices are row-major 3x4 column-vector transforms; transpose for Numerics.
    private Matrix4x4 ReadInverseBind(long at)
    {
        float[] m = reader.ReadSingleArrayAt(at, 12);
        return new Matrix4x4(
            m[0], m[4], m[8], 0,
            m[1], m[5], m[9], 0,
            m[2], m[6], m[10], 0,
            m[3], m[7], m[11], 1);
    }

    #endregion

    #region Materials

    private BfresMaterial ReadMaterial(long at)
    {
        var samplers = ReadSamplers(at);
        var renderInfos = ReadRenderInfos(at);
        var parameters = ReadShaderParameters(at);

        // Shader info: archive and model names, then attribute, sampler and option assignments.
        long shader = reader.ReadPointerAt(at + 32);
        Dictionary<string, string> Assignments(int offset) => shader == 0 ? [] : ReadStringDictionary(shader + offset);

        return new BfresMaterial
        {
            Name = reader.ReadNameAt(at + 8),
            ShaderArchive = shader == 0 ? "" : reader.ReadNameAt(shader),
            ShadingModel = shader == 0 ? "" : reader.ReadNameAt(shader + 8),
            AttributeAssignments = Assignments(16),
            SamplerAssignments = Assignments(32),
            ShaderOptions = Assignments(48),
            OriginalParameters = parameters,
            RenderInfos = renderInfos,
            Samplers = samplers
        };
    }

    private BfresSampler[] ReadSamplers(long material)
    {
        long textureNames = reader.ReadPointerAt(material + 48);
        long samplerArray = reader.ReadPointerAt(material + 64);
        string[] names = reader.ReadNamesAt(reader.ReadPointerAt(material + 72));

        var samplers = new BfresSampler[reader.ReadByteAt(material + 157)];
        for (int i = 0; i < samplers.Length; i++)
        {
            long at = samplerArray + i * SamplerSize;
            string textureName = reader.ReadNameAt(textureNames + i * PointerSize);
            samplers[i] = new BfresSampler(
                Name: names[i],
                Texture: textures.GetValueOrDefault(textureName),
                WrapU: reader.ReadByteAt(at),
                WrapV: reader.ReadByteAt(at + 1),
                FilterFlags: reader.ReadUInt16At(at + 6),
                BorderColor: reader.ReadByteAt(at + 4),
                MaxAnisotropy: reader.ReadByteAt(at + 5),
                MinLod: reader.ReadSingleAt(at + 8),
                MaxLod: reader.ReadSingleAt(at + 12),
                LodBias: reader.ReadSingleAt(at + 16))
            {
                TextureName = textureName
            };
        }
        return samplers;
    }

    private Dictionary<string, BfresRenderInfo> ReadRenderInfos(long material)
    {
        long array = reader.ReadPointerAt(material + 16);
        int count = reader.ReadUInt16At(material + 154);

        var infos = new Dictionary<string, BfresRenderInfo>();
        for (int i = 0; i < count; i++)
        {
            long at = array + i * RenderInfoSize;
            infos.Add(reader.ReadNameAt(at), ReadRenderInfo(at));
        }
        return infos;
    }

    private BfresRenderInfo ReadRenderInfo(long at)
    {
        long values = reader.ReadPointerAt(at + 8);
        int count = values == 0 ? 0 : reader.ReadUInt16At(at + 16);
        if (count == 0) return new(Strings: [], Floats: [], Integers: []);

        byte type = reader.ReadByteAt(at + 18);
        return type switch
        {
            0 => new(Strings: [], Floats: [], Integers: reader.ReadInt32ArrayAt(values, count)),
            1 => new(Strings: [], Floats: reader.ReadSingleArrayAt(values, count), Integers: []),
            2 => new(Strings: [.. Enumerable.Range(0, count).Select(i => reader.ReadNameAt(values + i * PointerSize))], Floats: [], Integers: []),
            _ => throw new NotSupportedException($"Render info type {type} is unsupported.")
        };
    }

    private Dictionary<string, BfresShaderParameter> ReadShaderParameters(long material)
    {
        long array = reader.ReadPointerAt(material + 80);
        long source = reader.ReadPointerAt(material + 96);
        int count = reader.ReadUInt16At(material + 158);

        var parameters = new Dictionary<string, BfresShaderParameter>();
        for (int i = 0; i < count; i++)
        {
            long at = array + i * ShaderParameterSize;
            byte type = reader.ReadByteAt(at + 16);
            int length = reader.ReadByteAt(at + 17);
            int offset = reader.ReadUInt16At(at + 18);
            parameters.Add(reader.ReadNameAt(at + 8), new(type, reader.ReadByteArrayAt(source + offset, length)));
        }
        return parameters;
    }

    private Dictionary<string, string> ReadStringDictionary(long at)
    {
        long values = reader.ReadPointerAt(at);
        string[] keys = reader.ReadNamesAt(reader.ReadPointerAt(at + 8));
        return keys.Select((key, i) => (key, reader.ReadNameAt(values + i * PointerSize))).ToDictionary();
    }

    #endregion

    #region Textures

    // The two tile modes of a BNTX surface.
    private const int BlockLinear = 0, PitchLinear = 1;

    // The first BCn format; everything from here on is stored in blocks of several texels.
    private const int FirstBlockFormat = 0x1a;

    // The format's low byte.
    private const int TypeUnorm = 1, TypeSnorm = 2, TypeSrgb = 6;

    // Checks the header and reads the textures of every BNTX among the external files.
    private void ReadEmbeddedTextures(byte[] data)
    {
        cancellation.ThrowIfCancellationRequested();
        CheckHeader();

        long external = reader.ReadPointerAt(184);
        int count = reader.ReadUInt16At(236);
        for (int i = 0; i < count; i++)
        {
            long file = external + i * ExternalFileSize;
            int offset = checked((int)reader.ReadPointerAt(file));
            int length = checked((int)reader.ReadInt64At(file + 8));
            if (!data.AsSpan(offset, length).StartsWith("BNTX"u8)) continue;

            using var bntx = Open(data, offset, length);
            ReadTextures(bntx);
        }
    }

    private void CheckHeader()
    {
        if (reader.ReadStringAt(4, 4) != "    " || reader.ReadUInt16At(12) != LittleEndianMark)
            throw new NotSupportedException("Only little endian Nintendo Switch BFRES is supported.");

        uint version = reader.ReadUInt32At(8);
        if (version >> 16 != 9)
            throw new NotSupportedException($"BFRES version 0x{version:X8} is not supported. This reader targets ACNH's Switch v9 layout.");
    }

    private void ReadTextures(BinaryReader bntx)
    {
        if (bntx.ReadUInt16At(12) != LittleEndianMark || bntx.ReadStringAt(32, 4) != "NX  ")
            throw new InvalidDataException("Invalid Switch BNTX header.");

        long array = bntx.ReadPointerAt(40);
        int count = bntx.ReadInt32At(36);
        for (int i = 0; i < count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            long at = bntx.ReadPointerAt(array + i * PointerSize);
            string name = bntx.ReadNameAt(at + 96);
            try
            {
                textures.TryAdd(name, ReadTexture(bntx, at, name));
            }
            catch (NotSupportedException ex)
            {
                warnings.Add($"Texture {name}: {ex.Message}");
            }
        }
    }

    private static BntxTexture ReadTexture(BinaryReader bntx, long at, string name)
    {
        uint format = bntx.ReadUInt32At(at + 28);
        int channel = (int)(format >> 8);
        int type = (int)(format & 255);
        var astc = AstcFootprint(channel);
        bool isAstc = astc.Width > 0;

        int blockBytes = BlockBytes(channel, isAstc, format);
        CheckTextureSupported(bntx, at, format, channel, type, isAstc);

        int tileMode = bntx.ReadUInt16At(at + 18);
        int width = bntx.ReadInt32At(at + 36);
        int height = bntx.ReadInt32At(at + 40);
        int blockSize = channel >= FirstBlockFormat ? 4 : 1;
        (int blockWidth, int blockHeight) = isAstc ? astc : (blockSize, blockSize);
        int gobsPerBlockLog2 = (int)(bntx.ReadUInt32At(at + 52) & 7);
        // Pitch-linear surfaces have no block-linear mip layout to follow; preview the base level only.
        int mipCount = tileMode == PitchLinear ? 1 : bntx.ReadUInt16At(at + 22);

        long mipArray = bntx.ReadPointerAt(at + 112);
        long firstMip = bntx.ReadPointerAt(mipArray);
        byte[] source = bntx.ReadByteArrayAt(firstMip, bntx.ReadInt32At(at + 80));

        var mips = new ReadOnlyMemory<byte>[mipCount];
        for (int level = 0; level < mipCount; level++)
        {
            int offset = (int)(bntx.ReadPointerAt(mipArray + level * PointerSize) - firstMip);
            int levelWidth = Math.Max(1, width >> level);
            int levelHeight = Math.Max(1, height >> level);
            int columns = (levelWidth + blockWidth - 1) / blockWidth;
            int rows = (levelHeight + blockHeight - 1) / blockHeight;

            var blocks = Deswizzle(source.AsSpan(offset), columns, rows, blockBytes, tileMode, gobsPerBlockLog2);
            mips[level] = isAstc ? AstcDecoder.Decode(blocks, levelWidth, levelHeight, blockWidth, blockHeight, type == TypeSrgb) : blocks;
        }

        // Decoded ASTC is plain RGBA8, which is what the texture then claims to be.
        if (isAstc) format = 0x0b00 | (uint)type;
        return new BntxTexture(name, width, height, format, mips, bntx.ReadByteArrayAt(at + 88, 4));
    }

    private static int BlockBytes(int channel, bool isAstc, uint format) => channel switch
    {
        0x1a or 0x1d => 8, // BC1, BC4
        >= 0x1b and <= 0x20 => 16, // BC2, BC3, BC5, BC6, BC7
        0x0b => 4, // RGBA8
        0x02 => 1, // R8
        0x09 => 2, // RG8
        _ when isAstc => 16,
        _ => throw new NotSupportedException($"Texture format 0x{format:X} is unsupported.")
    };

    private static void CheckTextureSupported(BinaryReader bntx, long at, uint format, int channel, int type, bool isAstc)
    {
        bool knownType = type is TypeUnorm or TypeSnorm or TypeSrgb;
        bool snormAllowed = channel >= FirstBlockFormat && !isAstc;
        if (!knownType || (type == TypeSnorm && !snormAllowed))
            throw new NotSupportedException($"Texture type 0x{format:X} is unsupported.");

        if (bntx.ReadUInt32At(at + 44) != 1 || bntx.ReadUInt32At(at + 48) != 1 || bntx.ReadByteAt(at + 92) != 1)
            throw new NotSupportedException("Only single-layer 2D textures are previewed.");

        if (bntx.ReadUInt16At(at + 18) is not (BlockLinear or PitchLinear))
            throw new NotSupportedException("Unsupported Tegra tile mode.");
    }

    // The texel footprint of an ASTC format's blocks, or zero for other formats.
    private static (int Width, int Height) AstcFootprint(int channel) => channel switch
    {
        0x2d => (4, 4),
        0x2e => (5, 4),
        0x2f => (5, 5),
        0x30 => (6, 5),
        0x31 => (6, 6),
        0x32 => (8, 5),
        0x33 => (8, 6),
        0x34 => (8, 8),
        0x35 => (10, 5),
        0x36 => (10, 6),
        0x37 => (10, 8),
        0x38 => (10, 10),
        0x39 => (12, 10),
        0x3a => (12, 12),
        _ => (0, 0)
    };

    // columns and rows count format blocks (4x4 texels for BCn, single texels otherwise).
    private static byte[] Deswizzle(ReadOnlySpan<byte> source, int columns, int rows, int blockBytes, int tileMode, int gobsPerBlockLog2)
    {
        // Each mip shrinks its GOB block height until it no longer exceeds the level's height.
        int gobsPerBlock = 1 << gobsPerBlockLog2;
        while (gobsPerBlock > 1 && rows <= (gobsPerBlock / 2) * 8) gobsPerBlock /= 2;

        int pitchAlignment = tileMode == PitchLinear ? 32 : 64;
        int pitch = (columns * blockBytes + pitchAlignment - 1) / pitchAlignment * pitchAlignment;

        var linear = new byte[columns * rows * blockBytes];
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                int xBytes = x * blockBytes;
                int address = tileMode == PitchLinear ? y * pitch + xBytes : BlockLinearAddress(xBytes, y, pitch, gobsPerBlock);
                source.Slice(address, blockBytes).CopyTo(linear.AsSpan((y * columns + x) * blockBytes, blockBytes));
            }
        }
        return linear;
    }

    // Tegra's block-linear layout: 512-byte GOBs of 64x8 bytes, stacked into blocks of gobsPerBlock.
    private static int BlockLinearAddress(int xBytes, int y, int pitch, int gobsPerBlock)
    {
        const int GobBytes = 512, GobWidth = 64, GobHeight = 8;
        int blockBytes = GobBytes * gobsPerBlock;
        int blockHeight = GobHeight * gobsPerBlock;

        int block = (y / blockHeight) * blockBytes * (pitch / GobWidth) + (xBytes / GobWidth) * blockBytes;
        int gob = ((y % blockHeight) / GobHeight) * GobBytes;
        int insideGob = ((xBytes % 64) / 32) * 256 + ((y % 8) / 2) * 64 + ((xBytes % 32) / 16) * 32 + (y % 2) * 16 + xBytes % 16;
        return block + gob + insideGob;
    }

    #endregion
}

internal static class NintendoWareReaderExtensions
{
    extension(BinaryReader reader)
    {
        // NintendoWare offsets are 64-bit and relative to the start of the file.
        public long ReadPointerAt(long position) => reader.ReadInt64At(position);

        // Names are pointers to strings with a two-byte length; a null pointer is an empty name.
        public string ReadNameAt(long position) => reader.ReadInt64At(position) is var pointer and not 0 ? reader.ReadPascalStringAt(pointer) : "";

        // Keys of a NintendoWare dictionary, in entry order; a null dictionary has none.
        public string[] ReadNamesAt(long dictionary) => dictionary == 0 ? []
            : [.. Enumerable.Range(0, reader.ReadInt32At(dictionary + 4)).Select(i => reader.ReadNameAt(dictionary + 32 + i * 16))];
    }
}
