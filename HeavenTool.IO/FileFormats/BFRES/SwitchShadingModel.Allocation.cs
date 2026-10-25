using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace HeavenTool.IO.FileFormats.BFRES;

// Reflection used by the ACNH resource loader. No native shader translation or
// vertex/texture decoding is needed to account for these allocation requests.
public sealed partial class SwitchShadingModel
{
    private readonly object allocationIndexLock = new();
    private Dictionary<string, List<int>>? staticAllocationPrograms;

    internal int MaterialAllocationUniformBytes
    {
        get
        {
            int index = owner.Byte(modelOffset + 0xf0);
            if (index == 255) return 0;
            if (index >= UniformBlocks.Count) throw new InvalidDataException("Invalid material uniform block index.");
            return UniformBlocks[index].Size;
        }
    }

    internal bool HasAllocationShaderOptionBindings
    {
        get
        {
            int array = owner.Pointer(modelOffset + 8), count = owner.U16(modelOffset + 0xe4);
            owner.Range(array, checked(count * 0x28));
            for (int i = 0; i < count; i++)
                if ((owner.Byte(array + i * 0x28 + 0x1c) & 1) != 0) return true;
            return UniformBlocks.Any(b => b.Type == 4 && b.Size != 0);
        }
    }

    internal int AllocationShaderObjectWorkBytes => AlignedBlocks(
        staticKeyWords * 4, staticKeyWords * 4,
        ((owner.U16(modelOffset + 0xe4) + 31) / 32) * 4, 0x48);

    internal int AllocationSelectorWorkBytes => AlignedBlocks(
        (keyWords - staticKeyWords) * 4, (keyWords - staticKeyWords) * 4, (keyWords - staticKeyWords) * 4);

    private static int AlignedBlocks(params int[] sizes)
    {
        int end = 0;
        foreach (int size in sizes) if (size != 0) end = checked(((end + 7) & ~7) + size);
        return end;
    }

    internal int[] AllocationStaticCandidates(IReadOnlyDictionary<string, string> choices)
    {
        EnsureAllocationIndex();
        uint[] words = AllocationKey(choices, false);
        return staticAllocationPrograms.TryGetValue(KeyText(words), out var indices)
            ? indices.ToArray() : [];
    }

    internal int FindAllocationProgram(IReadOnlyList<int> candidates, IReadOnlyDictionary<string, string> choices)
    {
        uint[] words = AllocationKey(choices, true);
        int match = -1;
        foreach (int index in candidates)
        {
            bool matches = true;
            for (int j = 0; j < words.Length; j++)
                if (owner.U32(keyTable + (index * keyWords + staticKeyWords + j) * 4) != words[j]) { matches = false; break; }
            if (!matches) continue;
            if (match >= 0) throw new InvalidDataException("Duplicate compiled shader key.");
            match = index;
        }
        return match;
    }

    private uint[] AllocationKey(IReadOnlyDictionary<string, string> choices, bool dynamic)
    {
        int staticCount = owner.U16(modelOffset + 0xe4);
        uint[] words = new uint[dynamic ? keyWords - staticKeyWords : staticKeyWords];
        // Static keys are encoded once per material, not once per render pass.
        for (int i = dynamic ? staticCount : 0; i < (dynamic ? Options.Count : staticCount); i++)
        {
            var option = Options[i];
            int word = option.Word - (dynamic ? staticKeyWords : 0);
            if ((uint)word >= words.Length) throw new InvalidDataException("Shader option references the wrong key region.");
            int selected = option.DefaultChoice;
            if (choices.TryGetValue(option.Name, out string? value))
            {
                int found = -1;
                for (int choice = 0; choice < option.Choices.Count; choice++)
                    if (option.Choices[choice] == value) { found = choice; break; }
                // The dynamic setter leaves its default for an unknown choice.
                // An unknown static value cannot establish a compiled candidate.
                if (found >= 0) selected = found;
                else if (!dynamic) throw new NotSupportedException($"Unknown shader choice {option.Name}={value}.");
            }
            uint bits = (uint)selected << option.Shift;
            if ((bits & ~option.Mask) != 0) throw new InvalidDataException("Shader choice exceeds its mask.");
            words[word] = (words[word] & ~option.Mask) | bits;
        }
        return words;
    }

    [MemberNotNull(nameof(staticAllocationPrograms))]
    private void EnsureAllocationIndex()
    {
        // These immutable indexes belong to the loaded archive, never to an
        // individual filename. Modified files always generate their own keys.
        lock (allocationIndexLock)
        {
            if (staticAllocationPrograms != null) return;
            var statics = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (int i = 0; i < ProgramCount; i++)
            {
                var words = new uint[staticKeyWords];
                for (int j = 0; j < words.Length; j++) words[j] = owner.U32(keyTable + (i * keyWords + j) * 4);
                string key = KeyText(words);
                if (!statics.TryGetValue(key, out var indices)) statics.Add(key, indices = []);
                indices.Add(i);
            }
            staticAllocationPrograms = statics;
        }
    }

    private static string KeyText(ReadOnlySpan<uint> words)
    {
        Span<byte> bytes = words.Length <= 256 ? stackalloc byte[words.Length * 4] : new byte[checked(words.Length * 4)];
        for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(i * 4, 4), words[i]);
        return Convert.ToHexString(bytes);
    }

    internal AllocationProgramLayout ReadAllocationLayout(int index)
    {
        if ((uint)index >= ProgramCount) throw new ArgumentOutOfRangeException(nameof(index));
        int program = programArray + index * 0x40;
        uint mask = owner.U32(program + 0x30);
        var attributes = Attributes.Where((a, i) => (mask & (1u << i)) != 0 && a.Location >= 0).ToArray();
        var samplers = AllocationLocations(owner.Pointer(program), samplerNames);
        var uniforms = AllocationLocations(owner.Pointer(program + 0x10), UniformBlocks.Select(b => b.Name).ToArray());
        var storage = AllocationLocations(owner.Pointer(program + 0x18), owner.Dictionary(owner.Pointer(modelOffset + 0x78)));
        return new(attributes, samplers, uniforms, storage);
    }

    private Dictionary<string, uint> AllocationLocations(int at, IReadOnlyList<string> names)
    {
        var locations = new Dictionary<string, uint>(StringComparer.Ordinal);
        if (names.Count == 0) return locations;
        if (at == 0) throw new InvalidDataException("Missing compiled resource locations.");
        owner.Range(at, checked(names.Count * 24));
        for (int i = 0; i < names.Count; i++)
        {
            int entry = at + i * 24;
            // The game packs source stages 0, 2, 1, and an absent compute slot.
            uint packed = (owner.U32(entry) & 255) | ((owner.U32(entry + 8) & 255) << 8)
                | ((owner.U32(entry + 4) & 255) << 16) | 0xff000000;
            locations.Add(names[i], packed);
        }
        return locations;
    }
}

internal sealed record AllocationProgramLayout(
    IReadOnlyList<ShaderAttributeLocation> Attributes,
    IReadOnlyDictionary<string, uint> Samplers,
    IReadOnlyDictionary<string, uint> Uniforms,
    IReadOnlyDictionary<string, uint> Storage);
