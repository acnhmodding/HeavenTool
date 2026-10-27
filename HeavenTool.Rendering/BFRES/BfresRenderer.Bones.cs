using HeavenTool.IO.FileFormats.BFRES;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using NVector3 = System.Numerics.Vector3;

namespace HeavenTool.Rendering.BFRES;

public sealed partial class BfresRenderer
{
    private sealed record BoneVisual(BfresBone Bone, Vector3 Head, Vector3 Tail, Vector3? Parent);
    private readonly List<BoneVisual> boneVisuals = [];
    private readonly HashSet<BfresBone> hiddenBones = new(ReferenceEqualityComparer.Instance);
    private BfresBone? selectedBone;
    private bool showBones, boneDirty = true;
    private int boneProgram, boneVao, boneVbo, boneMatrix, boneLineCount, bonePointCount;
    private Matrix4 boneViewProjection;
    public event Action<BfresBone>? BonePicked;
    [System.ComponentModel.DefaultValue(false)]
    public bool ShowBones { get => showBones; set { showBones = value; Invalidate(); } }

    public void SetSkeletons(IEnumerable<IReadOnlyList<BfresBone>> skeletons)
    {
        hiddenBones.Clear(); selectedBone = null; boneVisuals.Clear();
        static Vector3 V(NVector3 p) => new(p.X, p.Y, p.Z);
        foreach (var bones in skeletons)
        {
            var children = bones.Where(b => b.ParentIndex >= 0).ToLookup(b => b.ParentIndex);
            foreach (var bone in bones)
            {
                var head = V(bone.Position);
                // BFRES stores joint transforms, not Blender head/tail lengths. Infer display tails.
                var axis = V(NVector3.TransformNormal(NVector3.UnitY, bone.WorldTransform));
                if (axis.LengthSquared < .00000001f) axis = Vector3.UnitY;
                // At branches, follow the incoming chain rather than the nearest side joint.
                var continuation = axis;
                int ancestor = bone.ParentIndex;
                while (ancestor >= 0)
                {
                    var incoming = head - V(bones[ancestor].Position);
                    if (incoming.LengthSquared > .00000001f) { continuation = incoming; break; }
                    ancestor = bones[ancestor].ParentIndex;
                }
                var chainDirection = new NVector3(continuation.X, continuation.Y, continuation.Z);
                chainDirection = NVector3.Normalize(chainDirection);
                // A zero-offset branch has no incoming segment of its own. Use its local X
                // frame for branch inference, rather than borrowing another chain's direction.
                var branchDirection = chainDirection;
                if (bone.ParentIndex >= 0 && NVector3.DistanceSquared(bone.Position, bones[bone.ParentIndex].Position) <= .00000001f)
                {
                    var forward = NVector3.TransformNormal(NVector3.UnitX, bone.WorldTransform);
                    if (forward.LengthSquared() > .00000001f) branchDirection = NVector3.Normalize(forward);
                }
                var candidates = children[bone.Index].Where(b => NVector3.DistanceSquared(b.Position, bone.Position) > .00000001f)
                    .OrderByDescending(b => NVector3.Dot(branchDirection, NVector3.Normalize(b.Position - bone.Position)))
                    .ThenBy(b => NVector3.DistanceSquared(b.Position, bone.Position)).ToArray();
                var child = candidates.FirstOrDefault();
                bool ambiguousBranch = candidates.Length > 1 && NVector3.Dot(branchDirection, NVector3.Normalize(child!.Position - bone.Position)) < .5f;
                if (ambiguousBranch) child = null;
                // A leaf has no stored BFRES endpoint. Continue its chain instead of assuming
                // local +Y is the bone's forward axis (mirrored joints may have opposite axes).
                var markerDirection = ambiguousBranch ? V(branchDirection) : continuation.Normalized();
                var tail = child != null ? V(child.Position) : head + markerDirection * Math.Max(pending.Radius * .035f, .001f);
                Vector3? parent = bone.ParentIndex >= 0 ? V(bones[bone.ParentIndex].Position) : null;
                boneVisuals.Add(new(bone, head, tail, parent));
            }
        }
        boneDirty = true; Invalidate();
    }
    public void SelectBone(BfresBone? bone) { selectedBone = bone; boneDirty = true; Invalidate(); }
    public void SetBoneVisible(BfresBone bone, bool visible)
    {
        if (visible) hiddenBones.Remove(bone); else hiddenBones.Add(bone);
        boneDirty = true; Invalidate();
    }
    public void FrameSelectedBone()
    {
        var bone = boneVisuals.FirstOrDefault(b => ReferenceEquals(b.Bone, selectedBone));
        if (bone == null) return;
        center = (bone.Head + bone.Tail) * .5f;
        FrameBounds((bone.Tail - bone.Head).Length * .5f, Math.Max((bone.Tail - bone.Head).Length * 5, radius * .2f));
        Invalidate();
    }
    public void FrameSelection()
    {
        if (selectedBone != null && showBones) FrameSelectedBone();
        else FrameSelectedMesh();
    }
    private void UploadBones()
    {
        var lines = new List<float>(); var points = new List<float>();
        void Vertex(List<float> output, Vector3 p, Vector3 c) { output.AddRange([p.X, p.Y, p.Z, c.X, c.Y, c.Z]); }
        void Line(Vector3 a, Vector3 b, Vector3 color) { Vertex(lines, a, color); Vertex(lines, b, color); }
        foreach (var visual in boneVisuals.OrderBy(b => ReferenceEquals(b.Bone, selectedBone) ? 1 : 0))
        {
            if (hiddenBones.Contains(visual.Bone)) continue;
            Vector3 color = ReferenceEquals(visual.Bone, selectedBone) ? new(1, .55f, .05f) : new(.35f, .8f, .95f);
            var delta = visual.Tail - visual.Head;
            var direction = delta.Normalized();
            var side = Vector3.Cross(direction, Math.Abs(direction.Y) < .9f ? Vector3.UnitY : Vector3.UnitX).Normalized();
            var up = Vector3.Cross(direction, side);
            float width = Math.Min(delta.Length * .15f, pending.Radius * .025f);
            var middle = visual.Head + delta * .22f;
            Vector3[] ring = [middle + side * width, middle + up * width, middle - side * width, middle - up * width];
            for (int i = 0; i < 4; i++)
            {
                Line(visual.Head, ring[i], color); Line(ring[i], visual.Tail, color); Line(ring[i], ring[(i + 1) % 4], color);
            }
            if (visual.Parent is Vector3 parent && (parent - visual.Head).LengthSquared > .00000001f)
                Line(parent, visual.Head, color * .65f);
            Vertex(points, visual.Head, color); Vertex(points, visual.Tail, color);
        }
        boneLineCount = lines.Count / 6; bonePointCount = points.Count / 6;
        lines.AddRange(points);
        GL.BindVertexArray(boneVao); GL.BindBuffer(BufferTarget.ArrayBuffer, boneVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, lines.Count * sizeof(float), lines.ToArray(), BufferUsageHint.DynamicDraw);
        boneDirty = false;
    }
    private void DrawBones(Matrix4 matrix)
    {
        boneViewProjection = matrix;
        if (!showBones || boneVisuals.Count == 0) return;
        if (boneProgram == 0)
        {
            boneProgram = CreateProgram("""
                    #version 330 core
                    layout(location=0) in vec3 position; layout(location=1) in vec3 tint;
                    uniform mat4 viewProjection; out vec3 vertexColor;
                    void main() { gl_Position=viewProjection*vec4(position,1); vertexColor=tint; }
                    """, """
                    #version 330 core
                    in vec3 vertexColor; out vec4 color;
                    void main() { color=vec4(vertexColor,1); }
                    """);
            boneMatrix = GL.GetUniformLocation(boneProgram, "viewProjection");
            boneVao = GL.GenVertexArray(); boneVbo = GL.GenBuffer();
            GL.BindVertexArray(boneVao); GL.BindBuffer(BufferTarget.ArrayBuffer, boneVbo);
            GL.EnableVertexAttribArray(0); GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 24, 0);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 24, 12);
        }
        if (boneDirty) UploadBones();

        GL.Disable(EnableCap.FramebufferSrgb); GL.Disable(EnableCap.Blend); GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.DepthTest);
        GL.DepthMask(false); GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
        GL.UseProgram(boneProgram); GL.UniformMatrix4(boneMatrix, false, ref matrix);
        GL.BindVertexArray(boneVao);
        GL.LineWidth(1); GL.DrawArrays(PrimitiveType.Lines, 0, boneLineCount);
        GL.PointSize(5 * renderScale); GL.DrawArrays(PrimitiveType.Points, boneLineCount, bonePointCount); GL.PointSize(1);
        GL.DepthMask(true);
    }
    private bool PickBone(Vector2 point)
    {
        if (!showBones) return false;
        bool Project(Vector3 p, out Vector2 screen, out float depth)
        {
            var clip = Vector4.TransformRow(new Vector4(p, 1), boneViewProjection);
            screen = new((clip.X / clip.W + 1) * width * .5f, (1 - clip.Y / clip.W) * height * .5f);
            depth = (clip.Z / clip.W + 1) * .5f;
            return clip.W > 0 && clip.Z >= -clip.W && clip.Z <= clip.W;
        }
        BfresBone? picked = null; float best = 100 * renderScale * renderScale;
        var cursor = new Vector2(point.X, point.Y);
        foreach (var bone in boneVisuals)
        {
            if (hiddenBones.Contains(bone.Bone) || !Project(bone.Head, out var a, out float depthA) || !Project(bone.Tail, out var b, out float depthB)) continue;
            var segment = b - a;
            float t = segment.LengthSquared > 0 ? Math.Clamp(Vector2.Dot(cursor - a, segment) / segment.LengthSquared, 0, 1) : 0;
            float distanceSquared = (cursor - (a + segment * t)).LengthSquared;
            if (distanceSquared < best) { best = distanceSquared; picked = bone.Bone; }
        }
        if (picked == null) return false;
        SelectBone(picked); BonePicked?.Invoke(picked); return true;
    }
    private void ReleaseBones()
    {
        if (boneVbo != 0) GL.DeleteBuffer(boneVbo); if (boneVao != 0) GL.DeleteVertexArray(boneVao);
        if (boneProgram != 0) GL.DeleteProgram(boneProgram);
        boneVbo = boneVao = boneProgram = 0; boneDirty = true;
    }
}
