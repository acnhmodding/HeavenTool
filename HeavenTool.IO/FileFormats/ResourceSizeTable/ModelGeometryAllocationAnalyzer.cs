using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using HeavenTool.IO.FileFormats.BFRES;

namespace HeavenTool.IO.FileFormats.ResourceSizeTable;

/// <summary>Recovered requests and RSTC allocation for the ACNH shared-shader resource profile.</summary>
internal sealed record ModelGeometryAllocationAnalysis(
    int BfresBytes, int Models, int Materials, int Shapes, int Meshes, int Textures,
    long SelectedPassVariationSlots, long ConfirmedRuntimePoolBytes, long MaterialCpuWorkBytes,
    long ShaderObjectWorkBytes, long ShaderSelectorWorkBytes,
    long MaterialUniformBlockBytes, long MaterialUniformBufferRequestedBytes, long AnimationNameStorageBytes,
    IReadOnlyList<long> ModelBlockBufferBytes,
    ModelGeometryRenderRequests RenderRequests)
{
    /// <summary>The decompressed model archive; single-BFRES archives are BfresBytes + 0x2000.</summary>
    public int ArchiveBytes { get; init; } = checked(BfresBytes + ArchiveHeaderBytes);
    /// <summary>ExpHeap bytes of the "output.phive" allocations, zero without physics.</summary>
    public long PhysicsExpHeapBytes { get; init; }

    private const int ArchiveHeaderBytes = 0x2000;
    // Every resource ExpHeap starts with one 16 KiB GPU pool already created; it and
    // its pool metadata form the fixed region after the page-aligned BFRES/runtime pool.
    private const long ResidentRegionBytes = 0x8000;
    // Fixed loader allocations around the page-aligned region and after the render objects.
    private const long PoolRegionOverheadBytes = 952, TrailingLoaderBytes = 1960;
    // The first ModelObj per FMDL requests 512 bytes more than the second one.
    private const long FirstModelBlockExtraBytes = 512;

    // One 0x228-byte shader wrapper per material.
    public long ShaderWrapperBytes => Materials * 0x228L;

    // Three 8-aligned allocations per shader instance: wrapper, object work,
    // selector work. The selector's 20 requested bytes occupy 24 heap bytes.
    // The resource creates two such sets (see CpuExpHeapBytes).
    public long SingleShaderSetExpHeapBytes => checked(ShaderWrapperBytes + ShaderObjectWorkBytes
        + ShaderSelectorWorkBytes + Materials * (3 * 0x20L + 4));

    /// <summary>ExpHeap high-water mark before any GPU pool beyond the resident one is created.</summary>
    /// <remarks>
    /// The phive allocations precede the BFRES setup (0x71006E1098), so they share the page-aligned
    /// region. For single-BFRES archives the archive term is Align(BfresBytes, 32); with physics it
    /// also covers the phive member and its padding (verified on 888 of 890 vanilla 3.0.3 archives).
    /// </remarks>
    public long CpuExpHeapBytes => checked(ExpHeapPlanner.AlignUp(ExpHeapPlanner.AlignUp(ArchiveBytes, 32) - ArchiveHeaderBytes
        + PhysicsExpHeapBytes + ConfirmedRuntimePoolBytes + PoolRegionOverheadBytes, 4096)
        + ResidentRegionBytes + 2 * SingleShaderSetExpHeapBytes + MaterialCpuWorkBytes
        + RenderRequests.BindingDescriptorExpHeapBytes + RenderRequests.VertexStateExpHeapBytes
        + RenderRequests.ResourceCommandWrapperExpHeapBytes + TrailingLoaderBytes);

    /// <summary>The RSTC value: the resource ExpHeap's high-water mark.</summary>
    public long AllocationBytes
    {
        get
        {
            // Requests that do not fit the resident pool create new pools whose metadata
            // and page-aligned backing are appended to the same ExpHeap, in creation order.
            var placements = ModelGpuPoolPlanner.Plan(GpuBufferRequests(), ModelGpuPoolConfiguration.Acnh303Startup, emptyPools: 1);
            return ExpHeapPlanner.HighWaterMark(placements.SelectMany(p => p.ParentRequests), CpuExpHeapBytes);
        }
    }

    /// <summary>GPU buffers (memory flags 3) in loader order.</summary>
    public IReadOnlyList<ModelGpuBufferRequest> GpuBufferRequests()
    {
        var requests = new List<ModelGpuBufferRequest>();
        // 0x71038D3D40 requests one g3d ModelObj block buffer per FMDL; every FMDL gets
        // two ModelObjs, all first ones before all second ones. An empty block requests nothing.
        var blocks = ModelBlockBufferBytes.Where(bytes => bytes > 0).ToArray();
        foreach (long bytes in blocks) requests.Add(new("Model block buffer", bytes + FirstModelBlockExtraBytes, 256));
        foreach (long bytes in blocks) requests.Add(new("Model block buffer", bytes, 256));
        if (MaterialUniformBufferRequestedBytes > 0) requests.Add(new("Material uniforms", MaterialUniformBufferRequestedBytes, 256));
        if (RenderRequests.CommandBufferRequestedBytes > 0) requests.Add(new("Resource commands", RenderRequests.CommandBufferRequestedBytes, 4));
        return requests.AsReadOnly();
    }
}

