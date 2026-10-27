using HeavenTool.IO.FileFormats.BFRES;
using HeavenTool.Shaders;

namespace HeavenTool.Rendering.BFRES;

/// <summary>CPU preparation, safe to run on a worker before publishing to a viewport.</summary>
public sealed record BfresScene(IReadOnlyList<BfresAsset> Assets, BfresRenderer.PreparedScene Geometry)
{
    public static BfresScene Load(Stream stream, CancellationToken cancellation = default)
    {
        var assets = BfresFileReader.Load(stream, cancellation);
        var meshes = assets.SelectMany(a => a.Document.Models).SelectMany(m => m.Meshes).ToArray();
        return new(assets, BfresRenderer.PrepareScene(meshes, cancellation));
    }

    public static string? FindShaderArchive(string modelPath)
    {
        string? parent = Directory.GetParent(Path.GetDirectoryName(Path.GetFullPath(modelPath))!)?.FullName;
        if (parent == null) return null;
        string path = Path.Combine(parent, "Shader", "Park_UBER_All.Nin_NX_NVN.szs");
        return File.Exists(path) ? path : null;
    }

    public static BfresShaderPreparation PrepareShaders(IReadOnlyList<BfresAsset> assets, SwitchShaderArchive archive, CancellationToken cancellation = default)
    {
        var programs = new Dictionary<BfresMesh, TranslatedShader>();
        var errors = new Dictionary<BfresMesh, string>();
        foreach (var mesh in assets.SelectMany(a => a.Document.Models).SelectMany(m => m.Meshes))
        {
            cancellation.ThrowIfCancellationRequested();
            try
            {
                if (archive.Name != mesh.Material.ShaderArchive) continue;
                var model = archive.Models.FirstOrDefault(m => m.Name == mesh.Material.ShadingModel);
                if (model == null) continue;
                int index = model.FindMaterialProgram(mesh);
                if (index >= 0) programs.Add(mesh, NativeShaderTranslator.Translate(model, model.ReadProgram(index), cancellation));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { errors[mesh] = ex.Message; }
        }
        return new(programs, errors);
    }
}

public sealed record BfresShaderPreparation(IReadOnlyDictionary<BfresMesh, TranslatedShader> Programs, IReadOnlyDictionary<BfresMesh, string> Errors);