using AltUI.Config;
using System.Drawing;
using System.Windows.Forms;

namespace HeavenTool.Forms.Components;

public class DarkMenuRenderer(DarkColorTable darkColorTable) : ToolStripProfessionalRenderer(darkColorTable)
{
    public DarkColorTable DarkColorTable = darkColorTable;

    public DarkMenuRenderer() : this(new DarkColorTable())
    {

    }

    protected override void InitializeItem(ToolStripItem item)
    {
        base.InitializeItem(item);

        item.BackColor =  DarkColorTable.ToolStripDropDownBackground;
        item.ForeColor = Color.White;

        if (item.GetType() == typeof(ToolStripSeparator))
        {
            item.Margin = new Padding(0, 0, 0, 1);
        }
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Color.White;
        base.OnRenderArrow(e);
    }
}

public class DarkMenuStrip : MenuStrip
{
    public DarkMenuStrip()
    {
        BackColor = Color.Transparent;
        ForeColor = Color.White;

        Renderer = new DarkMenuRenderer();
    }
}

public class DarkStatusStrip : StatusStrip { 
    
    public DarkStatusStrip() : base()
    {
        BackColor = Color.Transparent;
        ForeColor = Color.White;

        Renderer = new DarkMenuRenderer();
    }
}

public class DarkContextMenuStrip : ContextMenuStrip
{
    public DarkContextMenuStrip() 
    {
        BackColor = Color.Transparent;
        ForeColor = Color.White;

        Renderer = new DarkMenuRenderer();
    }
}

public class DarkColorTable : ProfessionalColorTable
{
    public readonly Color background = ThemeProvider.Theme.Colors.GreyBackground;
    public readonly Color darkColor1 = ThemeProvider.Theme.Colors.LightBorder;
    public readonly Color highlightColor = ThemeProvider.Theme.Colors.LightText;

    public override Color CheckBackground => highlightColor;
    public override Color CheckPressedBackground => highlightColor;
    public override Color CheckSelectedBackground => highlightColor;

    public override Color StatusStripBorder => darkColor1;
    public override Color StatusStripGradientBegin => darkColor1;
    public override Color StatusStripGradientEnd => darkColor1;

    public override Color GripDark => Color.RebeccaPurple;
    public override Color GripLight => Color.Pink;

    public override Color ToolStripBorder => darkColor1;

    public override Color MenuItemBorder => Color.Empty;
    public override Color MenuItemSelected => darkColor1;
    public override Color MenuItemSelectedGradientBegin => darkColor1;
    public override Color MenuItemSelectedGradientEnd => darkColor1;
    public override Color MenuItemPressedGradientBegin => darkColor1;
    public override Color MenuItemPressedGradientMiddle => darkColor1;
    public override Color MenuItemPressedGradientEnd => darkColor1;

    public override Color ToolStripDropDownBackground => background;

    public override Color ImageMarginGradientBegin => Color.Empty;
    public override Color ImageMarginGradientMiddle => Color.Empty;
    public override Color ImageMarginGradientEnd => Color.Empty;

    public override Color ButtonSelectedBorder => Color.Empty;
    public override Color ButtonPressedBorder => Color.Empty;
    public override Color ButtonSelectedGradientBegin => darkColor1;
    public override Color ButtonSelectedGradientMiddle => darkColor1;
    public override Color ButtonSelectedGradientEnd => darkColor1;
    public override Color ButtonPressedGradientBegin => darkColor1;
    public override Color ButtonPressedGradientMiddle => darkColor1;
    public override Color ButtonPressedGradientEnd => darkColor1;
    public override Color ButtonCheckedGradientBegin => darkColor1;
    public override Color ButtonCheckedGradientMiddle => darkColor1;
    public override Color ButtonCheckedGradientEnd => darkColor1;
    public override Color ButtonSelectedHighlightBorder => Color.Empty;
    public override Color ButtonPressedHighlightBorder => Color.Empty;
    public override Color ButtonCheckedHighlightBorder => Color.Empty;
    public override Color ButtonSelectedHighlight => darkColor1;
    public override Color ButtonPressedHighlight => darkColor1;
    public override Color ButtonCheckedHighlight => darkColor1;

    public override Color ToolStripPanelGradientBegin => darkColor1;
    public override Color ToolStripPanelGradientEnd => darkColor1;

    public override Color ToolStripContentPanelGradientBegin => darkColor1;
    public override Color ToolStripContentPanelGradientEnd => darkColor1;

    public override Color SeparatorLight => Color.Empty;
    public override Color SeparatorDark => darkColor1;
}

