using static HeavenTool.IO.Audio.DspAdpcm.DspAdpcmCodec;

namespace HeavenTool.IO.Audio.DspAdpcm;

public static class DspAdpcmEncoder
{
    private const int MaxScalePower = 12;

    public static byte[] Encode(ReadOnlySpan<short> samples, ReadOnlySpan<short> coefficients, short history1 = 0, short history2 = 0)
    {
        ValidateCoefficients(coefficients);
        var data = new byte[GetByteCount(samples.Length)];
        Span<short> frame = stackalloc short[SamplesPerFrame + 2];
        Span<byte> encodedFrame = stackalloc byte[BytesPerFrame];
        Span<int> decoded = stackalloc int[SamplesPerFrame + 2];
        Span<int> nibbles = stackalloc int[SamplesPerFrame];
        frame[0] = history2;
        frame[1] = history1;

        for (int position = 0, frameOffset = 0; position < samples.Length; frameOffset += BytesPerFrame)
        {
            int frameSamples = Math.Min(SamplesPerFrame, samples.Length - position);
            samples.Slice(position, frameSamples).CopyTo(frame[2..]);
            // Predictor selection includes the zero-padded end of a partial frame.
            frame[(frameSamples + 2)..].Clear();
            EncodeFrame(frame, encodedFrame, coefficients, decoded, nibbles);
            encodedFrame[..GetByteCount(frameSamples)].CopyTo(data.AsSpan(frameOffset));
            position += frameSamples;
        }
        return data;
    }

    private static void EncodeFrame(Span<short> samples, Span<byte> output, ReadOnlySpan<short> coefficients, Span<int> decoded, Span<int> nibbles)
    {
        double bestError = double.MaxValue;
        short nextHistory1 = 0, nextHistory2 = 0;
        for (int predictor = 0; predictor < PredictorCount; predictor++)
        {
            EncodePredictor(samples, coefficients[predictor * 2], coefficients[predictor * 2 + 1],
                decoded, nibbles, out int scalePower, out double error);
            if (error >= bestError) continue;
            bestError = error;
            output[0] = PackNibbles(predictor, scalePower);
            for (int i = 1; i < BytesPerFrame; i++)
                output[i] = PackNibbles(nibbles[(i - 1) * 2], nibbles[(i - 1) * 2 + 1]);
            nextHistory2 = (short)decoded[SamplesPerFrame];
            nextHistory1 = (short)decoded[SamplesPerFrame + 1];
        }
        samples[0] = nextHistory2;
        samples[1] = nextHistory1;
    }

    private static byte PackNibbles(int high, int low) => (byte)((high << 4) | (low & 0xf));

    private static void EncodePredictor(ReadOnlySpan<short> samples, short coefficient1, short coefficient2, Span<int> decoded, Span<int> nibbles, out int scalePower, out double error)
    {
        decoded[0] = samples[0];
        decoded[1] = samples[1];
        int maxDistance = 0;
        for (int i = 0; i < SamplesPerFrame; i++)
        {
            int prediction = (samples[i] * coefficient2 + samples[i + 1] * coefficient1) / CoefficientScale;
            int distance = ClampSample(samples[i + 2] - prediction);
            if (Math.Abs(distance) > Math.Abs(maxDistance)) maxDistance = distance;
        }

        scalePower = 0;
        while (scalePower <= MaxScalePower && (maxDistance > 7 || maxDistance < -8))
        {
            maxDistance /= 2;
            scalePower++;
        }
        scalePower = scalePower <= 1 ? -1 : scalePower - 2;

        int maxOverflow;
        do
        {
            scalePower++;
            int scale = (1 << scalePower) * CoefficientScale;
            error = 0;
            maxOverflow = 0;
            for (int i = 0; i < SamplesPerFrame; i++)
            {
                int prediction = decoded[i] * coefficient2 + decoded[i + 1] * coefficient1;
                int distance = samples[i + 2] * CoefficientScale - prediction;
                // Preserve the encoder's float division, double promotion and rounding order.
                int quantized = distance > 0
                    ? (int)((double)((float)distance / scale) + 0.4999999f)
                    : (int)((double)((float)distance / scale) - 0.4999999f);
                int nibble = Math.Clamp(quantized, -8, 7);
                maxOverflow = Math.Max(maxOverflow, Math.Abs(quantized - nibble));
                nibbles[i] = nibble;
                decoded[i + 2] = ClampSample((prediction + nibble * scale + RoundingBias) >> CoefficientShift);
                double difference = samples[i + 2] - decoded[i + 2];
                error += difference * difference;
            }
            // A maximum-scale attempt must finish even for unstable caller-supplied coefficients.
            if (scalePower < MaxScalePower)
                for (int overflow = maxOverflow + 8; overflow > 256; overflow >>= 1)
                    if (++scalePower >= MaxScalePower) scalePower = MaxScalePower - 1;
        } while (scalePower < MaxScalePower && maxOverflow > 1);
    }
}