#nullable enable
using AeonSake.NintendoTools.FileFormats.Byml;
using AeonSake.NintendoTools.FileFormats.Sarc;
using HeavenTool.IO.FileFormats.BYML;
using System.Numerics;

namespace HeavenTool.IO.AcnhEnvironment;

/// <summary>Typed access to the BYML parameter files of StaticParam, which fail loudly on anything unexpected.</summary>
internal static class AcnhEnvironmentParameters
{
    public static Func<string, IBymlNode?> OpenStaticParam(string romfs)
    {
        using var stream = File.OpenRead(Path.Combine(romfs, "Pack", "StaticParam.pack"));
        var archive = new SarcFileReader().Read(stream);
        return path =>
        {
            var file = archive.Files.SingleOrDefault(file => file.Name == path)
                ?? throw new InvalidDataException($"StaticParam.pack is missing {path}.");
            using var data = new MemoryStream(file.Data, false);
            return AcnhByml.Read(data).RootNode;
        };
    }

    public static string Hash(string name) => name.ToCRC32().ToString("x");

    public static BymlDictionary Dictionary(IBymlNode? node) =>
        node as BymlDictionary ?? throw new InvalidDataException("Expected an environment parameter dictionary.");

    public static BymlArray Array(IBymlNode node) =>
        node as BymlArray ?? throw new InvalidDataException("Expected an environment parameter array.");

    public static IBymlNode Field(BymlDictionary p, string name, string? type = null)
    {
        if (p.TryGetValue(Hash(type == null ? name : $"{name}.{type}"), out var node)) return node;
        // The executable also accepts the legacy untyped leaf name.
        if (type != null && p.TryGetValue(Hash(name), out node)) return node;
        throw new InvalidDataException($"Missing environment parameter {name}{(type == null ? "" : "." + type)}.");
    }

    public static string Text(BymlDictionary p, string name) =>
        Field(p, name, "Str") is BymlStringValue text ? text.Value : throw new InvalidDataException($"Invalid {name}.");

    public static float Number(BymlDictionary p, string name)
    {
        if (Field(p, name, "f32") is BymlSingleValue number && float.IsFinite(number.Value)) return number.Value;
        throw new InvalidDataException($"Invalid numeric environment parameter {name}.");
    }

    public static Vector4 Color(BymlDictionary p, string name)
    {
        float[] c = Components(p, name, "C4f", 4);
        return new(c[0], c[1], c[2], c[3]);
    }

    public static Vector3 Position3(BymlDictionary p, string name)
    {
        float[] c = Components(p, name, "V3f", 3);
        return new(c[0], c[1], c[2]);
    }

    public static Vector2 Position2(BymlDictionary p, string name)
    {
        float[] c = Components(p, name, "V2f", 2);
        return new(c[0], c[1]);
    }

    private static float[] Components(BymlDictionary p, string name, string type, int count)
    {
        var values = Array(Field(p, name, type));
        if (values.Count != count) throw new InvalidDataException($"Invalid vector {name}.");
        return [.. values.Select(value => value is BymlSingleValue component && float.IsFinite(component.Value)
            ? component.Value
            : throw new InvalidDataException($"Invalid component {name}."))];
    }
}
