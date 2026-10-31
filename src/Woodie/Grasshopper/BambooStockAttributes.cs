using System.Drawing;
using System.Windows.Forms;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Woodie.Stock;

namespace Woodie.Grasshopper;

/// <summary>
/// On-canvas species list for Bamboo Stock. Same light utility chrome as Timber Material.
/// Species choice uses name + dimensions + skin chip so colour is not the only cue.
/// </summary>
internal sealed class BambooStockAttributes : GH_ComponentAttributes
{
    static readonly Color Accent = Color.FromArgb(135, 93, 58);
    static readonly Color PanelFill = Color.FromArgb(252, 250, 245);
    static readonly Color PanelLine = Color.FromArgb(120, 116, 105);
    static readonly Color FieldIdle = Color.White;
    static readonly Color FieldSelected = Color.FromArgb(236, 226, 212);
    static readonly Color FieldDisabled = Color.FromArgb(232, 230, 224);
    static readonly Color Ink = Color.FromArgb(36, 39, 32);
    static readonly Color InkMuted = Color.FromArgb(100, 100, 94);
    static readonly Color LabelInk = Color.FromArgb(72, 70, 62);

    readonly BambooStockComponent bamboo;
    RectangleF panel, species, summary, disclosure;
    readonly List<RectangleF> options = new();
    bool open;
    public BambooStockAttributes(BambooStockComponent owner) : base(owner) => bamboo = owner;

    protected override void Layout()
    {
        base.Layout();
        var standard = Bounds;
        disclosure = new RectangleF(standard.Left + (standard.Width - 24) / 2, standard.Bottom + 4, 24, 16);
        options.Clear();
        if (!bamboo.PanelExpanded) {
            open = false;
            panel = species = summary = RectangleF.Empty;
            Bounds = RectangleF.Union(standard, disclosure);
            return;
        }
        float width = Math.Max(200, standard.Width);
        float left = standard.Left + (standard.Width - width) / 2;
        float top = disclosure.Bottom + 4;
        const float row = 22, gap = 8, label = 16;
        species = new RectangleF(left + 8, top + label, width - 16, row);
        float optionTop = species.Bottom + 2;
        if (open)
            for (int i = 0; i < BambooSpecies.All.Length; i++)
                options.Add(new RectangleF(left + 8, optionTop + i * row, width - 16, row));
        float after = open ? optionTop + BambooSpecies.All.Length * row + gap : species.Bottom + gap;
        summary = new RectangleF(left + 8, after, width - 16, 28);
        panel = new RectangleF(left, top, width, summary.Bottom - top + 12);
        Bounds = RectangleF.Union(RectangleF.Union(standard, disclosure), panel);
    }

    protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
    {
        base.Render(canvas, graphics, channel);
        Bounds = UnionControls(Bounds);
        if (channel != GH_CanvasChannel.Objects) return;
        using (var fill = new SolidBrush(PanelFill))
        using (var outline = new Pen(Selected ? Accent : PanelLine))
        using (var ink = new SolidBrush(Ink)) {
            graphics.FillRectangle(fill, disclosure);
            graphics.DrawRectangle(outline, disclosure.X, disclosure.Y, disclosure.Width, disclosure.Height);
            float x = disclosure.X + disclosure.Width / 2, y = disclosure.Y + disclosure.Height / 2;
            graphics.FillPolygon(ink, bamboo.PanelExpanded
                ? new[] { new PointF(x - 4, y + 2), new PointF(x + 4, y + 2), new PointF(x, y - 2) }
                : new[] { new PointF(x - 4, y - 2), new PointF(x + 4, y - 2), new PointF(x, y + 2) });
        }
        if (!bamboo.PanelExpanded) return;
        using var background = new SolidBrush(PanelFill);
        using var border = new Pen(Selected ? Accent : PanelLine);
        graphics.FillRectangle(background, panel); graphics.DrawRectangle(border, panel.X, panel.Y, panel.Width, panel.Height);
        Label(graphics, "Species", new RectangleF(species.X, species.Y - 16, species.Width, 14));
        var current = BambooSpecies.For(bamboo.SelectedSpecies);
        var closed = bamboo.SpeciesControlled ? "Controlled by input" : BambooSpecies.MenuLabel(current);
        Field(graphics, species, closed, !bamboo.SpeciesControlled, open, true, bamboo.SpeciesControlled ? null : current.Skin);
        for (int i = 0; i < options.Count; i++) {
            var preset = BambooSpecies.All[i];
            Field(graphics, options[i], BambooSpecies.MenuLabel(preset), true, preset.Id == bamboo.SelectedSpecies, false, preset.Skin);
        }
        Label(graphics, bamboo.SpeciesSource ?? BambooSpecies.Summary(current),
            new RectangleF(summary.X, summary.Y, summary.Width, summary.Height));
    }

