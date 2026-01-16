using AltUI.Config;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Security;
using System.Windows.Forms;

namespace AltUI.Controls
{
    public class DarkNumericUpDown : NumericUpDown
    {
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Color ForeColor { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Color BackColor { get; set; }

        private bool _mouseDown;

        public DarkNumericUpDown()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                   ControlStyles.ResizeRedraw |
                   ControlStyles.UserPaint, true);

            base.ForeColor = ThemeProvider.Theme.Colors.LightText;
            base.BackColor = ThemeProvider.Theme.Colors.LightBackground;

            Controls[0].Paint += DarkNumericUpDown_Paint;

            try
            {
                // Prevent flickering, only if our assembly has reflection permission
                var type = Controls[0].GetType();
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
                var method = type.GetMethod("SetStyle", flags);

                if (method == null) return;
                object[] param = { ControlStyles.AllPaintingInWmPaint | ControlStyles.DoubleBuffer, true };
                method.Invoke(Controls[0], param);
            }
            catch (SecurityException)
            {
                // Don't do anything, we are running in a trusted context
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _mouseDown = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _mouseDown = false;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override void OnTextBoxLostFocus(object source, EventArgs e)
        {
            base.OnTextBoxLostFocus(source, e);
            Invalidate();
        }

        private void DarkNumericUpDown_Paint(object sender, PaintEventArgs e)
        {
            CustomPaint(e);
        }

        private void CustomPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var rect = e.ClipRectangle;

            var fillColor = ThemeProvider.Theme.Colors.HeaderBackground;

            using (var b = new SolidBrush(fillColor))
            {
                var modRect = rect with { Y = rect.Y - 1, Height = rect.Height + 1 };
                var modRect2 = new Rectangle(rect.X - 2, rect.Y - 1, rect.Width + 2, rect.Height + 1);

                g.FillRectangle(new SolidBrush(ThemeProvider.Theme.Colors.LightBackground), modRect2);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawRoundedRectangle(new Pen(ThemeProvider.Theme.Colors.GreySelection, 1), modRect, 4);
                g.FillRoundedRectangle(b, modRect, 5);
                g.SmoothingMode = SmoothingMode.None;
            }

            var mousePos = Controls[0].PointToClient(Cursor.Position);

            var upArea = new Rectangle(0, 0, rect.Width, rect.Height / 2);
            var upHot = upArea.Contains(mousePos);

            var arrowColor = upHot ? ThemeProvider.Theme.Colors.ActiveControl : ThemeProvider.Theme.Colors.GreyHighlight;
            if (upHot && _mouseDown)
                arrowColor = ThemeProvider.Theme.Colors.LightText;

            using (var p = new Pen(arrowColor, 1))
            {
                var x = upArea.Width / 2 - 3;
                var y = upArea.Height / 2 - 2;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawLine(p, x, y + 3, x + 3, y);
                g.DrawLine(p, x + 3, y, x + 6, y + 3);
            }

            var downArea = new Rectangle(0, rect.Height / 2, rect.Width, rect.Height / 2);
            var downHot = downArea.Contains(mousePos);

            arrowColor = downHot ? ThemeProvider.Theme.Colors.ActiveControl : ThemeProvider.Theme.Colors.GreyHighlight;
            if (downHot && _mouseDown)
                arrowColor = ThemeProvider.Theme.Colors.LightText;

            using (var p = new Pen(arrowColor, 1))
            {
                var x = downArea.Width / 2 - 3;
                var y = downArea.Top + downArea.Height / 2 - 2;
                g.DrawLine(p, x, y, x + 3, y + 3);
                g.DrawLine(p, x + 3, y + 3, x + 6, y);
                g.SmoothingMode = SmoothingMode.None;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            CustomPaint(e);

            var g = e.Graphics;
            var rect = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);

            var borderColor = ThemeProvider.Theme.Colors.GreySelection;

            if (Focused && TabStop)
                borderColor = ThemeProvider.Theme.Colors.BlueHighlight;

            using var p = new Pen(borderColor, 1);
            var modRect = new Rectangle(rect.Left, rect.Top, rect.Width - 1, rect.Height - 1);

            g.DrawRectangle(new Pen(ThemeProvider.Theme.Colors.GreyBackground, 2), rect);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillRoundedRectangle(new SolidBrush(ThemeProvider.Theme.Colors.LightBackground), modRect, 4);
            g.DrawRoundedRectangle(p, modRect, 4);
            g.SmoothingMode = SmoothingMode.None;
        }
    }
}
