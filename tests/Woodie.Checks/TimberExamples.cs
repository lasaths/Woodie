using System.Collections;
using System.Drawing;
using System.Reflection;
using GH_IO.Serialization;
using Grasshopper;
using Grasshopper.GUI.Base;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;
using Woodie.Appearance;
using Woodie.Geometry;
using Woodie.Grasshopper;
using static Woodie.Checks.Program;
using static Woodie.Checks.ExampleCanvas;

namespace Woodie.Checks;

internal static class TimberExamples
{
    static void Number(GH_Component c, int i, double n)
    {
        if(c.Params.Input[i] is Param_Number p) { p.PersistentData.Clear(); p.SetPersistentData(n); }
        else if(c.Params.Input[i] is Param_GenericObject g) { g.PersistentData.Clear(); g.SetPersistentData(new GH_Number(n)); }
        else throw new Exception("Native numeric input is unsupported: "+c.Params.Input[i].Name);
    }
    static GH_Component Native(GH_Document doc, string[] names, float x, float y)
    {
        var component = Instances.ComponentServer.ObjectProxies.Where(p => names.Contains(p.Desc.Name))
            .Select(p => Instances.ComponentServer.EmitObject(p.Guid)).OfType<GH_Component>().FirstOrDefault(c => !c.Obsolete);
        if (component == null) throw new Exception("Cannot find native component " + string.Join("/",names));
        return Add(doc,component,x,y);
    }
    static SettingsComponent Material(GH_Document doc, int seed, float x, float y)
    {
        var material = Add(doc,new SettingsComponent(),x,y); material.SelectProduct(TimberProduct.SolidTimber); material.SetPanelExpanded(false);
        Number(material,2,1);
        var p = (Param_Integer)material.Params.Input[1]; p.PersistentData.Clear(); p.SetPersistentData(seed);
        return material;
    }
    static Param_Geometry Geometry(GH_Document doc, GeometryBase geometry, string name, float x, float y)
    {
        var p = Add(doc,new Param_Geometry { NickName=name, Hidden=true },x,y);
        p.SetPersistentData(GH_Convert.ToGeometricGoo(geometry)); return p;
    }
    static GH_NumberSlider Slider(GH_Document doc, string name, decimal min, decimal max, decimal value, string expression, float x, float y)
    {
        var slider = new GH_NumberSlider { NickName=name }; slider.Slider.Type = GH_SliderAccuracy.Float;
        slider.Slider.DecimalPlaces = 1; slider.Slider.Minimum = min; slider.Slider.Maximum = max;
        slider.SetSliderValue(value); slider.Expression = expression ?? ""; return Add(doc,slider,x,y);
    }
    public static void AddDemonstrations(GH_Document doc)
    {
        IsolatedHost.LoadExampleBuiltIns(); int seed=ReviewedKnotSeed();
        int start=doc.Objects.Count;
        Text(doc,"01 / CUT THROUGH THE STOCK",40,270,20,true);
        Text(doc,"Move Position and Angle to expose the interior. Keep Positive reverses the retained side.\nThe original stock fixes the knot field; Cut outputs document units. Preview uses Document.\nRight-click Preview > Bake for native textured geometry. Save textures in the 3dm.",40,325,12);
        Text(doc,"Cut controls",40,420,12,true);
        var position=Slider(doc,"Position (%)",0,100,50,null,40,500);
        var angle=Slider(doc,"Angle (degrees)",-75,75,0,null,40,690);
        Text(doc,"Retain side",1040,920,12);
        var toggle=Add(doc,new GH_BooleanToggle { NickName="Keep Positive",Value=false },1040,980);
        Text(doc,"Construct the plane",370,420,12,true);
        var distance=Native(doc,new[]{"Multiplication"},330,500); distance.Params.Input[0].AddSource(position); Number(distance,1,12);
        var point=Native(doc,new[]{"Construct Point"},560,500); point.Params.Input[0].AddSource(distance.Params.Output[0]); Number(point,1,150); Number(point,2,60);
        var radians=Native(doc,new[]{"Radians","Degrees to Radians"},330,690); radians.Params.Input[0].AddSource(angle);
        var cos=Native(doc,new[]{"Cosine"},500,690); cos.Params.Input[0].AddSource(radians.Params.Output[0]);
        var sin=Native(doc,new[]{"Sine"},500,880); sin.Params.Input[0].AddSource(radians.Params.Output[0]);
        var normal=Native(doc,new[]{"Vector XYZ","Construct Vector"},730,780);
        normal.Params.Input[0].AddSource(cos.Params.Output[0]); normal.Params.Input[2].AddSource(sin.Params.Output[0]);
        var plane=Native(doc,new[]{"Plane Normal"},940,780); plane.Params.Input[0].AddSource(point.Params.Output[0]); plane.Params.Input[1].AddSource(normal.Params.Output[0]);
        Text(doc,"1200 × 300 × 120 mm",820,420,12);
        using(var stock=BuildupCutChecks.Stock()) {
            var input=Geometry(doc,stock.DuplicateBrep(),"Original stock (mm)",940,500);
            var cut=Add(doc,new CutComponent { Hidden=true },1190,600); cut.Params.Input[0].AddSource(input); cut.Params.Input[1].AddSource(plane.Params.Output[0]); cut.Params.Input[2].AddSource(toggle);
            ((Param_String)cut.Params.Input[3]).SetPersistentData("Millimetres");
            Text(doc,"Appearance",1300,580,12);
            var material=Material(doc,seed,1370,710); material.NickName="Cutting material";
            var preview=Add(doc,new PreviewComponent { NickName="Cutting preview" },1510,710);
            preview.Params.Input[0].AddSource(cut.Params.Output[0]); preview.Params.Input[1].AddSource(material.Params.Output[0]); preview.Params.Input[7].AddSource(cut.Params.Output[1]);
            Shared(preview,7);
            ((Param_Plane)preview.Params.Input[3]).SetPersistentData(Plane.WorldXY);
            var outlines=Add(doc,new Param_Curve { NickName="Cutting plane outline" },1510,1040); outlines.AddSource(cut.Params.Output[2]);
            foreach(var c in doc.Objects.Skip(start).OfType<GH_Component>().Where(c=>c!=preview)) c.Hidden=true;
        }
        Group(doc,"01 / Cutting",start,DemoColour);
        start=doc.Objects.Count;
        Text(doc,"02 / FOLLOW A KNOT",1850,270,20,true);
        Text(doc,"One connected piece; terraces at 120 / 80 / 40 mm.\nMove Knot section to expose a capped cross-section.\nAutomatic seed 3; the uncut stepped stock fixes the grain.",1850,325,12);
        var section=Slider(doc,"Knot section (%)",0,100,70,null,1950,470);
        var sectionDistance=Native(doc,new[]{"Multiplication"},2240,470);
        sectionDistance.Params.Input[0].AddSource(section); Number(sectionDistance,1,12);
        var sectionPoint=Native(doc,new[]{"Construct Point"},2500,470);
        sectionPoint.Params.Input[0].AddSource(sectionDistance.Params.Output[0]); Number(sectionPoint,1,800); Number(sectionPoint,2,60);
        var sectionPlane=Native(doc,new[]{"Plane Normal"},2750,470);
        sectionPlane.Params.Input[0].AddSource(sectionPoint.Params.Output[0]);
        var sectionNormal=(Param_Vector)sectionPlane.Params.Input[1];
        sectionNormal.PersistentData.Clear(); sectionNormal.SetPersistentData(Vector3d.XAxis);
        Text(doc,"Stepped solid / mm",1950,590,12);
        using(var shape=Cutaway()) {
            shape.Translate(0,650,0);
            var stepped=Geometry(doc,shape.DuplicateBrep(),"Uncut stepped stock (mm)",2080,690);
            var cut=Add(doc,new CutComponent { NickName="Knot section",Hidden=true },2450,700);
            cut.Params.Input[0].AddSource(stepped); cut.Params.Input[1].AddSource(sectionPlane.Params.Output[0]);
            ((Param_String)cut.Params.Input[3]).SetPersistentData("Millimetres");
            Text(doc,"Natural / seed 3",1950,930,12);
            var material=Material(doc,seed,2080,1020); material.NickName="Cutaway material";
            var preview=Add(doc,new PreviewComponent { NickName="Cutaway preview" },2800,800);
            preview.Params.Input[0].AddSource(cut.Params.Output[0]); preview.Params.Input[1].AddSource(material.Params.Output[0]); preview.Params.Input[7].AddSource(cut.Params.Output[1]);
            Shared(preview,7);
            ((Param_Plane)preview.Params.Input[3]).SetPersistentData(Plane.WorldXY);
            var outlines=Add(doc,new Param_Curve { NickName="Knot section outline" },2800,1120); outlines.AddSource(cut.Params.Output[2]);
            foreach(var c in doc.Objects.Skip(start).OfType<GH_Component>().Where(c=>c!=preview)) c.Hidden=true;
        }
        Group(doc,"02 / Knot cutaway",start,DemoColour);
        AddRotatedTower(doc);
        AddBambooCulm(doc);
    }
    static void AddRotatedTower(GH_Document doc)
    {
        int start=doc.Objects.Count;
        Text(doc,"05 / ROTATED TOWER",5200,270,20,true);
        Text(doc,"Straight timber slats between two rings; the top ring is twisted so the generators form a hyperboloid waist.\nEach slat is item-matched through Preview; fibre follows the long axis. See 06 for natural bamboo. Right-click Preview > Bake for native textured geometry.",5200,325,12);
        Text(doc,"16 slats · 100 × 40 mm · Solid timber",5200,420,12);
        var material=Material(doc,ReviewedKnotSeed(),5350,520); material.NickName="Tower material";
        Number(material,2,8); // many slats — stay under the preview triangle cap
        var slats=RotatedTower(height:2000, radiusBottom:800, radiusTop:650, width:100, thickness:40);
        Assert(slats.Length==16 && slats.All(b=>b.IsSolid),"Rotated tower slats must be closed solids");
        // Duplicate into GH_Brep so the archive keeps geometry after the working slats are disposed.
        var goo=slats.Select(b=> (IGH_GeometricGoo)new GH_Brep(b.DuplicateBrep())).ToArray();
        foreach(var b in slats) b.Dispose();
        var input=Add(doc,new Param_Geometry { Name="Tower slats (mm)",NickName="Slats",Hidden=true },5550,520);
        input.SetPersistentData((IEnumerable<IGH_GeometricGoo>)goo);
        var preview=Add(doc,new PreviewComponent { NickName="Tower preview" },5800,520);
        preview.Params.Input[0].AddSource(input); preview.Params.Input[1].AddSource(material.Params.Output[0]);
        ((Param_String)preview.Params.Input[6]).SetPersistentData("Millimetres");
        foreach(var c in doc.Objects.Skip(start).OfType<GH_Component>().Where(c=>c!=preview)) c.Hidden=true;
        Group(doc,"05 / Rotated tower",start,DemoColour);
    }
    static void AddBambooCulm(GH_Document doc)
    {
        int start=doc.Objects.Count;
        Text(doc,"06 / NATURAL BAMBOO",6600,270,20,true);
        Text(doc,"Natural Stock with Kind=Bamboo builds a hollow Moso culm on a Line from base to tip (Ø 80 · wall 7 · 1500 mm).\nWire Stock Model to Timber Preview Stock for texture. Natural Stock alone shows the culm solid.",6600,325,12);
        var material=Material(doc,ReviewedKnotSeed(),6750,520); material.NickName="Culm material";
        Number(material,2,8);
        var natural=Add(doc,new NaturalStockComponent { NickName="Bamboo culm" },6950,520);
        ((Param_Line)natural.Params.Input[0]).SetPersistentData(new Line(new Point3d(2800,4500,0),new Point3d(2800,4500,1500)));
        ((Param_String)natural.Params.Input[1]).PersistentData.Clear();
        ((Param_String)natural.Params.Input[1]).SetPersistentData("Bamboo");
        ((Param_String)natural.Params.Input[9]).SetPersistentData("Millimetres");
        natural.Params.Input[2].AddSource(material.Params.Output[0]);
        var geometry=Add(doc,new Param_Geometry { NickName="Culm geometry",Hidden=true },7200,520);
        geometry.AddSource(natural.Params.Output[1]);
        material.Hidden=true; // keep Natural Stock previewable so the culm shows without Timber Preview
        Group(doc,"06 / Natural bamboo",start,DemoColour);
    }
    // Straight generators between bottom/top circles; twist creates the cooling-tower waist.
    internal static Brep[] RotatedTower(int count=16, double height=3600, double radiusBottom=1100, double radiusTop=900, double twistDegrees=55, double width=90, double thickness=35, Point3d? origin=null)
    {
        var o = origin ?? new Point3d(0, 2800, 0);
        double twist = twistDegrees * Math.PI / 180;
        var slats = new Brep[count];
        for (int i = 0; i < count; i++)
        {
            double a0 = 2 * Math.PI * i / count, a1 = a0 + twist;
            var p0 = o + new Vector3d(radiusBottom * Math.Cos(a0), radiusBottom * Math.Sin(a0), 0);
            var p1 = o + new Vector3d(radiusTop * Math.Cos(a1), radiusTop * Math.Sin(a1), height);
            var x = p1 - p0; double length = x.Length; x.Unitize();
            var mid = (p0 + p1) * .5;
            var z = Vector3d.CrossProduct(x, Vector3d.ZAxis); if (z.IsTiny()) z = Vector3d.CrossProduct(x, Vector3d.YAxis); z.Unitize();
            var y = Vector3d.CrossProduct(z, x); y.Unitize();
            var box = new Box(new Plane(mid, x, y), new Interval(-length * .5, length * .5), new Interval(-width * .5, width * .5), new Interval(-thickness * .5, thickness * .5));
            slats[i] = Brep.CreateFromBox(box) ?? throw new Exception("Tower slat box failed");
        }
        return slats;
    }
    internal static Brep Cutaway()
    {
        using var profile = new PolylineCurve(new[] { new Point3d(0,0,0),new Point3d(0,300,0),new Point3d(0,300,40),new Point3d(0,200,40),new Point3d(0,200,80),new Point3d(0,100,80),new Point3d(0,100,120),new Point3d(0,0,120),new Point3d(0,0,0) });
        using var surface = Surface.CreateExtrusion(profile,new Vector3d(1200,0,0)); using var open = surface.ToBrep();
        var closed = open.CapPlanarHoles(.001); Assert(closed?.IsSolid == true,"Stepped cutaway must be one closed solid");
        closed.Faces.SplitKinkyFaces(.01,true); if (closed.SolidOrientation == BrepSolidOrientation.Inward) closed.Flip(); return closed;
    }
    // Test/example authoring only: inspect the existing automatic growth field, without placing knots.
    internal static Func<Point3d,double> KnotSampler(TimberPanel panel)
    {
        var flags = BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        var lams = (Array)typeof(TimberPanel).GetField("lams",flags).GetValue(panel);
        var lam = ((IList)lams.GetValue(0))[0]; var segments = (IList)lam.GetType().GetField("segs",flags).GetValue(lam); var segment = segments[0];
        double pv = (double)segment.GetType().GetField("pv",flags).GetValue(segment), pw = (double)segment.GetType().GetField("pw",flags).GetValue(segment);
        var growth = typeof(TimberPanel).GetMethod("Growth",flags); var knot = growth.ReturnType.GetField("Knot",flags);
        return p => { double v=p.Y-150,w=p.Z-60,dv=v-pv,dw=w-pw,r=Math.Sqrt(dv*dv+dw*dw); return (double)knot.GetValue(growth.Invoke(panel,new object[]{segment,p.X,r,Math.Atan2(dw,dv),v,w})); };
    }
    static int ReviewedKnotSeed()
    {
        const int seed = 3;
        var sampler = KnotSampler(new TimberPanel(TimberProduct.SolidTimber,1200,300,120,seed));
        double score = 0;
        foreach (int y in new[]{100,200}) for (int x=100;x<1100;x+=4) {
            double h = y==100?120:80;
            double face = sampler(new Point3d(x,y-2,h));
            double riser = 0; for (double z=h-40;z<=h;z+=2) riser = Math.Max(riser,sampler(new Point3d(x,y,z)));
            score += Math.Min(face,riser);
        }
        Assert(score>0,"The reviewed knot no longer crosses a terrace/riser; review the showcase before changing its fixed seed");
        Console.WriteLine($"  Reviewed seed {seed}: knot transition score {score:0.###}"); return seed;
    }
    public static void Verify(string directory)
    {
        IsolatedHost.LoadWoodie(typeof(PreviewComponent).Assembly.Location);
        IsolatedHost.LoadExampleBuiltIns(); ReviewedKnotSeed();
        var rhino=RhinoDoc.ActiveDoc; var previous=rhino.ModelUnitSystem; double tolerance=rhino.ModelAbsoluteTolerance;
        try { foreach(var units in new[]{UnitSystem.Millimeters,UnitSystem.Meters,UnitSystem.Inches}) {
            rhino.ModelUnitSystem=units; double unit=RhinoMath.UnitScale(units,UnitSystem.Millimeters); rhino.ModelAbsoluteTolerance=.001/unit;
            using var doc=new GH_Document { Enabled=true }; var archive=new GH_Archive();
            Assert(archive.ReadFromFile(Path.Combine(directory,"Timber-preview.ghx")) && archive.ExtractObject(doc,"Definition"),"Combined example did not reopen");
            Assert(doc.Objects.All(o=>!o.GetType().FullName.Contains("Script")),"Example must not contain scripts");
            foreach(var p in doc.Objects.OfType<PreviewComponent>()) p.Hidden=true;
            doc.NewSolution(false);
            var demos=doc.Objects.OfType<PreviewComponent>().Where(p=>!ExampleDefinitions.IsLibrary(p)).ToArray();
            Assert(demos.Length==4 && demos.All(p=>p.Params.Output[0].VolatileDataCount>0),"Combined demonstrations must all solve");
            var tower=demos.Single(p=>p.NickName=="Tower preview");
            Assert(tower.Params.Output[0].VolatileDataCount==16,"Rotated tower needs one preview mesh per slat");
            var culm=doc.Objects.OfType<NaturalStockComponent>().Single(c=>c.NickName=="Bamboo culm");
            Assert(!culm.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any(),"Bamboo culm Natural Stock failed");
            var culmStock=culm.Params.Output[0].VolatileData.AllData(true).OfType<StockGoo>().Single().Value;
            Assert(culmStock.Settings.Product==TimberProduct.Bamboo && culmStock.Geometry is Brep { IsSolid: true },"Bamboo culm stock must be a solid Bamboo model");
            foreach(var c in doc.Objects.OfType<GH_Component>()) Assert(!c.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any(),c.Name+": "+string.Join(";",c.RuntimeMessages(GH_RuntimeMessageLevel.Error)));
            var sliders=doc.Objects.OfType<GH_NumberSlider>().ToArray();
            Assert(sliders.All(s=>!s.IsExpression),"Percentage and degree sliders must display their labelled raw units");
            var preview=demos.Single(p=>p.NickName=="Cutting preview");
            foreach(decimal position in new[]{20m,50m,80m}) foreach(decimal angle in new[]{0m,25m}) {
                sliders.Single(s=>s.NickName=="Position (%)").SetSliderValue(position); sliders.Single(s=>s.NickName=="Angle (degrees)").SetSliderValue(angle);
                foreach(var slider in sliders) slider.ExpireSolution(false); doc.NewSolution(false);
                var cut=doc.Objects.OfType<CutComponent>().Single(c=>c.NickName!="Knot section"); var plane=cut.Params.Input[1].VolatileData.AllData(true).OfType<GH_Plane>().Single().Value;
                Near(plane.OriginX,(double)position*12,.001); Near(plane.Normal.Z,Math.Sin((double)angle*Math.PI/180),.00001);
                Assert(!preview.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any() && preview.Params.Output[0].VolatileDataCount>0,"Moving the example cut failed");
            }
            var knotPreview=demos.Single(p=>p.NickName=="Cutaway preview");
            Plane? initialFrame=null;
            foreach(decimal section in new[]{20m,50m,80m}) {
                var slider=sliders.Single(s=>s.NickName=="Knot section (%)"); slider.SetSliderValue(section); slider.ExpireSolution(false); doc.NewSolution(false);
                Assert(!knotPreview.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any() && knotPreview.Params.Output[0].VolatileDataCount>0,"Moving the stepped section failed");
                var sectionCut=doc.Objects.OfType<CutComponent>().Single(c=>c.NickName=="Knot section");
                var sectionPlane=sectionCut.Params.Input[1].VolatileData.AllData(true).OfType<GH_Plane>().Single().Value;
                Near(sectionPlane.OriginX,(double)section*12,.001); Near(sectionPlane.Normal.X,1,.000001);
                var piece=sectionCut.Params.Output[0].VolatileData.AllData(true).OfType<GH_Brep>().Single().Value;
                using var properties=VolumeMassProperties.Compute(piece);
                double expectedVolume=(double)section*12*24000/(unit*unit*unit);
                Near(properties.Volume,expectedVolume,Math.Max(1e-9,expectedVolume*.000001));
                var frames=knotPreview.Params.Output[3].VolatileData.AllData(true).OfType<GH_Plane>().Select(p=>p.Value).ToArray();
                Assert(frames.Length==1,"Stepped section must produce exactly one material reference frame");
                initialFrame ??= frames[0];
                Assert(frames.All(f=>f.Equals(initialFrame.Value)),"Stepped section changed its material reference frame");
            }
            Console.WriteLine("  Combined cutting, knot, buildup and tower demos solve in "+units);
        } } finally { rhino.ModelUnitSystem=previous; rhino.ModelAbsoluteTolerance=tolerance; }
    }
}
