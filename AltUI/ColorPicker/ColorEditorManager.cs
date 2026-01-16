// Cyotek Color Picker Controls Library
// http://cyotek.com/blog/tag/colorpicker

// Copyright © 2013-2021 Cyotek Ltd.

// This work is licensed under the MIT License.
// See LICENSE.TXT for the full text

// Found this code useful?
// https://www.cyotek.com/contribute

using System;
using System.ComponentModel;
using System.Drawing;

namespace AltUI.ColorPicker
{
    /// <summary>
    /// Represents a control that binds multiple editors together as a single composite unit.
    /// </summary>
    [DefaultEvent(nameof(ColorChanged))]
    [DefaultProperty(nameof(Color))]
    [ToolboxBitmap(typeof(ColorEditorManager), "ColorEditorManagerToolboxBitmap.bmp")]
    [ToolboxItem(true)]
    public class ColorEditorManager : Component, IColorEditor
    {
        #region Private Fields

        private const double _rgbMultiplier = 0.00392156862745098;

        private static readonly object _eventColorChanged = new object();

        private static readonly object _eventColorEditorChanged = new object();

        private static readonly object _eventColorGridChanged = new object();

        private static readonly object _eventColorWheelChanged = new object();

        private static readonly object _eventLightnessColorSliderChanged = new object();

        private static readonly object _eventScreenColorPickerChanged = new object();

        private Color _color;

        private ColorEditor _colorEditor;

        private ColorGrid _colorGrid;

        private ColorWheel _colorWheel;

        private HslColor _hslColor;

        private LightnessColorSlider _lightnessColorSlider;

        private bool _lockUpdates;

        private ScreenColorPicker _screenColorPicker;

        #endregion Private Fields

        #region Public Events

        [Category("Property Changed")]
        public event EventHandler ColorChanged
        {
            add => Events.AddHandler(_eventColorChanged, value);
            remove => Events.RemoveHandler(_eventColorChanged, value);
        }

        [Category("Property Changed")]
        public event EventHandler ColorEditorChanged
        {
            add => Events.AddHandler(_eventColorEditorChanged, value);
            remove => Events.RemoveHandler(_eventColorEditorChanged, value);
        }

        [Category("Property Changed")]
        public event EventHandler ColorGridChanged
        {
            add => Events.AddHandler(_eventColorGridChanged, value);
            remove => Events.RemoveHandler(_eventColorGridChanged, value);
        }

        [Category("Property Changed")]
        public event EventHandler ColorWheelChanged
        {
            add => Events.AddHandler(_eventColorWheelChanged, value);
            remove => Events.RemoveHandler(_eventColorWheelChanged, value);
        }

        [Category("Property Changed")]
        public event EventHandler LightnessColorSliderChanged
        {
            add => Events.AddHandler(_eventLightnessColorSliderChanged, value);
            remove => Events.RemoveHandler(_eventLightnessColorSliderChanged, value);
        }

        [Category("Property Changed")]
        public event EventHandler ScreenColorPickerChanged
        {
            add => Events.AddHandler(_eventScreenColorPickerChanged, value);
            remove => Events.RemoveHandler(_eventScreenColorPickerChanged, value);
        }

        #endregion Public Events

        #region Public Properties

