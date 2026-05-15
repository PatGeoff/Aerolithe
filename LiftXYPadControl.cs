namespace Aerolithe
{
    public sealed class LiftXYPadControl : Control
    {
        private const int KnobRadius = 9;
        private bool _dragging;
        private int _horizontalValue;
        private int _verticalValue;

        public event EventHandler<LiftXYPadChangedEventArgs>? PadChanged;
        public event EventHandler? PadReleased;

        public int HorizontalMinimum { get; set; } = -20;
        public int HorizontalMaximum { get; set; } = 20;
        public int VerticalMinimum { get; set; } = -80;
        public int VerticalMaximum { get; set; } = 80;

        public int HorizontalValue
        {
            get => _horizontalValue;
            private set
            {
                _horizontalValue = Math.Clamp(value, HorizontalMinimum, HorizontalMaximum);
            }
        }

        public int VerticalValue
        {
            get => _verticalValue;
            private set
            {
                _verticalValue = Math.Clamp(value, VerticalMinimum, VerticalMaximum);
            }
        }

        public LiftXYPadControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);

            BackColor = Color.FromArgb(24, 24, 24);
            ForeColor = Color.White;
            Cursor = Cursors.Cross;
            MinimumSize = new Size(140, 140);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            _dragging = true;
            Capture = true;
            SetValuesFromPoint(e.Location);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging) return;

            SetValuesFromPoint(e.Location);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!_dragging) return;

            _dragging = false;
            Capture = false;
            ResetToCenter();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!_dragging)
            {
                Invalidate();
            }
        }

        private void SetValuesFromPoint(Point point)
        {
            var pad = GetPadRectangle();
            int x = Math.Clamp(point.X, pad.Left, pad.Right);
            int y = Math.Clamp(point.Y, pad.Top, pad.Bottom);

            double xRatio = pad.Width <= 0 ? 0.5 : (double)(x - pad.Left) / pad.Width;
            double yRatio = pad.Height <= 0 ? 0.5 : (double)(y - pad.Top) / pad.Height;

            int nextHorizontal = (int)Math.Round(HorizontalMinimum + xRatio * (HorizontalMaximum - HorizontalMinimum));
            int nextVertical = (int)Math.Round(VerticalMaximum - yRatio * (VerticalMaximum - VerticalMinimum));

            if (nextHorizontal == HorizontalValue && nextVertical == VerticalValue)
            {
                return;
            }

            HorizontalValue = nextHorizontal;
            VerticalValue = nextVertical;
            PadChanged?.Invoke(this, new LiftXYPadChangedEventArgs(HorizontalValue, VerticalValue));
            Invalidate();
        }

        public void ResetToCenter()
        {
            bool changed = HorizontalValue != 0 || VerticalValue != 0;
            HorizontalValue = 0;
            VerticalValue = 0;

            if (changed)
            {
                PadChanged?.Invoke(this, new LiftXYPadChangedEventArgs(0, 0));
            }

            PadReleased?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var pad = GetPadRectangle();
            using var backgroundBrush = new SolidBrush(BackColor);
            using var borderPen = new Pen(Color.FromArgb(90, 90, 90));
            using var axisPen = new Pen(Color.FromArgb(75, 75, 75));
            using var activePen = new Pen(Color.FromArgb(70, 150, 220), 2);
            using var knobBrush = new SolidBrush(Color.FromArgb(230, 230, 230));
            using var knobBorderPen = new Pen(Color.FromArgb(35, 35, 35));

            e.Graphics.FillRectangle(backgroundBrush, ClientRectangle);
            e.Graphics.DrawRectangle(borderPen, pad);

            int centerX = pad.Left + pad.Width / 2;
            int centerY = pad.Top + pad.Height / 2;
            e.Graphics.DrawLine(axisPen, centerX, pad.Top, centerX, pad.Bottom);
            e.Graphics.DrawLine(axisPen, pad.Left, centerY, pad.Right, centerY);

            Point knob = GetKnobPoint(pad);
            e.Graphics.DrawLine(activePen, centerX, centerY, knob.X, knob.Y);
            e.Graphics.FillEllipse(knobBrush, knob.X - KnobRadius, knob.Y - KnobRadius, KnobRadius * 2, KnobRadius * 2);
            e.Graphics.DrawEllipse(knobBorderPen, knob.X - KnobRadius, knob.Y - KnobRadius, KnobRadius * 2, KnobRadius * 2);

            TextRenderer.DrawText(
                e.Graphics,
                $"{HorizontalValue}, {VerticalValue}",
                Font,
                new Rectangle(pad.Left + 6, pad.Bottom - 34, pad.Width - 12, 30),
                Color.FromArgb(180, 180, 180),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private Rectangle GetPadRectangle()
        {
            int margin = 12;
            return new Rectangle(
                margin,
                margin,
                Math.Max(1, Width - margin * 2 - 1),
                Math.Max(1, Height - margin * 2 - 1));
        }

        private Point GetKnobPoint(Rectangle pad)
        {
            double xRatio = (double)(HorizontalValue - HorizontalMinimum) / (HorizontalMaximum - HorizontalMinimum);
            double yRatio = (double)(VerticalMaximum - VerticalValue) / (VerticalMaximum - VerticalMinimum);

            return new Point(
                pad.Left + (int)Math.Round(xRatio * pad.Width),
                pad.Top + (int)Math.Round(yRatio * pad.Height));
        }
    }

    public sealed class LiftXYPadChangedEventArgs : EventArgs
    {
        public LiftXYPadChangedEventArgs(int horizontalValue, int verticalValue)
        {
            HorizontalValue = horizontalValue;
            VerticalValue = verticalValue;
        }

        public int HorizontalValue { get; }
        public int VerticalValue { get; }
    }
}
