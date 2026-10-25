using System.Buffers.Binary;
using System.Text;

namespace HeavenTool.IO.FileFormats.ResourceSizeTable;

/// <summary>
/// Resource ExpHeap requests ACNH 3.0.3 makes for a model archive's "output.phive"
/// (0x710070FD2C, 0x71007104E4), in allocation order. Havok's own tagfile memory comes
/// from the phive system heap and is not charged to the resource.
/// </summary>
internal static class ModelPhysicsAllocationPlanner
{
    // The phive holder directly follows the outer archive's SARC resource object
    // (0x58 bytes, 0x20-aligned), so its free block starts at 24 mod 32.
    private const long StartAddress = 0x58 % 32;

    private static readonly HashSet<string> ClothOperators = new(StringComparer.Ordinal)
    {
        "hclBoneSpaceSkinPOperator", "hclCopyVerticesOperator", "hclGatherAllVerticesOperator", "hclMoveParticlesOperator",
        "hclObjectSpaceSkinPOperator", "hclSimpleMeshBoneDeformOperator", "hclSimulateOperator"
    };

    /// <summary>Bytes the phive allocations add to the resource ExpHeap, including headers and padding.</summary>
    public static long ExpHeapBytes(ReadOnlySpan<byte> phive)
    {
        return checked(ExpHeapPlanner.HighWaterMark(Requests(phive), StartAddress) - StartAddress);
    }

    /// <summary>The phive requests in allocation order; throws for members that are not modelled.</summary>
    internal static List<ExpHeapRequest> Requests(ReadOnlySpan<byte> phive)
    {
        var files = new Dictionary<string, ModelArchive.Member>(StringComparer.Ordinal);
        foreach (var member in ModelArchive.ReadSarc(phive))
            if (member.Name is not ("output.bphhb" or "output_sub.bphhb" or "output.bphcl" or "output.bphfbc") || !files.TryAdd(member.Name, member))
                throw new NotSupportedException("Unsupported physics member " + member.Name);

        var requests = new List<ExpHeapRequest> { new("Physics holder", 0x28, 8), new("Physics archive resource", 0x58, 0x20) };
        foreach (string name in new[] { "output.bphhb", "output_sub.bphhb" })
            if (files.TryGetValue(name, out var helperBones))
                AddHelperBones(requests, new(phive.Slice(helperBones.Offset, helperBones.Length)));
        if (files.TryGetValue("output.bphcl", out var cloth))
            AddCloth(requests, new(phive.Slice(cloth.Offset, cloth.Length)));
        if (files.TryGetValue("output.bphfbc", out var fishBones))
            AddFishBones(requests, new(phive.Slice(fishBones.Offset, fishBones.Length)));
        return requests;
    }

    // 0x7103DECFC0 creates the 0x4D0-byte object; 0x7103DE9C30 allocates its
    // arrays, each with an 8-byte count cookie, and stops at the first empty one
    // of the curve keys, driver bones, driven bones and aim constraints.
    private static void AddHelperBones(List<ExpHeapRequest> requests, ResourceReader file)
    {
        requests.Add(new("Helper bone object", 0x4d0, 0x20));
        var aamp = new Aamp(file);
        var root = aamp.List(aamp.Root);
        if (root.Objects == 0 || root.Lists == 0) throw new InvalidDataException("Helper bone parameters are missing.");
        int header = aamp.Object(root, 0);
        int bones = aamp.Int(header, "bone_num"), curves = aamp.Int(header, "connection_curve_num"),
            outputs = aamp.Int(header, "output_num"), drivers = aamp.Int(header, "driver_bone_num"),
            driven = aamp.Int(header, "driven_bone_num"), pose = aamp.Int(header, "pose_driven_num"),
            position = aamp.Int(header, "position_driven_num"), rotate = aamp.Int(header, "rotate_driven_num"),
            aim = aamp.Int(header, "aim_driven_num");
        void Add(string purpose, int count, int stride) => requests.Add(new(purpose, checked(count * (long)stride + 8), 8));

        if (bones < 1) return;
        if (curves >= 1)
        {
            Add("Connection curves", curves, 0xa0);
            var settings = aamp.List(aamp.ChildList(root, 0));
            if (settings.Lists < 2) throw new InvalidDataException("Helper bone curves are missing.");
            var curveList = aamp.List(aamp.ChildList(settings, 1));
            if (curveList.Objects < curves) throw new InvalidDataException("Helper bone curve count exceeds its parameters.");
            for (int i = 0; i < curves; i++)
            {
                int keys = aamp.Int(aamp.Object(curveList, i), "key_num");
                if (keys == 0) return;
                if (keys > 0) Add("Curve keys", keys, 0x28);
            }
        }
        Add("Bones", bones, 0x58);
        if (outputs > 0) Add("Outputs", outputs, 0xb0);
        if (drivers < 1) return;
        Add("Driver bones", drivers, 0x130);
        if (driven < 1) return;
        Add("Driven bones", driven, 0xd0);
        if (pose > 0) Add("Pose constraints", pose, 0x290);
        if (position > 0) Add("Position constraints", position, 0x30);
        if (rotate > 0) Add("Rotation constraints", rotate, 0x30);
        if (aim < 1) return;
        Add("Aim constraints", aim, 0x30);
    }