internal sealed record ModelGeometryRenderRequests(
    int BindingDescriptors, int VertexStates, int ResourceCommandWrappers,
    long BindingDescriptorBytes, long VertexStateBytesIncludingWorkReserve,
    long ResourceCommandWrapperBytes, long RecordedResourceCommandBytes, long RecordedMeshDrawBytes)
{
    // ACNH 3.0.3 ExpHeap 0x71037C77A0: 32-byte header, payload rounded
    // to 8 bytes. These particular object requests are already multiples of 8
    // and request alignment 8; no additional head padding is needed.
    // The allocator branch is covered by 67 ARM64 emulation checks.
    public long BindingDescriptorExpHeapBytes => checked(BindingDescriptorBytes + BindingDescriptors * 0x20L);
    public long VertexStateExpHeapBytes => checked(VertexStateBytesIncludingWorkReserve + VertexStates * 0x20L);
    public long ResourceCommandWrapperExpHeapBytes => checked(ResourceCommandWrapperBytes + ResourceCommandWrappers * 0x20L);
    public long CommandBufferRequestedBytes => RecordedResourceCommandBytes + RecordedMeshDrawBytes == 0 ? 0
        : checked((RecordedResourceCommandBytes + RecordedMeshDrawBytes + 0x203) & ~3L);
}

/// <summary>
/// Reads allocation metadata without decoding meshes, textures or shader binaries.
/// Supply the game's shared BFSHA archive explicitly; no filenames or cached
/// vanilla allocations are used to select the result.
/// </summary>
internal sealed class ModelGeometryAllocationAnalyzer(SwitchShaderArchive shaders)
{
    private static readonly string[] Passes = ["material", "zonly", "zprepass", "gbuffer", "depth_silhouette", "dilate",
        "reflection", "sssss", "cubemap", "depthshadow", "xlu_zprepass", "visualize", "user0", "user1", "user2", "user3"];
    // ACNH 3.0.3 0x71040B4070; 16 is the terminal (no fallback) sentinel.
    private static readonly int[] ParentPasses = [16, 0, 1, 1, 1, 0, 8, 0, 0, 1, 0, 0, 0, 0, 0, 0];
    private static readonly string[] Uniforms = ["invalid", "context", "skeleton", "skeleton_ex", "shape", "shape_ex",
        "environment", "res_material", "material", "shader_option", "scene_material", "user0", "user1", "user2", "material_ffl"];
    private static readonly string[] Storage = ["invalid", "environment", "material", "scene_material", "user0", "user1", "user2", "analyze_buffer"];
    private readonly SwitchShaderArchive shaders = shaders ?? throw new ArgumentNullException(nameof(shaders));
    private readonly Dictionary<(SwitchShadingModel, int), AllocationProgramLayout> layouts = [];

