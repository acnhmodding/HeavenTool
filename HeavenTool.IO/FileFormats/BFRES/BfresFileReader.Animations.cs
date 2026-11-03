using System.Numerics;

namespace HeavenTool.IO.FileFormats.BFRES;

public sealed partial class BfresFileReader
{
    private const uint BfresVersion9 = 0x90000;
    private const uint FskaMagic = 0x414b5346;
    private const int SkeletalAnimationSize = 80, BoneAnimationSize = 56, AnimationCurveSize = 48;
    // Verified encoding: byte frames, signed 16-bit normalized cubic coefficients, clamped endpoints.
    // Other encodings/wrap modes need their own decoder; they are not silently interpreted as this layout.
    private const ushort SupportedCurveEncoding = 6;
    private const int CoefficientsPerKey = 4;

    private IReadOnlyList<BfresSkeletalAnimation> ReadSkeletalAnimations()
    {
        int count = reader.ReadUInt16At(226);
        if (count == 0) return [];

        if (reader.ReadUInt32At(8) != BfresVersion9)
        {
            warnings.Add("Skeletal animations require BFRES v9.");
            return [];
        }

        long array = reader.ReadPointerAt(88);
        CheckAnimationRange(array, count * SkeletalAnimationSize);

        var animations = new List<BfresSkeletalAnimation>();
        for (int i = 0; i < count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            long at = array + i * SkeletalAnimationSize;
            string name = reader.ReadNameAt(at + 8);
            try
            {
                animations.Add(ReadSkeletalAnimation(at, name));
            }
            catch (NotSupportedException ex)
            {
                // Unsupported playback must not prevent the document's static models from loading.
                warnings.Add($"Animation {name}: {ex.Message}");
            }
        }
        return animations;
    }

    private BfresSkeletalAnimation ReadSkeletalAnimation(long at, string name)
    {
        if (reader.ReadUInt32At(at) != FskaMagic) throw new InvalidDataException("Expected FSKA animation.");

        int frameCount = reader.ReadInt32At(at + 64);
        if (frameCount < 0) throw new InvalidDataException("Negative skeletal animation frame count.");

        int boneCount = reader.ReadUInt16At(at + 76);
        long array = reader.ReadPointerAt(at + 40);
        CheckAnimationRange(array, boneCount * BoneAnimationSize);

        var bones = new BfresBoneAnimation[boneCount];
        for (int i = 0; i < boneCount; i++) bones[i] = ReadBoneAnimation(array + i * BoneAnimationSize);
        return new(name, frameCount, bones);
    }

    private BfresBoneAnimation ReadBoneAnimation(long at)
    {
        int count = reader.ReadByteAt(at + 46);
        long array = reader.ReadPointerAt(at + 8);
        CheckAnimationRange(array, count * AnimationCurveSize);

        var curves = new BfresAnimationCurve[count];
        var channels = new HashSet<BfresAnimationChannel>();
        for (int i = 0; i < count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            curves[i] = ReadAnimationCurve(array + i * AnimationCurveSize);
            if (!channels.Add(curves[i].Channel)) throw new InvalidDataException("Duplicate bone animation channel.");
        }
        return new(reader.ReadNameAt(at), curves);
    }

    private BfresAnimationCurve ReadAnimationCurve(long at)
    {
        ushort encoding = reader.ReadUInt16At(at + 16);
        if (encoding != SupportedCurveEncoding) throw new NotSupportedException($"Unsupported animation curve encoding 0x{encoding:x4}.");

        var channel = (BfresAnimationChannel)reader.ReadUInt32At(at + 20);
        if (!Enum.IsDefined(channel)) throw new NotSupportedException($"Unsupported skeletal animation channel 0x{(uint)channel:x}.");

        int count = reader.ReadUInt16At(at + 18);
        if (count < 2) throw new NotSupportedException("Cubic animation curves require at least two keys.");

        float[] frames = ReadCurveFrames(reader.ReadPointerAt(at), count);
        Vector4[] coefficients = ReadCurveCoefficients(reader.ReadPointerAt(at + 8), count);

        float start = reader.ReadSingleAt(at + 24), end = reader.ReadSingleAt(at + 28);
        float scale = reader.ReadSingleAt(at + 32), offset = reader.ReadSingleAt(at + 36);
        if (!float.IsFinite(start) || !float.IsFinite(end) || !float.IsFinite(scale) || !float.IsFinite(offset))
            throw new InvalidDataException("Nonfinite animation curve parameters.");
        if (start != frames[0] || end != frames[^1]) throw new NotSupportedException("Unsupported animation curve frame range.");

        return new(channel, frames, coefficients, start, end, scale, offset);
    }

    private float[] ReadCurveFrames(long at, int count)
    {
        CheckAnimationRange(at, count);
        float[] frames = Array.ConvertAll(reader.ReadByteArrayAt(at, count), value => (float)value);
        for (int i = 1; i < count; i++)
            if (frames[i] <= frames[i - 1]) throw new InvalidDataException("Unordered animation curve frames.");
        return frames;
    }

    private Vector4[] ReadCurveCoefficients(long at, int count)
    {
        CheckAnimationRange(at, count * CoefficientsPerKey * sizeof(short));
        short[] values = reader.ReadInt16ArrayAt(at, count * CoefficientsPerKey);
        var coefficients = new Vector4[count];
        for (int i = 0; i < count; i++)
        {
            int key = i * CoefficientsPerKey;
            coefficients[i] = new(values[key], values[key + 1], values[key + 2], values[key + 3]);
        }
        return coefficients;
    }

    private void CheckAnimationRange(long at, int length)
    {
        if (at < 0 || length < 0 || at > reader.Length - length || length > 0 && at == 0)
            throw new InvalidDataException("Invalid BFRES animation data range.");
    }
}
