using AltUI.Config;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AltUI.Controls
{
    public class DarkSectionPanel : Panel
    {
        #region Field Region

        private string _sectionHeader;

        #endregion

        #region Property Region

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Padding Padding => base.Padding;

        [Category("Appearance")]
        [Description("The section header text associated with this control.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string SectionHeader
        {
            get => _sectionHeader;
            set
            {
                _sectionHeader = value;
                Invalidate();
            }
        }

        #endregion

        #region Constructor Region

        public DarkSectionPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);
            BackColor = ThemeProvider.Theme.Colors.OpaqueBackground;
            foreach (Control c in Controls)
            {
                c.BackColor = ThemeProvider.Theme.Colors.OpaqueBackground;
            }

            base.Padding = new Padding(1, 25, 1, 1);
        }

        #endregion

        #region Event Handler Region

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);

            Invalidate();
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);

            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (Controls.Count > 0)
                Controls[0].Focus();
        }

        #endregion

        #region Paint Region

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var rect = ClientRectangle;

            // Fill body
            using (var b = new SolidBrush(ThemeProvider.Theme.Colors.GreyBackground))
            {
                g.FillRectangle(b, rect);
            }

            // Draw header
            var bgColor = ContainsFocus ? ThemeProvider.Theme.Colors.BlueBackground : ThemeProvider.Theme.Colors.HeaderBackground;
            var borderColor = ContainsFocus ? ThemeProvider.Theme.Colors.DarkBlueBorder : ThemeProvider.Theme.Colors.GreySelection;

            using (var b = new SolidBrush(bgColor))
            {
                var bgRect = new Rectangle(0, 0, rect.Width - 1, 25);
                g.FillRoundedRectangle(b, bgRect, 4, true);
            }

            using (var p = new Pen(borderColor))
            {
                g.DrawLine(p, rect.Left, 25 - 1, rect.Right, 25 - 1);
            }

            var xOffset = 3;

            using (var b = new SolidBrush(ThemeProvider.Theme.Colors.LightText))
            {
                var textRect = new Rectangle(xOffset, 0, rect.Width - 4 - xOffset, 25);

                var format = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    FormatFlags = StringFormatFlags.NoWrap,
                    Trimming = StringTrimming.EllipsisCharacter
                };

                g.DrawString(SectionHeader, Font, b, textRect, format);
            }

            // Draw border
            using (var p = new Pen(borderColor, 1))
            {
                var modRect = new Rectangle(rect.Left, rect.Top, rect.Width - 1, rect.Height - 1);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawRoundedRectangle(p, modRect, 4);
                g.SmoothingMode = SmoothingMode.None;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Absorb event
        }

        #endregion
    }
}
