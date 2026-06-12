using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MaterialSkin;
using MaterialSkin.Controls;

namespace HotCornersWin
{
    /// <summary>
    /// Drop-in replacement for MaterialTabSelector that renders the tab labels
    /// with a caller-supplied (DPI-scaled) font. MaterialSkin's own selector draws
    /// its labels with getFontByType(Body2), a 12px size baked into the library as
    /// a constant that ignores DPI — so on a high-DPI display the tab text stays
    /// tiny while everything else scales. This control draws crisp, scaled labels
    /// with GDI (TextRenderer) and keeps the MaterialTabControl's SelectedIndex in
    /// sync, matching the Material theme colors (primary bar, accent indicator).
    /// </summary>
    internal sealed class ScaledTabSelector : Control
    {
        private MaterialTabControl _baseTabControl;
        private readonly List<Rectangle> _tabRects = new List<Rectangle>();
        private readonly Font _font;
        private readonly int _indicatorHeight;
        private readonly int _tabPadding;
        private int _hoverIndex = -1;

        public ScaledTabSelector(Font font, int indicatorHeight, int tabPadding)
        {
            // Keep our own reference: the Control.Font property gets reassigned to
            // MaterialSkin's default 14px once the control is parented, so all
            // measuring/drawing uses this field instead.
            _font = font;
            Font = font;
            _indicatorHeight = Math.Max(1, indicatorHeight);
            _tabPadding = Math.Max(0, tabPadding);
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.UserPaint
                   | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
        }

        public MaterialTabControl BaseTabControl
        {
            get { return _baseTabControl; }
            set
            {
                if (_baseTabControl != null)
                {
                    _baseTabControl.SelectedIndexChanged -= OnBaseChanged;
                    _baseTabControl.ControlAdded   -= OnBaseChanged;
                    _baseTabControl.ControlRemoved -= OnBaseChanged;
                }
                _baseTabControl = value;
                if (_baseTabControl != null)
                {
                    _baseTabControl.SelectedIndexChanged += OnBaseChanged;
                    _baseTabControl.ControlAdded   += OnBaseChanged;
                    _baseTabControl.ControlRemoved += OnBaseChanged;
                }
                UpdateTabRects();
                Invalidate();
            }
        }

        private static MaterialSkinManager Skin { get { return MaterialSkinManager.Instance; } }

        private void OnBaseChanged(object sender, EventArgs e)
        {
            UpdateTabRects();
            Invalidate();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            UpdateTabRects();
            Invalidate();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateTabRects();
            Invalidate();
        }

        private void UpdateTabRects()
        {
            _tabRects.Clear();
            if (_baseTabControl == null) return;

            int x = 0;
            foreach (TabPage page in _baseTabControl.TabPages)
            {
                Size sz = TextRenderer.MeasureText(page.Text, _font);
                int w = sz.Width + _tabPadding * 2;
                _tabRects.Add(new Rectangle(x, 0, w, Height));
                x += w;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            bool dark = Skin.Theme == MaterialSkinManager.Themes.DARK;

            // Selected tab uses the accent (title-bar) colour; unselected tabs use
            // a muted, theme-specific background and grey text so the active
            // display is obvious at a glance.
            Color inactiveBack = dark ? Color.FromArgb(50, 50, 50)   : Color.FromArgb(220, 220, 220);
            Color inactiveText = dark ? Color.FromArgb(180, 180, 180) : Color.FromArgb(100, 100, 100);
            Color activeBack   = Skin.ColorScheme.PrimaryColor;       // accent
            Color activeText   = Color.White;
            Color indicator    = Skin.ColorScheme.DarkPrimaryColor;   // darker accent, visible on activeBack

            // Fill the whole strip (incl. empty space past the tabs) with the
            // inactive colour so it reads as one tab bar.
            g.Clear(inactiveBack);
            if (_baseTabControl == null) return;

            if (_tabRects.Count != _baseTabControl.TabCount)
                UpdateTabRects();

            int selected = _baseTabControl.SelectedIndex;
            for (int i = 0; i < _tabRects.Count; i++)
            {
                var rect = _tabRects[i];
                bool isSelected = i == selected;

                Color back = isSelected
                    ? activeBack
                    : (i == _hoverIndex ? Shade(inactiveBack, dark ? 0.12f : -0.06f) : inactiveBack);
                using (var b = new SolidBrush(back))
                    g.FillRectangle(b, rect);

                TextRenderer.DrawText(g, _baseTabControl.TabPages[i].Text, _font, rect,
                    isSelected ? activeText : inactiveText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

                if (isSelected)
                {
                    using (var b = new SolidBrush(indicator))
                        g.FillRectangle(b, rect.X, Height - _indicatorHeight, rect.Width, _indicatorHeight);
                }
            }
        }

        // Lighten (factor &gt; 0) or darken (factor &lt; 0) a colour toward white/black.
        private static Color Shade(Color c, float factor)
        {
            if (factor >= 0)
                return Color.FromArgb(c.A,
                    (int)(c.R + (255 - c.R) * factor),
                    (int)(c.G + (255 - c.G) * factor),
                    (int)(c.B + (255 - c.B) * factor));
            factor = -factor;
            return Color.FromArgb(c.A,
                (int)(c.R * (1 - factor)),
                (int)(c.G * (1 - factor)),
                (int)(c.B * (1 - factor)));
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            for (int i = 0; i < _tabRects.Count; i++)
            {
                if (_tabRects[i].Contains(e.Location))
                {
                    if (_baseTabControl != null && _baseTabControl.SelectedIndex != i)
                        _baseTabControl.SelectedIndex = i;
                    Invalidate();
                    break;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hover = -1;
            for (int i = 0; i < _tabRects.Count; i++)
            {
                if (_tabRects[i].Contains(e.Location)) { hover = i; break; }
            }
            if (hover != _hoverIndex)
            {
                _hoverIndex = hover;
                Cursor = hover >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }
    }
}
