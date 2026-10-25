using HeavenTool.IO.FileFormats.BFRES;
using System.Globalization;
using System.Text.RegularExpressions;

namespace HeavenTool.Shaders;

public static partial class NativeMaterialUsage
{
    public static IReadOnlySet<(string Name, int Component)> FindComponents(BfresMaterial material, TranslatedShader shader)
    {
        var result = new HashSet<(string, int)>();
        var block = shader.Model.UniformBlocks.FirstOrDefault(b => b.Name == "gsys_material");
        var location = shader.Binary.Buffers.FirstOrDefault(b => b.Name == "gsys_material");
        if (block == null || location == null) return result;
        var reads = new HashSet<int>();
        ReadStage(shader.Vertex, "vp", location.Vertex, block.Size, reads);
        ReadStage(shader.Fragment, "fp", location.Fragment, block.Size, reads);
        foreach (var uniform in block.Uniforms)
        {
            if (!material.Parameters.TryGetValue(uniform.Name, out var parameter) || parameter.Type is not (>= 12 and <= 15)) continue;
            for (int component = 0; component < parameter.Type - 11; component++)
                if (reads.Contains(uniform.Offset + component * 4)) result.Add((uniform.Name, component));
        }
        return result;
    }

    // Dynamic addressing retains the entire block: absence of a static index must not hide a potentially used value.
    private static void ReadStage(string source, string prefix, int location, int blockSize, HashSet<int> reads)
    {
        if (location < 0) return;
        source = SourceRegex().Replace(source, "");
        source = Regex.Replace(source, @$"\bvec4\s+{prefix}_c\d+_data\s*\[[^\]]*\]\s*;", "");
        string name = $"{prefix}_c{location + 3}_data";
        foreach (Match match in Regex.Matches(source, $@"\b{name}\s*\[([^\]]+)\]\s*(?:\.([xyzwrgba]{{1,4}}))?"))
        {
            string index = match.Groups[1].Value.Trim();
            bool hex = index.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            if (!int.TryParse(hex ? index[2..] : index, hex ? NumberStyles.HexNumber : NumberStyles.Integer, CultureInfo.InvariantCulture, out int slot))
            {
                for (int offset = 0; offset < blockSize; offset += 4) 
                    reads.Add(offset);
                return;
            }

            if (slot < 0 || slot >= (blockSize + 15) / 16) continue;
            string lanes = match.Groups[2].Success ? match.Groups[2].Value : "xyzw";
            foreach (char lane in lanes)
            {
                int component = "xyzw".IndexOf(lane);
                if (component < 0) component = "rgba".IndexOf(lane);
                int offset = slot * 16 + component * 4;
                if (offset < blockSize) reads.Add(offset);
            }
        }
    }

    [GeneratedRegex(@"/\*[\s\S]*?\*/|//[^\r\n]*")]
    private static partial Regex SourceRegex();
}
