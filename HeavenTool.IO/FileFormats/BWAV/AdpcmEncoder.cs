using System;
using System.Collections.Generic;
using System.Text;

namespace HeavenTool.IO.FileFormats.BWAV;

public class AdpcmEncodeBuffers
{
    public short[][] Coefs { get; } = new short[8][];
    public int[][] PcmOut { get; } = new int[8][];
    public int[][] AdpcmOut { get; } = new int[8][];
    public int[] Scale { get; } = new int[8];
    public double[] TotalDistance { get; } = new double[8];

    public AdpcmEncodeBuffers()
    {
        for (int i = 0; i < 8; i++)
        {
            PcmOut[i] = new int[16];
            AdpcmOut[i] = new int[14];
            Coefs[i] = new short[2];
        }
    }
}

public static class AdpcmEncoder
{
    public static readonly int BytesPerFrame = 8;
    public static readonly int SamplesPerFrame = 14;
    public static readonly int NibblesPerFrame = 16;

    public static short Clamp16(int value)
    {
        if (value > short.MaxValue)
            return short.MaxValue;
        if (value < short.MinValue)
            return short.MinValue;
        return (short)value;
    }

    public static sbyte Clamp4(int value)
    {
        if (value > 7)
            return 7;
        if (value < -8)
            return -8;
        return (sbyte)value;
    }

    public static byte CombineNibbles(int high, int low) => (byte)((high << 4) | (low & 0xF));

    public static int SampleCountToNibbleCount(int sampleCount)
    {
        int frames = sampleCount / SamplesPerFrame;
        int extraSamples = sampleCount % SamplesPerFrame;
        int extraNibbles = extraSamples == 0 ? 0 : extraSamples + 2;

        return NibblesPerFrame * frames + extraNibbles;
    }
    public static int SampleCountToByteCount(int sampleCount) => (SampleCountToNibbleCount(sampleCount) + 1) / 2;
    

    public class GcAdpcmParameters
    {
        public int SampleCount { get; set; } = -1;
        public short History1 { get; set; }
        public short History2 { get; set; }
    }

    public static byte[] Encode(short[] pcm, short[] coefs, GcAdpcmParameters? config = null)
    {
        config ??= new GcAdpcmParameters();
        int sampleCount = config.SampleCount == -1 ? pcm.Length : config.SampleCount;
        var adpcm = new byte[SampleCountToByteCount(sampleCount)];

        /* Execute encoding-predictor for each frame */
        var pcmBuffer = new short[2 + SamplesPerFrame];
        var adpcmBuffer = new byte[BytesPerFrame];

        pcmBuffer[0] = config.History2;
        pcmBuffer[1] = config.History1;

        int frameCount = (int) Math.Ceiling((double)sampleCount / SamplesPerFrame);
        var buffers = new AdpcmEncodeBuffers();

        for (int frame = 0; frame < frameCount; frame++)
        {
            int samplesToCopy = Math.Min(sampleCount - frame * SamplesPerFrame, SamplesPerFrame);
            Array.Copy(pcm, frame * SamplesPerFrame, pcmBuffer, 2, samplesToCopy);
            Array.Clear(pcmBuffer, 2 + samplesToCopy, SamplesPerFrame - samplesToCopy);

            DspEncodeFrame(pcmBuffer, SamplesPerFrame, adpcmBuffer, coefs, buffers);

            Array.Copy(adpcmBuffer, 0, adpcm, frame * BytesPerFrame, SampleCountToByteCount(samplesToCopy));

            pcmBuffer[0] = pcmBuffer[14];
            pcmBuffer[1] = pcmBuffer[15];
        }

        return adpcm;
    }