    // 0x7103DE4B10 creates the 0x3B8-byte object; 0x7103DE4D40 then allocates a 0x30-byte bone
    // list and one 0x28-byte entry per bone, with an 8-byte count cookie. The loader rejects
    // files whose root list has fewer than three objects.
    private static void AddFishBones(List<ExpHeapRequest> requests, ResourceReader file)
    {
        requests.Add(new("Fish bone object", 0x3b8, 8));
        var aamp = new Aamp(file);
        var root = aamp.List(aamp.Root);
        if (root.Objects < 3) throw new InvalidDataException("Fish bone parameters are missing.");
        requests.Add(new("Fish bone list", 0x30, 8));
        int bones = aamp.Int(aamp.Object(root, 0), "mBoneNum", 2);
        if (bones > 0) requests.Add(new("Fish bones", checked(bones * 0x28L + 8), 8));
    }

    // 0x7103E05A64 creates the 0xF8-byte cloth resource; 0x7103E05C30 loads the
    // Havok tagfile (0x7103E06650) and then sizes its mesh and collidable lists.
    private static void AddCloth(List<ExpHeapRequest> requests, ResourceReader file)
    {
        requests.Add(new("Cloth resource", 0xf8, 0x20));
        if (!file.Magic(0, "Phive\0"u8) || file.U16(8) != 0xfeff)
            throw new NotSupportedException("Cloth must be a little-endian Phive file.");
        AddClothHavok(requests, new Tagfile(file.Slice(file.U32(0xc), file.U32(0x18))));

        var aamp = new Aamp(file.Slice(file.U32(0x10), file.U32(0x1c)));
        var root = aamp.List(aamp.Root);
        int meshes = 0, collidables = 0;
        for (int i = 0; i < root.Lists; i++)
        {
            var list = aamp.List(aamp.ChildList(root, i));
            if (list.Crc == "cloth_mesh_list".ToCRC32()) meshes = list.Objects;
            else if (list.Crc == "collidable_list".ToCRC32()) collidables = list.Objects;
        }
        if (meshes > 0) requests.Add(new("Cloth meshes", meshes * 0x1a0L + 8, 8));
        if (collidables > 0) requests.Add(new("Cloth collidables", collidables * 0x78L + 8, 8));
    }

