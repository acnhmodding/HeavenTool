using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HeavenTool.Avalonia.Views.FileEditors;

public partial class BfresScenePane : UserControl
{
    public BfresScenePane() => AvaloniaXamlLoader.Load(this);
}

public partial class BfresViewportPane : UserControl
{
    public BfresViewportPane() => AvaloniaXamlLoader.Load(this);
}

public partial class BfresMaterialPane : UserControl
{
    public BfresMaterialPane() => AvaloniaXamlLoader.Load(this);
}
