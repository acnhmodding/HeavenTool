using System.ComponentModel;
using System.Drawing;

namespace AltUI.ColorPicker
{
    public class EditColorCancelEventArgs : CancelEventArgs
    {
        #region Constructors

        public EditColorCancelEventArgs(Color color, int colorIndex)
        {
            Color = color;
            ColorIndex = colorIndex;
        }

        protected EditColorCancelEventArgs()
        { }

        #endregion

        #region Properties

        public Color Color { get; protected set; }

        public int ColorIndex { get; protected set; }

        #endregion
    }
}
