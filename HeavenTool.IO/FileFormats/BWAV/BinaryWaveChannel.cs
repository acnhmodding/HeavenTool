using HeavenTool.IO.Audio.DspAdpcm;
using System.ComponentModel;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BWAV;

public class BinaryWaveChannel
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

#if DEBUG
    [Category("Offsets")]
#else
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
#endif
    public uint AudioOffsetNonPrefetch { get; internal set; }

#if DEBUG
    [Category("Offsets")]
#else
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
#endif
    public uint AudioOffset { get; internal set; }

    [Category("Looping")]
    public uint Always1 { get; private set; }

    [Category("Looping")]
    public int LoopEnd { get; private set; }

    [Category("Looping")]
    public int LoopStart { get; private set; }

    [Category("Predictor")]
    public ushort Predictor { get; private set; }

    [Category("History")]
    public short[] History { get; private set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public byte[]? ChannelData { get; internal set; }

    //internal long writeAudioOffset;
    //internal long writeAudioOffsetNonPrefetch;
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

    public BinaryWaveChannel(BinaryReader reader)
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
        AudioOffsetNonPrefetch = reader.ReadUInt32();
        AudioOffset = reader.ReadUInt32();
        Always1 = reader.ReadUInt32();
        LoopEnd = reader.ReadInt32();
        LoopStart = reader.ReadInt32();
        Predictor = reader.ReadUInt16();
        History = reader.ReadInt16Array(2);

        reader.Skip(2); // padding

        int encoded_size = DspAdpcmEncoder.SampleCountToByteCount(TotalSamples);

        using (reader.CreateScope())
            ChannelData = reader.ReadByteArrayAt(AudioOffset, encoded_size);
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
        writer.Write(Always1);
        writer.Write(LoopEnd);
        writer.Write(LoopStart);
        writer.Write(Predictor);
        writer.Write(History);
        writer.Skip(2);
    }

    public short[] Decode()
    {
        var newData = new short[TotalSamples];

        var coeffs = new short[8][];

        int currentCoeeff = 0;
        for (int i = 0; i < 8; i++)
        {
            coeffs[i] = new short[2];
            for (int j = 0; j <= 1; j++)
                coeffs[i][j] = Coefficients[currentCoeeff++];
        }

        Decode(ChannelData, ref newData, History[0], History[1], coeffs, TotalSamples);

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
}