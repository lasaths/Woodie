using System.Drawing;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Woodie.Appearance;

namespace Woodie.Grasshopper;

// In-canvas lists: all bounds and hit testing use canvas coordinates, so GH owns zoom/DPI transforms.
// Informed by Arup's custom-attributes pattern; no external UI runtime or platform dialogs.
internal sealed class MaterialAttributes : GH_ComponentAttributes
{
    static readonly System.Reflection.PropertyInfo button = typeof(GH_CanvasMouseEvent).GetProperty("Button");
    readonly SettingsComponent material;
    RectangleF panel, product, look, advanced, disclosure;
    readonly List<RectangleF> options = new();
    int open; // 0 = closed, 1 = products, 2 = appearances
    public MaterialAttributes(SettingsComponent owner) : base(owner) => material = owner;
    protected override void Layout()
    {
        base.Layout();
        var standard = Bounds;
        disclosure = new RectangleF(standard.Left + (standard.Width - 22) / 2, standard.Bottom + 2, 22, 14);
        options.Clear();
        if (!material.PanelExpanded) {
            open = 0;
            panel = product = look = advanced = RectangleF.Empty;
            Bounds = RectangleF.Union(standard, disclosure);
            return;
        }
        float width = Math.Max(168, standard.Width);
        float left = standard.Left + (standard.Width - width) / 2;
        float top = disclosure.Bottom + 3;
        product = new RectangleF(left + 6, top + 18, width - 12, 20);
        look = new RectangleF(left + 6, top + 60, width - 12, 20);
        int count = open == 1 ? TimberProducts.MenuOrder.Length : open == 2 ? 2 : 0;
        options.Clear();
        float optionTop = open == 1 ? product.Bottom : look.Bottom;
        for (int i = 0; i < count; i++) options.Add(new RectangleF(left + 6, optionTop + i * 20, width - 12, 20));
        if (open == 1) look.Offset(0, count * 20);
        advanced = new RectangleF(left + 6, look.Bottom + 8 + (open == 2 ? count * 20 : 0), width - 12, 20);
        float notice = material.AdvancedNotice == null ? 0 : 36;
        panel = new RectangleF(left, top, width, advanced.Bottom - top + 22 + notice);
        Bounds = RectangleF.Union(RectangleF.Union(standard, disclosure), panel);
    }
    protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
    {
        base.Render(canvas, graphics, channel);
        if (channel != GH_CanvasChannel.Objects) return;
        using (var fill = new SolidBrush(Color.FromArgb(235, 234, 228)))
        using (var outline = new Pen(Color.FromArgb(110, 107, 97)))
        using (var ink = new SolidBrush(Color.FromArgb(48, 48, 43))) {
            graphics.FillRectangle(fill, disclosure);
            graphics.DrawRectangle(outline, disclosure.X, disclosure.Y, disclosure.Width, disclosure.Height);
            float x = disclosure.X + disclosure.Width / 2, y = disclosure.Y + disclosure.Height / 2;
            graphics.FillPolygon(ink, material.PanelExpanded
                ? new[] { new PointF(x - 4, y + 2), new PointF(x + 4, y + 2), new PointF(x, y - 2) }
                : new[] { new PointF(x - 4, y - 2), new PointF(x + 4, y - 2), new PointF(x, y + 2) });
        }
        if (!material.PanelExpanded) return;
        using var background = new SolidBrush(Color.FromArgb(248, 246, 239));
        using var border = new Pen(Selected ? Color.FromArgb(56, 109, 62) : Color.FromArgb(110, 107, 97));
        graphics.FillRectangle(background, panel); graphics.DrawRectangle(border, panel.X, panel.Y, panel.Width, panel.Height);
        Label(graphics, "Product", new RectangleF(product.X, product.Y - 16, product.Width, 14));
        Field(graphics, product, material.ProductControlled ? "Controlled by input" : material.SelectedProduct.Label(), !material.ProductControlled, open == 1);
        Label(graphics, "Appearance", new RectangleF(look.X, look.Y - 16, look.Width, 14));
        Field(graphics, look, material.SelectedLook == TimberLook.Natural ? "Natural" : "Clean/Pale", true, open == 2);
        for (int i = 0; i < options.Count; i++) {
            var selected = open == 1 ? TimberProducts.MenuOrder[i] == material.SelectedProduct : i == (int)material.SelectedLook;
            Field(graphics, options[i], open == 1 ? TimberProducts.MenuOrder[i].Label() : i == 0 ? "Natural" : "Clean/Pale", true, selected, false);
        }
        Field(graphics, advanced, material.Advanced ? "−  Advanced inputs" : "+  Advanced inputs", true, material.Advanced, false);
        Label(graphics, material.ProductSource ?? "Resolution: mm/pixel", new RectangleF(advanced.X, advanced.Bottom + 2, advanced.Width, 16));
        if (material.AdvancedNotice != null)
            Label(graphics, "Keep advanced wires / stored values.\nClear them before collapsing.", new RectangleF(advanced.X, advanced.Bottom + 20, advanced.Width, 32));
    }
    // Match GH parameter labels: raw point fonts are enlarged again by screen DPI.
    static void Label(Graphics g, string text, RectangleF rect)
    {
        using var brush = new SolidBrush(Color.FromArgb(64, 62, 55));
        g.DrawString(text, GH_FontServer.StandardAdjusted, brush, rect);
    }
    static void Field(Graphics g, RectangleF rect, string text, bool enabled, bool selected, bool arrow = true)
    {
        using var fill = new SolidBrush(!enabled ? Color.FromArgb(229, 228, 222) : selected ? Color.FromArgb(220, 232, 213) : Color.White);
        using var pen = new Pen(Color.FromArgb(142, 139, 128));
        using var ink = new SolidBrush(enabled ? Color.FromArgb(36, 39, 32) : Color.FromArgb(100, 100, 94));
        using var format = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        g.FillRectangle(fill, rect); g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
        var textBox = new RectangleF(rect.X + 6, rect.Y, rect.Width - (arrow ? 24 : 12), rect.Height);
        g.DrawString(text, GH_FontServer.StandardAdjusted, ink, textBox, format);
        if (arrow) { float x = rect.Right - 10, y = rect.Y + rect.Height / 2; g.FillPolygon(ink, new[] { new PointF(x - 3, y - 2), new PointF(x + 3, y - 2), new PointF(x, y + 2) }); }
    }
    internal bool Click(PointF location)
    {
        if (disclosure.Contains(location)) {
            open = 0; material.SetPanelExpanded(!material.PanelExpanded); ExpireLayout(); return true;
        }
        if (!material.PanelExpanded) return false;
        for (int i = 0; i < options.Count; i++) if (options[i].Contains(location)) {
            if (open == 1) material.SelectProduct(TimberProducts.MenuOrder[i]); else material.SelectLook((TimberLook)i);
            open = 0; ExpireLayout(); return true;
        }
        if (product.Contains(location)) { if (!material.ProductControlled) open = open == 1 ? 0 : 1; ExpireLayout(); return true; }
        if (look.Contains(location)) { open = open == 2 ? 0 : 2; ExpireLayout(); return true; }
        if (advanced.Contains(location)) { open = 0; material.SetAdvanced(!material.Advanced); ExpireLayout(); return true; }
        if (open != 0) { open = 0; ExpireLayout(); return true; }
        return false;
    }
    public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
    {
        // Read the host button enum without adding a Windows Forms target/runtime dependency.
        if (button.GetValue(e)?.ToString() == "Left" && Click(e.CanvasLocation)) { Instances.RedrawCanvas(); return GH_ObjectResponse.Handled; }
        return base.RespondToMouseDown(sender, e);
    }
    internal RectangleF DisclosureBounds => disclosure;
    internal RectangleF ProductBounds => product;
    internal RectangleF LookBounds => look;
    internal RectangleF AdvancedBounds => advanced;
    internal IReadOnlyList<RectangleF> OptionBounds => options;
}
