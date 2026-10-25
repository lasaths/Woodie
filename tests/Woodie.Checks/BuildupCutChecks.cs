using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Woodie.Appearance;
using Woodie.Geometry;
using Woodie.Grasshopper;
using Woodie.Rendering;
using static Woodie.Checks.Program;

namespace Woodie.Checks;

internal static class BuildupCutChecks
{
    internal static Brep Stock() => new Box(Plane.WorldXY, new Interval(0,1200), new Interval(0,300), new Interval(0,120)).ToBrep();
    static TimberPanel Panel(PreparedPart p, TimberSettings s) => new(s.Product,p.LengthMm,p.WidthMm,p.DepthMm,s.Seed,s.EffectiveKnotDensity,s.Palette(),resolvedLayers:TimberLayups.Resolve(s,p.DepthMm,p.WidthMm,p.ToleranceMm));
    public static void Layers()
    {
        foreach (var product in TimberProducts.MenuOrder) {
            var original = new TimberSettings { Product=product, Seed=17 }; double h=original.VirtualDepthMm;
            var expanded = original.Duplicate(); expanded.Layers=TimberLayups.Recipes(original,h);
            var before = new TimberPanel(product,1200,300,h,17,original.EffectiveKnotDensity,original.Palette());
            var after = new TimberPanel(product,1200,300,h,17,original.EffectiveKnotDensity,original.Palette(),resolvedLayers:TimberLayups.Resolve(expanded,h,300));
            foreach(var layer in TimberLayups.Resolve(expanded,h,300)) for(int i=0;i<12;i++) {
                double x=31+i*89,y=11+i*23,z=(layer.z0+layer.z1)/2;
                Assert(before.Col(x,y,z,1,0,1)==after.Col(x,y,z,1,0,1),"Preset expansion changed calibrated appearance: "+product);
            }
        }
        var definitions = new[] { new LayerDefinition(30), new LayerDefinition(60,false), new LayerDefinition(30) };
        var settings = new TimberSettings { Layers=definitions, Seed=17 };
        definitions[0] = new LayerDefinition(99); Near(settings.Layers[0].Thickness,30);
        var copy = settings.Duplicate(); copy.Layers = new[] { new LayerDefinition(120) }; Near(settings.Layers[0].Thickness,30);
        var resolved = TimberLayups.Resolve(settings,120,300);
        Assert(resolved.Count == 3 && !resolved[1].alongX && resolved.All(l => l.continuousLength && !l.fingers),"Layer topology differs");
        Near(resolved[1].z0,30); Near(resolved[1].z1,90);
        Reject(() => TimberLayups.Resolve(settings,100,300),"stock is 100");
        settings.FitToPart = true; Near(TimberLayups.Resolve(settings,60,300)[1].z1,45);
        Reject(() => new LayerDefinition(-1).Validate(),"thickness");
        Reject(() => new LayerDefinition(3,Construction:LayerConstruction.Veneer,Joints:LayerJoints.Finger).Validate(),"only to board");
        foreach (var type in Enum.GetValues<LayerConstruction>()) {
            var s = new TimberSettings { Layers=new[] { new LayerDefinition(120,Construction:type) } };
            var l = TimberLayups.Resolve(s,120,300).Single();
            Assert(l.veneer == (type == LayerConstruction.Veneer) && l.continuous == (type != LayerConstruction.Boards),"Construction lost");
        }
        foreach (var joint in Enum.GetValues<LayerJoints>()) {
            var s = new TimberSettings { Layers=new[] { new LayerDefinition(120,Joints:joint) } };
            var l = TimberLayups.Resolve(s,120,300).Single(); Assert(l.fingers == (joint == LayerJoints.Finger) && l.continuousLength == (joint == LayerJoints.None),"Joint flags lost");
        }
        var archive = new GH_Archive(); archive.AppendObject(new SettingsGoo(settings),"settings"); var restored = new SettingsGoo();
        Assert(archive.ExtractObject(restored,"settings") && restored.Value.Key == settings.Key,"Custom settings archive differs");
        var la = new GH_Archive(); la.AppendObject(new LayerGoo(definitions[1]),"layer"); var lg = new LayerGoo();
        Assert(la.ExtractObject(lg,"layer") && lg.Value == definitions[1],"Layer archive differs");
        foreach (int version in new[]{1,2}) {
            var chunk = new GH_LooseChunk("old"); new SettingsGoo(new TimberSettings { Seed=21 }).Write(chunk); chunk.RemoveItem("Version"); chunk.SetInt32("Version",version);
            var legacy = new SettingsGoo(); Assert(legacy.Read(chunk) && legacy.Value.Layers == null && legacy.Value.Seed == 21,"Old settings archive no longer loads");
        }
        using var surface = new PlaneSurface(Plane.WorldXY,new Interval(0,1200),new Interval(0,300));
        using var open = PreparedPart.Create(surface,settings,1,.001,Plane.WorldXY); Near(open.DepthMm,120);
        using var stock = Stock(); using var p1 = PreparedPart.Create(stock,settings,1,.001,Plane.WorldXY);
        var alternate = settings.Duplicate(); alternate.Layers = new[] { new LayerDefinition(120) };
        using var p2 = PreparedPart.Create(stock,alternate,1,.001,Plane.WorldXY);
        var a = TextureAtlas.Prepare(p1,settings); var b = TextureAtlas.Prepare(p2,alternate);
        try { Assert(a[0].TexturePath != b[0].TexturePath,"Buildup cache collision"); } finally { foreach (var page in a.Concat(b)) page.Dispose(); }
    }
    public static void Cutting()
    {
        foreach (double unit in new[] { 1d,1000,25.4 }) {
            using var stock = Stock(); stock.Transform(Transform.Scale(Point3d.Origin,1/unit));
            var settings = new TimberSettings { Product=TimberProduct.SolidTimber, Seed=17 };
            using var reference = PreparedPart.Create(stock,settings,unit,.001/unit,Plane.WorldXY);
            var panel = Panel(reference,settings); double volume = stock.GetVolume();
            foreach (double fraction in new[] { .2,.5,.8 }) foreach (double angle in new[] { 0d,.35 }) {
                var plane = new Plane(new Point3d(1200*fraction/unit,150/unit,60/unit),new Vector3d(Math.Cos(angle),0,Math.Sin(angle)));
                using var cut = TimberCut.Create(stock,plane,false,.001/unit);
                Assert(cut.Geometry.Count > 0 && cut.Outlines.Count > 0,"Missing cut or curves");
                foreach (var geometry in cut.Geometry) {
                    Assert(geometry is Brep b && b.IsSolid,"Cut not capped");
                    using var p = PreparedPart.Create(geometry,settings,unit,.001/unit,Plane.WorldXY,stock:stock);
                    Near(p.LengthMm,1200,.01); Near(p.WidthMm,300,.01); Near(p.DepthMm,120,.01);
                    Near(p.Alignment.Frame.Origin.DistanceTo(reference.Alignment.Frame.Origin),0,1e-8);
                    Assert(p.Alignment.Box.X.Length < reference.Alignment.Box.X.Length,"Inspection box is not the cut geometry");
                    var field = Panel(p,settings);
                    foreach (var c in p.Coordinates) Assert(field.Col(c.X,c.Y,c.Z,1,0,0) == panel.Col(c.X,c.Y,c.Z,1,0,0),"Cut regenerated knots");
                }
            }
            Near(stock.GetVolume(),volume,Math.Max(1e-8,volume*1e-10));
            using var whole = TimberCut.Create(stock,new Plane(new Point3d(1300/unit,0,0),Vector3d.XAxis),false,.001/unit);
            using var empty = TimberCut.Create(stock,Plane.WorldYZ,false,.001/unit);
            Assert(whole.Geometry.Count == 1 && empty.Geometry.Count == 0 && whole.Remark != null && empty.Remark != null,"Outside/boundary behavior differs");
            using var positive = TimberCut.Create(stock,new Plane(new Point3d(600/unit,0,0),Vector3d.XAxis),true,.001/unit);
            Near(((Brep)positive.Geometry.Single()).GetVolume(),volume/2,Math.Max(1e-8,volume*1e-7));
        }
        using var mesh = Mesh.CreateFromBox(new BoundingBox(0,0,0,1200,300,120),4,2,2);
        using var mc = TimberCut.Create(mesh,new Plane(new Point3d(550,0,0),new Vector3d(1,0,.2)),false,.001);
        Assert(mc.Geometry.Count > 0 && mc.Geometry.All(g => g is Mesh m && m.IsClosed && m.IsValid),"Mesh cap invalid"); Assert(mesh.IsClosed,"Stock changed");
        using var profile = new Rectangle3d(Plane.WorldXY,1200,300).ToNurbsCurve(); using var extrusion = Extrusion.Create(profile,120,true);
        using var ec = TimberCut.Create(extrusion,new Plane(new Point3d(600,0,0),Vector3d.XAxis),false,.001);
        Assert(ec.Geometry.All(g => g is Brep b && b.IsSolid),"Extrusion cap missing");
        using var surface = new PlaneSurface(Plane.WorldXY,new Interval(0,1200),new Interval(0,300));
        Reject(() => TimberCut.Create(surface,Plane.WorldYZ,false,.001),"supports closed");
        using var box = Stock(); using var moved = Stock(); moved.Translate(1201,0,0);
        Reject(() => PreparedPart.Create(moved,new TimberSettings(),1,.001,Plane.WorldXY,stock:box),"outside");
        using var guide = GeometryGallery.Bow(); using var bent = GeometryGallery.BowedExtrusion(120);
        var bentSettings = new TimberSettings { Product=TimberProduct.SolidTimber };
        using var bentReference = PreparedPart.Create(bent,bentSettings,1,.001,Plane.WorldXY,guide:guide);
        using var bentCut = TimberCut.Create(bent,new Plane(new Point3d(900,0,0),Vector3d.XAxis),false,.001);
        foreach(var geometry in bentCut.Geometry) {
            using var mapped = PreparedPart.Create(geometry,bentSettings,1,.001,Plane.WorldXY,guide:guide,stock:bent);
            Near(mapped.LengthMm,bentReference.LengthMm,.001); Near(mapped.WidthMm,bentReference.WidthMm,.001); Near(mapped.DepthMm,bentReference.DepthMm,.001);
            var before = Panel(bentReference,bentSettings); var after = Panel(mapped,bentSettings);
            foreach(var c in mapped.Coordinates) Assert(before.Col(c.X,c.Y,c.Z) == after.Col(c.X,c.Y,c.Z),"Guided cut regenerated the stock field");
        }
    }
    public static void Components()
    {
        IsolatedHost.LoadWoodie(typeof(PreviewComponent).Assembly.Location); GH_Document.EnableSolutions = true;
        using var doc = new GH_Document { Enabled=true };
        var layer = new LayerComponent(); var buildup = new BuildupComponent(); var cut = new CutComponent(); var preview = new PreviewComponent { Hidden=true };
        foreach (var c in new GH_Component[] { layer,buildup,cut,preview }) { c.CreateAttributes(); doc.AddObject(c,false); }
        ((Param_Number)layer.Params.Input[0]).PersistentData.Clear();
        ((Param_Number)layer.Params.Input[0]).SetPersistentData(120d); buildup.Params.Input[1].AddSource(layer.Params.Output[0]);
        using var stock = Stock(); ((Param_Geometry)cut.Params.Input[0]).SetPersistentData(new GH_Brep(stock.DuplicateBrep()));
        ((Param_Plane)cut.Params.Input[1]).SetPersistentData(new Plane(new Point3d(600,0,0),Vector3d.XAxis));
        preview.Params.Input[0].AddSource(cut.Params.Output[0]); preview.Params.Input[1].AddSource(buildup.Params.Output[0]); preview.Params.Input[7].AddSource(cut.Params.Output[1]);
        doc.NewSolution(false);
        foreach (var c in new GH_Component[] { layer,buildup,cut,preview }) Assert(!c.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any(),c.Name+": "+string.Join(";",c.RuntimeMessages(GH_RuntimeMessageLevel.Error)));
        Assert(preview.Params.Output[1].VolatileData.AllData(true).Any(g => g.ToString().Contains("Custom buildup")),"Custom label missing");
        Assert(buildup.Params.Output[4].VolatileDataCount == 1,"Diagram missing");
        var archive = new GH_Archive(); archive.AppendObject(doc,"Definition"); using var reopened = new GH_Document { Enabled=true };
        Assert(archive.ExtractObject(reopened,"Definition"),"New component archive failed"); reopened.NewSolution(false);
        Assert(reopened.Objects.OfType<PreviewComponent>().Single().Params.Input[7].SourceCount == 1,"Stock wire lost");
    }
}
