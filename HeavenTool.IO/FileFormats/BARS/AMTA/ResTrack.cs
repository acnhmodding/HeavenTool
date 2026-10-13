using BinaryReader = AeonSake.BinaryTools.BinaryReader;

namespace HeavenTool.IO.FileFormats.BARS.AMTA;

public struct ResTrack
{
    public ResTrack(BinaryReader br)
    {
        var count = br.ReadByte();

        Channels = new ResStreamChannel[count];
        for (byte i = 0; i < count; i++)
        {
            Channels[i] = new ResStreamChannel
            {
                ChannelIndex = br.ReadByte(),
                OutputChannel = (AttenuationChannel)br.ReadByte()
            };
        }
    }

    public ResStreamChannel[] Channels;
}
