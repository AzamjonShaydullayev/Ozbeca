using System.Drawing;
using WinForms = System.Windows.Forms;

namespace Ozbeca.App;

/// <summary>Tray menyusi uchun mavzuga mos (yorug'/qorong'i) sokin ko'rinish.</summary>
internal static class TrayMenu
{
    public static void Style(WinForms.ContextMenuStrip menu)
    {
        bool dark = Theme.IsDark;
        menu.Renderer = new Renderer(new Colors(dark));
        menu.Font = new Font("Segoe UI", 9.5f);
        menu.ShowImageMargin = false;
        menu.ShowCheckMargin = true;
        menu.Padding = new WinForms.Padding(4, 6, 4, 6);
        menu.BackColor = dark ? Color.FromArgb(0x2C, 0x2C, 0x2C) : Color.FromArgb(0xF9, 0xF9, 0xF9);
        menu.ForeColor = dark ? Color.FromArgb(0xF3, 0xF3, 0xF3) : Color.FromArgb(0x1A, 0x1A, 0x1A);
        foreach (WinForms.ToolStripItem item in menu.Items)
        {
            item.Padding = new WinForms.Padding(4, 5, 12, 5);
            item.ForeColor = menu.ForeColor;
        }
    }

    /// <summary>Menyu tepasidagi nom va versiya (bosilmaydi).</summary>
    public static WinForms.ToolStripLabel Header() => new($"Özbeça  {Theme.Version}")
    {
        Font = new Font("Segoe UI Semibold", 9.5f),
        Padding = new WinForms.Padding(10, 4, 12, 6),
    };

    private sealed class Renderer : WinForms.ToolStripProfessionalRenderer
    {
        private readonly bool _dark;
        public Renderer(Colors c) : base(c) { _dark = c.Dark; RoundedEdges = false; }

        protected override void OnRenderItemCheck(WinForms.ToolStripItemImageRenderEventArgs e)
        {
            // Oddiy belgi o'rniga — aksent rangdagi ✓
            var r = e.ImageRectangle;
            using var brush = new SolidBrush(_dark ? Color.FromArgb(0x2E, 0xC4, 0xDE) : Color.FromArgb(0x00, 0x99, 0xB5));
            using var font = new Font("Segoe UI Symbol", 9f, FontStyle.Bold);
            var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString("✓", font, brush, new RectangleF(r.X - 2, r.Y, r.Width + 4, r.Height), fmt);
        }

        protected override void OnRenderItemText(WinForms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? e.Item.ForeColor : (_dark ? Color.FromArgb(0x80, 0x80, 0x80) : Color.FromArgb(0x9A, 0x9A, 0x9A));
            base.OnRenderItemText(e);
        }
    }

    private sealed class Colors : WinForms.ProfessionalColorTable
    {
        public readonly bool Dark;
        public Colors(bool dark) { Dark = dark; UseSystemColors = false; }

        private Color Bg => Dark ? Color.FromArgb(0x2C, 0x2C, 0x2C) : Color.FromArgb(0xF9, 0xF9, 0xF9);
        private Color Hover => Dark ? Color.FromArgb(0x3A, 0x3A, 0x3A) : Color.FromArgb(0xEA, 0xEA, 0xEA);
        private Color Line => Dark ? Color.FromArgb(0x45, 0x45, 0x45) : Color.FromArgb(0xE0, 0xE0, 0xE0);

        public override Color ToolStripDropDownBackground => Bg;
        public override Color ImageMarginGradientBegin => Bg;
        public override Color ImageMarginGradientMiddle => Bg;
        public override Color ImageMarginGradientEnd => Bg;
        public override Color MenuBorder => Line;
        public override Color MenuItemBorder => Hover;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color SeparatorDark => Line;
        public override Color SeparatorLight => Bg;
        public override Color CheckBackground => Bg;
        public override Color CheckSelectedBackground => Hover;
        public override Color CheckPressedBackground => Hover;
    }
}
