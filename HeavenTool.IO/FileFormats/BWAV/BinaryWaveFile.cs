using HeavenTool.IO.FileFormats.BARS;
using System.ComponentModel;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;

namespace HeavenTool.IO.FileFormats.BWAV;

public class BinaryWaveFile
{
    public class BinaryWaveChannel
    {
        internal BinaryWaveFile Parent {  get; set; }

        // We are defining categories for the properties to make it easier to understand when inspecting in a property grid. (WinForms)
        [Category("Audio Configuration")]
        public ushort Codec { get; private set; }

        [Category("Audio Configuration")]
        public ushort ChannelPan { get; private set; }

        [Category("Audio Configuration")]
        public int SampleRate { get; private set; }

        [Category("Audio Configuration")]
        public uint TotalSamplesPrefetch { get; private set; }

        [Category("Audio Configuration")]
        public int TotalSamples { get; private set; }

        [Category("Audio Configuration")]
        public short[] Coefficients { get; private set; }

#if DEBUG
        [Category("Offsets")]
#else
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
#endif
        public uint AudioOffsetNonPrefetch { get; private set; }

#if DEBUG
        [Category("Offsets")]
#else
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
#endif
        public uint AudioOffset { get; private set; }

        [Category("Looping")]
        public bool IsLoop { get; private set; }

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
        public byte[] ChannelData { get; internal set; }

        public BinaryWaveChannel(BinaryReader reader, BinaryWaveFile parent)
        {
            Parent = parent;

            Codec = reader.ReadUInt16();

            //if (Codec != 1)
            //  throw new Exception($"Codec {Codec} is not supported! Only 16-bit PCM is supported!");

            ChannelPan = reader.ReadUInt16();
            SampleRate = reader.ReadInt32();
            TotalSamplesPrefetch = reader.ReadUInt32();
            TotalSamples = reader.ReadInt32();
            Coefficients = reader.ReadInt16Array(16);
            AudioOffsetNonPrefetch = reader.ReadUInt32();
            AudioOffset = reader.ReadUInt32();
            IsLoop = reader.ReadUInt32() == 1;
            LoopEnd = reader.ReadInt32();
            LoopStart = reader.ReadInt32();
            Predictor = reader.ReadUInt16();
            History = reader.ReadInt16Array(2);

            reader.Skip(2); // padding
        }

       
        internal byte[] GetAudioData(BinaryReader reader)
        {
            var channelIndex = Array.IndexOf(Parent.Channels, this);

            if (channelIndex == Parent.Channels.Length - 1)
            {
                int dataSize = Parent.FileSize - (int) AudioOffset;

                return reader.ReadByteArrayAt(AudioOffset, dataSize);
            } 
            else
            {
                int dataSize = (int) Parent.Channels[channelIndex + 1].AudioOffset - (int)AudioOffset;

                return reader.ReadByteArrayAt(AudioOffset, dataSize);
            }
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

        private static readonly sbyte[] NibbleToSbyte = [0, 1, 2, 3, 4, 5, 6, 7, -8, -7, -6, -5, -4, -3, -2, -1];

        private static sbyte GetHighNibble(byte value)
        {
            return NibbleToSbyte[(value >> 4) & 0xF];
        }

        private static sbyte GetLowNibble(byte value)
        {
            return NibbleToSbyte[value & 0xF];
        }

        /// <summary>
        /// Decode DSP-ADPCM data.
        /// </summary>
        /// <param name="src">DSP-ADPCM source.</param>
        /// <param name="dst">Destination array of samples.</param>
        /// <param name="cxt">DSP-APCM context.</param>
        /// <param name="samples">Number of samples.</param>
        public static void Decode(byte[] src, ref short[] dst, short hist1, short hist2, short[][] coefs, int samples)
        {
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
                        short sample = (short) Math.Clamp(val, short.MinValue, short.MaxValue);

                        hist2 = hist1;
                        hist1 = sample;
                        dst[dstIndex++] = sample;

                        if (dstIndex >= samples) break;
                    }

                    if (dstIndex >= samples) break;
                }
            }
        }
    }

    public int FileSize { get; }
    public bool IsBigEndian { get; }
    public ushort Version { get; }
    private uint Hash { get; }
    public ushort IsPrefetch { get; }
    public AudioMetadata Metadata { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public BinaryWaveChannel[] Channels { get; }

    public BinaryWaveFile(byte[] buffer, AudioMetadata metadata = null)
    {
        using var stream = new MemoryStream(buffer);
        using var reader = new BinaryReader(stream);

        Metadata = metadata;

        var MAGIC = reader.ReadString(4);
        if (MAGIC != "BWAV") throw new Exception($"File is not a BWAV ({MAGIC})!");

        FileSize = buffer.Length;
        IsBigEndian = reader.BigEndian = reader.ReadUInt16() == 0xFFFE;
        Version = reader.ReadUInt16();
        Hash = reader.ReadUInt32();
        IsPrefetch = reader.ReadUInt16();
        var channelCount = reader.ReadUInt16();

        Channels = new BinaryWaveChannel[channelCount];

        // Read channels and pass this BinaryWaveFile instance so GetAudioData() can access the file size and other channels.
        for (int i = 0; i < channelCount; i++)
            Channels[i] = new BinaryWaveChannel(reader, this);

        foreach (var channel in Channels)
            channel.ChannelData = channel.GetAudioData(reader);
    }

    public bool ValidateHash(string fileName) => fileName.ToCRC32() != Hash;
}