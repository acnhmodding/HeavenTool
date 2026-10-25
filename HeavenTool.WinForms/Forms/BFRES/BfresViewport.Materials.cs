using HeavenTool.IO.FileFormats.BFRES;
using HeavenTool.Shaders;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HeavenTool.Forms.BFRES;

internal sealed partial class BfresViewport
{
    private sealed class MaterialGpu(ParkMaterialProfile profile, int[] textures, int[] samplers)
    {
        public ParkMaterialProfile Profile { get; set; } = profile;
        public int[] Textures { get; } = textures;
        public int[] Samplers { get; } = samplers;
    }
    private readonly HashSet<BfresMaterial> changedMaterials = [];
    public void RefreshMaterial(BfresMaterial material) { changedMaterials.Add(material); Invalidate(); }

    private void UpdateMaterials()
    {
        foreach (var source in changedMaterials)
        {
            if (materials.TryGetValue(source, out var material)) material.Profile = new(source);
            foreach (var pair in nativeMeshes)
            {
                if (!ReferenceEquals(pair.Key.Material, source) || !pair.Value.Buffers.TryGetValue("gsys_material", out int buffer)) continue;
                var block = pair.Value.Program.Source.Model.UniformBlocks.Single(x => x.Name == "gsys_material");
                byte[] bytes = ParkUniforms.Material(source, block);
                GL.BindBuffer(BufferTarget.UniformBuffer, buffer);
                GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, bytes.Length, bytes);
            }
        }
        changedMaterials.Clear();
    }
    private readonly Dictionary<string, int> uniforms = [];
    private int U(string name) => uniforms[name];

    private void InitializeMaterialUniforms()
    {
        string[] names = ["albedo", "normalMap", "mixMap", "opacityMap", "emissionMap", "mapPresent", "materialTint", "cameraPosition",
            "normalEnabled", "normalSigned", "normalBc1", "roughnessEnabled", "metalnessEnabled", "occlusionEnabled", "emissionEnabled",
            "roughnessChannel", "metalnessChannel", "occlusionChannel", "roughness", "metalness", "occlusion", "emissionIntensity",
            "alphaSource", "alphaChannel", "forceAlpha", "alphaFunction", "alphaReference", "diffuseEnabled", "specularEnabled", "doubleSided", "constantColor", "unlitMode",
            "lightDirection", "lightColor", "skyColor", "groundColor"];
        foreach (string name in names) uniforms[name] = GL.GetUniformLocation(program, name);
        for (int i = 0; i < 5; i++) GL.Uniform1(U(names[i]), i);
    }

    private MaterialGpu UploadMaterial(BfresMaterial source)
    {
        if (materials.TryGetValue(source, out var existing)) return existing;
        var profile = new ParkMaterialProfile(source);
        var material = new MaterialGpu(profile, new int[5], new int[5]);
        materials.Add(source, material);
        BfresSampler?[] bindings = [profile.Albedo, profile.Normal, profile.Mix, profile.Opacity, profile.Emission];
        for (int i = 0; i < bindings.Length; i++)
        {
            var binding = bindings[i];
            if (binding?.Texture == null) continue;
            material.Textures[i] = UploadTexture(binding.Texture);
            material.Samplers[i] = CreateSampler(binding);
        }
        return material;
    }

    private float maxAnisotropy;

    private int CreateSampler(BfresSampler binding)
    {
        static TextureWrapMode Wrap(byte value) => value switch
        {
            0 => TextureWrapMode.Repeat, 1 => TextureWrapMode.MirroredRepeat,
            3 or 5 => TextureWrapMode.ClampToBorder, _ => TextureWrapMode.ClampToEdge
        };
        int sampler = GL.GenSampler();
        GL.SamplerParameter(sampler, SamplerParameterName.TextureWrapS, (int)Wrap(binding.WrapU));
        GL.SamplerParameter(sampler, SamplerParameterName.TextureWrapT, (int)Wrap(binding.WrapV));
        // NX filter flags: mip in bits 0-1, magnification in 2-3, minification in 4-5 (1 = point, 2 = linear).
        bool pointMin = (binding.FilterFlags & 0x30) == 0x10, pointMip = (binding.FilterFlags & 0x03) == 1;
        var min = pointMin ? pointMip ? TextureMinFilter.NearestMipmapNearest : TextureMinFilter.NearestMipmapLinear
            : pointMip ? TextureMinFilter.LinearMipmapNearest : TextureMinFilter.LinearMipmapLinear;
        GL.SamplerParameter(sampler, SamplerParameterName.TextureMinFilter, (int)min);
        GL.SamplerParameter(sampler, SamplerParameterName.TextureMagFilter, (int)((binding.FilterFlags & 0x0c) == 4 ? TextureMagFilter.Nearest : TextureMagFilter.Linear));
        float[] border = binding.BorderColor switch { 0 => [1f, 1f, 1f, 1f], 1 => [0f, 0f, 0f, 0f], _ => [0f, 0f, 0f, 1f] };
        GL.SamplerParameter(sampler, SamplerParameterName.TextureBorderColor, border);
        if (float.IsFinite(binding.MinLod) && float.IsFinite(binding.MaxLod) && binding.MaxLod >= binding.MinLod)
        {
            GL.SamplerParameter(sampler, SamplerParameterName.TextureMinLod, binding.MinLod);
            GL.SamplerParameter(sampler, SamplerParameterName.TextureMaxLod, binding.MaxLod);
        }
        if (float.IsFinite(binding.LodBias)) GL.SamplerParameter(sampler, SamplerParameterName.TextureLodBias, binding.LodBias);
        if (maxAnisotropy == 0)
        {
            // Anisotropic filtering is an extension before GL 4.6, but drivers expose it almost universally.
            maxAnisotropy = 1;
            for (int i = 0, count = GL.GetInteger(GetPName.NumExtensions); i < count; i++)
                if (GL.GetString(StringNameIndexed.Extensions, i) is "GL_EXT_texture_filter_anisotropic" or "GL_ARB_texture_filter_anisotropic")
                {
                    maxAnisotropy = GL.GetFloat((GetPName)0x84FF);
                    break;
                }
        }
        if (maxAnisotropy > 1)
            GL.SamplerParameter(sampler, (SamplerParameterName)0x84FE, Math.Clamp((float)binding.MaxAnisotropy, 1, maxAnisotropy));
        return sampler;
    }

    // Uniforms that are the same for every fallback mesh; the program keeps them for the whole frame.
    private void SetFrameUniforms(Matrix4 viewProjection)
    {
        GL.UseProgram(program);
        GL.UniformMatrix4(matrixUniform, false, ref viewProjection);
        GL.Uniform3(U("cameraPosition"), cameraPosition);
        GL.Uniform1(U("unlitMode"), lighting == null ? 1 : 0);
        if (lighting == null) return;
        // Same light inputs the native programs receive through gsys_environment.
        var fill = lighting.PreviewIndirectLight;
        var direct = lighting.DirectColor * lighting.DirectIntensity;
        var sky = lighting.SkyColor * lighting.HemisphereIntensity;
        var ground = lighting.GroundColor * lighting.HemisphereIntensity;
        GL.Uniform3(U("lightDirection"), lighting.DirectDirection.X, lighting.DirectDirection.Y, lighting.DirectDirection.Z);
        GL.Uniform3(U("lightColor"), direct.X, direct.Y, direct.Z);
        GL.Uniform3(U("skyColor"), sky.X + fill.X, sky.Y + fill.Y, sky.Z + fill.Z);
        GL.Uniform3(U("groundColor"), ground.X + fill.X, ground.Y + fill.Y, ground.Z + fill.Z);
    }

    private int CompareTransparent(GpuMesh a, GpuMesh b)
    {
        int priority = a.Material.Profile.Priority.CompareTo(b.Material.Profile.Priority);
        if (priority != 0) return priority;
        return (b.Center - cameraPosition).LengthSquared.CompareTo((a.Center - cameraPosition).LengthSquared);
    }

    private void DrawMaterialMesh(GpuMesh mesh)
    {
        var gpu = mesh.Material;
        var p = gpu.Profile;
        var source = p.Source;
        ApplyRasterState(p);
        int present = 0;
        for (int i = 0; i < 5; i++)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + i);
            GL.BindTexture(TextureTarget.Texture2D, gpu.Textures[i]); GL.BindSampler(i, gpu.Samplers[i]);
            if (gpu.Textures[i] != 0 && mesh.Source.TexCoords.Length > 0) present |= 1 << i;
        }
        GL.Uniform1(U("mapPresent"), present);
        var tint = p.Tint;
        GL.Uniform4(U("materialTint"), tint.X, tint.Y, tint.Z, tint.W);
        void Flag(string name, bool value) => GL.Uniform1(U(name), value ? 1 : 0);
        Flag("normalEnabled", p.NormalMap); Flag("roughnessEnabled", p.RoughnessMap); Flag("metalnessEnabled", p.MetalnessMap);
        Flag("occlusionEnabled", p.OcclusionMap); Flag("emissionEnabled", p.EmissionMap);
        Flag("normalSigned", (p.Normal?.Texture?.Format & 255) == 2); Flag("normalBc1", source.Option("gsys_normalmap_BC1") != 0);
        Flag("diffuseEnabled", !source.IsParkUber || source.Option("enable_diffuse", 1) != 0);
        Flag("specularEnabled", source.IsParkUber && source.Option("enable_glossy") != 0);
        Flag("doubleSided", p.DoubleSided || source.Option("enable_normal_both") != 0);
        Flag("constantColor", source.IsParkUber && source.Option("material_diffuse_color") is >= 100 and <= 105);
        GL.Uniform1(U("roughnessChannel"), Math.Clamp(source.Option("material_select_roughness_channel", 2), 0, 3));
        GL.Uniform1(U("metalnessChannel"), Math.Clamp(source.Option("material_select_metalness_channel"), 0, 3));
        GL.Uniform1(U("occlusionChannel"), Math.Clamp(source.Option("material_select_occlusion_channel", 1), 0, 3));
        GL.Uniform1(U("roughness"), p.Roughness); GL.Uniform1(U("metalness"), p.Metalness);
        GL.Uniform1(U("occlusion"), p.Occlusion); GL.Uniform1(U("emissionIntensity"), p.EmissionIntensity);
        GL.Uniform1(U("alphaSource"), source.IsParkUber ? p.AlphaSource : 0); GL.Uniform1(U("alphaChannel"), p.AlphaChannel);
        GL.Uniform1(U("forceAlpha"), p.ForceAlpha); GL.Uniform1(U("alphaFunction"), p.AlphaFunction); GL.Uniform1(U("alphaReference"), p.AlphaReference);
        GL.BindVertexArray(mesh.Vao);
        GL.DrawElements(PrimitiveType.Triangles, mesh.Count, DrawElementsType.UnsignedInt, 0);
    }


    private static void ApplyRasterState(ParkMaterialProfile p)
    {
        var source = p.Source;
        if (p.DepthTest) GL.Enable(EnableCap.DepthTest); else GL.Disable(EnableCap.DepthTest);
        GL.DepthMask(p.DepthWrite);
        GL.DepthFunc((DepthFunction)((int)DepthFunction.Never + ParkMaterialProfile.Comparison(source.Info("gsys_depth_test_func", "lequal"))));
        string faces = source.Info("gsys_render_state_display_face", "both");
        if (faces is "front" or "back")
        {
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(faces == "front" ? TriangleFace.Back : TriangleFace.Front);
        }
        else GL.Disable(EnableCap.CullFace);
        if (p.Translucent)
        {
            GL.Enable(EnableCap.Blend);
            GL.BlendEquationSeparate(Equation(source.Info("gsys_color_blend_rgb_op", "add")), Equation(source.Info("gsys_color_blend_alpha_op", "add")));
            GL.BlendFuncSeparate((BlendingFactorSrc)Factor(source.Info("gsys_color_blend_rgb_src_func", "src_alpha")), (BlendingFactorDest)Factor(source.Info("gsys_color_blend_rgb_dst_func", "one_minus_src_alpha")),
                (BlendingFactorSrc)Factor(source.Info("gsys_color_blend_alpha_src_func", "one")), (BlendingFactorDest)Factor(source.Info("gsys_color_blend_alpha_dst_func", "zero")));
            var blend = source.RenderInfos.TryGetValue("gsys_color_blend_const_color", out var info) ? info.Floats : Array.Empty<float>();
            GL.BlendColor(blend.Count > 0 ? blend[0] : 0, blend.Count > 1 ? blend[1] : 0, blend.Count > 2 ? blend[2] : 0, blend.Count > 3 ? blend[3] : 0);
        }
        else GL.Disable(EnableCap.Blend);
    }

    private static BlendEquationMode Equation(string name) => name switch
    {
        "subtract" or "sub" => BlendEquationMode.FuncSubtract, "reverse_subtract" or "reverse_sub" => BlendEquationMode.FuncReverseSubtract,
        "min" => BlendEquationMode.Min, "max" => BlendEquationMode.Max, _ => BlendEquationMode.FuncAdd
    };
    private static BlendingFactor Factor(string name) => name switch
    {
        "zero" => BlendingFactor.Zero, "one" => BlendingFactor.One, "src_color" => BlendingFactor.SrcColor,
        "one_minus_src_color" => BlendingFactor.OneMinusSrcColor, "dst_color" => BlendingFactor.DstColor,
        "one_minus_dst_color" => BlendingFactor.OneMinusDstColor, "src_alpha" => BlendingFactor.SrcAlpha,
        "one_minus_src_alpha" => BlendingFactor.OneMinusSrcAlpha, "dst_alpha" => BlendingFactor.DstAlpha,
        "one_minus_dst_alpha" => BlendingFactor.OneMinusDstAlpha, "constant_color" => BlendingFactor.ConstantColor,
        "one_minus_constant_color" => BlendingFactor.OneMinusConstantColor, "constant_alpha" => BlendingFactor.ConstantAlpha,
        "one_minus_constant_alpha" => BlendingFactor.OneMinusConstantAlpha, "src_alpha_saturate" => BlendingFactor.SrcAlphaSaturate, _ => BlendingFactor.One
    };

    private const string MaterialFragmentShader = """
        #version 330 core
        in vec3 surfaceNormal;
        in vec3 worldPosition;
        in vec2 uv;
        uniform sampler2D albedo, normalMap, mixMap, opacityMap, emissionMap;
        uniform int mapPresent, alphaSource, alphaChannel, alphaFunction;
        uniform int roughnessChannel, metalnessChannel, occlusionChannel;
        uniform bool normalEnabled, normalSigned, normalBc1, roughnessEnabled, metalnessEnabled, occlusionEnabled, emissionEnabled;
        uniform bool diffuseEnabled, specularEnabled, doubleSided, constantColor, unlitMode;
        uniform float roughness, metalness, occlusion, emissionIntensity, forceAlpha, alphaReference;
        uniform vec4 materialTint;
        uniform vec3 cameraPosition, lightDirection, lightColor, skyColor, groundColor;
        out vec4 color;
        bool hasMap(int bit) { return (mapPresent & bit) != 0; }
        void main() {
            vec4 base = hasMap(1) ? texture(albedo,uv) : vec4(0.65,0.7,0.75,1.0);
            base.rgb = constantColor ? materialTint.rgb : base.rgb * materialTint.rgb;
            vec4 mixValue = hasMap(4) ? texture(mixMap,uv) : vec4(1.0);
            vec4 op = hasMap(8) ? texture(opacityMap,uv) : vec4(1.0);
            float alpha = 1.0;
            if (alphaSource == 0) alpha = base[alphaChannel];
            else if (alphaSource == 2) alpha = mixValue[alphaChannel];
            else if (alphaSource == 10) alpha = op[alphaChannel];
            alpha = clamp(alpha * forceAlpha * materialTint.a,0.0,1.0);
            bool pass = alphaFunction == 7 || (alphaFunction == 1 && alpha < alphaReference)
                || (alphaFunction == 2 && alpha == alphaReference) || (alphaFunction == 3 && alpha <= alphaReference)
                || (alphaFunction == 4 && alpha > alphaReference) || (alphaFunction == 5 && alpha != alphaReference)
                || (alphaFunction == 6 && alpha >= alphaReference);
            if (!pass) discard;
            if (unlitMode) {
                color = vec4(pow(max(base.rgb,vec3(0.0)),vec3(1.0/2.2)),alpha);
                return;
            }
            vec3 n = normalize(surfaceNormal);
            if (doubleSided && !gl_FrontFacing) n = -n;
            if (normalEnabled && hasMap(2)) {
                vec4 map = texture(normalMap,uv);
                vec2 xy = normalBc1 ? map.ag : map.rg;
                if (!normalSigned) xy = xy * 2.0 - 1.0;
                vec3 dp1 = dFdx(worldPosition), dp2 = dFdy(worldPosition);
                vec2 du1 = dFdx(uv), du2 = dFdy(uv);
                vec3 q1 = cross(dp2,n), q2 = cross(n,dp1);
                vec3 t = q1 * du1.x + q2 * du2.x, b = q1 * du1.y + q2 * du2.y;
                float norm = max(dot(t,t),dot(b,b));
                if (norm > 1e-12) n = normalize(mat3(t * inversesqrt(norm),b * inversesqrt(norm),n) * vec3(xy,sqrt(max(1.0-dot(xy,xy),0.0))));
            }
            float r = clamp(roughnessEnabled && hasMap(4) ? mixValue[roughnessChannel] : roughness,0.04,1.0);
            float m = clamp(metalnessEnabled && hasMap(4) ? mixValue[metalnessChannel] : metalness,0.0,1.0);
            float ao = clamp(occlusionEnabled && hasMap(4) ? mixValue[occlusionChannel] : occlusion,0.0,1.0);
            // Lit frames are linear HDR in the native programs' units; the tone pass applies exposure.
            vec3 l = normalize(-lightDirection), v = normalize(cameraPosition-worldPosition), h = normalize(l+v);
            float nl = max(dot(n,l),0.0), nv = max(dot(n,v),0.001), nh = max(dot(n,h),0.0);
            vec3 f0 = mix(vec3(0.04),base.rgb,m), f = f0 + (1.0-f0)*pow(1.0-max(dot(v,h),0.0),5.0);
            float a = r*r, a2 = a*a, d = a2 / max(3.14159265 * pow(nh*nh*(a2-1.0)+1.0,2.0),0.00001);
            float k = (r+1.0)*(r+1.0)/8.0, g = nv/(nv*(1.0-k)+k) * nl/(nl*(1.0-k)+k);
            vec3 ambient = mix(groundColor, skyColor, n.y*0.5+0.5) * ao;
            // Native programs also add studio-environment light the fallback lacks; this gain was matched to them by eye.
            vec3 diffuse = diffuseEnabled ? base.rgb * (1.0-m) * (lightColor*nl / 3.14159265 + ambient) * 2.0 : vec3(0.0);
            vec3 specular = specularEnabled ? d*g*f / max(4.0*nv*max(nl,0.001),0.001)*nl * lightColor : vec3(0.0);
            vec3 emission = emissionEnabled && hasMap(16) ? texture(emissionMap,uv).rgb * emissionIntensity : vec3(0.0);
            color = vec4(max(diffuse + specular + emission, vec3(0.0)), alpha);
        }
        """;

    private readonly Dictionary<(BfresMesh Mesh, string Mode), IReadOnlySet<(string Name, int Component)>> materialVisibility = [];

    // Parameters that affect the current preview, so the material pane can tell active and inactive properties apart.
    public IReadOnlySet<(string Name, int Component)> GetMaterialVisibility(BfresMesh mesh)
    {
        bool native = lighting != null && nativeMeshes.ContainsKey(mesh);
        string mode = native ? nativeMeshes[mesh].Program.Source.Key : lighting == null ? "Unlit" : "Material fallback";
        if (materialVisibility.TryGetValue((mesh, mode), out var cached)) return cached;
        if (native)
            cached = NativeMaterialUsage.FindComponents(mesh.Material, nativeMeshes[mesh].Program.Source);
        else
        {
            var source = mesh.Material; var p = new ParkMaterialProfile(source);
            var components = new HashSet<(string, int)>();
            void Add(string name, int count = 1) { for (int i = 0; i < count; i++) components.Add((name, i)); }
            int diffuse = source.Option("material_diffuse_color");
            if (diffuse is >= 100 and <= 105) Add($"const_color{diffuse - 100}", 4);
            Add("material_force_alpha");
            if (p.AlphaFunction != 7) Add("gsys_alpha_test_ref_value");
            if (lighting != null)
            {
                bool mix = p.Mix?.Texture != null && mesh.TexCoords.Length > 0;
                if (!p.RoughnessMap || !mix) Add("material_roughness");
                if (!p.MetalnessMap || !mix) Add("material_metalness");
                if (!p.OcclusionMap || !mix) Add("material_occlusion");
                if (p.EmissionMap && p.Emission?.Texture != null && mesh.TexCoords.Length > 0) Add("material_emission_intensity");
            }
            cached = components;
        }
        materialVisibility[(mesh, mode)] = cached;
        return cached;
    }
}

