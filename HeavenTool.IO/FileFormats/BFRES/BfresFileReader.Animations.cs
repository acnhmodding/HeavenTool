using System.Numerics;

namespace HeavenTool.IO.FileFormats.BFRES;

public sealed partial class BfresFileReader
{
    private const uint BfresVersion9 = 0x90000;
    private const uint FskaMagic = 0x414b5346;
    private const int SkeletalAnimationSize = 80, BoneAnimationSize = 56, AnimationCurveSize = 48;
    // Verified encodings: byte frames with signed 16-bit or signed 8-bit normalized cubic coefficients, or with float cubic
    // coefficients stored unscaled (scale 1, offset 0), clamped endpoints.
    // Other encodings/wrap modes need their own decoder; they are not silently interpreted as these layouts.
    private const ushort FloatCurveEncoding = 2, Int16CurveEncoding = 6, SByteCurveEncoding = 10;
    private const int CoefficientsPerKey = 4;
    // A bone animation's flags: which base values it stores.
    private const uint BaseScaleFlag = 1 << 3, BaseRotationFlag = 1 << 4, BaseTranslationFlag = 1 << 5;
    private const uint BaseFlags = BaseScaleFlag | BaseRotationFlag | BaseTranslationFlag;

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
        // Base values follow one another in this order, each present when its flag is set.
        uint flags = reader.ReadUInt32At(at + 40);
        long data = reader.ReadPointerAt(at + 16);
        Vector3? scale = null, translation = null;
        Vector4? rotation = null;
        if ((flags & BaseFlags) != 0 && data == 0) throw new InvalidDataException("Bone animation has base flags but no base values.");
        if ((flags & BaseScaleFlag) != 0) { scale = ReadAnimationVector3(data); data += 12; }
        if ((flags & BaseRotationFlag) != 0) { rotation = new(ReadAnimationVector3(data), reader.ReadSingleAt(data + 12)); data += 16; }
        if ((flags & BaseTranslationFlag) != 0) translation = ReadAnimationVector3(data);
        return new(reader.ReadNameAt(at), curves) { BaseScale = scale, BaseRotation = rotation, BaseTranslation = translation };
    }

    private Vector3 ReadAnimationVector3(long at)
    {
        CheckAnimationRange(at, 12);
        return new(reader.ReadSingleAt(at), reader.ReadSingleAt(at + 4), reader.ReadSingleAt(at + 8));
    }

    private BfresAnimationCurve ReadAnimationCurve(long at)
    {
        ushort encoding = reader.ReadUInt16At(at + 16);
        if (encoding is not (FloatCurveEncoding or Int16CurveEncoding or SByteCurveEncoding)) throw new NotSupportedException($"Unsupported animation curve encoding 0x{encoding:x4}.");

        var channel = (BfresAnimationChannel)reader.ReadUInt32At(at + 20);
        if (!Enum.IsDefined(channel)) throw new NotSupportedException($"Unsupported skeletal animation channel 0x{(uint)channel:x}.");

        int count = reader.ReadUInt16At(at + 18);
        if (count < 2) throw new NotSupportedException("Cubic animation curves require at least two keys.");

        float[] frames = ReadCurveFrames(reader.ReadPointerAt(at), count);
        Vector4[] coefficients = encoding == FloatCurveEncoding
            ? ReadFloatCurveCoefficients(reader.ReadPointerAt(at + 8), count)
            : ReadCurveCoefficients(reader.ReadPointerAt(at + 8), count, encoding == SByteCurveEncoding);

        float start = reader.ReadSingleAt(at + 24), end = reader.ReadSingleAt(at + 28);
        float scale = reader.ReadSingleAt(at + 32), offset = reader.ReadSingleAt(at + 36);
        if (!float.IsFinite(start) || !float.IsFinite(end) || !float.IsFinite(scale) || !float.IsFinite(offset))
            throw new InvalidDataException("Nonfinite animation curve parameters.");
        if (start != frames[0] || end != frames[^1]) throw new NotSupportedException("Unsupported animation curve frame range.");
        if (encoding == FloatCurveEncoding && (scale != 1 || offset != 0))
            throw new NotSupportedException("Unsupported scaled float animation curve.");

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

    private Vector4[] ReadCurveCoefficients(long at, int count, bool bytes)
    {
        int length = count * CoefficientsPerKey;
        CheckAnimationRange(at, length * (bytes ? sizeof(sbyte) : sizeof(short)));
        short[] values = bytes
            ? Array.ConvertAll(reader.ReadByteArrayAt(at, length), value => (short)(sbyte)value)
            : reader.ReadInt16ArrayAt(at, length);
        var coefficients = new Vector4[count];
        for (int i = 0; i < count; i++)
        {
            int key = i * CoefficientsPerKey;
            coefficients[i] = new(values[key], values[key + 1], values[key + 2], values[key + 3]);
        }
        return coefficients;
    }

    private Vector4[] ReadFloatCurveCoefficients(long at, int count)
    {
        CheckAnimationRange(at, count * CoefficientsPerKey * sizeof(float));
        var coefficients = new Vector4[count];
        for (int i = 0; i < count; i++)
        {
            long key = at + i * CoefficientsPerKey * sizeof(float);
            coefficients[i] = new(reader.ReadSingleAt(key), reader.ReadSingleAt(key + 4), reader.ReadSingleAt(key + 8), reader.ReadSingleAt(key + 12));
        }
        return coefficients;
    }

    private void CheckAnimationRange(long at, int length)
    {
        if (at < 0 || length < 0 || at > reader.Length - length || length > 0 && at == 0)
            throw new InvalidDataException("Invalid BFRES animation data range.");
    }
}
