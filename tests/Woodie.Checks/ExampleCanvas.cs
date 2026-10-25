using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;

namespace Woodie.Checks;

internal static class ExampleCanvas
{
    internal static readonly Color DemoColour = Color.FromArgb(35,151,129,100);
    internal static readonly Color LibraryColour = Color.FromArgb(28,137,151,137);
    internal static T Add<T>(GH_Document doc, T obj, float x, float y) where T : IGH_DocumentObject
    {
        obj.CreateAttributes(); obj.Attributes.Pivot = new PointF(x,y);
        // Toggles persist their rectangle rather than the normal component pivot.
        if(obj is GH_BooleanToggle) obj.Attributes.Bounds=new RectangleF(x,y,150,22);
        doc.AddObject(obj,false); return obj;
    }
    internal static GH_Scribble Text(GH_Document doc, string text, float x, float y, float size = 12, bool bold = false)
    {
        var scribble = new GH_Scribble { Text=text, Font=new Font("Consolas",size,bold?FontStyle.Bold:FontStyle.Regular) };
        return Add(doc,scribble,x,y);
    }
    internal static GH_Group Group(GH_Document doc, string name, int start, Color colour)
    {
        var group = new GH_Group { NickName=name, Colour=colour, Border=GH_GroupBorder.Box };
        foreach (var obj in doc.Objects.Skip(start).ToArray()) group.AddObject(obj.InstanceGuid);
        doc.AddObject(group,false); return group;
    }
    internal static void Shared(GH_Component component, params int[] inputs)
    { foreach(int i in inputs) component.Params.Input[i].WireDisplay = GH_ParamWireDisplay.hidden; }
    internal static void CheckLayout(GH_Document doc)
    {
        Program.Assert(doc.Objects.OfType<GH_Scribble>().All(s=>s.Font.FontFamily.Name=="Consolas"),"All example Scribbles must use Consolas");
        Program.Assert(doc.Objects.OfType<GH_Panel>().All(p=>p.Properties.Font.FontFamily.Name=="Consolas"),"The example schedule must use Consolas");
        foreach(var obj in doc.Objects) obj.Attributes.PerformLayout();
        var nodes=doc.Objects.Where(o=>o is not GH_Group).ToArray();
        var overlaps=new List<string>();
        for(int i=0;i<nodes.Length;i++) for(int j=i+1;j<nodes.Length;j++) {
            var a=nodes[i].Attributes.Bounds; var b=nodes[j].Attributes.Bounds;
            a.Inflate(4,4); b.Inflate(4,4); var hit=RectangleF.Intersect(a,b);
            string Label(IGH_DocumentObject o) => o is GH_Scribble s ? s.Text.Split('\n')[0] : o.NickName;
            if(hit.Width>1 && hit.Height>1) overlaps.Add(Label(nodes[i])+" / "+Label(nodes[j]));
        }
        Directory.CreateDirectory(Program.Output);
        File.WriteAllText(Path.Combine(Program.Output,"playground-layout.json"),System.Text.Json.JsonSerializer.Serialize(nodes.Select(o=>new {
            type=o.GetType().Name, name=o.NickName, text=(o as GH_Scribble)?.Text, x=o.Attributes.Bounds.X,y=o.Attributes.Bounds.Y,w=o.Attributes.Bounds.Width,h=o.Attributes.Bounds.Height
        })));
        Program.Assert(overlaps.Count==0,"Canvas objects overlap or have less than 8 px spacing: "+string.Join("; ",overlaps.Take(12)));
    }
}
