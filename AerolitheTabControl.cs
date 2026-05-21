using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Aerolithe
{
    [ToolboxItem(true)]
    public class AerolitheTabControl : TabControl
    {
        private const int MinimumTabWidth = 28;

        private Color _tabBackColor = Color.FromArgb(34, 34, 34);
        private Color _selectedTabBackColor = Color.FromArgb(68, 68, 68);
        private Color _tabHoverBackColor = Color.FromArgb(48, 48, 48);
        private Color _tabBorderColor = Color.FromArgb(78, 78, 78);
        private Color _tabTextColor = Color.Gainsboro;
        private Color _selectedTabTextColor = Color.White;
        private Color _pageBackColor = Color.FromArgb(40, 40, 40);
        private int _tabHeight = 34;
        private int _hoveredIndex = -1;
        private bool _updatingTabSize;
        private bool _showTabBorders = true;

        public AerolitheTabControl()
        {
            ConfigureTabControl();
            MinimumSize = new Size(0, _tabHeight + 8);
            DoubleBuffered = true;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw,
                true);
        }

        private bool IsInDesigner
        {
            get
            {
                if (LicenseManager.UsageMode == LicenseUsageMode.Designtime || DesignMode || Site?.DesignMode == true)
                {
                    return true;
                }

                for (Control? parent = Parent; parent != null; parent = parent.Parent)
                {
                    if (parent.Site?.DesignMode == true)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        [Category("Aerolithe")]
        public Color TabBackColor
        {
            get => _tabBackColor;
            set { _tabBackColor = value; Invalidate(); }
        }

        [Category("Aerolithe")]
        public Color SelectedTabBackColor
        {
            get => _selectedTabBackColor;
            set { _selectedTabBackColor = value; Invalidate(); }
        }

        [Category("Aerolithe")]
        public Color TabHoverBackColor
        {
            get => _tabHoverBackColor;
            set { _tabHoverBackColor = value; Invalidate(); }
        }

        [Category("Aerolithe")]
        public Color TabBorderColor
        {
            get => _tabBorderColor;
            set { _tabBorderColor = value; Invalidate(); }
        }

        [Category("Aerolithe")]
        public Color TabTextColor
        {
            get => _tabTextColor;
            set { _tabTextColor = value; Invalidate(); }
        }

        [Category("Aerolithe")]
        public Color SelectedTabTextColor
        {
            get => _selectedTabTextColor;
            set { _selectedTabTextColor = value; Invalidate(); }
        }

        [Category("Aerolithe")]
        public Color PageBackColor
        {
            get => _pageBackColor;
            set { _pageBackColor = value; ApplyPageBackColor(); Invalidate(); }
        }

        [Category("Aerolithe")]
        [DefaultValue(34)]
        public int TabHeight
        {
            get => _tabHeight;
            set
            {
                _tabHeight = Math.Max(24, value);
                MinimumSize = new Size(MinimumSize.Width, _tabHeight + 8);
                RefreshTabLayout();
            }
        }

        public new Size ItemSize
        {
            get => base.ItemSize;
            set
            {
                int height = Math.Max(24, value.Height);
                _tabHeight = height;
                MinimumSize = new Size(MinimumSize.Width, _tabHeight + 8);
                base.ItemSize = new Size(Math.Max(MinimumTabWidth, value.Width), _tabHeight);
                ApplyNativeItemSize(base.ItemSize);
                Invalidate();
            }
        }

        [Category("Aerolithe")]
        [DefaultValue(true)]
        public bool ShowTabBorders
        {
            get => _showTabBorders;
            set { _showTabBorders = value; Invalidate(); }
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            ConfigureTabControl();
            ApplyPageBackColor();
            if (!IsInDesigner)
            {
                RefreshTabLayout();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ConfigureTabControl();
            if (!IsInDesigner)
            {
                RefreshTabLayout();
            }
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            if (!IsInDesigner)
            {
                RefreshTabLayout();
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!IsInDesigner)
            {
                RefreshTabLayout();
            }
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (!IsInDesigner)
            {
                RecalculateTabSize();
            }
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            if (!IsInDesigner)
            {
                RefreshTabLayout();
            }
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            if (e.Control is TabPage page)
            {
                page.BackColor = _pageBackColor;
            }
            if (!IsInDesigner)
            {
                RefreshTabLayout();
            }
        }

        protected override void OnControlRemoved(ControlEventArgs e)
        {
            base.OnControlRemoved(e);
            if (!IsInDesigner)
            {
                RefreshTabLayout();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (!IsInDesigner)
            {
                RefreshTabLayout();
            }
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hovered = GetTabIndexAt(e.Location);
            if (hovered == _hoveredIndex) return;

            int previousHovered = _hoveredIndex;
            _hoveredIndex = hovered;
            InvalidateTab(previousHovered);
            InvalidateTab(_hoveredIndex);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredIndex < 0) return;

            int previousHovered = _hoveredIndex;
            _hoveredIndex = -1;
            InvalidateTab(previousHovered);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= TabPages.Count)
            {
                return;
            }

            Rectangle bounds = e.Bounds;
            bounds.Inflate(-1, -1);

            bool selected = e.Index == SelectedIndex;
            bool hovered = e.Index == _hoveredIndex;
            Color backColor = selected
                ? _selectedTabBackColor
                : hovered ? _tabHoverBackColor : _tabBackColor;
            Color textColor = selected ? _selectedTabTextColor : _tabTextColor;

            using var backBrush = new SolidBrush(backColor);

            e.Graphics.FillRectangle(backBrush, bounds);
            if (_showTabBorders)
            {
                using var borderPen = new Pen(_tabBorderColor);
                e.Graphics.DrawRectangle(borderPen, bounds);
            }

            TextRenderer.DrawText(
                e.Graphics,
                TabPages[e.Index].Text,
                Font,
                bounds,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private int GetTabIndexAt(Point point)
        {
            for (int i = 0; i < TabCount; i++)
            {
                if (GetTabRect(i).Contains(point))
                {
                    return i;
                }
            }

            return -1;
        }

        private void InvalidateTab(int index)
        {
            if (index < 0 || index >= TabCount || !IsHandleCreated)
            {
                return;
            }

            Rectangle bounds = GetTabRect(index);
            bounds.Inflate(2, 2);
            Invalidate(bounds, false);
        }

        private void RecalculateTabSize()
        {
            if (IsInDesigner) return;
            if (_updatingTabSize || Width <= 0) return;

            if (TabCount <= 0)
            {
                Invalidate();
                return;
            }

            int availableWidth = Math.Max(1, ClientSize.Width - 12);
            int tabWidth = Math.Max(MinimumTabWidth, availableWidth / TabCount);
            Size itemSize = new(tabWidth, _tabHeight);

            try
            {
                _updatingTabSize = true;
                if (base.ItemSize != itemSize)
                {
                    base.ItemSize = itemSize;
                }

                ApplyNativeItemSize(itemSize);
            }
            finally
            {
                _updatingTabSize = false;
            }

            Invalidate();
        }

        private void ApplyPageBackColor()
        {
            foreach (TabPage page in TabPages)
            {
                page.BackColor = _pageBackColor;
            }
        }

        private void ConfigureTabControl()
        {
            DrawMode = TabDrawMode.OwnerDrawFixed;
            SizeMode = TabSizeMode.Fixed;
            Multiline = true;
            Alignment = TabAlignment.Top;
            Padding = new Point(0, 0);

            if (base.ItemSize.Height != _tabHeight)
            {
                base.ItemSize = new Size(Math.Max(MinimumTabWidth, base.ItemSize.Width), _tabHeight);
            }

            ApplyNativeItemSize(base.ItemSize);
        }

        private void ApplyNativeItemSize(Size itemSize)
        {
            if (IsInDesigner)
            {
                return;
            }

            if (!IsHandleCreated)
            {
                return;
            }

            int width = Math.Max(MinimumTabWidth, itemSize.Width);
            int height = Math.Max(24, _tabHeight);
            nint size = (height << 16) | (width & 0xFFFF);
            NativeMethods.SendMessage(Handle, NativeMethods.TCM_SETITEMSIZE, nint.Zero, size);
        }

        private void RefreshTabLayout()
        {
            if (IsInDesigner)
            {
                return;
            }

            ConfigureTabControl();
            RecalculateTabSize();

            if (!IsHandleCreated || IsDisposed || Disposing)
            {
                return;
            }

            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (IsDisposed || Disposing)
                    {
                        return;
                    }

                    ConfigureTabControl();
                    RecalculateTabSize();
                    Invalidate();
                }));
            }
            catch (InvalidOperationException)
            {
                // The designer can temporarily destroy the handle while moving the control.
            }
        }

        private static class NativeMethods
        {
            public const int TCM_SETITEMSIZE = 0x1329;

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);
        }
    }
}
