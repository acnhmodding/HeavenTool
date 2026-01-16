using AltUI.Config;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AltUI.Controls
{
    public class DarkGroupBox : GroupBox
    {
        #region Field Region

        private bool _opaqueBackground;

        #endregion

        #region Property Region

        [Category("Appearance")]
        [Description("Determines whether the background of the GroupBox should be filled in.")]
        [DefaultValue(false)]
        public bool OpaqueBackground
        {
            get => _opaqueBackground;
            set
            {
                _opaqueBackground = value;
                Invalidate();
            }
        }

        #endregion

        public DarkGroupBox()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.UserPaint, true);

            ResizeRedraw = true;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var rect = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
            var stringSize = g.MeasureString(Text, Font);

            var borderColor = ThemeProvider.Theme.Colors.GreySelection;
            var textColor = ThemeProvider.Theme.Colors.LightText;
            var fillColor = ThemeProvider.Theme.Colors.GreyBackground;

            using (var b = new SolidBrush(fillColor))
            {
                g.FillRectangle(b, rect);
            }

            using (var p = new Pen(borderColor, 1))
            {
                var borderRect = new Rectangle(0, (int)stringSize.Height / 2, rect.Width - 1, rect.Height - ((int)stringSize.Height / 2) - 1);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (_opaqueBackground)
                    g.FillRoundedRectangle(new SolidBrush(ThemeProvider.Theme.Colors.LightBackground), borderRect, 4);
                g.DrawRoundedRectangle(p, borderRect, 4);
                g.SmoothingMode = SmoothingMode.None;
            }

            var textRect = new Rectangle(rect.Left + ThemeProvider.Theme.Sizes.Padding,
                    rect.Top,
                    rect.Width - (ThemeProvider.Theme.Sizes.Padding * 2),
                    (int)stringSize.Height);

            using (var b2 = new SolidBrush(ThemeProvider.Theme.Colors.GreyBackground))
            {
                var modRect = new Rectangle(textRect.Left, textRect.Top, Math.Min(textRect.Width, (int)stringSize.Width), textRect.Height);
                g.FillRectangle(b2, modRect);
            }

            using (var b = new SolidBrush(textColor))
            {
                var stringFormat = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Alignment = StringAlignment.Near,
                    FormatFlags = StringFormatFlags.NoWrap,
                    Trimming = StringTrimming.EllipsisCharacter
                };

                g.DrawString(Text, Font, b, textRect, stringFormat);
            }
        }
    }
}
