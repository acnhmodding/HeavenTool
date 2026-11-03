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

    /// <summary>The material archive of the romfs a model was opened from.</summary>
    public static string? FindShaderArchive(string modelPath)
    {
        string? parent = Directory.GetParent(Path.GetDirectoryName(Path.GetFullPath(modelPath))!)?.FullName;
        return parent == null ? null : FindRomfsShaderArchive(parent);
    }

    public static string? FindRomfsShaderArchive(string romfs)
    {
        string path = Path.Combine(romfs, "Shader", "Park_UBER_All.Nin_NX_NVN.szs");
        return File.Exists(path) ? path : null;
    }

    public static BfresShaderPreparation PrepareShaders(IReadOnlyList<BfresAsset> assets, SwitchShaderArchive archive, CancellationToken cancellation = default) =>
        PrepareShaders(assets.SelectMany(a => a.Document.Models).SelectMany(m => m.Meshes), archive, cancellation);

    /// <summary>Prepares the game materials of meshes put together from several models.</summary>
    public static BfresShaderPreparation PrepareShaders(IEnumerable<BfresMesh> meshes, SwitchShaderArchive archive, CancellationToken cancellation = default)
    {
        var programs = new Dictionary<BfresMesh, BfresMeshShaders>();
        var errors = new Dictionary<BfresMesh, string>();
        foreach (var mesh in meshes)
        {
            cancellation.ThrowIfCancellationRequested();
            try
            {
                if (archive.Name != mesh.Material.ShaderArchive) continue;
                var model = archive.Models.FirstOrDefault(m => m.Name == mesh.Material.ShadingModel);
                if (model == null) continue;

                var failures = new List<string>();
                var prepared = new BfresMeshShaders(
                    PreparePass(model, mesh, BfresShaderPass.Material, failures, cancellation),
                    PreparePass(model, mesh, BfresShaderPass.ZOnly, failures, cancellation),
                    PreparePass(model, mesh, BfresShaderPass.DepthShadow, failures, cancellation),
                    mesh.Material.InEnvironmentCapture ? PreparePass(model, mesh, BfresShaderPass.CubeMap, failures, cancellation) : null);

                if (prepared.Material != null || prepared.ZOnly != null || prepared.DepthShadow != null) programs.Add(mesh, prepared);
                if (failures.Count > 0) errors[mesh] = string.Join("; ", failures);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { errors[mesh] = ex.Message; }
        }
        return new(programs, errors);
    }

    // A pass that fails is reported, without giving up on the mesh's other passes.
    private static TranslatedShader? PreparePass(SwitchShadingModel model, BfresMesh mesh, BfresShaderPass pass, List<string> failures, CancellationToken cancellation)
    {
        try
        {
            int index = model.FindProgram(mesh, pass);
            if (index < 0) throw new InvalidDataException("No program matches the material's shader options.");
            return NativeShaderTranslator.Translate(model, model.ReadProgram(index), cancellation);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            failures.Add($"{pass}: {ex.Message}");
            return null;
        }
    }
}

public sealed record BfresMeshShaders(TranslatedShader? Material, TranslatedShader? ZOnly, TranslatedShader? DepthShadow, TranslatedShader? CubeMap = null);

public sealed record BfresShaderPreparation(IReadOnlyDictionary<BfresMesh, BfresMeshShaders> Programs, IReadOnlyDictionary<BfresMesh, string> Errors);