    /// <summary>Unsupported or malformed inputs return a reason, never a partial result.</summary>
    public bool TryAnalyze(ReadOnlyMemory<byte> archive, [NotNullWhen(true)] out ModelGeometryAllocationAnalysis? analysis, out string reason)
    {
        analysis = null;
        reason = "Expected a canonical BFRES v9 model archive, optionally with output.phive.";
        if (!TryReadArchive(archive.Span, out var bfres, out var phive)) return false;
        try
        {
            long physics = phive.Length == 0 ? 0 : ModelPhysicsAllocationPlanner.ExpHeapBytes(archive.Span.Slice(phive.Offset, phive.Length));
            // Per-instance reflection caching is protected so a caller can share the
            // analyzer across batch workers without publishing mutable state.
            lock (layouts) analysis = Analyze(new ResourceReader(archive.Span.Slice(bfres.Offset, bfres.Length))) with
            {
                ArchiveBytes = archive.Length,
                PhysicsExpHeapBytes = physics
            };
            reason = "";
            return true;
        }
        catch (Exception error) when (error is InvalidDataException or NotSupportedException or OverflowException or ArgumentException)
        {
            reason = error.Message;
            return false;
        }
    }

    // 3.0.3 model archives: a single BFRES at 0x2000, or "output.phive" then "output.bfres"
    // (0x710070FD2C). Shader archives and cloth wind (.bphcw) members are not supported.
    private static bool TryReadArchive(ReadOnlySpan<byte> archive, out ModelArchive.Member bfres, out ModelArchive.Member phive)
    {
        phive = default;
        if (ModelArchive.TryReadSingleBfres(archive, out bfres)) return true;
        if (!ModelArchive.TryReadSarc(archive, out var members) ||
            members is not [{ Name: "output.phive", Offset: 0x2000 } physics, { Name: "output.bfres" } model])
            return false;
        (phive, bfres) = (physics, model);
        return ModelArchive.IsBfres(archive.Slice(bfres.Offset, bfres.Length));
    }