        /// <summary>
        /// Gets or sets the component color.
        /// </summary>
        /// <value>The component color.</value>
        [Category("Appearance")]
        [DefaultValue(typeof(Color), "Empty")]
        public Color Color
        {
            get => _color;
            set
            {
                if (_color != value)
                {
                    _color = value;
                    _hslColor = new HslColor(value);

                    OnColorChanged(EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets or sets the linked <see cref="ColorEditor"/>.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(typeof(ColorEditor), null)]
        public ColorEditor ColorEditor
        {
            get => _colorEditor;
            set
            {
                if (!ReferenceEquals(_colorEditor, value))
                {
                    if (_colorEditor != null)
                    {
                        UnbindEvents(_colorEditor);
                    }

                    _colorEditor = value;

                    OnColorEditorChanged(EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets or sets the linked <see cref="ColorGrid"/>.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(typeof(ColorGrid), null)]
        public ColorGrid ColorGrid
        {
            get => _colorGrid;
            set
            {
                if (!ReferenceEquals(_colorGrid, value))
                {
                    if (_colorGrid != null)
                    {
                        UnbindEvents(_colorGrid);
                    }

                    _colorGrid = value;

                    OnColorGridChanged(EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets or sets the linked <see cref="ColorWheel"/>.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(typeof(ColorWheel), null)]
        public ColorWheel ColorWheel
        {
            get => _colorWheel;
            set
            {
                if (!ReferenceEquals(_colorWheel, value))
                {
                    if (_colorWheel != null)
                    {
                        UnbindEvents(_colorWheel);
                    }

                    _colorWheel = value;

                    OnColorWheelChanged(EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets or sets the component color as a HSL structure.
        /// </summary>
        /// <value>The component color.</value>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public HslColor HslColor
        {
            get => _hslColor;
            set
            {
                if (HslColor != value)
                {
                    _hslColor = value;
                    _color = value.ToRgbColor();

                    OnColorChanged(EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets or sets the linked <see cref="LightnessColorSlider"/>.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(typeof(LightnessColorSlider), null)]
        public LightnessColorSlider LightnessColorSlider
        {
            get => _lightnessColorSlider;
            set
            {
                if (!ReferenceEquals(_lightnessColorSlider, value))
                {
                    if (_lightnessColorSlider != null)
                    {
                        UnbindEvents(_lightnessColorSlider);
                    }

                    _lightnessColorSlider = value;

                    OnLightnessColorSliderChanged(EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets or sets the linked <see cref="ScreenColorPicker"/>.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(typeof(ScreenColorPicker), null)]
        public ScreenColorPicker ScreenColorPicker
        {
            get => _screenColorPicker;
            set
            {
                if (!ReferenceEquals(_screenColorPicker, value))
                {
                    if (_screenColorPicker != null)
                    {
                        UnbindEvents(_screenColorPicker);
                    }

                    _screenColorPicker = value;

                    OnScreenColorPickerChanged(EventArgs.Empty);
                }
            }
        }

        #endregion Public Properties

        #region Protected Properties

        /// <summary>
        /// Gets or sets a value indicating whether updating of linked components is disabled.
        /// </summary>
        /// <value><c>true</c> if updated of linked components is disabled; otherwise, <c>false</c>.</value>
        protected bool LockUpdates
        {
            get => _lockUpdates;
            set => _lockUpdates = value;
        }

        #endregion Protected Properties

        #region Protected Methods

        /// <summary>
        /// Binds events for the specified editor.
        /// </summary>
        /// <param name="control">The <see cref="IColorEditor"/> to bind to.</param>
        protected virtual void BindEvents(IColorEditor control)
        {
            switch (control)
            {
                case ColorWheel colorWheel:
                    colorWheel.HslColorChanged += HslColorChangedHandler;
                    break;

                case LightnessColorSlider lightnessColorSlider:
                    lightnessColorSlider.ValueChanged += ColorChangedHandler;
                    break;

                default:
                    control.ColorChanged += ColorChangedHandler;
                    break;
            }
        }

        /// <summary>
        /// Raises the <see cref="ColorChanged" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnColorChanged(EventArgs e)
        {
            EventHandler handler;

            Synchronize(this);

            handler = (EventHandler)Events[_eventColorChanged];

            handler?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the <see cref="ColorEditorChanged" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnColorEditorChanged(EventArgs e)
        {
            EventHandler handler;

            if (ColorEditor != null)
            {
                BindEvents(ColorEditor);
            }

            handler = (EventHandler)Events[_eventColorEditorChanged];

            handler?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the <see cref="ColorGridChanged" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnColorGridChanged(EventArgs e)
        {
            EventHandler handler;

            if (_colorGrid != null)
            {
                BindEvents(_colorGrid);
            }

            handler = (EventHandler)Events[_eventColorGridChanged];

            handler?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the <see cref="ColorWheelChanged" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnColorWheelChanged(EventArgs e)
        {
            EventHandler handler;

            if (_colorWheel != null)
            {
                BindEvents(_colorWheel);
            }

            handler = (EventHandler)Events[_eventColorWheelChanged];

            handler?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the <see cref="LightnessColorSliderChanged" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnLightnessColorSliderChanged(EventArgs e)
        {
            EventHandler handler;

            if (_lightnessColorSlider != null)
            {
                BindEvents(_lightnessColorSlider);
            }

            handler = (EventHandler)Events[_eventLightnessColorSliderChanged];

            handler?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the <see cref="ScreenColorPickerChanged" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnScreenColorPickerChanged(EventArgs e)
        {
            EventHandler handler;

            if (_screenColorPicker != null)
            {
                BindEvents(_screenColorPicker);
            }

            handler = (EventHandler)Events[_eventScreenColorPickerChanged];

            handler?.Invoke(this, e);
        }

        /// <summary>
        /// Sets the color of the given editor.
        /// </summary>
        /// <param name="control">The <see cref="IColorEditor"/> to update.</param>
        /// <param name="sender">The <see cref="IColorEditor"/> triggering the update.</param>
        protected virtual void SetColor(IColorEditor control, IColorEditor sender)
        {
            // Interface is fairly useless as it only allows for RGB
            // which completely breaks for colours with 0% or 100%
            // lightness

            if (control != null && !ReferenceEquals(control, sender))
            {
                if (control is ColorEditor colorEditor)
                {
                    colorEditor.HslColor = _hslColor;
                }
                else if (control is ColorWheel colorWheel)
                {
                    colorWheel.HslColor = _hslColor;
                }
                else
                {
                    control.Color = sender.Color;
                }
            }
        }

        /// <summary>
        /// Synchronizes linked components with the specified <see cref="IColorEditor"/>.
        /// </summary>
        /// <param name="sender">The <see cref="IColorEditor"/> triggering the update.</param>
        protected virtual void Synchronize(IColorEditor sender)
        {
            if (!_lockUpdates)
            {
                try
                {
                    _lockUpdates = true;

                    if (ReferenceEquals(sender, this))
                    {
                        if (_colorWheel != null)
                        {
                            _colorWheel.Lightness = _hslColor.L;
                            _colorWheel.Alpha = _color.A * _rgbMultiplier;
                        }
                    }
                    else if (ReferenceEquals(sender, _colorEditor))
                    {
                        SyncWheel(_colorEditor.Color.A, _colorEditor.HslColor.L);

                        if (_lightnessColorSlider != null)
                        {
                            _lightnessColorSlider.Value = (float)(_colorEditor.HslColor.L * 100);
                        }
                    }
                    else if (ReferenceEquals(sender, _lightnessColorSlider))
                    {
                        if (_colorWheel != null)
                        {
                            _colorWheel.Lightness = _lightnessColorSlider.Value / 100D;
                        }

                        if (_colorEditor != null)
                        {
                            HslColor color;

                            color = _colorEditor.HslColor;
                            color.L = _lightnessColorSlider.Value / 100D;
                            _colorEditor.HslColor = color;
                        }
                    }
                    else if (ReferenceEquals(sender, _colorGrid))
                    {
                        SyncWheel(_colorGrid.Color.A, _colorGrid.Color.GetBrightness());
                    }

                    SetColor(_colorGrid, sender);
                    SetColor(_colorWheel, sender);
                    SetColor(_screenColorPicker, sender);
                    if (!ReferenceEquals(sender, _lightnessColorSlider))
                    {
                        SetColor(ColorEditor, sender);
                    }

                    SetColor(_lightnessColorSlider, sender);
                }
                finally
                {
                    _lockUpdates = false;
                }
            }
        }

        protected virtual void UnbindEvents(IColorEditor control)
        {
            switch (control)
            {
                case ColorWheel colorWheel:
                    colorWheel.HslColorChanged -= HslColorChangedHandler;
                    break;

                case LightnessColorSlider lightnessColorSlider:
                    lightnessColorSlider.ValueChanged -= ColorChangedHandler;
                    break;

                default:
                    control.ColorChanged -= ColorChangedHandler;
                    break;
            }
        }

        #endregion Protected Methods

        #region Private Methods

        /// <summary>
        /// Handler for linked controls.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void ColorChangedHandler(object sender, EventArgs e)
        {
            if (!_lockUpdates)
            {
                IColorEditor source;

                source = (IColorEditor)sender;

                _lockUpdates = true;
                Color = source.Color;
                _lockUpdates = false;
                Synchronize(source);
            }
        }

        private void HslColorChangedHandler(object sender, EventArgs e)
        {
            if (!_lockUpdates)
            {
                HslColor color;

                if (sender is ColorWheel colorWheel)
                {
                    color = colorWheel.HslColor;
                }
                else
                {
                    color = HslColor.Empty;
                }

                _lockUpdates = true;
                HslColor = color;
                Color = color.ToRgbColor(Convert.ToInt32(_colorWheel.Alpha * 255));
                _lockUpdates = false;
                Synchronize((IColorEditor)sender);
            }
        }

        private void SyncWheel(byte alpha, double lightness)
        {
            if (_colorWheel != null)
            {
                _colorWheel.Lightness = lightness;
                _colorWheel.Alpha = alpha * _rgbMultiplier;
            }
        }

        #endregion Private Methods
    }
}