    // The tagfile stores objects in their native layout with item indices in pointer
    // and array slots; an array's length is its item's count.
    private static void AddClothHavok(List<ExpHeapRequest> requests, Tagfile tag)
    {
        int container = tag.Single("hclClothContainer");
        requests.Add(new("Cloth instance holder", 0x18, 8));
        var clothDatas = tag.Array(tag.U32(container + 0x28));
        if (clothDatas.Count < 1) return;
        requests.Add(new("Cloth instances", clothDatas.Count * 32L + 8, 8));
        for (int i = 0; i < clothDatas.Count; i++)
        {
            int clothData = tag.Object(tag.U32(clothDatas.Offset + i * 8), "hclClothData");
            var simClothDatas = tag.Array(tag.U32(clothData + 0x20));
            if (simClothDatas.Count < 1) return;
            requests.Add(new("Simulation instances", simClothDatas.Count * 32L + 8, 8));
            var operators = tag.Array(tag.U32(clothData + 0x50));
            int simulation = 0;
            for (int j = 0; j < operators.Count; j++)
            {
                uint item = tag.U32(operators.Offset + j * 8);
                if (item == 0) continue;
                string type = tag.TypeName(item);
                if (!ClothOperators.Contains(type)) throw new NotSupportedException("Unverified cloth operator " + type);
                if (type != "hclSimulateOperator") continue;
                // Operator type 1: one 2-byte entry per constraint set in execution order.
                var configs = tag.Array(tag.U32(tag.Object(item, type) + 0x50));
                if (configs.Count < 1) continue;
                long entries = tag.Array(tag.U32(configs.Offset + 8)).Count;
                if (entries < 1)
                {
                    if (simulation >= simClothDatas.Count) throw new InvalidDataException("Simulate operator has no simulation data.");
                    int simClothData = tag.Object(tag.U32(simClothDatas.Offset + simulation * 8), "hclSimClothData");
                    entries = tag.Array(tag.U32(simClothData + 0x88)).Count + 1L;
                }
                requests.Add(new("Constraint execution order", entries * 2, 8));
                simulation++;
            }
        }
    }

    private readonly record struct AampList(uint Crc, int ListsAt, int Lists, int ObjectsAt, int Objects);

    private readonly ref struct Aamp
    {
        private readonly ResourceReader data;
        public int Root { get; }

        public Aamp(ResourceReader data)
        {
            if (!data.Magic(0, "AAMP"u8) || data.U32(4) != 2) throw new NotSupportedException("Expected an AAMP v2 parameter file.");
            this.data = data;
            Root = checked(0x30 + (int)data.U32(0x14));
        }

        public AampList List(int at) => new(data.U32(at), at + data.U16(at + 4) * 4, data.U16(at + 6),
            at + data.U16(at + 8) * 4, data.U16(at + 10));
        public int ChildList(AampList list, int index) => list.ListsAt + index * 12;
        public int Object(AampList list, int index) => list.ObjectsAt + index * 8;

        /// <summary>A missing parameter keeps the game's default, which is 0 unless given.</summary>
        public int Int(int obj, string name, int fallback = 0)
        {
            uint crc = name.ToCRC32();
            int parameters = obj + data.U16(obj + 4) * 4, count = data.U16(obj + 6);
            for (int i = 0; i < count; i++)
            {
                int at = parameters + i * 8;
                if (data.U32(at) != crc) continue;
                uint info = data.U32(at + 4);
                if (info >> 24 != 2) throw new NotSupportedException($"Physics parameter {name} is not an int.");
                return unchecked((int)data.U32(at + (info & 0xffffff) * 4));
            }
            return fallback;
        }
    }

