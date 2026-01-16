using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HeavenTool.Forms.Components
{
    public partial class CustomWaveViewer : UserControl
    {
        private WaveStream waveStream;
        private int samplesPerPixel = 128;
        private long startPosition;
        private int bytesPerSample;
        private Color waveColor = Color.White;

        public CustomWaveViewer()
        {
            InitializeComponent();
            DoubleBuffered = true;
        }

        /// <summary>
        /// sets the associated wavestream
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public WaveStream WaveStream
        {
            get
            {
                return waveStream;
            }
            set
            {
                waveStream = value;
                if (waveStream != null)
                {
                    bytesPerSample = (waveStream.WaveFormat.BitsPerSample / 8) * waveStream.WaveFormat.Channels;
                }
                this.Invalidate();
            }
        }

        /// <summary>
        /// The zoom level, in samples per pixel
        /// </summary>
        [DefaultValue(128)]
        public int SamplesPerPixel
        {
            get
            {
                return samplesPerPixel;
            }
            set
            {
                samplesPerPixel = value;
                this.Invalidate();
            }
        }

        /// <summary>
        /// Start position (currently in bytes)
        /// </summary>
        [DefaultValue(0)]
        public long StartPosition
        {
            get
            {
                return startPosition;
            }
            set
            {
                startPosition = value;
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color WaveColor
        {
            get
            {
                return waveColor;
            }

            set
            {
                waveColor = value;
                penColor = new Pen(waveColor);
                Invalidate();
            }
        }

        private Pen penColor;

        protected override void OnPaint(PaintEventArgs e)
        {
            penColor ??= new Pen(waveColor);

            if (waveStream != null)
            {
                waveStream.Position = 0;
                int bytesRead;
                byte[] waveData = new byte[samplesPerPixel * bytesPerSample];
                waveStream.Position = startPosition + (e.ClipRectangle.Left * bytesPerSample * samplesPerPixel);

                for (float x = e.ClipRectangle.X; x < e.ClipRectangle.Right; x += 1)
                {
                    short low = 0;
                    short high = 0;
                    bytesRead = waveStream.Read(waveData, 0, samplesPerPixel * bytesPerSample);
                    if (bytesRead == 0)
                        break;
                    for (int n = 0; n < bytesRead; n += 2)
                    {
                        short sample = BitConverter.ToInt16(waveData, n);
                        if (sample < low) low = sample;
                        if (sample > high) high = sample;
                    }
                    float lowPercent = ((((float)low) - short.MinValue) / ushort.MaxValue);
                    float highPercent = ((((float)high) - short.MinValue) / ushort.MaxValue);
                    e.Graphics.DrawLine(penColor, x, Height * lowPercent, x, Height * highPercent);
                }
            }

            base.OnPaint(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (e.Delta > 0)
                ZoomIn();
            else
                ZoomOut();

            base.OnMouseWheel(e);
        }

        public void ZoomIn()
        {
            SamplesPerPixel = Math.Max(16, SamplesPerPixel / 2); // finer detail
        }

        public void ZoomOut()
        {
            SamplesPerPixel = Math.Min(4096, SamplesPerPixel * 2); // coarser detail
        }

        public void ResetZoom()
        {
            SamplesPerPixel = 128;
        }

        private bool isDragging = false;
        private Point lastMousePos;
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = true;
                lastMousePos = e.Location;
                Cursor = Cursors.Hand;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (isDragging && waveStream != null)
            {
                int dx = e.X - lastMousePos.X;

                // how many bytes correspond to one pixel?
                long bytesPerPixel = samplesPerPixel * bytesPerSample;

                // shift start position
                StartPosition = Math.Max(0, StartPosition - dx * bytesPerPixel);

                lastMousePos = e.Location;
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = false;
                Cursor = Cursors.Default;
            }
            base.OnMouseUp(e);
        }
    }
}
