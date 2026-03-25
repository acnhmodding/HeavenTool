using HeavenTool.IO.Audio.DspAdpcm;
using System.ComponentModel;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BWAV;

public class BinaryWaveChannel : IDisposable
{
    public enum PanType : ushort
    {
        Left = 0,
        Right = 1,
        Center = 2
    }

    public enum CodecType : ushort
    {
        PCM = 0,
        DSP_ADPCM = 1
    }


    // We are defining categories for the properties to make it easier to understand when inspecting in a property grid. (WinForms)
    [Category("Audio Configuration")]
    public CodecType Codec { get; set; } = CodecType.DSP_ADPCM;

    [Category("Audio Configuration")]
    public PanType ChannelPan { get; set; } = PanType.Center; // channel layout (0=L, 1=R, 2=C)

    [Category("Audio Configuration")]
    public int SampleRate { get; set; } = 48000;

    [Category("Audio Configuration")]
    public uint TotalSamplesPrefetch { get; set; }

    [Category("Audio Configuration")]
    public int TotalSamples { get; set; }

    [Category("Audio Configuration")]
    public short[] Coefficients { get; set; }

    [Category("Looping")]
    public uint LoopCount { get; private set; }

    [Category("Looping")]
    public int LoopEnd { get; private set; }

    [Category("Looping")]
    public int LoopStart { get; private set; }

    [Category("DSP_ADPCM")]
    public ushort Predictor { get; private set; }