    private ModelGeometryAllocationAnalysis Analyze(ResourceReader d)
    {
        int modelCount = d.U16(0xdc);
        if (modelCount == 0) throw new NotSupportedException("No geometry models to analyze.");
        int textures = BfresRuntimePool.TextureCount(d);
        int materials = 0, shapes = 0, meshes = 0;
        long materialWork = 0, objectWork = 0, selectorWork = 0, uniformBytes = 0, uniformRequests = 0, slots = 0;
        int bindingsCount = 0, verticesCount = 0, commandsCount = 0;
        long verticesBytes = 0, resourceBytes = 0, meshBytes = 0;
        var modelBlocks = new long[modelCount];
        int modelIndex = 0;
        foreach (int model in Entries(d, 0x28, modelCount, 0x78))
        {
            d.RequireMagic(model, "FMDL"u8);
            int[] materialOffsets = Entries(d, model + 0x38, d.U16(model + 0x6c), 0xa8);
            int[] vertexOffsets = Entries(d, model + 0x20, d.U16(model + 0x68), 0x58);
            var materialData = new List<Material>();
            foreach (int material in materialOffsets)
            {
                var parsed = ReadMaterial(d, material);
                if (parsed.Candidates.Length == 0)
                    throw new NotSupportedException("No compiled static candidate for material " + d.Name(d.Pointer(material + 8)));
                if (parsed.Model.HasAllocationShaderOptionBindings)
                    throw new NotSupportedException("Additional shader-option command bindings are unresolved.");
                if (parsed.Model.Options.Any(o => o.Name == "gsys_assign_variation"))
                    throw new NotSupportedException("Assign-variation shader profiles are unresolved.");
                materialData.Add(parsed);
                if (parsed.Model.MaterialAllocationUniformBytes != 0)
                    materialWork = checked(materialWork + MaterialWork(d.U16(material + 0x9e), d.U16(material + 0xa2), d.Byte(material + 0x9d)));
                objectWork = checked(objectWork + parsed.Model.AllocationShaderObjectWorkBytes);
                selectorWork = checked(selectorWork + parsed.Model.AllocationSelectorWorkBytes);
                uniformBytes = checked(uniformBytes + parsed.Model.MaterialAllocationUniformBytes);
                // 0x71002B0E1C queries uniform alignment, then 0x71038FB8C0
                // rounds each material's GPU work separately to 256 bytes.
                // SDK 0x71004B5F30 also returns 256 for device query 4.
                long uniform = (parsed.Model.MaterialAllocationUniformBytes + 255L) & ~255L;
                uniformRequests = checked(uniformRequests + uniform);
                // The ModelObj block (0x71002ACF30) holds each material's block too.
                modelBlocks[modelIndex] = checked(modelBlocks[modelIndex] + uniform);
            }
            materials = checked(materials + materialOffsets.Length);
            foreach (int shape in Entries(d, model + 0x28, d.U16(model + 0x6a), 0x60))
            {
                d.RequireMagic(shape, "FSHP"u8);
                int materialIndex = d.U16(shape + 0x52), vertexIndex = d.U16(shape + 0x56);
                if (materialIndex >= materialData.Count || vertexIndex >= vertexOffsets.Length || d.Pointer(shape + 0x10) != vertexOffsets[vertexIndex])
                    throw new InvalidDataException("Shape material or vertex reference is invalid.");
                var material = materialData[materialIndex];
                int vertex = vertexOffsets[vertexIndex];
                d.RequireMagic(vertex, "FVTX"u8);
                var bindings = new HashSet<string>(StringComparer.Ordinal);
                var states = new Dictionary<string, int>(StringComparer.Ordinal);
                var commands = new Dictionary<string, int>(StringComparer.Ordinal);
                var options = new Dictionary<string, string>(material.Options, StringComparer.Ordinal)
                {
                    ["gsys_weight"] = d.Byte(shape + 0x5a).ToString(CultureInfo.InvariantCulture),
                    ["gsys_index_stream_format"] = "0"
                };
                var passOption = material.Model.Options.SingleOrDefault(o => o.Name == "gsys_assign_type")
                    ?? throw new NotSupportedException("Missing render-pass shader option.");
                for (int requested = 0; requested < Passes.Length; requested++)
                {
                    int pass = requested;
                    while (pass < Passes.Length && !passOption.Choices.Contains("gsys_assign_" + Passes[pass])) pass = ParentPasses[pass];
                    if (pass == Passes.Length) throw new NotSupportedException("No render-pass fallback.");
                    options["gsys_assign_type"] = "gsys_assign_" + Passes[pass];
                    int index = material.Model.FindAllocationProgram(material.Candidates, options);
                    if (index < 0) throw new NotSupportedException("No compiled dynamic shader program.");
                    if (!layouts.TryGetValue((material.Model, index), out var layout))
                        layouts.Add((material.Model, index), layout = material.Model.ReadAllocationLayout(index));
                    bindings.Add(BindingKey(layout, material));
                    var (Key, Bytes, Streams, Attributes) = VertexKey(d, vertex, material, layout);
                    states[Key] = Bytes;
                    string command = LocationKey(layout.Uniforms, Uniforms) + LocationKey(layout.Storage, Storage) + Key;
                    commands[command] = VertexCommandBytes(Streams, Attributes);
                    slots = checked(slots + 4);
                }
                bindingsCount = checked(bindingsCount + bindings.Count);
                verticesCount = checked(verticesCount + states.Count);
                commandsCount = checked(commandsCount + commands.Count);
                verticesBytes = checked(verticesBytes + states.Values.Sum(x => (long)x));
                resourceBytes = checked(resourceBytes + commands.Values.Sum(x => (long)x));
                foreach (int mesh in Entries(d, shape + 0x18, d.Byte(shape + 0x5b), 0x38))
                {
                    // The loader aggregates all submeshes into one indexed draw.
                    if (d.U16(mesh + 0x34) == 0) throw new NotSupportedException("Mesh has no submeshes.");
                    meshBytes = checked(meshBytes + (d.U32(mesh + 0x30) == 0 ? 44 : 48));
                    meshes++;
                }
                // 0x71002AE3E0: one 256-byte shape block (no view-dependent shapes or key shapes here).
                modelBlocks[modelIndex] = checked(modelBlocks[modelIndex] + 0x100);
                shapes++;
            }
            modelIndex++;
        }
        long animations = BfresRuntimePool.AnimationBytes(d, out long animationNames);
        long pool = BfresRuntimePool.Bytes(modelCount, materials, shapes, meshes, textures, animations);
        return new(d.Length, modelCount, materials, shapes, meshes, textures, slots, pool,
            materialWork, objectWork, selectorWork, uniformBytes, uniformRequests, animationNames,
            Array.AsReadOnly(modelBlocks),
            new(bindingsCount, verticesCount, commandsCount, bindingsCount * 0x410L, verticesBytes,
                commandsCount * 0x260L, resourceBytes, meshBytes));
    }