// Park_UBER options, parameters and render info resolved into what the viewer's material shader and raster state need.
internal sealed class ParkMaterialProfile
{
    public BfresMaterial Source { get; }
    public BfresSampler? Albedo { get; }
    public BfresSampler? Normal { get; }
    public BfresSampler? Mix { get; }
    public BfresSampler? Opacity { get; }
    public BfresSampler? Emission { get; }
    public System.Numerics.Vector4 Tint { get; }
    public float ForceAlpha { get; }
    public int AlphaSource { get; }
    public int AlphaChannel { get; }
    public bool NormalMap { get; }
    public bool RoughnessMap { get; }
    public bool MetalnessMap { get; }
    public bool OcclusionMap { get; }
    public bool EmissionMap { get; }
    public float Roughness { get; }
    public float Metalness { get; }
    public float Occlusion { get; }
    public float EmissionIntensity { get; }
    public bool Translucent { get; }
    public bool DoubleSided => Source.Info("gsys_render_state_display_face", "both") == "both";
    public bool DepthTest { get; }
    public bool DepthWrite { get; }
    public int AlphaFunction { get; }
    public float AlphaReference { get; }
    public int Priority { get; }

    public ParkMaterialProfile(BfresMaterial material)
    {
        Source = material;
        Albedo = material.Sampler("_a0"); Normal = material.Sampler("_n0"); Mix = material.Sampler("_s0");
        Opacity = material.Sampler("_op0"); Emission = material.Sampler("_e0");
        int diffuse = material.Option("material_diffuse_color");
        Tint = diffuse is >= 100 and <= 105 ? material.Color($"const_color{diffuse - 100}", System.Numerics.Vector4.One) : System.Numerics.Vector4.One;
        ForceAlpha = Math.Clamp(material.Parameter("material_force_alpha", 1), 0, 1);
        AlphaSource = material.Option("material_alpha", 1000);
        AlphaChannel = Math.Clamp(material.Option("material_alpha_channel", 3), 0, 3);
        NormalMap = material.IsParkUber && material.Option("enable_normal_map") != 0 && material.Option("material_select_normal", 1) == 1;
        RoughnessMap = material.IsParkUber && material.Option("material_select_roughness") == 2;
        MetalnessMap = material.IsParkUber && material.Option("enable_metal_map") != 0 && material.Option("material_select_metalness") == 2;
        OcclusionMap = material.IsParkUber && material.Option("enable_ambient_occlusion") != 0 && material.Option("material_select_occlusion") == 2;
        EmissionMap = material.IsParkUber && material.Option("enable_emission") != 0 && material.Option("material_emission_color") == 3;
        Roughness = Math.Clamp(material.Parameter("material_roughness", .8f), .04f, 1);
        Metalness = Math.Clamp(material.Parameter("material_metalness", 0), 0, 1);
        Occlusion = Math.Clamp(material.Parameter("material_occlusion", 1), 0, 1);
        EmissionIntensity = Math.Max(0, material.Parameter("material_emission_intensity", 1));
        Translucent = material.Info("gsys_render_state_mode", "opaque") is "translucent" or "custom"
            && material.Info("gsys_render_state_blend_mode", "color") != "none";
        DepthTest = material.Info("gsys_depth_test_enable", "true") == "true";
        DepthWrite = material.Info("gsys_depth_test_write", Translucent ? "false" : "true") == "true";
        bool alphaTest = material.Info("gsys_alpha_test_enable", material.Option("gsys_alpha_test_enable") != 0 ? "true" : "false") == "true";
        AlphaFunction = alphaTest ? Comparison(material.Info("gsys_alpha_test_func", "gequal")) : 7;
        AlphaReference = Math.Clamp(material.Parameter("gsys_alpha_test_ref_value", material.InfoFloat("gsys_alpha_test_value", .5f)), 0, 1);
        Priority = material.RenderInfos.TryGetValue("gsys_priority", out var info) && info.Integers.Count > 0 ? info.Integers[0] : 0;
    }

    public static int Comparison(string value) => value switch
    {
        "never" => 0, "less" => 1, "equal" => 2, "lequal" => 3,
        "greater" => 4, "not_equal" or "notequal" or "nequal" => 5, "gequal" => 6, _ => 7
    };
}
