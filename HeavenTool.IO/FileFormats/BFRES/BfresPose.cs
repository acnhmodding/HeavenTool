using System.Numerics;

namespace HeavenTool.IO.FileFormats.BFRES;

public static class BfresPose
{
    /// <summary>
    /// The model held at <paramref name="frame"/> of <paramref name="animation"/>. Each curve sets its channel of the bone's
    /// local scale, Euler rotation or translation; the channels it leaves alone keep the bind pose's.
    /// </summary>
    public static BfresModel Posed(this BfresModel model, BfresSkeletalAnimation animation, float frame)
    {
        var bones = model.Bones;
        var curves = animation.Bones.ToDictionary(bone => bone.Name, bone => bone.Curves);
        var posed = new Matrix4x4[bones.Count];
        var moves = new Matrix4x4[bones.Count];
        for (int i = 0; i < bones.Count; i++)
        {
            var bone = bones[i];
            if (bone.ParentIndex >= i) throw new InvalidDataException("Bones must follow their parents.");
            var parentBind = bone.ParentIndex < 0 ? Matrix4x4.Identity : bones[bone.ParentIndex].WorldTransform;
            var parentPosed = bone.ParentIndex < 0 ? Matrix4x4.Identity : posed[bone.ParentIndex];
            if (!Matrix4x4.Invert(parentBind, out var inverseParent) || !Matrix4x4.Invert(bone.WorldTransform, out var inverseBind))
                throw new InvalidDataException("Singular bone transform.");

            var local = bone.WorldTransform * inverseParent;
            if (curves.TryGetValue(bone.Name, out var animated)) local = Animated(local, animated, frame);
            posed[i] = local * parentPosed;
            moves[i] = inverseBind * posed[i];
        }
        return model with
        {
            Meshes = [.. model.Meshes.Select(mesh => mesh.Posed(moves))],
            Bones = [.. bones.Select(bone => bone with { WorldTransform = posed[bone.Index] })],
        };
    }

    private static Matrix4x4 Animated(Matrix4x4 local, IReadOnlyList<BfresAnimationCurve> curves, float frame)
    {
        if (!Matrix4x4.Decompose(local, out var scale, out var rotation, out var translation))
            throw new InvalidDataException("Bone transform has no scale, rotation and translation.");
        if (curves.Any(curve => curve.Channel == BfresAnimationChannel.RotationW))
            throw new NotSupportedException("Quaternion bone animations are not supported.");

        // The reader's X * Y * Z Euler order: Z = atan2(M12, M11), Y = asin(-M13), X = atan2(M23, M33).
        var m = Matrix4x4.CreateFromQuaternion(rotation);
        var euler = new Vector3(MathF.Atan2(m.M23, m.M33), MathF.Asin(Math.Clamp(-m.M13, -1, 1)), MathF.Atan2(m.M12, m.M11));
        foreach (var curve in curves)
        {
            float value = curve.Evaluate(frame);
            switch (curve.Channel)
            {
                case BfresAnimationChannel.ScaleX: scale.X = value; break;
                case BfresAnimationChannel.ScaleY: scale.Y = value; break;
                case BfresAnimationChannel.ScaleZ: scale.Z = value; break;
                case BfresAnimationChannel.RotationX: euler.X = value; break;
                case BfresAnimationChannel.RotationY: euler.Y = value; break;
                case BfresAnimationChannel.RotationZ: euler.Z = value; break;
                case BfresAnimationChannel.TranslationX: translation.X = value; break;
                case BfresAnimationChannel.TranslationY: translation.Y = value; break;
                case BfresAnimationChannel.TranslationZ: translation.Z = value; break;
            }
        }
        return Matrix4x4.CreateScale(scale) * Matrix4x4.CreateRotationX(euler.X) * Matrix4x4.CreateRotationY(euler.Y)
            * Matrix4x4.CreateRotationZ(euler.Z) * Matrix4x4.CreateTranslation(translation);
    }
}
