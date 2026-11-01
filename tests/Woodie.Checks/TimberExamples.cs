using System.Drawing;
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
using Woodie.Stock;
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
        AddSameLogTower(doc);
        AddBambooCulm(doc);
    }
    static void AddSameLogTower(GH_Document doc)
    {
        int start=doc.Objects.Count;
        Text(doc,"05 / SAME LOG → TOWER",5200,270,20,true);
        Text(doc,"One seed-3 log is cut into 16 blanks. Each blank keeps that cut as Stock Geometry so grain differs.\nLines map blank end to tower foot. Fibre and plane stay along each blank so cylinder ends do not remap the log field.\nBlanks and tower start Textured. Right-click for Gradient or Force flow. The knot-influence disk uses the same log-sized field.",5200,325,12);
        Text(doc,"16 blanks · 100 × 40 mm · seed 3 · 1 mm/pixel · per-cut Stock Geometry",5200,420,12);
        int seed=ReviewedKnotSeed();
        var material=Material(doc,seed,5220,500); material.NickName="Tower material";
        // Material() already sets 1 mm/pixel — do not coarsen (8 mm/pixel washed out grain diversity).
        const int count=16; const double width=100, thickness=40, height=2000, rBot=800, rTop=650;
        var towerOrigin=new Point3d(0,2800,0);
        var built=RotatedTower(count,height,rBot,rTop,55,width,thickness,towerOrigin);
        Assert(built.Slats.Length==count && built.Slats.All(b=>b.IsSolid),"Rotated tower slats must be closed solids");
        double blankLength=built.Length;
        double logR=Math.Max(280,Math.Sqrt(width*width+thickness*thickness)*2.2);
        var logCenter=new Point3d(towerOrigin.X+rBot+logR+900,towerOrigin.Y,logR);
        var packed=ExplodedLogCut(count,blankLength,width,thickness,logCenter,logR,explode:1.0);
        var exploded=ExplodedLogCut(count,blankLength,width,thickness,logCenter,logR,explode:1.35);
        Assert(packed.Blanks.Length==count && exploded.Blanks.Length==count,"Log cut needs one blank per slat");
        var logStock=LogCylinder(logCenter,blankLength,logR);
        Assert(logStock?.IsSolid==true,"Log stock must be a closed solid");
        var endOrigin=logCenter-new Vector3d(blankLength*.5,0,0);
        var blankGoo=new IGH_GeometricGoo[count]; var blankStockGoo=new IGH_GeometricGoo[count];
        var slatGoo=new IGH_GeometricGoo[count]; var slatStockGoo=new IGH_GeometricGoo[count];
        var blankPlanes=new Plane[count]; var slatPlanes=new Plane[count];
        var blankFibres=new Vector3d[count]; var slatFibres=new Vector3d[count];
        var lines=new Curve[count];
        for(int i=0;i<count;i++)
        {
            // Explode blank + stock together so each blank keeps its packed cut in the shared log field.
            var Texp=Transform.Translation(exploded.Mids[i]-packed.Mids[i]);
            var blankExp=packed.Blanks[i].DuplicateBrep(); Assert(blankExp.Transform(Texp),"Blank explode transform failed");
            var stockExp=logStock.DuplicateBrep(); Assert(stockExp.Transform(Texp),"Stock explode transform failed");
            blankGoo[i]=new GH_Brep(blankExp); blankStockGoo[i]=new GH_Brep(stockExp);
            blankPlanes[i]=new Plane(exploded.Mids[i],Vector3d.XAxis,Vector3d.YAxis);
            blankFibres[i]=Vector3d.XAxis;
            // Carry that same cut to the tower: blank+stock → slat pose (not a fresh local remap of each slat).
            var from=new Plane(packed.Mids[i],Vector3d.XAxis,Vector3d.YAxis);
            var to=built.Frames[i];
            var Ttower=Transform.PlaneToPlane(from,to);
            var blankTower=packed.Blanks[i].DuplicateBrep(); Assert(blankTower.Transform(Ttower),"Blank→tower transform failed");
            var stockTower=logStock.DuplicateBrep(); Assert(stockTower.Transform(Ttower),"Stock→tower transform failed");
            slatGoo[i]=new GH_Brep(blankTower); slatStockGoo[i]=new GH_Brep(stockTower);
            slatPlanes[i]=to; slatFibres[i]=to.XAxis;
            lines[i]=new LineCurve(new Point3d(endOrigin.X,exploded.Mids[i].Y,exploded.Mids[i].Z),built.Feet[i]);
        }
        // End-face disk samples the same log-AABB material field Preview locks with Stock Geometry (not a 1200×300×120 card).
        double logL=blankLength, logW=2*logR, logH=2*logR;
        var panel=new TimberPanel(TimberProduct.SolidTimber,logL,logW,logH,seed);
        double sampleU=logL*.5, fieldMax=0;
        for(double u=logL*.05;u<logL*.95;u+=logL*.02) for(double v=logW*.05;v<logW*.95;v+=logW*.05) for(double w=logH*.05;w<logH*.95;w+=logH*.05) {
            double k=panel.KnotInfluence(u,v,w); if(k>fieldMax){ fieldMax=k; sampleU=u; }
        }
        Assert(fieldMax>0.2,"Seed 3 knot field must light the log end face");
        double midV=logW*.5, midW=logH*.5;
        var gradient=KnotInfluenceDisk(panel,sampleU,midV,midW,endOrigin,Vector3d.YAxis,Vector3d.ZAxis,logR,vy=>midV+vy,vz=>midW+vz,rings:24,segments:48);
        Assert(gradient.VertexColors.Count==gradient.Vertices.Count && gradient.Vertices.Count>0,"Knot influence disk needs vertex colours");
        var envelope=new Circle(new Plane(endOrigin,Vector3d.XAxis),logR).ToNurbsCurve();
        foreach(var b in packed.Blanks) b.Dispose(); foreach(var b in exploded.Blanks) b.Dispose(); foreach(var b in built.Slats) b.Dispose(); logStock.Dispose();
        var blankFibreIn=Add(doc,new Param_Vector{Name="Blank fibre",NickName="BlankFibre"},5380,520);
        blankFibreIn.SetPersistentData(blankFibres);
        var blankPlaneIn=Add(doc,new Param_Plane{Name="Blank plane",NickName="BlankPlane",Hidden=true},5380,600);
        blankPlaneIn.SetPersistentData(blankPlanes);
        var blanksIn=Add(doc,new Param_Geometry{Name="Log blanks (mm)",NickName="Blanks",Hidden=true},5550,520);
        blanksIn.SetPersistentData((IEnumerable<IGH_GeometricGoo>)blankGoo);
        var blankStockIn=Add(doc,new Param_Geometry{Name="Log stock per blank (mm)",NickName="BlankStock",Hidden=true},5550,600);
        blankStockIn.SetPersistentData((IEnumerable<IGH_GeometricGoo>)blankStockGoo);
        var blanksPreview=Add(doc,new PreviewComponent{NickName="Log blanks preview"},5800,540);
        blanksPreview.Params.Input[0].AddSource(blanksIn); blanksPreview.Params.Input[1].AddSource(material.Params.Output[0]);
        blanksPreview.Params.Input[2].AddSource(blankFibreIn); blanksPreview.Params.Input[3].AddSource(blankPlaneIn);
        blanksPreview.Params.Input[7].AddSource(blankStockIn);
        ((Param_String)blanksPreview.Params.Input[6]).SetPersistentData("Millimetres");
        // Textured so per-cut grain reads; Gradient / Force flow stay on the right-click menu.
        var slatFibreIn=Add(doc,new Param_Vector{Name="Slat fibre",NickName="SlatFibre"},5380,760);
        slatFibreIn.SetPersistentData(slatFibres);
        var slatPlaneIn=Add(doc,new Param_Plane{Name="Slat plane",NickName="SlatPlane",Hidden=true},5380,840);
        slatPlaneIn.SetPersistentData(slatPlanes);
        var slatsIn=Add(doc,new Param_Geometry{Name="Tower slats (mm)",NickName="Slats",Hidden=true},5550,760);
        slatsIn.SetPersistentData((IEnumerable<IGH_GeometricGoo>)slatGoo);
        var slatStockIn=Add(doc,new Param_Geometry{Name="Log stock per slat (mm)",NickName="SlatStock",Hidden=true},5550,840);
        slatStockIn.SetPersistentData((IEnumerable<IGH_GeometricGoo>)slatStockGoo);
        var towerPreview=Add(doc,new PreviewComponent{NickName="Tower preview"},5800,780);
        towerPreview.Params.Input[0].AddSource(slatsIn); towerPreview.Params.Input[1].AddSource(material.Params.Output[0]);
        towerPreview.Params.Input[2].AddSource(slatFibreIn); towerPreview.Params.Input[3].AddSource(slatPlaneIn);
        towerPreview.Params.Input[7].AddSource(slatStockIn);
        ((Param_String)towerPreview.Params.Input[6]).SetPersistentData("Millimetres");
        var lineParam=Add(doc,new Param_Curve{NickName="Placement lines"},6050,620);
        lineParam.SetPersistentData(lines.Select(c=>(IGH_GeometricGoo)new GH_Curve(c)).ToArray());
        var envParam=Add(doc,new Param_Curve{NickName="Log envelope",Hidden=false},6050,820);
        envParam.SetPersistentData(new GH_Curve(envelope));
        var gradParam=Add(doc,new Param_Mesh{NickName="Knot influence"},6300,620);
        gradParam.SetPersistentData(new GH_Mesh(gradient));
        material.Hidden=true;
        foreach(var c in doc.Objects.Skip(start).OfType<GH_Component>().Where(c=>c!=blanksPreview && c!=towerPreview)) c.Hidden=true;
        Group(doc,"05 / Same log tower",start,DemoColour);
    }
    static Brep LogCylinder(Point3d center, double length, double radius)
    {
        var start=center-new Vector3d(length*.5,0,0);
        var circle=new Circle(new Plane(start,Vector3d.XAxis),radius).ToNurbsCurve();
        var solid=Extrusion.Create(circle,length,true)?.ToBrep();
        Assert(solid?.IsSolid==true,"Log cylinder extrusion failed");
        return solid;
    }
    static void AddBambooCulm(GH_Document doc)
    {
        int start=doc.Objects.Count;
        Text(doc,"06 / NATURAL BAMBOO",6600,270,20,true);
        Text(doc,"Bamboo Stock builds a hollow Moso culm (Ø 80 · wall 7 · 1500 mm) on the demo ground line.\nA vertical half-cut shows wall, cavity and nodes with cured bamboo texture.",6600,325,12);
        var material=Material(doc,ReviewedKnotSeed(),6750,520); material.NickName="Culm material";
        Number(material,2,8);
        var basePt=new Point3d(1600,150,0); var tipPt=new Point3d(1600,150,1500);
        var culm=Add(doc,new BambooStockComponent{NickName="Bamboo culm",Hidden=true},6950,520);
        ((Param_Line)culm.Params.Input[0]).SetPersistentData(new Line(basePt,tipPt));
        // Species comes from SelectedSpecies (panel). Stored Species text locks the dropdown.
        ((Param_String)culm.Params.Input[1]).PersistentData.Clear();
        Assert(culm.SetAdvanced(true),"Bamboo advanced inputs required for Geometry Units");
        ((Param_String)culm.Params.Input[8]).SetPersistentData("Millimetres");
        culm.Params.Input[2].AddSource(material.Params.Output[0]);
        var geometry=Add(doc,new Param_Geometry{NickName="Culm geometry",Hidden=true},7200,650);
        geometry.AddSource(culm.Params.Output[1]);
        var plane=Add(doc,new Param_Plane{NickName="Culm section plane",Hidden=true},7100,780);
        plane.PersistentData.Clear();
        plane.SetPersistentData(new Plane(new Point3d(1600,150,750),Vector3d.XAxis));
        var cut=Add(doc,new CutComponent{NickName="Culm vertical section",Hidden=true},7400,780);
        cut.Params.Input[4].AddSource(culm.Params.Output[0]);
        cut.Params.Input[1].AddSource(plane);
        ((Param_String)cut.Params.Input[3]).SetPersistentData("Millimetres");
        var section=Add(doc,new PreviewComponent{NickName="Bamboo culm section preview"},7600,780);
        section.Params.Input[8].AddSource(cut.Params.Output[3]);
        ((Param_Boolean)section.Params.Input[5]).PersistentData.Clear();
        ((Param_Boolean)section.Params.Input[5]).SetPersistentData(true);
        var outlines=Add(doc,new Param_Curve{NickName="Culm section outline",Hidden=true},7600,980);
        outlines.AddSource(cut.Params.Output[2]);
        material.Hidden=true;
        foreach(var c in doc.Objects.Skip(start).OfType<GH_Component>().Where(c=>c!=section)) c.Hidden=true;
        Group(doc,"06 / Natural bamboo",start,DemoColour);
    }
    // Straight generators between bottom/top circles; twist creates the cooling-tower waist.
    internal static (Brep[] Slats, Point3d[] Mids, Point3d[] Feet, Plane[] Frames, double Length) RotatedTower(int count=16, double height=3600, double radiusBottom=1100, double radiusTop=900, double twistDegrees=55, double width=90, double thickness=35, Point3d? origin=null)
    {
        var o = origin ?? new Point3d(0, 2800, 0);
        double twist = twistDegrees * Math.PI / 180;
        var slats = new Brep[count]; var mids = new Point3d[count]; var feet = new Point3d[count]; var frames = new Plane[count]; double length=0;
        for (int i = 0; i < count; i++)
        {
            double a0 = 2 * Math.PI * i / count, a1 = a0 + twist;
            var p0 = o + new Vector3d(radiusBottom * Math.Cos(a0), radiusBottom * Math.Sin(a0), 0);
            var p1 = o + new Vector3d(radiusTop * Math.Cos(a1), radiusTop * Math.Sin(a1), height);
            var x = p1 - p0; length = x.Length; x.Unitize();
            var mid = (p0 + p1) * .5; mids[i]=mid; feet[i]=p0;
            var z = Vector3d.CrossProduct(x, Vector3d.ZAxis); if (z.IsTiny()) z = Vector3d.CrossProduct(x, Vector3d.YAxis); z.Unitize();
            var y = Vector3d.CrossProduct(z, x); y.Unitize();
            frames[i] = new Plane(mid, x, y);
            var box = new Box(frames[i], new Interval(-length * .5, length * .5), new Interval(-width * .5, width * .5), new Interval(-thickness * .5, thickness * .5));
            slats[i] = Brep.CreateFromBox(box) ?? throw new Exception("Tower slat box failed");
        }
        return (slats,mids,feet,frames,length);
    }
    // 4×4 cant packing inside the log cross-section, then exploded radially for readability.
    internal static (Brep[] Blanks, Point3d[] Mids) ExplodedLogCut(int count, double length, double width, double thickness, Point3d logCenter, double logRadius, double explode)
    {
        int cols=(int)Math.Ceiling(Math.Sqrt(count)), rows=(int)Math.Ceiling(count/(double)cols);
        var blanks=new Brep[count]; var mids=new Point3d[count];
        double packW=cols*width, packH=rows*thickness;
        Assert(Math.Sqrt(packW*packW+packH*packH)*.5 < logRadius*.98,"Log radius must contain the blank pack");
        for(int i=0;i<count;i++)
        {
            int col=i%cols, row=i/cols;
            double py=(col+.5)*width-packW*.5, pz=(row+.5)*thickness-packH*.5;
            var packed=new Point3d(logCenter.X,logCenter.Y+py,logCenter.Z+pz);
            var offset=new Vector3d(0,py,pz); if(!offset.IsTiny()) offset.Unitize();
            var mid=packed+offset*(Math.Sqrt(py*py+pz*pz)*(explode-1));
            mids[i]=mid;
            var box=new Box(new Plane(mid,Vector3d.XAxis,Vector3d.YAxis),new Interval(-length*.5,length*.5),new Interval(-width*.5,width*.5),new Interval(-thickness*.5,thickness*.5));
            blanks[i]=Brep.CreateFromBox(box)??throw new Exception("Log blank box failed");
        }
        return (blanks,mids);
    }
    internal static Mesh KnotInfluenceDisk(TimberPanel panel, double sampleU, double sampleV0, double sampleW0, Point3d center, Vector3d yAxis, Vector3d zAxis, double radius, Func<double,double> mapV=null, Func<double,double> mapW=null, int rings=10, int segments=36)
    {
        yAxis.Unitize(); zAxis.Unitize();
        mapV ??= vy => sampleV0+vy; mapW ??= vz => sampleW0+vz;
        var mesh=new Mesh();
        var samples=new List<double>();
        samples.Add(panel.KnotInfluence(sampleU,mapV(0),mapW(0)));
        mesh.Vertices.Add(center);
        for(int r=1;r<=rings;r++)
        {
            double rr=radius*r/rings;
            for(int s=0;s<segments;s++)
            {
                double a=2*Math.PI*s/segments;
                var p=center+yAxis*(rr*Math.Cos(a))+zAxis*(rr*Math.Sin(a));
                mesh.Vertices.Add(p);
                double vy=Vector3d.Multiply(p-center,yAxis), vz=Vector3d.Multiply(p-center,zAxis);
                samples.Add(panel.KnotInfluence(sampleU,mapV(vy),mapW(vz)));
            }
        }
        foreach(var v in samples) mesh.VertexColors.Add(Heat(Math.Max(0,Math.Min(1,v))));
        for(int s=0;s<segments;s++) mesh.Faces.AddFace(0,1+s,1+(s+1)%segments);
        for(int r=1;r<rings;r++)
        {
            int ring0=1+(r-1)*segments, ring1=1+r*segments;
            for(int s=0;s<segments;s++)
            {
                int a=ring0+s, b=ring0+(s+1)%segments, c=ring1+(s+1)%segments, d=ring1+s;
                mesh.Faces.AddFace(a,b,c,d);
            }
        }
        mesh.Normals.ComputeNormals(); return mesh;
    }
    static Color Heat(double soft) => PreviewComponent.InfluenceHeat(soft);
    internal static Brep Cutaway()
    {
        using var profile = new PolylineCurve(new[] { new Point3d(0,0,0),new Point3d(0,300,0),new Point3d(0,300,40),new Point3d(0,200,40),new Point3d(0,200,80),new Point3d(0,100,80),new Point3d(0,100,120),new Point3d(0,0,120),new Point3d(0,0,0) });
        using var surface = Surface.CreateExtrusion(profile,new Vector3d(1200,0,0)); using var open = surface.ToBrep();
        var closed = open.CapPlanarHoles(.001); Assert(closed?.IsSolid == true,"Stepped cutaway must be one closed solid");
        closed.Faces.SplitKinkyFaces(.01,true); if (closed.SolidOrientation == BrepSolidOrientation.Inward) closed.Flip(); return closed;
    }
    // Test/example authoring only: sample the volumetric knot field.
    internal static Func<Point3d,double> KnotSampler(TimberPanel panel) => p => panel.KnotInfluence(p.X,p.Y,p.Z);
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
            Assert(demos.Length==6 && demos.All(p=>p.Params.Output[0].VolatileDataCount>0),"Combined demonstrations must all solve");
            var tower=demos.Single(p=>p.NickName=="Tower preview");
            var blanks=demos.Single(p=>p.NickName=="Log blanks preview");
            string PreviewDiag(PreviewComponent p)=>$"{p.NickName}: g={p.Params.Input[0].VolatileDataCount}, meshes={p.Params.Output[0].VolatileDataCount}, products={p.Params.Output[1].VolatileDataCount}, sg={p.Params.Input[7].VolatileDataCount}, hidden={p.Hidden}, knot={p.ShowKnotInfluence}, msg={p.Message}, err={string.Join(";",p.RuntimeMessages(GH_RuntimeMessageLevel.Error))}, warn={string.Join(";",p.RuntimeMessages(GH_RuntimeMessageLevel.Warning))}";
            Assert(tower.Params.Output[0].VolatileDataCount==16,"Rotated tower needs one preview mesh per slat ("+PreviewDiag(tower)+" | "+PreviewDiag(blanks)+" | units="+units+")");
            Assert(blanks.Params.Output[0].VolatileDataCount==16,"Log cut needs one preview mesh per blank ("+PreviewDiag(blanks)+" | units="+units+")");
            Assert(!blanks.ShowKnotInfluence && !tower.ShowKnotInfluence,"Same-log previews must start Textured so cut grain reads");
            Assert(blanks.Params.Input[7].SourceCount==1,"Log blanks Preview needs shared log Stock Geometry");
            Assert(tower.Params.Input[7].SourceCount==1,"Tower Preview needs shared log Stock Geometry");
            Assert(blanks.Params.Input[2].SourceCount==1 && blanks.Params.Input[3].SourceCount==1,"Log blanks need explicit fibre and base plane");
            Assert(tower.Params.Input[2].SourceCount==1 && tower.Params.Input[3].SourceCount==1,"Tower slats need explicit fibre and base plane");
            Assert(blanks.Params.Input[7].VolatileDataCount==16,"Each blank needs its own transformed log Stock Geometry");
            Assert(tower.Params.Input[7].VolatileDataCount==16,"Each slat needs its own transformed log Stock Geometry");
            Assert(tower.Params.Input[2].VolatileDataCount==16 && tower.Params.Input[3].VolatileDataCount==16,"Each slat needs its own fibre and plane");
            // Cuts from one log must not collapse to the same material sample (the failure mode when SG/fibre/plane is missing).
            var blankGeoms=doc.Objects.OfType<Param_Geometry>().Single(p=>p.NickName=="Blanks").PersistentData.AllData(true).OfType<GH_Brep>().Select(g=>g.Value).ToArray();
            var blankStocks=doc.Objects.OfType<Param_Geometry>().Single(p=>p.NickName=="BlankStock").PersistentData.AllData(true).OfType<GH_Brep>().Select(g=>g.Value).ToArray();
            var blankFibres=doc.Objects.OfType<Param_Vector>().Single(p=>p.NickName=="BlankFibre").PersistentData.AllData(true).OfType<GH_Vector>().Select(g=>g.Value).ToArray();
            var blankPlanes=doc.Objects.OfType<Param_Plane>().Single(p=>p.NickName=="BlankPlane").PersistentData.AllData(true).OfType<GH_Plane>().Select(g=>g.Value).ToArray();
            Assert(blankGeoms.Length==16 && blankStocks.Length==16 && blankFibres.Length==16 && blankPlanes.Length==16,"Log blanks, stocks, fibres and planes must stay paired");
            var settings=new TimberSettings{Product=TimberProduct.SolidTimber,Seed=ReviewedKnotSeed()};
            var samples=new HashSet<int>();
            for(int i=0;i<16;i++) {
                using var part=PreparedPart.Create(blankGeoms[i],settings,1,.001,blankPlanes[i],blankFibres[i],stock:blankStocks[i]);
                var mid=part.Coordinates[part.Coordinates.Length/2];
                samples.Add(StockDefinition.FromTimber(settings,part.LengthMm,part.WidthMm,part.DepthMm).Sample(mid.X,mid.Y,mid.Z));
            }
            Assert(samples.Count>=8,$"Same-log blanks must show distinct cuts; only {samples.Count} unique mid samples");
            var lines=doc.Objects.OfType<Param_Curve>().Single(p=>p.NickName=="Placement lines");
            Assert(lines.PersistentDataCount==16,"Placement lines must map each blank to a slat");
            var influence=doc.Objects.OfType<Param_Mesh>().Single(p=>p.NickName=="Knot influence");
            var influenceMesh=influence.PersistentData.AllData(true).OfType<GH_Mesh>().Single().Value;
            Assert(influenceMesh.VertexColors.Count==influenceMesh.Vertices.Count && influenceMesh.Vertices.Count>0,"Knot influence disk must carry vertex colours");
            var culm=doc.Objects.OfType<BambooStockComponent>().Single(c=>c.NickName=="Bamboo culm");
            Assert(!culm.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any(),"Bamboo culm Bamboo Stock failed");
            Assert(!culm.SpeciesControlled && culm.SelectedSpecies==BambooSpeciesId.Moso,"Bamboo Species dropdown must stay free; store SelectedSpecies only");
            var culmStock=culm.Params.Output[0].VolatileData.AllData(true).OfType<StockGoo>().Single().Value;
            Assert(culmStock.Settings.Product==TimberProduct.Bamboo && culmStock.Geometry is Brep { IsSolid: true },"Bamboo culm stock must be a solid Bamboo model");
            var culmSection=demos.Single(p=>p.NickName=="Bamboo culm section preview");
            Assert(culmSection.Params.Output[1].VolatileData.AllData(true).OfType<GH_String>().All(s=>s.Value=="Bamboo"),"Culm section must use Bamboo product");
            Assert(!demos.Any(p=>p.NickName=="Bamboo culm preview"),"Full culm preview stays off; section only");
            foreach(var c in doc.Objects.OfType<GH_Component>()) Assert(!c.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any(),c.Name+": "+string.Join(";",c.RuntimeMessages(GH_RuntimeMessageLevel.Error)));
            var sliders=doc.Objects.OfType<GH_NumberSlider>().ToArray();
            Assert(sliders.All(s=>!s.IsExpression),"Percentage and degree sliders must display their labelled raw units");
            var preview=demos.Single(p=>p.NickName=="Cutting preview");
            foreach(decimal position in new[]{20m,50m,80m}) foreach(decimal angle in new[]{0m,25m}) {
                sliders.Single(s=>s.NickName=="Position (%)").SetSliderValue(position); sliders.Single(s=>s.NickName=="Angle (degrees)").SetSliderValue(angle);
                foreach(var slider in sliders) slider.ExpireSolution(false); doc.NewSolution(false);
                var cut=doc.Objects.OfType<CutComponent>().Single(c=>c.NickName=="TimberCut"); var plane=cut.Params.Input[1].VolatileData.AllData(true).OfType<GH_Plane>().Single().Value;
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
            Console.WriteLine("  Combined cutting, knot, buildup, same-log tower and bamboo demos solve in "+units);
        } } finally { rhino.ModelUnitSystem=previous; rhino.ModelAbsoluteTolerance=tolerance; }
    }
}
