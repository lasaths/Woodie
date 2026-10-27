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
    // Woodie Phosphor accent; selected chrome only (utility surface, light-first).
    static readonly Color Accent = Color.FromArgb(135, 93, 58);
    static readonly Color PanelFill = Color.FromArgb(252, 250, 245);
    static readonly Color PanelLine = Color.FromArgb(120, 116, 105);
    static readonly Color FieldIdle = Color.White;
    static readonly Color FieldSelected = Color.FromArgb(236, 226, 212);
    static readonly Color FieldDisabled = Color.FromArgb(232, 230, 224);
    static readonly Color Ink = Color.FromArgb(36, 39, 32);
    static readonly Color InkMuted = Color.FromArgb(100, 100, 94);
    static readonly Color LabelInk = Color.FromArgb(72, 70, 62);

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
        // 8pt rhythm; disclosure ≥14px tall but centred under the capsule.
        disclosure = new RectangleF(standard.Left + (standard.Width - 24) / 2, standard.Bottom + 4, 24, 16);
        options.Clear();
        if (!material.PanelExpanded) {
            open = 0;
            panel = product = look = advanced = RectangleF.Empty;
            Bounds = RectangleF.Union(standard, disclosure);
            return;
        }
        float width = Math.Max(176, standard.Width);
        float left = standard.Left + (standard.Width - width) / 2;
        float top = disclosure.Bottom + 4;
        const float row = 22, gap = 8, label = 16;
        product = new RectangleF(left + 8, top + label, width - 16, row);
        look = new RectangleF(left + 8, product.Bottom + gap + label, width - 16, row);
        int count = open == 1 ? TimberProducts.MenuOrder.Length : open == 2 ? 2 : 0;
        options.Clear();
        float optionTop = open == 1 ? product.Bottom + 2 : look.Bottom + 2;
        for (int i = 0; i < count; i++) options.Add(new RectangleF(left + 8, optionTop + i * row, width - 16, row));
        if (open == 1) look.Offset(0, count * row + 2);
        advanced = new RectangleF(left + 8, look.Bottom + gap + (open == 2 ? count * row + 2 : 0), width - 16, row);
        float notice = material.AdvancedNotice == null ? 0 : 40;
        panel = new RectangleF(left, top, width, advanced.Bottom - top + 24 + notice);
        Bounds = RectangleF.Union(RectangleF.Union(standard, disclosure), panel);
    }
    protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
    {
        base.Render(canvas, graphics, channel);
        if (channel != GH_CanvasChannel.Objects) return;
        using (var fill = new SolidBrush(PanelFill))
        using (var outline = new Pen(Selected ? Accent : PanelLine))
        using (var ink = new SolidBrush(Ink)) {
            graphics.FillRectangle(fill, disclosure);
            graphics.DrawRectangle(outline, disclosure.X, disclosure.Y, disclosure.Width, disclosure.Height);
            float x = disclosure.X + disclosure.Width / 2, y = disclosure.Y + disclosure.Height / 2;
            graphics.FillPolygon(ink, material.PanelExpanded
                ? new[] { new PointF(x - 4, y + 2), new PointF(x + 4, y + 2), new PointF(x, y - 2) }
                : new[] { new PointF(x - 4, y - 2), new PointF(x + 4, y - 2), new PointF(x, y + 2) });
        }
        if (!material.PanelExpanded) return;
        using var background = new SolidBrush(PanelFill);
        using var border = new Pen(Selected ? Accent : PanelLine);
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
        Label(graphics, material.ProductSource ?? "Resolution: mm/pixel", new RectangleF(advanced.X, advanced.Bottom + 4, advanced.Width, 16));
        if (material.AdvancedNotice != null)
            Label(graphics, "Keep advanced wires / stored values.\nClear them before collapsing.", new RectangleF(advanced.X, advanced.Bottom + 22, advanced.Width, 32));
    }
    // Match GH parameter labels: raw point fonts are enlarged again by screen DPI.
    static void Label(Graphics g, string text, RectangleF rect)
    {
        using var brush = new SolidBrush(LabelInk);
        g.DrawString(text, GH_FontServer.StandardAdjusted, brush, rect);
    }
    static void Field(Graphics g, RectangleF rect, string text, bool enabled, bool selected, bool arrow = true)
    {
        using var fill = new SolidBrush(!enabled ? FieldDisabled : selected ? FieldSelected : FieldIdle);
        using var pen = new Pen(selected && enabled ? Accent : Color.FromArgb(142, 139, 128));
        using var ink = new SolidBrush(enabled ? Ink : InkMuted);
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
