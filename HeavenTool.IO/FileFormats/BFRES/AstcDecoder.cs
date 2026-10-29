using System.Buffers.Binary;

namespace HeavenTool.IO.FileFormats.BFRES;

// Decodes LDR ASTC blocks to RGBA8, following the Khronos data format specification. Desktop GPUs rarely sample ASTC, so
// ASTC textures are decoded on the CPU. HDR blocks and invalid encodings decode to magenta, as the specification asks.
internal static class AstcDecoder
{
    private const int BlockBytes = 16;
    private static readonly byte[] Error = [255, 0, 255, 255];

    // Each quantization level as its trits, quints and plain bits: level i holds values 0..Levels[i]-1.
    private static readonly int[] Levels = [2, 3, 4, 5, 6, 8, 10, 12, 16, 20, 24, 32, 40, 48, 64, 80, 96, 128, 160, 192, 256];
    private static readonly (int Trits, int Quints, int Bits)[] Encodings = [.. Levels.Select(Encoding)];

    // The specification's unquantization scale and bit pattern for each count of plain bits, from one up.
    private static readonly (int Scale, string Pattern)[] TritColors =
        [(204, "000000000"), (93, "b000b0bb0"), (44, "cb000cbcb"), (22, "dcb000dcb"), (11, "edcb000ed"), (5, "fedcb000f")];
    private static readonly (int Scale, string Pattern)[] QuintColors =
        [(113, "000000000"), (54, "b0000bb00"), (26, "cb0000cbc"), (13, "dcb0000dc"), (6, "edcb0000e")];
    private static readonly (int Scale, string Pattern)[] TritWeights = [(50, "0000000"), (23, "b000b0b"), (11, "cb000cb")];
    private static readonly (int Scale, string Pattern)[] QuintWeights = [(28, "0000000"), (13, "b0000b0")];
    private static readonly int[] TritWeightsOnly = [0, 32, 63], QuintWeightsOnly = [0, 16, 32, 47, 63];

    private static readonly byte[][] ColorValues = [.. Levels.Select((_, level) => UnquantizeTable(level, color: true))];
    private static readonly byte[][] WeightValues = [.. Levels.Take(12).Select((_, level) => UnquantizeTable(level, color: false))];

    // blocks holds whole blocks in row order; the result is width x height RGBA8.
    public static byte[] Decode(ReadOnlySpan<byte> blocks, int width, int height, int blockWidth, int blockHeight, bool srgb)
    {
        var pixels = new byte[width * height * 4];
        int columns = (width + blockWidth - 1) / blockWidth, rows = (height + blockHeight - 1) / blockHeight;
        var texels = new byte[blockWidth * blockHeight * 4];
        for (int by = 0; by < rows; by++)
            for (int bx = 0; bx < columns; bx++)
            {
                var block = BinaryPrimitives.ReadUInt128LittleEndian(blocks.Slice((by * columns + bx) * BlockBytes, BlockBytes));
                DecodeBlock(block, blockWidth, blockHeight, srgb, texels);
                for (int y = 0; y < blockHeight && by * blockHeight + y < height; y++)
                {
                    int count = Math.Min(blockWidth, width - bx * blockWidth) * 4;
                    texels.AsSpan(y * blockWidth * 4, count).CopyTo(pixels.AsSpan(((by * blockHeight + y) * width + bx * blockWidth) * 4));
                }
            }
        return pixels;
    }

