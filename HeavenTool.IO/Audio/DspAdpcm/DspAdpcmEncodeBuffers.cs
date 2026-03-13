namespace HeavenTool.IO.Audio.DspAdpcm;

public class DspAdpcmEncodeBuffers
{
    public short[][] Coefs { get; } = new short[8][];
    public int[][] PcmOut { get; } = new int[8][];
    public int[][] AdpcmOut { get; } = new int[8][];
    public int[] Scale { get; } = new int[8];
    public double[] TotalDistance { get; } = new double[8];

    public DspAdpcmEncodeBuffers()
    {
        for (int i = 0; i < 8; i++)
        {
            PcmOut[i] = new int[16];
            AdpcmOut[i] = new int[14];
            Coefs[i] = new short[2];
        }
    }
}
