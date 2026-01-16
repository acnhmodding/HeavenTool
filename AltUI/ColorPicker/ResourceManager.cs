// Cyotek Color Picker controls library
// http://cyotek.com/blog/tag/colorpicker

// Copyright © 2021 Cyotek Ltd.

// This work is licensed under the MIT License.
// See LICENSE.TXT for the full text

// Found this code useful?
// https://www.cyotek.com/contribute

using AltUI.Config;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AltUI.ColorPicker
{
    internal static class ResourceManager
    {
        #region Public Properties

        public static Image CellBackground => ThemeProvider.LightMode ? Resources.cellbackground : Resources.lcellbackground;

        public static Cursor EyeDropper => new(new MemoryStream(Resources.eyedroppercur));

        public static Image LoadPalette => Resources.palette_load;

        public static Image SavePalette => Resources.palette_save;

        public static Image ScreenPicker => Resources.eyedropper;

        #endregion Public Properties
    }
}
;