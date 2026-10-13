using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace HeavenTool.Forms.BCSV.Controls;

public sealed class BCSVEntryPanel : TableLayoutPanel
{
    private readonly HashSet<Control> hiddenDuringResize = [];
    private bool interactiveResize;
    private bool updatingVisibility;

    public BCSVEntryPanel()
    {
        DoubleBuffered = true;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
            return parameters;
        }
    }

    public void BeginResize()
    {
        if (interactiveResize || IsDisposed || Disposing)
            return;

        interactiveResize = true;
        if (UpdateResizeVisibility())
            PerformLayout();

    }

    public void EndResize()
    {
        if (!interactiveResize)
            return;

        interactiveResize = false;
        if (IsDisposed || Disposing)
            return;

        SuspendLayout();
        try
        {
            foreach (var entry in hiddenDuringResize)
                if (!entry.IsDisposed && entry.Parent == this)
                    entry.Visible = true;
            hiddenDuringResize.Clear();
        }
        finally
        {
            ResumeLayout(true);
        }
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        UpdateResizeVisibility();
        base.OnLayout(e);
    }

    protected override void OnScroll(ScrollEventArgs se)
    {
        base.OnScroll(se);
        if (UpdateResizeVisibility())
            PerformLayout();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (UpdateResizeVisibility())
            PerformLayout();
    }

    private bool UpdateResizeVisibility()
    {
        if (!interactiveResize || updatingVisibility || IsDisposed || Disposing)
            return false;

        var heights = GetRowHeights();
        var rowTops = new int[RowStyles.Count + 1];
        rowTops[0] = Padding.Top;
        for (int row = 0; row < RowStyles.Count; row++)
            rowTops[row + 1] = rowTops[row] + (row < heights.Length && heights[row] > 0
                ? heights[row] : (int)Math.Round(RowStyles[row].Height));

        int viewTop = -AutoScrollPosition.Y;
        int viewBottom = viewTop + ClientSize.Height;
        bool changed = false;
        updatingVisibility = true;
        SuspendLayout();
        try
        {
            foreach (Control entry in Controls)
            {
                bool wasHidden = hiddenDuringResize.Contains(entry);
                // Respect controls hidden by the caller rather than by resizing.
                if (!wasHidden && !entry.Visible)
                    continue;

                int row = GetRow(entry);
                bool show = entry.ContainsFocus || row < 0 || row >= RowStyles.Count
                    || (rowTops[row + 1] > viewTop && rowTops[row] < viewBottom);
                if (show == !wasHidden)
                    continue;

                changed = true;
                if (show)
                    hiddenDuringResize.Remove(entry);
                else
                    hiddenDuringResize.Add(entry);
                entry.Visible = show;
            }
        }
        finally
        {
            ResumeLayout(false);
            updatingVisibility = false;
        }
        return changed;
    }
}