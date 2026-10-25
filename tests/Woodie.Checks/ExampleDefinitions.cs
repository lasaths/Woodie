using System.Drawing;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;
using Woodie.Appearance;
using Woodie.Grasshopper;
using Woodie.Geometry;
using Woodie.Rendering;
using static Woodie.Checks.Program;

namespace Woodie.Checks;

internal static class ExampleDefinitions
{
    internal static bool IsLibrary(PreviewComponent p) => p.NickName.Contains(" / ");
    internal static IEnumerable<SettingsComponent> LibraryMaterials(GH_Document doc) => doc.Objects.OfType<SettingsComponent>().Where(m => m.NickName.StartsWith("Library · "));

    public static void Write(string directory)
    {
        Directory.CreateDirectory(directory);
        IsolatedHost.LoadExampleBuiltIns();
        using var doc = new GH_Document { Enabled=true };
        doc.Properties.ViewTarget = new System.Drawing.Point(40,40); doc.Properties.ZoomFactor=1;
        ExampleCanvas.Text(doc,"WOODIE / TIMBER PLAYGROUND",40,40,28,true);
        ExampleCanvas.Text(doc,"01 Cut through the stock     02 Follow a knot     03 Build your own stack     04 Product library",40,100,14);
        ExampleCanvas.Text(doc,"Start with the three demonstrations below. Open a material's arrow for its controls.\nUnits are handled by the definition · 1 mm/pixel · input geometry stays unchanged.\nProcedural appearance inspired by Larsson et al. (2022), DOI 10.1145/3528223.3530081.",40,145,12);
        TimberExamples.AddDemonstrations(doc);
        AddCustomBuildup(doc);
        int libraryStart=doc.Objects.Count;
        ExampleCanvas.Text(doc,"04 / PRODUCT LIBRARY",40,1490,24,true);
        ExampleCanvas.Text(doc,"Eight products × eleven geometry cases. Use each card's Show toggle to reveal it in the viewport.\nTurn one product on at a time for close inspection; the three demonstrations stay visible. Shared input wires appear when selected.",40,1550,12);
        using var referenceGuide=GeometryGallery.Bow(); double length=referenceGuide.GetLength();
        string[] labels={"01 Solid Brep","02 Extrusion","03 Surface","04 Closed mesh","05 Open mesh","06 Trimmed + hole","07 Bowed surface","08 Bowed Brep","09 Guided solid","10 Guided extrusion","11 Guided mesh"};
        int productIndex=0;
        foreach(var product in TimberProducts.MenuOrder) {
            int start=doc.Objects.Count;
            float x=40+(productIndex%4)*1450, y=1770+(productIndex/4)*1450;
            double depth=new TimberSettings { Product=product }.VirtualDepthMm;
            ExampleCanvas.Text(doc,product.Label(),x+20,y,20,true);
            ExampleCanvas.Text(doc,$"{depth:0.#} mm · Clean/Pale · 1 mm/pixel",x+20,y+45,12);
            ExampleCanvas.Text(doc,"Material",x+20,y+120,12);
            var material=ExampleCanvas.Add(doc,new SettingsComponent { NickName="Library · "+product.Label() },x+110,y+210);
            material.SelectProduct(product); material.SelectLook(TimberLook.CleanPale); material.SetPanelExpanded(false);
            var resolution=(Param_Number)material.Params.Input[2]; resolution.PersistentData.Clear(); resolution.SetPersistentData(1d);
            var show=ExampleCanvas.Add(doc,new GH_BooleanToggle { NickName="Show "+product.Label(),Value=false },x+20,y+340);
            ExampleCanvas.Text(doc,"Source coordinates",x+20,y+420,12);
            var units=ExampleCanvas.Add(doc,new Param_String { Name="Source coordinate units",NickName="Millimetres" },x+110,y+480); units.SetPersistentData("Millimetres");
            var fixtures=GeometryGallery.Fixtures(depth); fixtures[0].Dispose();
            fixtures[0]=new GeometryGallery.Fixture("01 Flat solid Brep (matched length)",new Box(Plane.WorldXY,new Interval(0,length),new Interval(0,500),new Interval(0,depth)).ToBrep(),"Straight");
            try {
                for(int i=0;i<fixtures.Count;i++) {
                    var fixture=fixtures[i]; int slot=i==0?0:i==8?1:i<8?i+1:i;
                    var move=Transform.Translation(4000+slot*2600,productIndex*1400,0); fixture.Geometry.Transform(move); fixture.Guide?.Transform(move);
                    float cx=x+340+(i%3)*350, cy=y+150+(i/3)*270;
                    ExampleCanvas.Text(doc,labels[i],cx,cy,12,true);
                    IGH_GeometricGoo goo=fixture.Geometry switch {
                        Extrusion e=>new GH_Extrusion((Extrusion)e.Duplicate()), Brep b=>new GH_Brep(b.DuplicateBrep()),
                        Surface s=>new GH_Surface((Surface)s.Duplicate()), Mesh m=>new GH_Mesh(m.DuplicateMesh()), _=>throw new Exception("Unsupported gallery fixture")
                    };
                    var input=ExampleCanvas.Add(doc,new Param_Geometry { Name=product.Label()+" / "+fixture.Name,NickName="Geometry",Hidden=true },cx+30,cy+80); input.SetPersistentData(goo);
                    var preview=ExampleCanvas.Add(doc,new PreviewComponent { NickName=product.Label()+" / "+fixture.Name },cx+230,cy+130);
                    preview.Params.Input[0].AddSource(input); preview.Params.Input[1].AddSource(material.Params.Output[0]);
                    preview.Params.Input[5].AddSource(show); preview.Params.Input[6].AddSource(units); ExampleCanvas.Shared(preview,1,5,6);
                    if(fixture.Guide!=null) {
                        var guide=ExampleCanvas.Add(doc,new Param_Curve { Name=product.Label()+" / "+fixture.Name+" guide",NickName="Guide",Hidden=true },cx+30,cy+185);
                        guide.SetPersistentData(new GH_Curve(fixture.Guide.DuplicateCurve())); preview.Params.Input[4].AddSource(guide);
                        ((Param_Plane)preview.Params.Input[3]).SetPersistentData(Plane.WorldXY);
                    }
                }
            } finally { foreach(var fixture in fixtures) fixture.Dispose(); }
            ExampleCanvas.Group(doc,"Library / "+product.Label(),start,ExampleCanvas.LibraryColour); productIndex++;
        }
        ExampleCanvas.Group(doc,"04 / Product library",libraryStart,ExampleCanvas.LibraryColour);
        var archive=new GH_Archive(); Assert(archive.AppendObject(doc,"Definition"),"Example serialization failed");
        string path=Path.Combine(directory,"Timber-preview.ghx"); Assert(archive.WriteToFile(path,true,false),"Could not write combined example");
        Verify(path); TimberExamples.Verify(directory); Console.WriteLine("  Wrote and reopened one combined timber playground");
        ExampleCanvas.CheckLayout(doc);
    }
    internal static void Verify(string path)
    {
        IsolatedHost.LoadExampleBuiltIns();
        var rhino=RhinoDoc.ActiveDoc ?? throw new Exception("Example verification needs an active Rhino document");
        var previous=rhino.ModelUnitSystem; double tolerance=rhino.ModelAbsoluteTolerance;
        var dimensions=new Dictionary<string,double[]>();
        try { foreach(var units in new[]{UnitSystem.Millimeters,UnitSystem.Meters}) {
            rhino.ModelUnitSystem=units; double toMm=RhinoMath.UnitScale(units,UnitSystem.Millimeters); rhino.ModelAbsoluteTolerance=.001/toMm;
            using var doc=new GH_Document { Enabled=true }; var archive=new GH_Archive();
            Assert(archive.ReadFromFile(path) && archive.ExtractObject(doc,"Definition"),"Combined example did not reopen");
            Assert(LibraryMaterials(doc).Count()==8,"Library needs eight materials");
            Assert(!doc.Objects.OfType<PresetsComponent>().Any(),"Legacy presets are not release examples");
            Assert(doc.Objects.OfType<GH_Scribble>().Count()>=100,"Example needs native Scribble headings and case labels");
            Assert(doc.Objects.OfType<GH_Group>().Count()>=12,"Example sections and product cards must be grouped");
            var inputs=doc.Objects.OfType<Param_Geometry>().Where(p=>TimberProducts.MenuOrder.Any(product=>p.Name.StartsWith(product.Label()+" / "))).ToArray();
            Assert(inputs.Length==88 && inputs.SelectMany(p=>p.PersistentData.AllData(true)).All(g=>g.IsValid),"Missing or invalid library geometry");
            var previews=doc.Objects.OfType<PreviewComponent>().Where(IsLibrary).ToArray(); Assert(previews.Length==88,"Library needs 88 previews");
            var toggles=doc.Objects.OfType<GH_BooleanToggle>().Where(t=>t.NickName.StartsWith("Show ")).ToArray();
            Assert(toggles.Length==8 && toggles.All(t=>!t.Value),"Product library must start off to keep the viewport uncluttered");
            Assert(doc.Objects.OfType<PreviewComponent>().Count(p=>!IsLibrary(p) && !p.Hidden)==3,"Initial view needs the three demonstrations");
            Assert(doc.Objects.OfType<SettingsComponent>().All(m=>!m.PanelExpanded),"Example material panels should start compact");
            foreach(var preview in doc.Objects.OfType<PreviewComponent>()) preview.Hidden=true;
            foreach(var toggle in toggles) { toggle.Value=true; toggle.ExpireSolution(false); }
            foreach(var product in TimberProducts.MenuOrder) {
                var flat=inputs.Single(p=>p.Name==product.Label()+" / 01 Flat solid Brep (matched length)");
                var bowed=inputs.Single(p=>p.Name==product.Label()+" / 09 Bowed solid Brep - guide");
                var a=((GH_Brep)flat.PersistentData.AllData(true).Single()).Value; var b=((GH_Brep)bowed.PersistentData.AllData(true).Single()).Value;
                using var guide=GeometryGallery.Bow(); Near(a.GetBoundingBox(true).Diagonal.X,guide.GetLength(),.02); Near(a.GetVolume(),b.GetVolume(),Math.Max(1,a.GetVolume()*.00001));
            }
            doc.NewSolution(false);
            foreach(var c in doc.Objects.OfType<GH_Component>()) Assert(!c.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any(),c.Name+": "+string.Join(";",c.RuntimeMessages(GH_RuntimeMessageLevel.Error)));
            foreach(var preview in previews) {
                Assert(preview.Params.Input[6].SourceCount==1 && preview.Params.Input[5].SourceCount==1,"Library units and Show must be wired");
                Assert(preview.Params.Output[0].VolatileDataCount>0,"Library mesh missing");
                var box=((GH_Box)preview.Params.Output[2].VolatileData.AllData(true).Single()).Value;
                var actual=new[]{box.X.Length*toMm,box.Y.Length*toMm,box.Z.Length*toMm};
                if(units==UnitSystem.Millimeters) dimensions[preview.NickName]=actual;
                else for(int axis=0;axis<3;axis++) Near(actual[axis],dimensions[preview.NickName][axis],.7);
                foreach(var mesh in preview.Params.Output[0].VolatileData.AllData(true).OfType<GH_Mesh>()) Assert(mesh.Value.IsValid && mesh.Value.TextureCoordinates.Count==mesh.Value.Vertices.Count,"Invalid library mesh/UVs");
            }
            Console.WriteLine("  Combined library: 88 cases solve in "+units);
        } } finally { rhino.ModelUnitSystem=previous; rhino.ModelAbsoluteTolerance=tolerance; }
    }
    internal static void SurfaceResolution()
    {
        IsolatedHost.LoadWoodie(typeof(PreviewComponent).Assembly.Location);
        using var doc = new GH_Document();
        var archive = new GH_Archive();
        string path = Path.GetFullPath(Path.Combine(Output, "../../examples/Timber-preview.ghx"));
        Assert(archive.ReadFromFile(path) && archive.ExtractObject(doc, "Definition"), "Gallery did not reopen");
        Assert(LibraryMaterials(doc).Count() == 8, "Gallery needs all eight materials");
        var coarse = new List<string>();
        foreach (var material in LibraryMaterials(doc)) {
            double resolution = ((Param_Number)material.Params.Input[2]).PersistentData.AllData(true).OfType<GH_Number>().Single().Value;
            var settings = new TimberSettings { Product = material.SelectedProduct, Look = material.SelectedLook, Resolution = resolution };
            var source = doc.Objects.OfType<Param_Geometry>().Single(p => p.Name == settings.Product.Label() + " / 06 Trimmed surface with hole");
            var geometry = (GeometryBase)source.PersistentData.AllData(true).Single().ScriptVariable();
            using var part = PreparedPart.Create(geometry, settings, 1, .001);
            var pages = TextureAtlas.Prepare(part, settings);
            try {
                double pitch = pages.SelectMany(p => p.Tiles).Max(t => t.A.DistanceTo(t.B) / t.Bx);
                Console.WriteLine($"  {settings.Product}: final surface texels {pitch:0.###} mm/pixel");
                if (pitch > 1.01) coarse.Add(settings.Product.ToString());
            } finally { foreach (var page in pages) page.Dispose(); }
        }
        Assert(coarse.Count == 0, "Gallery surface textures exceed 1 mm/pixel: " + string.Join(", ", coarse));
    }

