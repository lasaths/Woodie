using System.Drawing;
using System.Windows.Forms;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Woodie.Geometry;

namespace Woodie.Grasshopper;

/// <summary>On-canvas Draft/Full preview toggle. Same light utility chrome as Bamboo Stock.</summary>
internal sealed class PreviewAttributes : GH_ComponentAttributes
{
    static readonly Color Accent = Color.FromArgb(135, 93, 58);
    static readonly Color PanelFill = Color.FromArgb(252, 250, 245);
    static readonly Color PanelLine = Color.FromArgb(120, 116, 105);
    static readonly Color FieldIdle = Color.White;
    static readonly Color FieldSelected = Color.FromArgb(236, 226, 212);
    static readonly Color Ink = Color.FromArgb(36, 39, 32);

    readonly PreviewComponent preview;
    RectangleF chip;

    public PreviewAttributes(PreviewComponent owner) : base(owner) => preview = owner;

    protected override void Layout()
    {
        base.Layout();
        var standard = Bounds;
        float width = Math.Max(88, standard.Width * 0.7f);
        chip = new RectangleF(standard.Left + (standard.Width - width) / 2, standard.Bottom + 4, width, 20);
        Bounds = RectangleF.Union(standard, chip);
    }

    protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
    {
        base.Render(canvas, graphics, channel);
        if (!chip.IsEmpty) Bounds = RectangleF.Union(Bounds, chip);
        if (channel != GH_CanvasChannel.Objects || chip.IsEmpty) return;
        bool draft = preview.PreviewLod == PreviewLod.Draft;
        string label = draft ? "Draft preview" : "Full preview";
        using var fill = new SolidBrush(draft ? FieldSelected : FieldIdle);
        using var pen = new Pen(Selected ? Accent : PanelLine);
        using var ink = new SolidBrush(Ink);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        graphics.FillRectangle(fill, chip);
        graphics.DrawRectangle(pen, chip.X, chip.Y, chip.Width, chip.Height);
        graphics.DrawString(label, GH_FontServer.StandardAdjusted, ink, chip, format);
    }

    public override bool IsPickRegion(PointF point) => base.IsPickRegion(point) || chip.Contains(point);

    public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
    {
        if (e.Button == MouseButtons.Left && chip.Contains(e.CanvasLocation))
        {
            preview.TogglePreviewLod();
            Instances.RedrawCanvas();
            return GH_ObjectResponse.Handled;
        }
        return base.RespondToMouseDown(sender, e);
    }
}
