using System.Numerics;

namespace HeavenTool.IO.FileFormats.BFRES;

/// <summary>An animation played at <paramref name="Frame"/>, on <paramref name="Only"/> those bones when set.</summary>
public sealed record BfresPoseLayer(BfresSkeletalAnimation Animation, float Frame, IReadOnlySet<string>? Only = null);

public static class BfresPose
{
    /// <summary>
    /// The model held at <paramref name="frame"/> of <paramref name="animation"/>. A bone's base values, then its curves, set
    /// its local scale, Euler rotation or translation; the channels the animation leaves alone keep the bind pose's. With
    /// <paramref name="retargetFrom"/>, the skeleton the animation was made on, its translations are retargeted the way
    /// nn::g3d does for a host skeleton: each one turns and stretches by the quaternion that takes that skeleton's bind
    /// translation of the bone onto this model's. With <paramref name="only"/>, the animation moves just those bones, the
    /// way a game layer that masks the rest off does; the others keep the bind pose.
    /// </summary>
    public static BfresModel Posed(this BfresModel model, BfresSkeletalAnimation animation, float frame, IReadOnlyList<BfresBone>? retargetFrom = null,
        IReadOnlySet<string>? only = null) => model.Posed([new BfresPoseLayer(animation, frame, only)], retargetFrom);

    /// <summary>
    /// The model held by <paramref name="layers"/>, as <see cref="Posed(BfresModel, BfresSkeletalAnimation, float, IReadOnlyList{BfresBone}?, IReadOnlySet{string}?)"/>
    /// does for one. Each bone takes the last layer whose mask lets it through and that animates it, from the bind pose.
    /// </summary>
    public static BfresModel Posed(this BfresModel model, IReadOnlyList<BfresPoseLayer> layers, IReadOnlyList<BfresBone>? retargetFrom = null)
    {
        var bones = model.Bones;
        var animated = layers.Select(layer => layer.Animation.Bones.ToDictionary(bone => bone.Name)).ToArray();
        var hosts = retargetFrom == null ? null : LocalTranslations(retargetFrom);
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
            for (int k = layers.Count - 1; k >= 0; k--)
            {
                if (layers[k].Only is { } only && !only.Contains(bone.Name) || !animated[k].TryGetValue(bone.Name, out var channels)) continue;
                Quaternion? retarget = hosts == null ? null
                    : hosts.TryGetValue(bone.Name, out var host) ? Retarget(host, local.Translation) : Quaternion.Identity;
                local = Animated(local, channels, layers[k].Frame, retarget);
                break;
            }
            posed[i] = local * parentPosed;
            moves[i] = inverseBind * posed[i];
        }
        return model with
        {
            Meshes = [.. model.Meshes.Select(mesh => mesh.Posed(moves))],
            Bones = [.. bones.Select(bone => bone with { WorldTransform = posed[bone.Index] })],
        };
    }

    private static Dictionary<string, Vector3> LocalTranslations(IReadOnlyList<BfresBone> bones)
    {
        var translations = new Dictionary<string, Vector3>();
        foreach (var bone in bones)
        {
            var parent = bone.ParentIndex < 0 ? Matrix4x4.Identity : bones[bone.ParentIndex].WorldTransform;
            if (!Matrix4x4.Invert(parent, out var inverseParent)) throw new InvalidDataException("Singular bone transform.");
            translations.TryAdd(bone.Name, (bone.WorldTransform * inverseParent).Translation);
        }
        return translations;
    }

    /// <summary>
    /// nn::g3d's retarget quaternion for a bone (SkeletalAnimObj's host bind): the turn from the host's bind translation
    /// <paramref name="host"/> onto the target's <paramref name="target"/>, scaled so q·v·q̄ also stretches by |t| / |h|.
    /// Either length being zero leaves the identity.
    /// </summary>
    private static Quaternion Retarget(Vector3 host, Vector3 target)
    {
        float hostLength = host.Length(), targetLength = target.Length();
        if (hostLength == 0 || targetLength == 0) return Quaternion.Identity;
        float stretch = MathF.Sqrt(targetLength / hostLength);
        Vector3 from = host / hostLength, to = target / targetLength;
        float twice = 2 * (Vector3.Dot(from, to) + 1);
        // Exactly opposite directions take a separate branch that reads a sine table this port hasn't confirmed.
        if (twice < 1.1920929e-07f) throw new NotSupportedException("A retargeted bone points opposite its host bone.");
        float root = MathF.Sqrt(twice);
        var axis = Vector3.Cross(from, to) * (stretch / root);
        return new(axis, stretch * 0.5f * root);
    }

    private static Matrix4x4 Animated(Matrix4x4 local, BfresBoneAnimation channels, float frame, Quaternion? retarget)
    {
        if (!Matrix4x4.Decompose(local, out var scale, out var rotation, out var translation))
            throw new InvalidDataException("Bone transform has no scale, rotation and translation.");
        var curves = channels.Curves;
        if (curves.Any(curve => curve.Channel == BfresAnimationChannel.RotationW))
            throw new NotSupportedException("Quaternion bone animations are not supported.");

        // The reader's X * Y * Z Euler order: Z = atan2(M12, M11), Y = asin(-M13), X = atan2(M23, M33).
        var m = Matrix4x4.CreateFromQuaternion(rotation);
        var euler = new Vector3(MathF.Atan2(m.M23, m.M33), MathF.Asin(Math.Clamp(-m.M13, -1, 1)), MathF.Atan2(m.M12, m.M11));
        if (channels.BaseScale is { } baseScale) scale = baseScale;
        if (channels.BaseRotation is { } baseRotation) euler = new(baseRotation.X, baseRotation.Y, baseRotation.Z);
        if (channels.BaseTranslation is { } baseTranslation) translation = baseTranslation;
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
        // Retargeting turns every translation the animation sets, base or curve; one it leaves at the bind pose stays.
        bool moved = channels.BaseTranslation != null || curves.Any(curve => curve.Channel is BfresAnimationChannel.TranslationX
            or BfresAnimationChannel.TranslationY or BfresAnimationChannel.TranslationZ);
        if (retarget is { } q && moved) translation = Turn(q, translation);
        return Matrix4x4.CreateScale(scale) * Matrix4x4.CreateRotationX(euler.X) * Matrix4x4.CreateRotationY(euler.Y)
            * Matrix4x4.CreateRotationZ(euler.Z) * Matrix4x4.CreateTranslation(translation);
    }

    // q·v·q̄ with q not normalized: the turn of the unit quaternion, stretched by |q|².
    private static Vector3 Turn(Quaternion q, Vector3 v)
    {
        float lengthSquared = q.LengthSquared();
        return lengthSquared == 0 ? Vector3.Zero : Vector3.Transform(v, Quaternion.Normalize(q)) * lengthSquared;
    }
}
