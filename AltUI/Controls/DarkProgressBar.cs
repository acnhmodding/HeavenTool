using AltUI.Config;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AltUI.Controls
{
    public class DarkProgressBar : ProgressBar
    {
        private string _text;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override string Text
        {
            get => _text;
            set { _text = value; Invalidate(); }
        }
        public DarkProgressBar()
        {
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clip = new Region(ClientRectangle);
            var modRect = new Rectangle(0, 0, Width - 1, Height + 1);
            using (var b = new SolidBrush(ThemeProvider.Theme.Colors.GreyBackground))
            {
                g.FillRectangle(b, ClientRectangle);
            }
            using (var b = new SolidBrush(ThemeProvider.Theme.Colors.LightBackground))
            {
                g.FillRoundedRectangle(b, modRect, 4);
            }
            using (var b = new SolidBrush(ThemeProvider.Theme.Colors.BlueHighlight))
            {
                var barWidth = (int)(modRect.Width * ((double)Value / Maximum));
                var barRect = new Rectangle(0, 0, barWidth, modRect.Height - 2);
                switch (barWidth)
                {
                    case 0:
                        goto skip;
                    case 1:
                        barRect = new Rectangle(0, modRect.Y + 1, barWidth, modRect.Height - 2);
                        break;
                    case 2:
                        barRect = new Rectangle(0, modRect.Y + 2, barWidth, modRect.Height - 4);
                        break;
                }
                modRect.Height -= 2;
                g.FillRoundedRectangle(b, barRect, 4);
            }
        skip:
            using (var p = new Pen(ThemeProvider.Theme.Colors.GreySelection))
            {
                g.DrawRoundedRectangle(p, modRect, 4);
            }
            base.OnPaint(e);
            using (var p = new Pen(ThemeProvider.Theme.Colors.LightText))
            {
                g.DrawString(Text, Font, p.Brush, new Point(modRect.X + 2, modRect.Height / 2 - Font.Height / 2));
            }
        }
        protected override void OnVisibleChanged(EventArgs e)
        {
            Invalidate();
            base.OnVisibleChanged(e);
        }
    }
}