    private static void DecodeBlock(UInt128 block, int blockWidth, int blockHeight, bool srgb, Span<byte> texels)
    {
        int texelCount = blockWidth * blockHeight;
        int mode = Bits(block, 0, 11);
        if ((mode & 0x1ff) == 0x1fc)
        {
            // A void-extent block is one color, as UNORM16 channels in its upper half. HDR ones are unsupported.
            Span<byte> color = stackalloc byte[4];
            for (int c = 0; c < 4; c++) color[c] = (byte)(Bits(block, 64 + c * 16, 16) >> 8);
            Fill(texels, texelCount, (mode & 0x200) != 0 ? Error : color);
            return;
        }
        if (!DecodeBlockMode(mode, out int gridWidth, out int gridHeight, out bool dualPlane, out int weightLevel)
            || gridWidth > blockWidth || gridHeight > blockHeight)
        {
            Fill(texels, texelCount, Error);
            return;
        }

        int partitions = Bits(block, 11, 2) + 1, weightCount = gridWidth * gridHeight * (dualPlane ? 2 : 1);
        int weightBits = SequenceBits(weightCount, weightLevel), belowWeights = 128 - weightBits;
        if (weightCount > 64 || weightBits < 24 || weightBits > 96 || (partitions == 4 && dualPlane))
        {
            Fill(texels, texelCount, Error);
            return;
        }

        Span<int> formats = stackalloc int[partitions];
        int colorStart;
        if (partitions == 1)
        {
            formats[0] = Bits(block, 13, 4);
            colorStart = 17;
        }
        else
        {
            colorStart = 29;
            int encoded = Bits(block, 23, 6);
            if ((encoded & 3) == 0)
                formats.Fill(encoded >> 2);
            else
            {
                // Each partition's format is the base class, maybe plus one, with its own two-bit mode. The bits that
                // don't fit beside the partition index sit just below the weights.
                int extra = 3 * partitions - 4;
                belowWeights -= extra;
                encoded = (encoded | Bits(block, belowWeights, extra) << 6) >> 2;
                int baseClass = Bits(block, 23, 2) - 1;
                for (int i = 0; i < partitions; i++, encoded >>= 1) formats[i] = ((encoded & 1) + baseClass) << 2;
                for (int i = 0; i < partitions; i++, encoded >>= 2) formats[i] |= encoded & 3;
            }
        }

        int dualComponent = -1;
        if (dualPlane)
        {
            belowWeights -= 2;
            dualComponent = Bits(block, belowWeights, 2);
        }

        int colorCount = 0;
        foreach (int format in formats) colorCount += ((format >> 2) + 1) * 2;
        int colorLevel = Levels.Length - 1;
        while (colorLevel >= 0 && SequenceBits(colorCount, colorLevel) > belowWeights - colorStart) colorLevel--;
        if (colorCount > 18 || colorLevel < 4)
        {
            Fill(texels, texelCount, Error);
            return;
        }

        Span<int> colors = stackalloc int[colorCount];
        ReadSequence(block, colorStart, colorCount, colorLevel, colors);
        for (int i = 0; i < colorCount; i++) colors[i] = ColorValues[colorLevel][Math.Min(colors[i], Levels[colorLevel] - 1)];

        Span<int> endpoints = stackalloc int[partitions * 8];
        for (int i = 0, used = 0; i < partitions; used += ((formats[i] >> 2) + 1) * 2, i++)
            if (!DecodeEndpoints(formats[i], colors[used..], endpoints.Slice(i * 8, 8)))
            {
                Fill(texels, texelCount, Error);
                return;
            }

        // Weights are stored from the top bit down.
        Span<int> weights = stackalloc int[weightCount];
        ReadSequence(Reverse(block), 0, weightCount, weightLevel, weights);
        for (int i = 0; i < weightCount; i++) weights[i] = WeightValues[weightLevel][Math.Min(weights[i], Levels[weightLevel] - 1)];

        int seed = partitions > 1 ? Bits(block, 13, 10) : 0;
        bool small = texelCount < 31;
        int planes = dualPlane ? 2 : 1;
        int ds = (1024 + blockWidth / 2) / Math.Max(1, blockWidth - 1), dt = (1024 + blockHeight / 2) / Math.Max(1, blockHeight - 1);
        for (int y = 0; y < blockHeight; y++)
            for (int x = 0; x < blockWidth; x++)
            {
                int partition = partitions > 1 ? SelectPartition(seed, x, y, partitions, small) : 0;
                var endpoint = endpoints.Slice(partition * 8, 8);
                int gs = (ds * x * (gridWidth - 1) + 32) >> 6, gt = (dt * y * (gridHeight - 1) + 32) >> 6;
                for (int c = 0; c < 4; c++)
                {
                    int weight = Infill(weights, gridWidth, gridHeight, gs, gt, planes, c == dualComponent ? 1 : 0);
                    // Endpoints widen to 16 bits before interpolating; sRGB fills the low byte with 0x80 rather than a copy.
                    int low = endpoint[c] << 8 | (srgb ? 0x80 : endpoint[c]), high = endpoint[4 + c] << 8 | (srgb ? 0x80 : endpoint[4 + c]);
                    texels[(y * blockWidth + x) * 4 + c] = (byte)(((low * (64 - weight) + high * weight + 32) >> 6) >> 8);
                }
            }
    }