    static void AddCustomBuildup(GH_Document doc)
    {
        int start=doc.Objects.Count;
        ExampleCanvas.Text(doc,"03 / BUILD YOUR OWN STACK",3150,270,20,true);
        ExampleCanvas.Text(doc,"Layer → Buildup → Preview. Stack order is bottom to top.\nExact 30 / 60 cross / 30 mm; enable Fit only to scale proportions.",3150,325,12);
        ExampleCanvas.Text(doc,"Appearance",3250,425,12);
        var material=ExampleCanvas.Add(doc,new SettingsComponent { NickName="Buildup material" },3350,500);
        material.SelectProduct(TimberProduct.CLT); material.SelectLook(TimberLook.CleanPale); material.SetPanelExpanded(false);
        var res=(Param_Number)material.Params.Input[2]; res.PersistentData.Clear(); res.SetPersistentData(1d);
        var buildup=ExampleCanvas.Add(doc,new BuildupComponent { Hidden=true },3700,770); buildup.Params.Input[0].AddSource(material.Params.Output[0]);
        for(int i=0;i<3;i++) {
            float y=650+i*210;
            ExampleCanvas.Text(doc,i==0?"Bottom / 30 mm":i==1?"Cross core / 60 mm":"Top / 30 mm",3150,y-70,12,true);
            var layer=ExampleCanvas.Add(doc,new LayerComponent(),3350,y);
            var thickness=(Param_Number)layer.Params.Input[0]; thickness.PersistentData.Clear(); thickness.SetPersistentData(i==1?60d:30d);
            var direction=(Param_Integer)layer.Params.Input[1]; direction.PersistentData.Clear(); direction.SetPersistentData(i==1?1:0);
            buildup.Params.Input[1].AddSource(layer.Params.Output[0]);
        }
        using var stock=BuildupCutChecks.Stock(); stock.Translate(0,1300,0);
        ExampleCanvas.Text(doc,"120 mm stock / mm",3720,475,12);
        var input=ExampleCanvas.Add(doc,new Param_Geometry { Name="Custom buildup / stock",NickName="Stock",Hidden=true },3800,540); input.SetPersistentData(new GH_Brep(stock.DuplicateBrep()));
        var preview=ExampleCanvas.Add(doc,new PreviewComponent { NickName="Custom buildup" },4120,600);
        preview.Params.Input[0].AddSource(input); preview.Params.Input[1].AddSource(buildup.Params.Output[0]);
        ((Param_String)preview.Params.Input[6]).SetPersistentData("Millimetres"); ((Param_Plane)preview.Params.Input[3]).SetPersistentData(Plane.WorldXY);
        ExampleCanvas.Text(doc,"Resolved schedule",4350,655,12,true);
        var schedule=ExampleCanvas.Add(doc,new GH_Panel { NickName="Layer schedule" },4350,720);
        schedule.Attributes.Bounds=new RectangleF(4350,720,620,110);
        schedule.Properties.Colour=Color.FromArgb(249,248,242); schedule.Properties.DrawPaths=false; schedule.Properties.DrawIndices=false;
        schedule.Properties.Font=new Font("Consolas",10f);
        schedule.AddSource(buildup.Params.Output[3]);
        ExampleCanvas.Text(doc,"Section diagram output\nPreview hidden; isolate it for inspection.",3950,910,12);
        var diagram=ExampleCanvas.Add(doc,new Param_Curve { NickName="Section diagram",Hidden=true },4130,1020); diagram.AddSource(buildup.Params.Output[4]);
        ExampleCanvas.Group(doc,"03 / Custom buildup",start,ExampleCanvas.DemoColour);
    }
    internal static Mesh BentSolid(Curve guide)
    {
        var mesh = new Mesh();
        double[] parameters = guide.DivideByCount(64, true);
        foreach (double t in parameters)
        {
            Point3d p = guide.PointAt(t); Vector3d x = guide.TangentAt(t); x.Unitize();
            Vector3d z = Vector3d.ZAxis - (Vector3d.ZAxis * x) * x; z.Unitize();
            Vector3d y = Vector3d.CrossProduct(z, x);
            foreach (var corner in new[] { (-120, -100), (120, -100), (120, 100), (-120, 100) })
                mesh.Vertices.Add(p + corner.Item1 * y + corner.Item2 * z);
        }
        for (int i = 0; i < parameters.Length - 1; i++)
            for (int j = 0; j < 4; j++) mesh.Faces.AddFace(i * 4 + j, i * 4 + (j + 1) % 4, (i + 1) * 4 + (j + 1) % 4, (i + 1) * 4 + j);
        mesh.Faces.AddFace(3, 2, 1, 0);
        int last = (parameters.Length - 1) * 4; mesh.Faces.AddFace(last, last + 1, last + 2, last + 3);
        mesh.Normals.ComputeNormals(); mesh.Unweld(Math.PI / 4, true);
        return mesh;
    }
}
