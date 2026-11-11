using System.Numerics;

namespace HeavenTool.IO.FileFormats.BFRES;

// Channel offsets in a NintendoWare skeletal animation result, independent of any game or bone name.
public enum BfresAnimationChannel : uint
{
    ScaleX = 0, ScaleY = 4, ScaleZ = 8,
    TranslationX = 16, TranslationY = 20, TranslationZ = 24,
    RotationX = 32, RotationY = 36, RotationZ = 40, RotationW = 44,
}

public sealed record BfresSkeletalAnimation(string Name, int FrameCount, IReadOnlyList<BfresBoneAnimation> Bones);

/// <summary>
/// A bone's channels in an animation: its curves, and the base values it holds where no curve moves it. A one-frame pose
/// keeps all of its values in the base.
/// </summary>
public sealed record BfresBoneAnimation(string Name, IReadOnlyList<BfresAnimationCurve> Curves)
{
    public Vector3? BaseScale { get; init; }
    /// <summary>Euler angles in radians (X, Y, Z), or a quaternion when the animation rotates by quaternions.</summary>
    public Vector4? BaseRotation { get; init; }
    public Vector3? BaseTranslation { get; init; }
}

/// <summary>A normalized cubic scalar curve decoded from a BFRES animation.</summary>
public sealed class BfresAnimationCurve
{
    private readonly float[] frames;
    private readonly Vector4[] coefficients;
    private readonly float start, end, scale, offset;

    public BfresAnimationChannel Channel { get; }

    internal BfresAnimationCurve(BfresAnimationChannel channel, float[] frames, Vector4[] coefficients,
        float start, float end, float scale, float offset)
    {
        Channel = channel;
        this.frames = frames;
        this.coefficients = coefficients;
        this.start = start;
        this.end = end;
        this.scale = scale;
        this.offset = offset;
    }

    public float Evaluate(float frame)
    {
        if (!float.IsFinite(frame)) throw new ArgumentOutOfRangeException(nameof(frame));

        frame = Math.Clamp(frame, start, end);
        int key = Array.BinarySearch(frames, frame);
        if (key < 0) key = ~key - 1;
        key = Math.Clamp(key, 0, frames.Length - 2);

        float t = (frame - frames[key]) / (frames[key + 1] - frames[key]);
        var c = coefficients[key];
        return offset + scale * (c.X + t * c.Y + t * t * (c.Z + t * c.W));
    }
}