    private sealed record Material(SwitchShadingModel Model, int[] Candidates, Dictionary<string, string> Options,
        Dictionary<string, string> Attributes, Dictionary<string, string> Samplers, string[] SamplerNames);

    private Material ReadMaterial(ResourceReader d, int at)
    {
        d.RequireMagic(at, "FMAT"u8);
        int assign = d.Pointer(at + 0x20);
        if (d.Name(d.Pointer(assign)) != shaders.Name) throw new NotSupportedException("Material references a different shader archive.");
        string modelName = d.Name(d.Pointer(assign + 8));
        var model = shaders.Models.SingleOrDefault(m => m.Name == modelName)
            ?? throw new NotSupportedException("Missing shading model " + modelName);
        var options = Assignments(d, assign, 0x30, 0x38, d.U16(assign + 0x46));
        var info = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (int entry in Entries(d, at + 0x10, d.U16(at + 0x9a), 0x18))
        {
            string name = d.Name(d.Pointer(entry));
            if (d.Byte(entry + 0x12) == 2 && d.U16(entry + 0x10) > 0) info.Add(name, d.Name(d.Pointer(d.Pointer(entry + 8))));
        }
        string Value(string name) => info.GetValueOrDefault(name, "");
        options["gsys_renderstate"] = Value("gsys_render_state_mode") switch
        {
            "opaque" => "0", "mask" => "1", "translucent" => "2", "custom" => "3",
            _ => throw new NotSupportedException("Unknown material render mode.")
        };
        options["gsys_display_face_type"] = Value("gsys_render_state_display_face") switch
        {
            "back" => "2", "front" => "1", "both" => "0", "none" => "3",
            _ => throw new NotSupportedException("Unknown material culling mode.")
        };
        string[] functions = ["never", "less", "equal", "lequal", "greater", "nequal", "gequal", "always"];
        options["gsys_alpha_test_func"] = Math.Max(0, Array.IndexOf(functions, Value("gsys_alpha_test_func"))).ToString(CultureInfo.InvariantCulture);
        options["gsys_alpha_test_enable"] = Value("gsys_alpha_test_enable") == "true" ? "1" : "0";
        options["gsys_pass"] = Value("gsys_pass") switch { "seal" => "1", "xlu_water" => "2", "reduced_buffer" => "3", _ => "0" };
        return new(model, model.AllocationStaticCandidates(options), options, Assignments(d, assign, 0x10, 0x18, d.Byte(assign + 0x44)),
            Assignments(d, assign, 0x20, 0x28, d.Byte(assign + 0x45)), DictionaryNames(d, d.Pointer(at + 0x48), d.Byte(at + 0x9d)));
    }

    private static long MaterialWork(int parameters, int source, int samplers)
    {
        long end = 0;
        foreach (int size in new[] { ((parameters + 31) / 32) * 4, (source + 7) & ~7, samplers * 8, 0x48, samplers * 8 })
            if (size != 0) end = checked(((end + 7) & ~7L) + size);
        return end;
    }