    private static bool DecodeBlockMode(int mode, out int width, out int height, out bool dualPlane, out int level)
    {
        int range = (mode >> 4) & 1, a = (mode >> 5) & 3;
        bool high = (mode & 0x200) != 0;
        dualPlane = (mode & 0x400) != 0;
        width = height = level = 0;
        if ((mode & 3) != 0)
        {
            range |= (mode & 3) << 1;
            int b = (mode >> 7) & 3;
            switch ((mode >> 2) & 3)
            {
                case 0: (width, height) = (b + 4, a + 2); break;
                case 1: (width, height) = (b + 8, a + 2); break;
                case 2: (width, height) = (a + 2, b + 8); break;
                default:
                    b &= 1;
                    (width, height) = (mode & 0x100) != 0 ? (b + 2, a + 2) : (a + 2, b + 6);
                    break;
            }
        }
        else
        {
            range |= ((mode >> 2) & 3) << 1;
            if (((mode >> 2) & 3) == 0) return false;
            int b = (mode >> 9) & 3;
            switch ((mode >> 7) & 3)
            {
                case 0: (width, height) = (12, a + 2); break;
                case 1: (width, height) = (a + 2, 12); break;
                case 2: (width, height, dualPlane, high) = (a + 6, b + 6, false, false); break;
                default:
                    if (a > 1) return false;
                    (width, height) = a == 0 ? (6, 10) : (10, 6);
                    break;
            }
        }
        level = range - 2 + (high ? 6 : 0);
        return true;
    }

    private static bool DecodeEndpoints(int format, ReadOnlySpan<int> v, Span<int> endpoint)
    {
        static void Set(Span<int> endpoint, int index, int r, int g, int b, int alpha)
        {
            endpoint[index * 4] = Math.Clamp(r, 0, 255);
            endpoint[index * 4 + 1] = Math.Clamp(g, 0, 255);
            endpoint[index * 4 + 2] = Math.Clamp(b, 0, 255);
            endpoint[index * 4 + 3] = Math.Clamp(alpha, 0, 255);
        }
        // Moves the top bit of the offset into the base, leaving a signed six-bit offset.
        static (int Offset, int Base) Transfer(int offset, int value)
        {
            value = (value >> 1) | (offset & 0x80);
            offset = (offset >> 1) & 0x3f;
            return ((offset & 0x20) != 0 ? offset - 0x40 : offset, value);
        }
        // Endpoints in the reverse order store blue at full precision and pull red and green toward it.
        static void Contracted(Span<int> endpoint, int index, int r, int g, int b, int alpha) =>
            Set(endpoint, index, (r + b) >> 1, (g + b) >> 1, b, alpha);

        switch (format)
        {
            case 0:
                Set(endpoint, 0, v[0], v[0], v[0], 255); Set(endpoint, 1, v[1], v[1], v[1], 255);
                return true;
            case 1:
            {
                int low = (v[0] >> 2) | (v[1] & 0xc0);
                Set(endpoint, 0, low, low, low, 255);
                int top = Math.Min(low + (v[1] & 0x3f), 255);
                Set(endpoint, 1, top, top, top, 255);
                return true;
            }
            case 4:
                Set(endpoint, 0, v[0], v[0], v[0], v[2]); Set(endpoint, 1, v[1], v[1], v[1], v[3]);
                return true;
            case 5:
            {
                var (l, l0) = Transfer(v[1], v[0]);
                var (a, a0) = Transfer(v[3], v[2]);
                Set(endpoint, 0, l0, l0, l0, a0); Set(endpoint, 1, l0 + l, l0 + l, l0 + l, a0 + a);
                return true;
            }
            case 6:
                Set(endpoint, 0, (v[0] * v[3]) >> 8, (v[1] * v[3]) >> 8, (v[2] * v[3]) >> 8, 255); Set(endpoint, 1, v[0], v[1], v[2], 255);
                return true;
            case 8 or 12:
            {
                int a0 = format == 12 ? v[6] : 255, a1 = format == 12 ? v[7] : 255;
                if (v[1] + v[3] + v[5] >= v[0] + v[2] + v[4]) { Set(endpoint, 0, v[0], v[2], v[4], a0); Set(endpoint, 1, v[1], v[3], v[5], a1); }
                else { Contracted(endpoint, 0, v[1], v[3], v[5], a1); Contracted(endpoint, 1, v[0], v[2], v[4], a0); }
                return true;
            }
            case 9 or 13:
            {
                var (r, r0) = Transfer(v[1], v[0]);
                var (g, g0) = Transfer(v[3], v[2]);
                var (b, b0) = Transfer(v[5], v[4]);
                var (a, a0) = format == 13 ? Transfer(v[7], v[6]) : (0, 255);
                if (r + g + b >= 0) { Set(endpoint, 0, r0, g0, b0, a0); Set(endpoint, 1, r0 + r, g0 + g, b0 + b, a0 + a); }
                else { Contracted(endpoint, 0, r0 + r, g0 + g, b0 + b, a0 + a); Contracted(endpoint, 1, r0, g0, b0, a0); }
                return true;
            }
            case 10:
                Set(endpoint, 0, (v[0] * v[3]) >> 8, (v[1] * v[3]) >> 8, (v[2] * v[3]) >> 8, v[4]); Set(endpoint, 1, v[0], v[1], v[2], v[5]);
                return true;
            default:
                // The remaining formats are HDR.
                return false;
        }
    }