    [Category("DSP_ADPCM")]
    public short[] History { get; private set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public byte[]? ChannelData { get; internal set; }

    internal WriterScopePointer? writeAudioOffsetNonPrefetch;
    internal WriterScopePointer? writeAudioOffsetPrefetch;

    public BinaryWaveChannel(byte[] channelData, short[] coeffs, int sampleRate = 48000)
    {
        Codec = CodecType.DSP_ADPCM;
        SampleRate = sampleRate;
        ChannelData = channelData;
        Coefficients = coeffs;
        History = new short[2];
    }

    public BinaryWaveChannel(BinaryReader reader, bool isPrefetched = false)
    {
        var codecType = reader.ReadUInt16();
        if (codecType < 0 || codecType > 1)
            throw new Exception($"Invalid Codec Type not supported ({codecType})!");

        Codec = (CodecType)codecType;

        var channelPan = reader.ReadUInt16();

        if (channelPan < 0 || channelPan > 2)
            throw new Exception($"Invalid channel pan ({channelPan})!");

        ChannelPan = (PanType)channelPan;
        SampleRate = reader.ReadInt32();
        TotalSamplesPrefetch = reader.ReadUInt32();
        TotalSamples = reader.ReadInt32();
        Coefficients = reader.ReadInt16Array(16);
        var audioOffsetPrefetched = reader.ReadUInt32();
        var audioOffset = reader.ReadUInt32();
        LoopCount = reader.ReadUInt32(); // Seems to be always 1 (or at least minimum 1)

        // Loops = new Loops[LoopCount];
        //for (uint i = 0; i < LoopCount; i++)
        //{
        //    var loopEnd = reader.ReadInt32();
        //    var loopStart = reader.ReadInt32();
        //}

        LoopEnd = reader.ReadInt32();
        LoopStart = reader.ReadInt32();
        Predictor = reader.ReadUInt16();
        History = reader.ReadInt16Array(2);

        reader.Skip(2); // padding

        int encoded_size = TotalSamples * 2;

        if (Codec == CodecType.DSP_ADPCM)
            encoded_size = DspAdpcmEncoder.SampleCountToByteCount(TotalSamples);

        using (reader.CreateScope())
            if (isPrefetched)
                ChannelData = TotalSamplesPrefetch > 0 ? reader.ReadByteArrayAt(audioOffsetPrefetched, encoded_size) : [];
            else
                ChannelData = TotalSamples > 0 ? reader.ReadByteArrayAt(audioOffset, encoded_size) : [];
    }

    internal void Write(BinaryWriter writer)
    {
        writer.Write((ushort)Codec);

        writer.Write((ushort)ChannelPan);
        writer.Write(SampleRate);
        writer.Write(TotalSamplesPrefetch);
        writer.Write(TotalSamples);
        writer.Write(Coefficients);
        writeAudioOffsetNonPrefetch = writer.CreatePointer();
        writeAudioOffsetPrefetch = writer.CreatePointer();
        writer.Write(LoopCount);
        writer.Write(LoopEnd);
        writer.Write(LoopStart);
        writer.Write(Predictor);
        writer.Write(History);
        writer.Pad(2);
    }

    public short[] Decode()
    {
        if (ChannelData == null) return [];

        var newData = new short[TotalSamples];

        if (Codec == CodecType.DSP_ADPCM)
        {
            var coeffs = new short[8][];

            int currentCoeeff = 0;
            for (int i = 0; i < 8; i++)
            {
                coeffs[i] = new short[2];
                for (int j = 0; j <= 1; j++)
                    coeffs[i][j] = Coefficients[currentCoeeff++];
            }

            Decode(ChannelData, ref newData, History[0], History[1], coeffs, TotalSamples);
        }
        else if (Codec == CodecType.PCM) {
            for (int i = 0; i < TotalSamples; i++)
            {
                newData[i] = BitConverter.ToInt16(ChannelData, i * 2);
            }
        }

        return newData;
    }

    private static readonly sbyte[] nibbleToSbyte = [0, 1, 2, 3, 4, 5, 6, 7, -8, -7, -6, -5, -4, -3, -2, -1];

    private static sbyte GetHighNibble(byte value) => nibbleToSbyte[(value >> 4) & 0xF];
    private static sbyte GetLowNibble(byte value) => nibbleToSbyte[value & 0xF];


    /// <summary>
    /// Decode DSP-ADPCM data.
    /// </summary>
    /// <param name="src">DSP-ADPCM source.</param>
    /// <param name="dst">Destination array of samples.</param>
    /// <param name="cxt">DSP-APCM context.</param>
    /// <param name="samples">Number of samples.</param>
    public static void Decode(byte[]? src, ref short[] dst, short hist1, short hist2, short[][] coefs, int samples)
    {
        ArgumentNullException.ThrowIfNull(src);

        //Each DSP-APCM frame is 8 bytes long. It contains 1 header byte, and 7 sample bytes.
        int dstIndex = 0;
        int srcIndex = 0;

        //Until all samples decoded.
        while (dstIndex < samples)
        {
            //Get the header.
            byte header = src[srcIndex++];

            //Get scale and co-efficient index.
            var scale = 1 << (header & 0xF);
            byte coef_index = (byte)(header >> 4);
            short coef1 = coefs[coef_index][0];
            short coef2 = coefs[coef_index][1];

            //7 sample bytes per frame.
            for (uint b = 0; b < 7; b++)
            {
                //Get byte.
                byte byt = src[srcIndex++];

                //2 samples per byte.
                for (uint s = 0; s < 2; s++)
                {
                    sbyte adpcm_nibble = (s == 0) ? GetHighNibble(byt) : GetLowNibble(byt);

                    var val = ((adpcm_nibble * scale) << 11) + 1024 + (coef1 * hist1) + (coef2 * hist2) >> 11;
                    short sample = DspAdpcmEncoder.Clamp16(val);

                    hist2 = hist1;
                    hist1 = sample;
                    dst[dstIndex++] = sample;

                    if (dstIndex >= samples) break;
                }

                if (dstIndex >= samples) break;
            }
        }
    }

    public static byte[] Encode(short[] samples, out short[] coeffs)
    {
        coeffs = DspAdpcmCoefficients.CalculateCoefficients(samples);
        return DspAdpcmEncoder.Encode(samples, coeffs);
    }

    private bool _disposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Release managed resources
            ChannelData = null;
            Coefficients = null!;
            History = null!;

            writeAudioOffsetNonPrefetch = null;
            writeAudioOffsetPrefetch = null;
        }

        _disposed = true;
    }
}