    private static string BindingKey(AllocationProgramLayout layout, Material material)
    {
        var entries = new List<(int, uint)>();
        foreach (var sampler in layout.Samplers)
            if (sampler.Value != uint.MaxValue && material.Samplers.TryGetValue(sampler.Key, out string? target))
            {
                int source = Array.IndexOf(material.SamplerNames, target);
                var entry = (source < 0 ? 255 : source, sampler.Value);
                if (!entries.Contains(entry)) entries.Add(entry);
            }
        return string.Join(';', entries.Select(e => $"{e.Item1}:{e.Item2:X8}"));
    }

    private static (string Key, int Bytes, int Streams, int Attributes) VertexKey(ResourceReader d, int vertex, Material material, AllocationProgramLayout layout)
    {
        var attributes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (int at in Entries(d, vertex + 8, d.Byte(vertex + 0x4c), 16)) attributes.Add(d.Name(d.Pointer(at)), at);
        var buffers = new Dictionary<int, int>();
        var key = new StringBuilder();
        int maxLocation = -1, count = 0;
        foreach (var input in layout.Attributes)
        {
            if (!material.Attributes.TryGetValue(input.Name, out string? target) || !attributes.TryGetValue(target, out int at)) continue;
            int source = d.Byte(at + 0xe);
            if (source >= d.Byte(vertex + 0x4d) || input.Location >= 16) throw new NotSupportedException("Vertex stream or location is out of range.");
            if (!buffers.TryGetValue(source, out int compressed)) buffers.Add(source, compressed = buffers.Count);
            key.Append(CultureInfo.InvariantCulture, $"{d.U32(at + 8):X8}:{d.U16(at + 0xc)}:{input.Location}:{compressed};");
            maxLocation = Math.Max(maxLocation, input.Location);
            count++;
        }
        int work = buffers.Count * 8 + (maxLocation + 1) * 4;
        return (key.ToString(), 0x230 + ((work + 63) & ~63), buffers.Count, count);
    }

    private static string LocationKey(IReadOnlyDictionary<string, uint> locations, string[] names) =>
        string.Concat(names.Select(n => locations.GetValueOrDefault("gsys_" + n, uint.MaxValue).ToString("X8", CultureInfo.InvariantCulture)));

    // SDK instructions validated by the separate 237-case ARM64 harness.
    private static int VertexCommandBytes(int streams, int attributes)
    {
        if (streams > 16) throw new NotSupportedException("Too many resource vertex streams.");
        return attributes == 0 ? 0 : 132 + 28 * streams;
    }

    // The offsets of a BFRES array's entries, whose pointer is stored at the given field.
    private static int[] Entries(ResourceReader d, int field, int count, int stride)
    {
        int start = d.Pointer(field);
        if (start == 0 && count != 0) throw new InvalidDataException("Missing allocation metadata array.");
        d.Range(start, checked(count * stride));
        var entries = new int[count];
        for (int i = 0; i < count; i++) entries[i] = start + i * stride;
        return entries;
    }

    // The key names of a BFRES dictionary in entry order, skipping its root node.
    private static string[] DictionaryNames(ResourceReader d, int at, int count)
    {
        if (at == 0 && count == 0) return [];
        if (at == 0 || d.U32(at + 4) != count) throw new InvalidDataException("Allocation dictionary count mismatch.");
        d.Range(at + 8, checked((count + 1) * 16));
        var names = new string[count];
        for (int i = 0; i < count; i++) names[i] = d.Name(d.Pointer(at + 32 + i * 16));
        return names;
    }

    // A shader assignment table: a name dictionary paired with an array of value strings.
    private static Dictionary<string, string> Assignments(ResourceReader d, int at, int array, int dictionary, int count)
    {
        string[] names = DictionaryNames(d, d.Pointer(at + dictionary), count);
        int[] entries = Entries(d, at + array, count, 8);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
            if (!result.TryAdd(names[i], d.Name(d.Pointer(entries[i])))) throw new InvalidDataException("Duplicate shader assignment.");
        return result;
    }
}
