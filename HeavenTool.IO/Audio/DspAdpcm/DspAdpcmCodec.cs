namespace HeavenTool.IO.Audio.DspAdpcm;

public static class DspAdpcmCodec
{
    public const int BytesPerFrame = 8;
    public const int SamplesPerFrame = 14;
    public const int PredictorCount = 8;
    public const int CoefficientCount = PredictorCount * 2;
    internal const int CoefficientShift = 11;
    internal const int CoefficientScale = 1 << CoefficientShift;
    internal const int RoundingBias = CoefficientScale / 2;

    public static int GetByteCount(int sampleCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sampleCount);
        int remaining = sampleCount % SamplesPerFrame;
        return sampleCount / SamplesPerFrame * BytesPerFrame + (remaining == 0 ? 0 : 1 + (remaining + 1) / 2);
    }

    public static byte[] Encode(ReadOnlySpan<short> samples, out short[] coefficients)
    {
        coefficients = DspAdpcmCoefficients.CalculateCoefficients(samples);
        return DspAdpcmEncoder.Encode(samples, coefficients);
    }

    public static void Decode(ReadOnlySpan<byte> data, Span<short> samples, ReadOnlySpan<short> coefficients, short history1 = 0, short history2 = 0)
    {
        ValidateCoefficients(coefficients);
        if (data.Length < GetByteCount(samples.Length))
            throw new InvalidDataException("DSP ADPCM data is shorter than the requested sample count.");

        for (int position = 0, frameOffset = 0; position < samples.Length; frameOffset += BytesPerFrame)
        {
            byte header = data[frameOffset];
            int predictor = header >> 4;
            if (predictor >= PredictorCount)
                throw new InvalidDataException("DSP ADPCM frame has an invalid predictor.");
            int scale = 1 << (header & 0xf);
            short coefficient1 = coefficients[predictor * 2];
            short coefficient2 = coefficients[predictor * 2 + 1];
            int frameSamples = Math.Min(SamplesPerFrame, samples.Length - position);

            for (int i = 0; i < frameSamples; i++)
            {
                byte packed = data[frameOffset + 1 + i / 2];
                int nibble = (packed >> ((i & 1) == 0 ? 4 : 0)) & 0xf;
                if (nibble >= 8) nibble -= 16;
                int decoded = (((nibble * scale) << CoefficientShift) + RoundingBias
                    + coefficient1 * history1 + coefficient2 * history2) >> CoefficientShift;
                short sample = ClampSample(decoded);
                samples[position++] = sample;
                history2 = history1;
                history1 = sample;
            }
        }
    }

    internal static short ClampSample(int value) => (short)Math.Clamp(value, short.MinValue, short.MaxValue);

    internal static void ValidateCoefficients(ReadOnlySpan<short> coefficients)
    {
        if (coefficients.Length != CoefficientCount)
            throw new ArgumentException("DSP ADPCM requires eight pairs of coefficients.", nameof(coefficients));
    }
}