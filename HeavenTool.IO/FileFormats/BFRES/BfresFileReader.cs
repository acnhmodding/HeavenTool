using AeonSake.NintendoTools.Compression.Zstd;
using AeonSake.NintendoTools.FileFormats.Sarc;
using HeavenTool.IO.Compression;
using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;

namespace HeavenTool.IO.FileFormats.BFRES;

// Reads the models, materials, skeletons and embedded textures of ACNH's Switch BFRES v9 files for previewing.
public sealed class BfresFileReader
{
    private readonly BinaryReader reader;
    private readonly CancellationToken cancellation;
    private readonly List<string> warnings = [];
    private readonly Dictionary<string, BntxTexture> textures = [];
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
        return files.Count > 0 ? [.. files.Select(file => new BfresAsset(file.Name, Read(file.Data, cancellation)))]
            : throw new InvalidDataException("Expected BFRES or a SARC model archive.");
    }

    // Decompresses the input and returns it when it starts with magic, or else every SARC entry that does.
    internal static List<(string Name, byte[] Data)> Unpack(Stream input, ReadOnlySpan<byte> magic, string name)
    {
        using var content = new MemoryStream();
        Span<byte> header = stackalloc byte[4];
        long start = input.Position;
        input.ReadExactly(header);
        input.Position = start;
        if (BinaryPrimitives.ReadUInt32LittleEndian(header) == 0xfd2fb528)
            new ZstdDecompressor().Decompress(input, content);
        else if (header.SequenceEqual("Yaz0"u8))
        {
            using var decompressed = Yaz0CompressionAlgorithm.Decompress(input) ?? throw new InvalidDataException("Yaz0 decompression failed.");
            decompressed.Position = 0;
            decompressed.CopyTo(content);
        }
        else input.CopyTo(content);

        byte[] data = content.ToArray();
        if (data.AsSpan().StartsWith(magic)) return [(name, data)];
        if (!data.AsSpan().StartsWith("SARC"u8)) return [];
        content.Position = 0;
        var files = new List<(string, byte[])>();
        foreach (var file in new SarcFileReader().Read(content).Files)
            if (file.Data.AsSpan().StartsWith(magic)) files.Add((file.Name, file.Data));
        return files;
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
        cancellation.ThrowIfCancellationRequested();
        if (reader.ReadStringAt(4, 4) != "    " || reader.ReadUInt16At(12) != 0xfeff)
            throw new NotSupportedException("Only little endian Nintendo Switch BFRES is supported.");
        uint version = reader.ReadUInt32At(8);
        if (version >> 16 != 9)
            throw new NotSupportedException($"BFRES version 0x{version:X8} is not supported. This reader targets ACNH's Switch v9 layout.");
        long bufferInfo = reader.ReadPointerAt(176);
        gpuBase = bufferInfo == 0 ? 0 : reader.ReadPointerAt(bufferInfo + 8);

        long external = reader.ReadPointerAt(184);
        for (int i = 0, count = reader.ReadUInt16At(236); i < count; i++)
        {
            int offset = checked((int)reader.ReadPointerAt(external + i * 16)), length = checked((int)reader.ReadInt64At(external + i * 16 + 8));
            if (!data.AsSpan(offset, length).StartsWith("BNTX"u8)) continue;
            using var bntx = Open(data, offset, length);
            ReadTextures(bntx);
        }

        long modelArray = reader.ReadPointerAt(40);
        var models = new List<BfresModel>();
        for (int i = 0, count = reader.ReadUInt16At(220); i < count; i++) models.Add(ReadModel(modelArray + i * 120));
        if (!models.Any(x => x.Meshes.Count > 0))
            warnings.Add("This BFRES contains no previewable model geometry (it may be a texture or animation resource).");
        return new BfresDocument(models, warnings);
    }

    #region Models

    private BfresModel ReadModel(long at)
    {
        cancellation.ThrowIfCancellationRequested();
        string name = reader.ReadNameAt(at + 8);
        var (bones, world, palette) = ReadSkeleton(reader.ReadPointerAt(at + 24));
        long materialArray = reader.ReadPointerAt(at + 56), shapeArray = reader.ReadPointerAt(at + 40);
        var materials = new BfresMaterial[reader.ReadUInt16At(at + 108)];
        for (int i = 0; i < materials.Length; i++) materials[i] = ReadMaterial(materialArray + i * 168);

        var meshes = new List<BfresMesh>();
        // Several shapes can share one FVTX, so decoded vertices are cached by offset.
        var vertexCache = new Dictionary<long, VertexData>();
        for (int i = 0, count = reader.ReadUInt16At(at + 106); i < count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            long shape = shapeArray + i * 96;
            string shapeName = reader.ReadNameAt(shape + 8);
            try
            {
                if ((reader.ReadUInt32At(shape + 4) & 16) != 0) throw new NotSupportedException("Meshlet geometry is unsupported.");
                long vertex = reader.ReadPointerAt(shape + 16);
                if (!vertexCache.TryGetValue(vertex, out var v)) vertexCache.Add(vertex, v = ReadVertices(vertex));
                if (reader.ReadByteAt(shape + 91) == 0) continue;

                int skinCount = reader.ReadByteAt(shape + 90);
                if (skinCount > 4 || skinCount > 0 && (v.BoneIndices.Length == 0 || skinCount > 1 && v.Weights.Length == 0))
                    throw new NotSupportedException("Unsupported skin weights or missing skin attributes.");
                var shapeTransform = skinCount == 0 && world.Length > 0 ? world[reader.ReadUInt16At(shape + 84)] : Matrix4x4.Identity;
                var indices = ReadIndices(reader.ReadPointerAt(shape + 24));
                var positions = new Vector3[v.Positions.Length];
                var normals = new Vector3[positions.Length];
                for (int j = 0; j < positions.Length; j++)
                {
                    var transform = shapeTransform;
                    if (skinCount > 0)
                    {
                        transform = default;
                        for (int k = 0; k < skinCount; k++)
                            transform += palette[(int)v.BoneIndices[j][k]] * (skinCount == 1 ? 1 : v.Weights[j][k]);
                    }
                    positions[j] = Vector3.Transform(v.Positions[j], transform);
                    if (v.Normals.Length == 0) continue;
                    var normal = Matrix4x4.Invert(transform, out var inverse) ? Vector3.TransformNormal(v.Normals[j], Matrix4x4.Transpose(inverse)) : v.Normals[j];
                    normals[j] = normal.LengthSquared() > 1e-12f ? Vector3.Normalize(normal) : normal;
                }
                if (v.Normals.Length == 0) normals = GenerateNormals(positions, indices);

                meshes.Add(new BfresMesh
                {
                    Name = shapeName, Material = materials[reader.ReadUInt16At(shape + 82)], VertexSkinCount = skinCount,
                    Positions = positions, Normals = normals, Indices = indices, TexCoords = v.TexCoords,
                    NativeAttributes = v.Attributes, BonePalette = palette, ShapeTransform = shapeTransform
                });
            }
            catch (NotSupportedException ex)
            {
                warnings.Add($"{name}/{shapeName}: {ex.Message}");
            }
        }
        return new BfresModel(name, meshes, materials, bones);
    }

    private sealed record VertexData(Vector3[] Positions, Vector3[] Normals, Vector4[] BoneIndices, Vector4[] Weights, Vector2[] TexCoords,
        Dictionary<string, ReadOnlyMemory<Vector4>> Attributes);

    private VertexData ReadVertices(long at)
    {
        long attributes = reader.ReadPointerAt(at + 8), sizes = reader.ReadPointerAt(at + 48), strides = reader.ReadPointerAt(at + 56);
        int count = reader.ReadInt32At(at + 80);
        // Vertex buffers are packed one after another, each aligned to 8 bytes.
        var buffers = new (long Offset, int Stride)[reader.ReadByteAt(at + 77)];
        long offset = gpuBase + reader.ReadUInt32At(at + 72);
        for (int i = 0; i < buffers.Length; i++)
        {
            offset = (offset + 7) & ~7L;
            buffers[i] = (offset, reader.ReadInt32At(strides + i * 16));
            offset += reader.ReadUInt32At(sizes + i * 16);
        }

        var decoded = new Dictionary<string, Vector4[]>();
        for (int i = 0, attributeCount = reader.ReadByteAt(at + 76); i < attributeCount; i++)
        {
            long attribute = attributes + i * 16;
            string name = reader.ReadNameAt(attribute);
            if (name.Length != 3 || name[0] != '_' || !"pniwutbc".Contains(name[1])) continue;
            // The attribute format is the only big endian field.
            ushort format = BinaryPrimitives.ReverseEndianness(reader.ReadUInt16At(attribute + 8));
            var buffer = buffers[reader.ReadByteAt(attribute + 14)];
            long first = buffer.Offset + reader.ReadUInt16At(attribute + 12);
            var values = new Vector4[count];
            for (int j = 0; j < count; j++)
            {
                if ((j & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                values[j] = DecodeVertex(first + (long)j * buffer.Stride, format);
            }
            decoded.Add(name, values);
        }
        if (!decoded.TryGetValue("_p0", out var position)) throw new NotSupportedException("No position attribute (_p0).");
        Vector4[] Values(string name) => decoded.TryGetValue(name, out var values) ? values : [];
        return new VertexData(Array.ConvertAll(position, x => new Vector3(x.X, x.Y, x.Z)),
            Array.ConvertAll(Values("_n0"), x => new Vector3(x.X, x.Y, x.Z)), Values("_i0"), Values("_w0"),
            Array.ConvertAll(Values("_u0"), x => new Vector2(x.X, x.Y)),
            decoded.ToDictionary(x => x.Key, x => (ReadOnlyMemory<Vector4>)x.Value));
    }

    private Vector4 DecodeVertex(long at, ushort format)
    {
        if (format == 0x020e)
        {
            // Packed signed 10-10-10-2.
            uint packed = reader.ReadUInt32At(at);
            static float Signed(uint v) => Math.Max(((int)(v << 22) >> 22) / 511f, -1);
            return new(Signed(packed & 1023), Signed((packed >> 10) & 1023), Signed((packed >> 20) & 1023), (packed >> 30) / 3f);
        }
        var (bits, count) = (format & 255) switch
        {
            0x02 => (8, 1), 0x09 => (8, 2), 0x0b => (8, 4), 0x0a => (16, 1), 0x12 => (16, 2), 0x15 => (16, 4),
            0x16 => (32, 1), 0x17 => (32, 2), 0x18 => (32, 3), 0x19 => (32, 4),
            _ => throw new NotSupportedException($"Vertex format 0x{format:X4} is unsupported.")
        };
        int kind = format >> 8;
        // Nintendo's scalar 16-bit UNorm and SNorm values swap their kind codes.
        if ((format & 255) == 0x0a && kind is 2 or 3) kind ^= 1;
        Vector4 value = default;
        for (int i = 0; i < count; i++)
        {
            long p = at + i * bits / 8;
            uint raw = bits switch { 8 => reader.ReadByteAt(p), 16 => reader.ReadUInt16At(p), _ => reader.ReadUInt32At(p) };
            int signed = bits switch { 8 => (sbyte)raw, 16 => (short)raw, _ => (int)raw };
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

    private uint[] ReadIndices(long at)
    {
        int topology = reader.ReadInt32At(at + 36), count = reader.ReadInt32At(at + 44);
        long offset = gpuBase + reader.ReadUInt32At(at + 32);
        uint first = reader.ReadUInt32At(at + 48);
        var (raw, restart) = reader.ReadInt32At(at + 40) switch
        {
            0 => (Array.ConvertAll(reader.ReadByteArrayAt(offset, count), x => (uint)x), byte.MaxValue),
            1 => (Array.ConvertAll(reader.ReadUInt16ArrayAt(offset, count), x => (uint)x), ushort.MaxValue),
            2 => (reader.ReadUInt32ArrayAt(offset, count), uint.MaxValue),
            _ => throw new NotSupportedException("Unsupported index format.")
        };
        for (int i = 0; i < raw.Length; i++) raw[i] = topology == 4 && raw[i] == restart ? uint.MaxValue : raw[i] + first;
        if (topology == 3) return raw;
        if (topology != 4) throw new NotSupportedException($"Primitive topology {topology} is unsupported.");

        // Convert the restartable triangle strip into a list, dropping degenerate triangles.
        var triangles = new List<uint>();
        for (int i = 0, start = 0; i < raw.Length; i++)
        {
            if (raw[i] == uint.MaxValue) { start = i + 1; continue; }
            if (i - start < 2) continue;
            uint a = raw[i - 2], b = raw[i - 1], c = raw[i];
            if (a == b || b == c || a == c) continue;
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
            var n = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
            normals[a] += n; normals[b] += n; normals[c] += n;
        }
        for (int i = 0; i < normals.Length; i++)
            normals[i] = normals[i].LengthSquared() > 1e-12f ? Vector3.Normalize(normals[i]) : Vector3.UnitY;
        return normals;
    }

    #endregion

    #region Skeleton

    private (BfresBone[] Bones, Matrix4x4[] World, Matrix4x4[] Palette) ReadSkeleton(long at)
    {
        if (at == 0) return ([], [], []);
        long boneArray = reader.ReadPointerAt(at + 16), paletteArray = reader.ReadPointerAt(at + 24), inverseArray = reader.ReadPointerAt(at + 32);
        int count = reader.ReadUInt16At(at + 56), smooth = reader.ReadUInt16At(at + 58), rigid = reader.ReadUInt16At(at + 60);
        int scalingMode = (int)((reader.ReadUInt32At(at + 4) >> 8) & 3);
        if (scalingMode == 3) warnings.Add("Softimage skeleton scaling uses standard inheritance; its compensation rules are not implemented.");

        var world = new Matrix4x4[count];
        var state = new byte[count];
        Vector3 Vector(long p) => new(reader.ReadSingleAt(p), reader.ReadSingleAt(p + 4), reader.ReadSingleAt(p + 8));
        Matrix4x4 World(int i)
        {
            if (state[i] == 2) return world[i];
            if (state[i] == 1) throw new InvalidDataException("Cycle in bone hierarchy.");
            state[i] = 1;
            long bone = boneArray + i * 96;
            int parent = reader.ReadInt16At(bone + 42);
            var r = reader.ReadSingleArrayAt(bone + 68, 4);
            var rotation = (reader.ReadUInt32At(bone + 52) & 0x1000) != 0
                ? Matrix4x4.CreateRotationX(r[0]) * Matrix4x4.CreateRotationY(r[1]) * Matrix4x4.CreateRotationZ(r[2])
                : Matrix4x4.CreateFromQuaternion(new(r[0], r[1], r[2], r[3]));
            var local = Matrix4x4.CreateScale(Vector(bone + 56)) * rotation;
            // Maya row-vector order: S * R * inverseParentScale * T.
            if (parent >= 0 && scalingMode == 2) local *= Matrix4x4.CreateScale(Vector3.One / Vector(boneArray + parent * 96 + 56));
            local *= Matrix4x4.CreateTranslation(Vector(bone + 84));
            world[i] = parent < 0 ? local : local * World(parent);
            state[i] = 2;
            return world[i];
        }

        var bones = new BfresBone[count];
        for (int i = 0; i < count; i++)
        {
            string name = reader.ReadNameAt(boneArray + i * 96);
            bones[i] = new(i, name.Length == 0 ? $"Bone {i}" : name, reader.ReadInt16At(boneArray + i * 96 + 42), World(i));
        }

        // Smooth skinning matrices bind through the inverse bind pose; rigid ones use the bone directly.
        var palette = new Matrix4x4[smooth + rigid];
        for (int i = 0; i < palette.Length; i++)
        {
            var boneWorld = world[reader.ReadUInt16At(paletteArray + i * 2)];
            if (i >= smooth) { palette[i] = boneWorld; continue; }
            // File matrices are row-major 3x4 column-vector transforms; transpose for Numerics.
            var m = reader.ReadSingleArrayAt(inverseArray + i * 48, 12);
            palette[i] = new Matrix4x4(m[0], m[4], m[8], 0, m[1], m[5], m[9], 0, m[2], m[6], m[10], 0, m[3], m[7], m[11], 1) * boneWorld;
        }
        return (bones, world, palette);
    }

    #endregion

    #region Materials

    private BfresMaterial ReadMaterial(long at)
    {
        long shader = reader.ReadPointerAt(at + 32), textureNames = reader.ReadPointerAt(at + 48), samplerArray = reader.ReadPointerAt(at + 64);
        var samplerNames = reader.ReadNamesAt(reader.ReadPointerAt(at + 72));
        var samplers = new BfresSampler[reader.ReadByteAt(at + 157)];
        for (int i = 0; i < samplers.Length; i++)
        {
            long s = samplerArray + i * 32;
            samplers[i] = new(samplerNames[i], textures.GetValueOrDefault(reader.ReadNameAt(textureNames + i * 8)),
                reader.ReadByteAt(s), reader.ReadByteAt(s + 1), reader.ReadUInt16At(s + 6), reader.ReadByteAt(s + 4), reader.ReadByteAt(s + 5),
                reader.ReadSingleAt(s + 8), reader.ReadSingleAt(s + 12), reader.ReadSingleAt(s + 16));
        }

        var infos = new Dictionary<string, BfresRenderInfo>();
        long infoArray = reader.ReadPointerAt(at + 16);
        for (int i = 0, count = reader.ReadUInt16At(at + 154); i < count; i++)
        {
            long p = infoArray + i * 24, values = reader.ReadPointerAt(p + 8);
            int valueCount = values == 0 ? 0 : reader.ReadUInt16At(p + 16);
            byte type = reader.ReadByteAt(p + 18);
            infos.Add(reader.ReadNameAt(p), valueCount == 0 ? new([], [], []) : type switch
            {
                0 => new([], [], reader.ReadInt32ArrayAt(values, valueCount)),
                1 => new([], reader.ReadSingleArrayAt(values, valueCount), []),
                2 => new([.. Enumerable.Range(0, valueCount).Select(j => reader.ReadNameAt(values + j * 8))], [], []),
                _ => throw new NotSupportedException($"Render info type {type} is unsupported.")
            });
        }

        var parameters = new Dictionary<string, BfresShaderParameter>();
        long parameterArray = reader.ReadPointerAt(at + 80), source = reader.ReadPointerAt(at + 96);
        for (int i = 0, count = reader.ReadUInt16At(at + 158); i < count; i++)
        {
            long p = parameterArray + i * 32;
            parameters.Add(reader.ReadNameAt(p + 8),
                new(reader.ReadByteAt(p + 16), reader.ReadByteArrayAt(source + reader.ReadUInt16At(p + 18), reader.ReadByteAt(p + 17))));
        }

        // Shader info: archive and model names, then attribute, sampler and option assignments.
        Dictionary<string, string> Assignments(int offset) => shader == 0 ? [] : StringDictionary(shader + offset);
        return new BfresMaterial
        {
            Name = reader.ReadNameAt(at + 8),
            ShaderArchive = shader == 0 ? "" : reader.ReadNameAt(shader),
            ShadingModel = shader == 0 ? "" : reader.ReadNameAt(shader + 8),
            AttributeAssignments = Assignments(16), SamplerAssignments = Assignments(32), ShaderOptions = Assignments(48),
            OriginalParameters = parameters, RenderInfos = infos, Samplers = samplers
        };
    }

    private Dictionary<string, string> StringDictionary(long at)
    {
        long values = reader.ReadPointerAt(at);
        return reader.ReadNamesAt(reader.ReadPointerAt(at + 8)).Select((key, i) => (key, reader.ReadNameAt(values + i * 8))).ToDictionary();
    }

    #endregion

    #region Textures

    private void ReadTextures(BinaryReader bntx)
    {
        if (bntx.ReadUInt16At(12) != 0xfeff || bntx.ReadStringAt(32, 4) != "NX  ") throw new InvalidDataException("Invalid Switch BNTX header.");
        long array = bntx.ReadPointerAt(40);
        for (int i = 0, count = bntx.ReadInt32At(36); i < count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            long at = bntx.ReadPointerAt(array + i * 8);
            string name = bntx.ReadNameAt(at + 96);
            try { textures.TryAdd(name, ReadTexture(bntx, at, name)); }
            catch (NotSupportedException ex) { warnings.Add($"Texture {name}: {ex.Message}"); }
        }
    }

    private static BntxTexture ReadTexture(BinaryReader bntx, long at, string name)
    {
        uint format = bntx.ReadUInt32At(at + 28);
        int channel = (int)(format >> 8), type = (int)(format & 255);
        int blockBytes = channel switch
        {
            0x1a or 0x1d => 8, >= 0x1b and <= 0x20 => 16, 0x0b => 4, 0x02 => 1, 0x09 => 2,
            _ => throw new NotSupportedException($"Texture format 0x{format:X} is unsupported.")
        };
        if (type is not (1 or 2 or 6) || (channel < 0x1a && type == 2))
            throw new NotSupportedException($"Texture type 0x{format:X} is unsupported.");
        if (bntx.ReadUInt32At(at + 44) != 1 || bntx.ReadUInt32At(at + 48) != 1 || bntx.ReadByteAt(at + 92) != 1)
            throw new NotSupportedException("Only single-layer 2D textures are previewed.");
        int tileMode = bntx.ReadUInt16At(at + 18);
        if (tileMode is not (0 or 1)) throw new NotSupportedException("Unsupported Tegra tile mode.");

        int width = bntx.ReadInt32At(at + 36), height = bntx.ReadInt32At(at + 40), blockSize = channel >= 0x1a ? 4 : 1;
        int blockHeightLog2 = (int)(bntx.ReadUInt32At(at + 52) & 7);
        // Pitch-linear surfaces have no block-linear mip layout to follow; preview the base level only.
        int mipCount = tileMode == 1 ? 1 : bntx.ReadUInt16At(at + 22);
        long mipArray = bntx.ReadPointerAt(at + 112), first = bntx.ReadPointerAt(mipArray);
        byte[] source = bntx.ReadByteArrayAt(first, bntx.ReadInt32At(at + 80));
        var mips = new ReadOnlyMemory<byte>[mipCount];
        for (int level = 0; level < mipCount; level++)
        {
            int offset = (int)(bntx.ReadPointerAt(mipArray + level * 8) - first);
            int w = (Math.Max(1, width >> level) + blockSize - 1) / blockSize, h = (Math.Max(1, height >> level) + blockSize - 1) / blockSize;
            mips[level] = Deswizzle(source.AsSpan(offset), w, h, blockBytes, tileMode, blockHeightLog2);
        }
        return new BntxTexture(name, width, height, format, mips, bntx.ReadByteArrayAt(at + 88, 4));
    }

    // w and h are in format blocks (4x4 texels for BCn, single texels otherwise).
    private static byte[] Deswizzle(ReadOnlySpan<byte> source, int w, int h, int blockBytes, int tileMode, int blockHeightLog2)
    {
        var linear = new byte[w * h * blockBytes];
        // Each mip shrinks its GOB block height until it no longer exceeds the level's height.
        int blockHeight = 1 << blockHeightLog2;
        while (blockHeight > 1 && h <= (blockHeight / 2) * 8) blockHeight /= 2;
        int pitch = ((w * blockBytes + (tileMode == 1 ? 31 : 63)) / (tileMode == 1 ? 32 : 64)) * (tileMode == 1 ? 32 : 64);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int xb = x * blockBytes;
                int address = tileMode == 1 ? y * pitch + xb
                    : (y / (8 * blockHeight)) * 512 * blockHeight * (pitch / 64)
                        + (xb / 64) * 512 * blockHeight + ((y % (8 * blockHeight)) / 8) * 512
                        + ((xb % 64) / 32) * 256 + ((y % 8) / 2) * 64 + ((xb % 32) / 16) * 32
                        + (y % 2) * 16 + xb % 16;
                source.Slice(address, blockBytes).CopyTo(linear.AsSpan((y * w + x) * blockBytes, blockBytes));
            }
        return linear;
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
