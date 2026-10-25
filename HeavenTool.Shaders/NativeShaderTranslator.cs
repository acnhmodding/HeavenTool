using HeavenTool.IO.FileFormats.BFRES;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Shader.Translation;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace HeavenTool.Shaders;

public sealed record TranslatedShader(string Key, string Vertex, string Fragment, SwitchShaderProgram Binary, SwitchShadingModel Model);
public static class NativeShaderTranslator
{
    private const string CacheVersion = "ryujinx-cafe-bc3834c1-v1";
    private static readonly ConcurrentDictionary<string, Lazy<string>> cache = new();
    private sealed class Accessor(byte[] code) : IGpuAccessor
    {
        public T MemoryRead<T>(ulong address) where T : unmanaged
        {
            int size = Marshal.SizeOf<T>();
            if (address > (ulong)code.Length || size > code.Length - (long)address) throw new InvalidDataException("Shader decoder read past Maxwell code.");
            return MemoryMarshal.Read<T>(code.AsSpan((int)address, size));
        }
    }

    public static TranslatedShader Translate(SwitchShadingModel model, SwitchShaderProgram program, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        if (program.Vertex == null || program.Fragment == null) throw new NotSupportedException("A vertex and fragment stage are required.");
        string vertex = Stage(program.Vertex, cancellation, out string vk);
        string fragment = Stage(program.Fragment, cancellation, out string fk);
        return new(vk + "-" + fk, vertex, fragment, program, model);
    }

    private static string Stage(SwitchShaderStage stage, CancellationToken cancellation, out string key)
    {
        byte[] code = stage.MaxwellCode.ToArray();
        key = Convert.ToHexString(SHA256.HashData(code));
        cancellation.ThrowIfCancellationRequested();
        string hash = key;
        return cache.GetOrAdd(hash, cacheKey => new Lazy<string>(() =>
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HeavenTool", "ShaderCache", CacheVersion);
            string path = Path.Combine(directory, hash + ".glsl");
            try { if (File.Exists(path)) return File.ReadAllText(path); } catch (IOException) { }
            string source = Translator.CreateContext(0, new Accessor(code), TranslationFlags.DebugMode).Translate(out _).Code;
            try
            {
                Directory.CreateDirectory(directory);
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllText(temporary, source);
                File.Move(temporary, path, true);
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
            return source;
        }, LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }
}
