using System.Text;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS;

public class StringPool
{
    public Dictionary<string, List<WriterScopePointer>> Strings { get; private set; } = [];

    public void Write(BinaryWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        // Need to order 
        var comparer = Comparer<byte[]>.Create((a, b) => a.SequenceCompareTo(b));
        var ordered = Strings.OrderBy(x => Encoding.UTF8.GetBytes(x.Key), comparer);

        foreach (var (str, scopes) in ordered)
        {
            foreach (var scope in scopes)
            {
                var offset = (uint) (writer.Position - scope.Position);
                scope.Resolve(offset);
            }
            writer.WriteTerminatedString(str);
        }
    }

    public WriterScopePointer AddString(BinaryWriter writer, string str)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(str);

        var pointer = writer.CreatePointer();

        // Add to the strings list so we can resolve the offset later
        if (Strings.TryGetValue(str, out List<WriterScopePointer>? value))
            value.Add(pointer);
        else Strings[str] = [pointer];

        return pointer;
    }
}