    public static void DspEncodeFrame(short[] pcmInOut, int sampleCount, byte[] adpcmOut, short[] coefsIn, AdpcmEncodeBuffers? b = null)
    {
        b ??= new AdpcmEncodeBuffers();

        for (int i = 0; i < 8; i++)
        {
            b.Coefs[i][0] = coefsIn[i * 2];
            b.Coefs[i][1] = coefsIn[i * 2 + 1];
        }

        /* Iterate through each coef set, finding the set with the smallest error */
        for (int i = 0; i < 8; i++)
        {
            DspEncodeCoef(pcmInOut, sampleCount, b.Coefs[i], b.PcmOut[i], b.AdpcmOut[i], out b.Scale[i],
                out b.TotalDistance[i]);
        }

        int bestCoef = 0;

        double min = double.MaxValue;
        for (int i = 0; i < 8; i++)
        {
            if (b.TotalDistance[i] < min)
            {
                min = b.TotalDistance[i];
                bestCoef = i;
            }
        }

        /* Write converted samples */
        for (int s = 0; s < sampleCount; s++)
            pcmInOut[s + 2] = (short)b.PcmOut[bestCoef][s + 2];

        /* Write predictor and scale */
        adpcmOut[0] = CombineNibbles(bestCoef, b.Scale[bestCoef]);

        /* Zero remaining samples */
        for (int s = sampleCount; s < 14; s++)
            b.AdpcmOut[bestCoef][s] = 0;

        /* Write output samples */
        for (int i = 0; i < 7; i++)
        {
            adpcmOut[i + 1] = CombineNibbles(b.AdpcmOut[bestCoef][i * 2], b.AdpcmOut[bestCoef][i * 2 + 1]);
        }
    }

    private static void DspEncodeCoef(short[] pcmIn, int sampleCount, short[] coefs, int[] pcmOut, int[] adpcmOut, out int scalePower, out double totalDistance)
    {
        int maxOverflow;
        int maxDistance = 0;

        // Set history values
        pcmOut[0] = pcmIn[0];
        pcmOut[1] = pcmIn[1];

        // Encode the frame with a scale of 1
        for (int s = 0; s < sampleCount; s++)
        {
            int inputSample = pcmIn[s + 2];
            int predictedSample = (pcmIn[s] * coefs[1] + pcmIn[s + 1] * coefs[0]) / 2048;
            int distance = inputSample - predictedSample;
            distance = Clamp16(distance);
            if (Math.Abs(distance) > Math.Abs(maxDistance))
                maxDistance = distance;
        }

        // Use the maximum distance of the encoded frame to find a scale that will fit the current frame
        scalePower = 0;
        while (scalePower <= 12 && (maxDistance > 7 || maxDistance < -8))
        {
            maxDistance /= 2;
            scalePower++;
        }
        scalePower = scalePower <= 1 ? -1 : scalePower - 2;

        // Try increasing scales until the encoded frame is in the range of a 4-bit value
        do
        {
            scalePower++;
            int scale = (1 << scalePower) * 2048;
            totalDistance = 0;
            maxOverflow = 0;

            for (int s = 0; s < sampleCount; s++)
            {
                // Calculate the difference between the actual and predicted samples
                int inputSample = pcmIn[s + 2] * 2048;
                int predictedSample = pcmOut[s] * coefs[1] + pcmOut[s + 1] * coefs[0];
                int distance = inputSample - predictedSample;
                // Scale to 4-bits, and round to the nearest sample
                // The official encoder does the casting this way, so match that behavior
                int unclampedAdpcmSample = (distance > 0)
                    ? (int)((double)((float)distance / scale) + 0.4999999f)
                    : (int)((double)((float)distance / scale) - 0.4999999f);

                int adpcmSample = Clamp4(unclampedAdpcmSample);
                if (adpcmSample != unclampedAdpcmSample)
                {
                    int overflow = Math.Abs(unclampedAdpcmSample - adpcmSample);
                    if (overflow > maxOverflow) maxOverflow = overflow;
                }

                adpcmOut[s] = adpcmSample;

                // Decode sample to use as history
                int decodedDistance = adpcmSample * scale;
                int correctedSample = predictedSample + decodedDistance;
                int scaledSample = (correctedSample + 1024) >> 11;
                // Clamp and store
                pcmOut[s + 2] = Clamp16(scaledSample);
                // Accumulate distance
                double actualDistance = pcmIn[s + 2] - pcmOut[s + 2];
                totalDistance += actualDistance * actualDistance;
            }

            for (int x = maxOverflow + 8; x > 256; x >>= 1)
                if (++scalePower >= 12)
                    scalePower = 11;

        } while (scalePower < 12 && maxOverflow > 1);
    }

}
