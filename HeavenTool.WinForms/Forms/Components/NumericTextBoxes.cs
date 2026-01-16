using System.ComponentModel;
using System.Text;

namespace HeavenTool.Forms.Components;

[ToolboxItem(true)]
[DisplayName("int32 TextBox")]
public class Int32TextBox : ValidatingTextBox
{
    protected override void OnTextValidating(object sender, TextValidatingEventArgs e)
    {
        e.Cancel = !int.TryParse(e.NewText, out int _);
    }
}

[ToolboxItem(true)]
[DisplayName("uint32 TextBox")]
public class UInt32TextBox : ValidatingTextBox
{
    protected override void OnTextValidating(object sender, TextValidatingEventArgs e)
    {
        e.Cancel = !uint.TryParse(e.NewText, out uint _);
    }
}

[ToolboxItem(true)]
[DisplayName("uint64 TextBox")]
public class UInt64TextBox : ValidatingTextBox
{
    protected override void OnTextValidating(object sender, TextValidatingEventArgs e)
    {
        e.Cancel = !ulong.TryParse(e.NewText, out ulong _);
    }
}

[ToolboxItem(true)]
[DisplayName("byte TextBox")]
public class ByteTextBox : ValidatingTextBox
{
    protected override void OnTextValidating(object sender, TextValidatingEventArgs e)
    {
        e.Cancel = !byte.TryParse(e.NewText, out byte _);
    }
}

[ToolboxItem(true)]
[DisplayName("sbyte TextBox")]
public class SByteTextBox : ValidatingTextBox
{
    protected override void OnTextValidating(object sender, TextValidatingEventArgs e)
    {
        e.Cancel = !sbyte.TryParse(e.NewText, out sbyte _);
    }
}

[ToolboxItem(true)]
[DisplayName("int16 TextBox")]
public class Int16TextBox : ValidatingTextBox
{
    protected override void OnTextValidating(object sender, TextValidatingEventArgs e)
    {
        e.Cancel = !short.TryParse(e.NewText, out short _);
    }
}

[ToolboxItem(true)]
[DisplayName("uint16 TextBox")]
public class UInt16TextBox : ValidatingTextBox
{
    protected override void OnTextValidating(object sender, TextValidatingEventArgs e)
    {
        e.Cancel = !ushort.TryParse(e.NewText, out ushort _);
    }
}



[ToolboxItem(true)]
[DisplayName("string TextBox")]
public class StringTextBox : ValidatingTextBox
{
    public int ByteSizeLimit = 64;

    protected override void OnTextValidating(object sender, TextValidatingEventArgs e)
    {
        var bytes = Encoding.UTF8.GetBytes(e.NewText);
        e.Cancel = bytes.Length >= ByteSizeLimit;
    }
}


[ToolboxItem(true)]
[DisplayName("float32 TextBox")]
public class Float32TextBox : ValidatingTextBox
{
    protected override void OnTextValidating(object sender, TextValidatingEventArgs e)
    {
        e.Cancel = !float.TryParse(e.NewText, out float _);
    }
}

[ToolboxItem(true)]
[DisplayName("float64 TextBox")]
public class Float64TextBox : ValidatingTextBox
{
    protected override void OnTextValidating(object sender, TextValidatingEventArgs e)
    {
        e.Cancel = !double.TryParse(e.NewText, out double _);
    }
}