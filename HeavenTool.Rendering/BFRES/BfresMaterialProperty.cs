using HeavenTool.IO.FileFormats.BFRES;
using System.Globalization;

namespace HeavenTool.Rendering.BFRES;

public sealed record BfresMaterialProperty(BfresMaterial Material, string Name, int Component, string Label, bool IsActive)
{
    public float Value => Material.Parameters[Name].Float(Component, 0);
    public float OriginalValue => Material.OriginalParameters[Name].Float(Component, 0);
    public bool IsChanged => Value != OriginalValue;
    public void SetValue(float value)
    {
        if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value), "Enter a finite number.");
        Material.SetParameterComponent(Name, Component, value);
    }
    public void Reset() => SetValue(OriginalValue);

    public static bool TryParse(object? text, out float value) =>
        (float.TryParse(Convert.ToString(text), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        || float.TryParse(Convert.ToString(text), NumberStyles.Float, CultureInfo.CurrentCulture, out value)) && float.IsFinite(value);

    public static IEnumerable<BfresMaterialProperty> Enumerate(BfresMaterial material,
        IReadOnlySet<(string Name, int Component)>? usage = null, bool showAll = false, string filter = "")
    {
        filter = filter.Trim();
        foreach (var pair in material.Parameters.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            int count = pair.Value.Type is >= 12 and <= 15 ? pair.Value.Type - 11 : 0;
            if (pair.Value.Data.Length < count * 4) continue;
            string label = DisplayName(pair.Key);
            if (!pair.Key.Contains(filter, StringComparison.OrdinalIgnoreCase) && !label.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
            string axes = pair.Key.Contains("color", StringComparison.OrdinalIgnoreCase) ? "RGBA" : "XYZW";
            for (int component = 0; component < count; component++)
            {
                bool active = usage == null || usage.Contains((pair.Key, component));
                if (showAll || active)
                    yield return new(material, pair.Key, component, label + (count == 1 ? "" : $" [{axes[component]}]"), active);
            }
        }
    }

    public static string DisplayName(string name)
    {
        string? known = name switch
        {
            "material_force_alpha" => "Opacity",
            "material_emission_intensity" => "Emission Strength",
            "material_metalness" => "Metallic",
            "material_roughness" => "Roughness",
            "material_occlusion" => "Ambient Occlusion",
            "gsys_alpha_test_ref_value" => "Alpha Cutoff",
            "gsys_xlu_zprepass_alpha" => "Depth Pass Opacity",
            "model_env_instance_intensity" => "Environment Intensity",
            _ => null
        };
        if (known != null) return known;

        foreach (string prefix in new[] { "material_", "model_", "gsys_" })
            if (name.StartsWith(prefix, StringComparison.Ordinal)) { name = name[prefix.Length..]; break; }

        name = name.Replace("const_color", "Color ").Replace("const_float", "Custom Value ").Replace("_", " ")
            .Replace("lgiht", "light").Replace("instensity", "intensity");
        return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];
    }

}