    // Bilinear infill of the weight grid at a texel, from its grid position in 1/16ths.
    private static int Infill(ReadOnlySpan<int> weights, int width, int height, int gs, int gt, int planes, int plane)
    {
        int js = gs >> 4, fs = gs & 15, jt = gt >> 4, ft = gt & 15;
        int w11 = (fs * ft + 8) >> 4, w10 = ft - w11, w01 = fs - w11, w00 = 16 - fs - ft + w11;
        int Weight(ReadOnlySpan<int> weights, int x, int y) => weights[(Math.Min(y, height - 1) * width + Math.Min(x, width - 1)) * planes + plane];
        return (Weight(weights, js, jt) * w00 + Weight(weights, js + 1, jt) * w01 + Weight(weights, js, jt + 1) * w10
            + Weight(weights, js + 1, jt + 1) * w11 + 8) >> 4;
    }

    private static int SelectPartition(int seed, int x, int y, int partitions, bool small)
    {
        if (small) { x <<= 1; y <<= 1; }
        seed += (partitions - 1) * 1024;
        uint r = (uint)seed;
        r ^= r >> 15; r -= r << 17; r += r << 7; r += r << 4;
        r ^= r >> 5; r += r << 16; r ^= r >> 7; r ^= r >> 3;
        r ^= r << 6; r ^= r >> 17;

        Span<int> s = stackalloc int[8];
        for (int i = 0; i < 8; i++) s[i] = (int)((r >> (i * 4)) & 15) * (int)((r >> (i * 4)) & 15);
        int sh1, sh2;
        if ((seed & 1) != 0) { sh1 = (seed & 2) != 0 ? 4 : 5; sh2 = partitions == 3 ? 6 : 5; }
        else { sh1 = partitions == 3 ? 6 : 5; sh2 = (seed & 2) != 0 ? 4 : 5; }
        for (int i = 0; i < 8; i++) s[i] >>= (i & 1) == 0 ? sh1 : sh2;

        // The z terms of the specification drop out for 2D blocks.
        int a = (s[0] * x + s[1] * y + (int)(r >> 14)) & 0x3f;
        int b = (s[2] * x + s[3] * y + (int)(r >> 10)) & 0x3f;
        int c = partitions < 3 ? 0 : (s[4] * x + s[5] * y + (int)(r >> 6)) & 0x3f;
        int d = partitions < 4 ? 0 : (s[6] * x + s[7] * y + (int)(r >> 2)) & 0x3f;
        return a >= b && a >= c && a >= d ? 0 : b >= c && b >= d ? 1 : c >= d ? 2 : 3;
    }

    // Reads count integer-sequence-encoded values of a level from bit start. Missing trailing bits read as zero.
    private static void ReadSequence(UInt128 block, int start, int count, int level, Span<int> values)
    {
        var (trits, quints, bits) = Encodings[level];
        int end = start + SequenceBits(count, level), position = start;
        int Read(int length)
        {
            int available = Math.Clamp(end - position, 0, length), value = available == 0 ? 0 : Bits(block, position, available);
            position += length;
            return value;
        }

        if (trits == 0 && quints == 0)
        {
            for (int i = 0; i < count; i++) values[i] = Read(bits);
            return;
        }

        // Trits come in groups of five with 8 packed bits, quints in groups of three with 7, spread between the values.
        int group = trits != 0 ? 5 : 3;
        int[] spread = trits != 0 ? [2, 2, 1, 2, 1] : [3, 2, 2];
        Span<int> low = stackalloc int[5], digits = stackalloc int[5];
        for (int first = 0; first < count; first += group)
        {
            int packed = 0, shift = 0;
            for (int i = 0; i < group; i++)
            {
                low[i] = Read(bits);
                packed |= Read(spread[i]) << shift;
                shift += spread[i];
            }
            if (trits != 0) DecodeTrits(packed, digits); else DecodeQuints(packed, digits);
            for (int i = 0; i < group && first + i < count; i++) values[first + i] = digits[i] << bits | low[i];
        }
    }