    static void Label(Graphics g, string text, RectangleF rect)
    {
        using var brush = new SolidBrush(LabelInk);
        g.DrawString(text, GH_FontServer.StandardAdjusted, brush, rect);
    }

    static void Field(Graphics g, RectangleF rect, string text, bool enabled, bool selected, bool arrow, double[] skin)
    {
        using var fill = new SolidBrush(!enabled ? FieldDisabled : selected ? FieldSelected : FieldIdle);
        using var pen = new Pen(selected && enabled ? Accent : Color.FromArgb(142, 139, 128));
        using var ink = new SolidBrush(enabled ? Ink : InkMuted);
        using var format = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        g.FillRectangle(fill, rect); g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
        float textLeft = rect.X + 6;
        if (skin != null && skin.Length >= 3) {
            var chip = new RectangleF(rect.X + 6, rect.Y + (rect.Height - 10) / 2, 10, 10);
            using var chipFill = new SolidBrush(Color.FromArgb(
                (int)Math.Clamp(skin[0], 0, 255), (int)Math.Clamp(skin[1], 0, 255), (int)Math.Clamp(skin[2], 0, 255)));
            using var chipEdge = new Pen(Color.FromArgb(90, 86, 78));
            g.FillRectangle(chipFill, chip); g.DrawRectangle(chipEdge, chip.X, chip.Y, chip.Width, chip.Height);
            textLeft = chip.Right + 6;
        }
        var textBox = new RectangleF(textLeft, rect.Y, rect.Right - textLeft - (arrow ? 18 : 6), rect.Height);
        g.DrawString(text, GH_FontServer.StandardAdjusted, ink, textBox, format);
        if (arrow) { float x = rect.Right - 10, y = rect.Y + rect.Height / 2; g.FillPolygon(ink, new[] { new PointF(x - 3, y - 2), new PointF(x + 3, y - 2), new PointF(x, y + 2) }); }
    }

    bool Click(PointF location)
    {
        if (disclosure.Contains(location)) {
            open = false; bamboo.SetPanelExpanded(!bamboo.PanelExpanded); ExpireLayout(); return true;
        }
        if (!bamboo.PanelExpanded) return false;
        for (int i = 0; i < options.Count; i++) if (options[i].Contains(location)) {
            bamboo.SelectSpecies(BambooSpecies.All[i].Id); open = false; ExpireLayout(); return true;
        }
        if (species.Contains(location)) { if (!bamboo.SpeciesControlled) open = !open; ExpireLayout(); return true; }
        if (open) { open = false; ExpireLayout(); return true; }
        return false;
    }

    public override bool IsPickRegion(PointF point) =>
        base.IsPickRegion(point) || disclosure.Contains(point) || (!panel.IsEmpty && panel.Contains(point));
    public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
    {
        if (e.Button == MouseButtons.Left && Click(e.CanvasLocation)) { Instances.RedrawCanvas(); return GH_ObjectResponse.Handled; }
        return base.RespondToMouseDown(sender, e);
    }
    RectangleF UnionControls(RectangleF box)
    {
        if (!disclosure.IsEmpty) box = RectangleF.Union(box, disclosure);
        if (!panel.IsEmpty) box = RectangleF.Union(box, panel);
        return box;
    }
}
