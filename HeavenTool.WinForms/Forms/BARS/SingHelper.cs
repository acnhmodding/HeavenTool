using System;

namespace HeavenTool.Forms.BARS;

public static class SingHelper
{
    public static void MixCount(float[] src, float[] dst, int channels, decimal dstOffset, decimal srcOffset, int count, float volume, bool fade)
    {
        int safeSOff = (int) Math.Round(srcOffset);
        int safeDOff = (int) Math.Round(dstOffset);

        int fadePos = count - 3840;
        if (fadePos < 0)
            fadePos = count - count / 4;

        for (int ch = 0; ch < channels; ch++)
        {
            for (int i = 0; i < count; i++)
            {
                float fadeAmount = 1;

                if (fade && i > fadePos)
                    fadeAmount = 1 - (float)(i - fadePos) / (count - fadePos);

                int dstSampleIndex = safeDOff + (i - safeSOff);

                if (dstSampleIndex >= 0 && dstSampleIndex < dst.Length / channels)
                {
                    int dstIndex = dstSampleIndex * channels + ch;
                    int srcIndex = i * channels + ch;

                    if (srcIndex >= 0 && srcIndex < src.Length)
                    {
                        dst[dstIndex] += src[srcIndex] * volume * fadeAmount;
                    }
                }
            }
        }
    }

    public static void MixCountPCM16(short[] src, short[] dst, int channels, int dstStart, int srcStart, int count, float volume, bool fade)
    {
        int fadePos = count - 3840;
        if (fadePos < 0)
            fadePos = count - count / 4;

        float fadeScale = fade ? 1f / (count - fadePos) : 0f;

        int dstSamples = dst.Length / channels;

        for (int i = 0; i < count; i++)
        {
            float fadeAmount = 1f;

            if (fade && i > fadePos)
                fadeAmount = 1f - (i - fadePos) * fadeScale;

            float gain = volume * fadeAmount;

            int dstSampleIndex = dstStart + (i - srcStart);

            if (dstSampleIndex < 0 || dstSampleIndex >= dstSamples)
                continue;

            int dstBase = dstSampleIndex * channels;
            int srcBase = i * channels;

            for (int ch = 0; ch < channels; ch++)
            {
                int srcIndex = srcBase + ch;
                if (srcIndex >= src.Length)
                    continue;

                int dstIndex = dstBase + ch;

                // Mix in 16-bit with scaling
                int mixed = dst[dstIndex] + (int)(src[srcIndex] * gain);

                // Clamp to 16-bit range
                dst[dstIndex] = (short) Math.Clamp(mixed, short.MinValue, short.MaxValue);
            }
        }
    }
}