    private static void DecodeTrits(int t, Span<int> trits)
    {
        int Bit(int value, int index) => (value >> index) & 1;
        int c;
        if (((t >> 2) & 7) == 7)
        {
            c = ((t >> 5) & 7) << 2 | (t & 3);
            trits[4] = trits[3] = 2;
        }
        else
        {
            c = t & 0x1f;
            if (((t >> 5) & 3) == 3) { trits[4] = 2; trits[3] = Bit(t, 7); }
            else { trits[4] = Bit(t, 7); trits[3] = (t >> 5) & 3; }
        }
        if ((c & 3) == 3) { trits[2] = 2; trits[1] = Bit(c, 4); trits[0] = Bit(c, 3) << 1 | (Bit(c, 2) & ~Bit(c, 3) & 1); }
        else if (((c >> 2) & 3) == 3) { trits[2] = 2; trits[1] = 2; trits[0] = c & 3; }
        else { trits[2] = Bit(c, 4); trits[1] = (c >> 2) & 3; trits[0] = Bit(c, 1) << 1 | (Bit(c, 0) & ~Bit(c, 1) & 1); }
    }

    private static void DecodeQuints(int q, Span<int> quints)
    {
        int Bit(int value, int index) => (value >> index) & 1;
        if (((q >> 1) & 3) == 3 && ((q >> 5) & 3) == 0)
        {
            quints[2] = Bit(q, 0) << 2 | (Bit(q, 4) & ~Bit(q, 0) & 1) << 1 | (Bit(q, 3) & ~Bit(q, 0) & 1);
            quints[1] = quints[0] = 4;
            return;
        }
        int c;
        if (((q >> 1) & 3) == 3) { quints[2] = 4; c = ((q >> 3) & 3) << 3 | (~(q >> 5) & 3) << 1 | (q & 1); }
        else { quints[2] = (q >> 5) & 3; c = q & 0x1f; }
        if ((c & 7) == 5) { quints[1] = 4; quints[0] = (c >> 3) & 3; }
        else { quints[1] = (c >> 3) & 3; quints[0] = c & 7; }
    }

    private static int SequenceBits(int count, int level)
    {
        var (trits, quints, bits) = Encodings[level];
        return count * bits + (trits != 0 ? (8 * count + 4) / 5 : 0) + (quints != 0 ? (7 * count + 2) / 3 : 0);
    }

    private static (int Trits, int Quints, int Bits) Encoding(int levels) =>
        levels % 3 == 0 ? (1, 0, int.Log2(levels / 3)) : levels % 5 == 0 ? (0, 1, int.Log2(levels / 5)) : (0, 0, int.Log2(levels));

    // Every encoded value of a level, as a color (0..255) or a weight (0..64).
    private static byte[] UnquantizeTable(int level, bool color)
    {
        var (trits, quints, bits) = Encodings[level];
        var table = new byte[Levels[level]];
        for (int value = 0; value < table.Length; value++)
        {
            int result;
            if (trits == 0 && quints == 0)
                result = Replicate(value, bits, color ? 8 : 6);
            else if (bits == 0)
                // Only weights use the levels without plain bits; colors need at least six levels.
                result = trits != 0 ? TritWeightsOnly[value] : QuintWeightsOnly[value];
            else
            {
                // The digit scales the range, and the plain bits above the lowest fill in a fixed pattern.
                var (scale, pattern) = (color ? trits != 0 ? TritColors : QuintColors : trits != 0 ? TritWeights : QuintWeights)[bits - 1];
                int digit = value >> bits, m = value & ((1 << bits) - 1);
                int a = (m & 1) != 0 ? (color ? 0x1ff : 0x7f) : 0, b = 0;
                foreach (char bit in pattern) b = b << 1 | (bit == '0' ? 0 : (m >> (bit - 'a')) & 1);
                result = (a & (color ? 0x80 : 0x20)) | (((digit * scale + b) ^ a) >> 2);
            }
            if (!color && result > 32) result++;
            table[value] = (byte)result;
        }
        return table;
    }

    private static int Replicate(int value, int bits, int outBits)
    {
        int result = 0, filled = 0;
        for (; filled < outBits; filled += bits) result = result << bits | value;
        return result >> (filled - outBits);
    }

    private static int Bits(UInt128 block, int start, int count) => (int)((block >> start) & ((UInt128.One << count) - 1));

    private static UInt128 Reverse(UInt128 block)
    {
        UInt128 reversed = 0;
        for (int i = 0; i < 128; i++, block >>= 1) reversed = reversed << 1 | (block & 1);
        return reversed;
    }

    private static void Fill(Span<byte> texels, int count, ReadOnlySpan<byte> color)
    {
        for (int i = 0; i < count; i++) color.CopyTo(texels[(i * 4)..]);
    }
}