    private readonly ref struct Tagfile
    {
        private readonly ResourceReader data;
        private readonly (int Type, int Offset, int Count)[] items;
        private readonly string?[] types;

        public Tagfile(ResourceReader file)
        {
            // The Phive header's tagfile size includes zero padding to 16 bytes after TAG0.
            var root = Sections(file, 0, Math.Min(file.Length, (int)(file.U32BigEndian(0) & 0x3fffffff))).SingleOrDefault();
            if (root.Tag != "TAG0" || file.Range(root.End, file.Length - root.End).ContainsAnyExcept((byte)0))
                throw new NotSupportedException("Expected a Havok TAG0 tagfile.");
            ResourceReader body = default;
            byte[]? strings = null, names = null, index = null;
            foreach (var section in Sections(file, root.Start, root.End))
            {
                if (section.Tag == "DATA") body = file.Slice(section.Start, section.End - section.Start);
                if (section.Tag is not ("TYPE" or "INDX")) continue;
                foreach (var child in Sections(file, section.Start, section.End))
                {
                    byte[] content = file.Range(child.Start, child.End - child.Start).ToArray();
                    if (child.Tag is "TSTR" or "TST1") strings = content;
                    else if (child.Tag is "TNAM" or "TNA1") names = content;
                    else if (child.Tag == "ITEM") index = content;
                }
            }
            if (body.Length == 0 || strings == null || names == null || index == null)
                throw new InvalidDataException("Havok tagfile is missing its data, types or items.");
            data = body;

            string[] text = Encoding.ASCII.GetString(strings).Split('\0');
            int position = 0;
            types = new string?[checked((int)Varint(names, ref position))];
            for (int i = 1; i < types.Length; i++)
            {
                long name = Varint(names, ref position);
                types[i] = name < text.Length ? text[name] : throw new InvalidDataException("Havok type name is out of range.");
                for (long templates = Varint(names, ref position); templates > 0; templates--)
                {
                    Varint(names, ref position);
                    Varint(names, ref position);
                }
            }

            items = new (int, int, int)[index.Length / 12];
            for (int i = 0; i < items.Length; i++)
            {
                uint type = BinaryPrimitives.ReadUInt32LittleEndian(index.AsSpan(i * 12)) & 0xffffff;
                uint offset = BinaryPrimitives.ReadUInt32LittleEndian(index.AsSpan(i * 12 + 4));
                uint count = BinaryPrimitives.ReadUInt32LittleEndian(index.AsSpan(i * 12 + 8));
                if (type >= types.Length || offset > data.Length || count > int.MaxValue)
                    throw new InvalidDataException("Havok item is out of range.");
                items[i] = ((int)type, (int)offset, (int)count);
            }
        }

        public uint U32(long at) => data.U32(at);
        public string TypeName(uint item) => item < items.Length ? types[items[item].Type] ?? "" : throw new InvalidDataException("Havok item index is out of range.");

        public (int Count, int Offset) Array(uint item)
        {
            if (item == 0) return (0, 0);
            TypeName(item);
            return (items[item].Count, items[item].Offset);
        }

        public int Object(uint item, string type)
        {
            if (item == 0 || TypeName(item) != type) throw new InvalidDataException("Expected Havok object " + type);
            return items[item].Offset;
        }

        public int Single(string type)
        {
            int found = -1;
            for (int i = 1; i < items.Length; i++)
                if (types[items[i].Type] == type)
                    found = found < 0 ? items[i].Offset : throw new NotSupportedException("Multiple " + type + " objects.");
            return found >= 0 ? found : throw new InvalidDataException("Missing Havok object " + type);
        }

        private static List<(string Tag, int Start, int End)> Sections(ResourceReader file, int start, int end)
        {
            var sections = new List<(string, int, int)>();
            while (start < end)
            {
                int size = (int)(file.U32BigEndian(start) & 0x3fffffff);
                if (size < 8 || size > end - start) throw new InvalidDataException("Invalid Havok section size.");
                sections.Add((Encoding.ASCII.GetString(file.Range(start + 4, 4)), start + 8, start + size));
                start += size;
            }
            return sections;
        }

        private static long Varint(byte[] data, ref int at)
        {
            if (at >= data.Length) throw new InvalidDataException("Truncated Havok varint.");
            int first = data[at];
            int extra = first switch { < 0x80 => 0, < 0xc0 => 1, < 0xe0 => 2, < 0xe8 => 3, < 0xf0 => 4, < 0xf8 => 7, _ => 8 };
            if (data.Length - at - 1 < extra) throw new InvalidDataException("Truncated Havok varint.");
            long value = extra switch { 0 => first, 1 => first & 0x3f, 2 => first & 0x1f, 3 or 4 or 7 => first & 7, _ => 0 };
            for (int i = 1; i <= extra; i++) value = value << 8 | data[at + i];
            at += extra + 1;
            return value;
        }
    }
}
