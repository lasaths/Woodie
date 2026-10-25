using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.FileIO;
using Rhino.Render;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Woodie.Appearance;
using Woodie.Baking;
using Woodie.Grasshopper;
using static Woodie.Checks.Program;
namespace Woodie.Checks;

internal static class NativeBakingChecks
{
    static RhinoDoc Document(string name,UnitSystem units=UnitSystem.Millimeters)
    {
        var doc=RhinoDoc.CreateHeadless(null);doc.ModelUnitSystem=units;doc.ModelAbsoluteTolerance=.001/RhinoMath.UnitScale(units,UnitSystem.Millimeters);
        string path=Path.Combine(Output,"baking",name,"model.3dm");Directory.CreateDirectory(Path.GetDirectoryName(path));
        Assert(doc.WriteFile(path,new FileWriteOptions { SuppressDialogBoxes=true,UpdateDocumentPath=true }),"Could not initialise bake test document");return doc;
    }
    static NativeBakePlan Plan(GeometryBase g,RhinoDoc doc,TimberSettings settings=null,GeometryBase stock=null,Curve guide=null,Plane? plane=null)
    {
        using var snapshot=new BakeSnapshot(g,settings??new TimberSettings { Product=TimberProduct.SolidTimber,Resolution=8,EdgeResolution=4,KnotDensity=0 },doc.ModelUnitSystem,plane,guide:guide,stock:stock);
        return new NativeBakePlan(snapshot,doc,CancellationToken.None);
    }
    public static void Core()
    {
        using var doc=Document("core");using var stock=BuildupCutChecks.Stock();uint crc=stock.DataCRC(0);
        using var plan=Plan(stock,doc);Assert(plan.Geometry is Brep b&&b.IsSolid&&b.Faces.Count==stock.Faces.Count,"Bake changed Brep topology");
        plan.Render(CancellationToken.None);Assert(File.Exists(plan.TexturePath)&&plan.Side<=8192,"Full-quality native texture missing");
        int layer=doc.Layers.Add("Native timber bake",System.Drawing.Color.Brown);
        var ids=TimberBaker.Commit(doc,new[]{plan},new ObjectAttributes { LayerIndex=layer,Name="My timber" },CancellationToken.None);
        Assert(ids.Count==1,"Bake must add one native object per input");var obj=doc.Objects.FindId(ids[0]);
        Assert(obj.Geometry is Brep&&obj.Attributes.LayerIndex==layer&&obj.Attributes.Name=="My timber","Bake attributes or native type lost");
        Assert(obj.GetTextureMapping(1)!=null&&obj.Attributes.MaterialSource==ObjectMaterialSource.MaterialFromObject,"Native mapping/material missing");
        var material=obj.RenderMaterial.Id;int renderCount=doc.RenderMaterials.Count;
        using(var bitmap=doc.Materials[obj.Attributes.MaterialIndex].GetBitmapTexture())Assert(bitmap!=null&&new[]{0,1,3,4,5,7}.All(i=>Math.Abs(bitmap.UvwTransform[i/4,i%4]-Transform.Identity[i/4,i%4])<1e-6),"Rhino changed the bake atlas inset transform");
        var second=TimberBaker.Commit(doc,new[]{plan},null,CancellationToken.None);
        Assert(second.Count==1&&doc.Objects.FindId(second[0]).RenderMaterial.Id==material&&doc.RenderMaterials.Count==renderCount,"Repeated bake duplicated the material");
        using var repeat=Plan(stock,doc);repeat.Render(CancellationToken.None);Assert(repeat.TexturePath==plan.TexturePath,"Identical baking did not reuse content-addressed texture");
        Assert(stock.DataCRC(0)==crc,"Baking changed source geometry");
        int before=doc.Objects.Count,materials=doc.Materials.Count(m=>!m.IsDeleted);using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        try{TimberBaker.Commit(doc,new[]{plan},null,cancelled.Token);throw new Exception("Cancelled bake committed");}catch(OperationCanceledException){}
        Assert(doc.Objects.Count==before&&doc.Materials.Count(m=>!m.IsDeleted)==materials,"Cancelled bake left objects or materials");
        using var other=Plan(stock,doc,new TimberSettings { Product=TimberProduct.Plywood,Resolution=8,EdgeResolution=4 });other.Render(CancellationToken.None);
        Assert(other.TexturePath!=plan.TexturePath,"Different constructions reused the same texture");
        int objectsBefore=doc.Objects.GetObjectList(ObjectType.AnyObject).Count(),renderBefore=doc.RenderMaterials.Count,legacyBefore=doc.Materials.Count(m=>!m.IsDeleted);
        other.Dispose(); // A failure on item two must roll back item one and its new material.
        try {TimberBaker.Commit(doc,new[]{plan,other},null,CancellationToken.None);throw new Exception("Invalid batch committed");}
        catch(ObjectDisposedException){}
        catch(ArgumentException){}
        Assert(doc.Objects.GetObjectList(ObjectType.AnyObject).Count()==objectsBefore&&doc.RenderMaterials.Count==renderBefore&&doc.Materials.Count(m=>!m.IsDeleted)==legacyBefore,"Failed bake left partial objects or unused render materials");
        Assert(doc.WriteFile(doc.Path,new FileWriteOptions { SuppressDialogBoxes=true,IncludeBitmapTable=true,IncludeRenderMeshes=false }),"Could not save baked native model");
        string hidden=plan.TexturePath+".unavailable";File.Move(plan.TexturePath,hidden,true);
        try {
        using var opened=RhinoDoc.OpenHeadless(doc.Path);
        var embedded=opened.Bitmaps.Find(plan.TexturePath,false,out var located);
        Assert(embedded!=null||(!string.IsNullOrEmpty(located)&&File.Exists(located)),"Embedded timber texture cannot be found with external PNG unavailable");
        var saved=opened.Objects.FindId(ids[0]);
        Assert(saved.Geometry is Brep native&&native.IsSolid&&saved.GetTextureMapping(1)!=null,"Native baked geometry/mapping did not reopen");
        Assert(opened.Materials[saved.Attributes.MaterialIndex].GetBitmapTexture()!=null,"Baked material did not reopen");
        Console.WriteLine($"Native bake: {plan.Side}px, texture reused, one joined Brep, embedded texture, persistence and cancellation pass");
        }finally{File.Move(hidden,plan.TexturePath,true);}
    }
    public static void Geometry()
    {
        var fixtures=GeometryGallery.Fixtures(27);
        try {using var doc=Document("geometry");foreach(var fixture in fixtures) {
            uint crc=fixture.Geometry.DataCRC(0);
            using var plan=Plan(fixture.Geometry,doc,guide:fixture.Guide);
            Assert(plan.Geometry.GetType()==fixture.Geometry.GetType(),"Bake replaced source type: "+fixture.Name);
            if(fixture.Geometry is Extrusion extrusion) {
                var baked=(Extrusion)plan.Geometry;Assert(baked.ProfileCount==extrusion.ProfileCount&&baked.CapCount==extrusion.CapCount&&baked.PathStart==extrusion.PathStart&&baked.PathEnd==extrusion.PathEnd,"Bake changed extrusion structure");
                using var before=extrusion.ToBrep();using var after=baked.ToBrep();Near(after.GetVolume(),before.GetVolume(),Math.Max(.000001,before.GetVolume()*.000000001));
                for(int profile=0;profile<extrusion.ProfileCount;profile++)using(var original=extrusion.Profile3d(profile,.5))using(var copy=baked.Profile3d(profile,.5))foreach(double parameter in original.DivideByCount(40,true)) {var p=original.PointAt(parameter);Assert(copy.ClosestPoint(p,out var q)&&copy.PointAt(q).DistanceTo(p)<.00001,"Bake changed extrusion profile shape");}
            }
            if(fixture.Geometry is Mesh mesh) {
                var baked=(Mesh)plan.Geometry;Assert(mesh.Vertices.Count==baked.Vertices.Count&&mesh.Faces.Count==baked.Faces.Count,"Bake changed mesh topology");
                for(int i=0;i<mesh.Vertices.Count;i++)Assert(mesh.Vertices[i]==baked.Vertices[i],"Bake moved mesh vertices");
                for(int i=0;i<mesh.Faces.Count;i++)Assert(mesh.Faces[i].Equals(baked.Faces[i]),"Bake changed mesh face indices");
            }
            Assert(crc==fixture.Geometry.DataCRC(0),"Baking changed a source fixture");
            plan.Render(CancellationToken.None);
            var ids=TimberBaker.Commit(doc,new[]{plan},new ObjectAttributes {Name=fixture.Name},CancellationToken.None);
            string file=Path.Combine(Path.GetDirectoryName(doc.Path),fixture.Name[..2]+".3dm");
            Assert(doc.WriteFile(file,new FileWriteOptions { SuppressDialogBoxes=true,IncludeRenderMeshes=false,IncludeBitmapTable=true }),"Could not save geometry proof");
            using(var opened=RhinoDoc.OpenHeadless(file))using(var reference=NativeUvGeometry.Create(fixture.Geometry,doc.ModelUnitSystem,64,CancellationToken.None)) {
                var obj=opened.Objects.FindId(ids[0]);CheckRemeshedUV(obj,reference.Samples);
            }
            Console.WriteLine("Native bake saves/reopens/remeshes "+fixture.Name+" / "+plan.Side+"px");
            doc.Objects.Delete(ids[0],true);
        }}finally{foreach(var fixture in fixtures)fixture.Dispose();}
    }
    static void CheckRemeshedUV(RhinoObject obj,Mesh reference)
    {
        using var tree=new RTree();
        for(int i=0;i<reference.Faces.Count;i++) {
            var f=reference.Faces[i];var uv=new[]{f.A,f.B,f.C}.Select(j=>reference.TextureCoordinates[j]).ToArray();
            var box=new BoundingBox(uv.Select(p=>new Point3d(p.X,p.Y,0)));box.Inflate(1e-7);tree.Insert(box,i);
        }
        var parameters=new MeshingParameters(obj.GetRenderMeshParameters()??MeshingParameters.Default){MaximumEdgeLength=37,SimplePlanes=false,RefineGrid=true};
        using var mapping=obj.GetTextureMapping(1);Assert(mapping.UvwTransform.TryGetInverse(out var uvInverse),"Invalid native UV transform");var meshes=new List<Mesh>();
        if(obj.Geometry is Mesh mesh)meshes.Add(mesh.DuplicateMesh());
        else {obj.SetRenderMeshParameters(parameters);obj.CreateMeshes(MeshType.Render,parameters,false);meshes.AddRange(obj.GetMeshes(MeshType.Render));}
        int samples=0,missing=0;double max=0;
        foreach(var m in meshes)using(m) {
            m.SetTextureCoordinates(mapping,Transform.Identity,false,true);m.Faces.ConvertQuadsToTriangles();
            foreach(var face in m.Faces) {
                var ids=new[]{face.A,face.B,face.C};var actual=new Point3d(ids.Average(j=>m.Vertices.Point3dAt(j).X),ids.Average(j=>m.Vertices.Point3dAt(j).Y),ids.Average(j=>m.Vertices.Point3dAt(j).Z));
                var uv=new Point2d(ids.Average(j=>(double)m.TextureCoordinates[j].X),ids.Average(j=>(double)m.TextureCoordinates[j].Y));
                var raw=uvInverse*new Point3d(uv.X,uv.Y,0);uv=new Point2d(raw.X,raw.Y);
                var candidates=new List<int>();tree.Search(new Sphere(new Point3d(uv.X,uv.Y,0),1e-7),(_,e)=>candidates.Add(e.Id));double best=double.MaxValue;
                foreach(int i in candidates) {
                    var f=reference.Faces[i];var a=reference.TextureCoordinates[f.A];var b=reference.TextureCoordinates[f.B];var c=reference.TextureCoordinates[f.C];
                    double det=(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);if(Math.Abs(det)<1e-15)continue;
                    double v=((uv.X-a.X)*(c.Y-a.Y)-(uv.Y-a.Y)*(c.X-a.X))/det,w=((b.X-a.X)*(uv.Y-a.Y)-(b.Y-a.Y)*(uv.X-a.X))/det;
                    if(v<-.00001||w<-.00001||v+w>1.00001)continue;
                    var p=reference.Vertices.Point3dAt(f.A)+v*(reference.Vertices.Point3dAt(f.B)-reference.Vertices.Point3dAt(f.A))+w*(reference.Vertices.Point3dAt(f.C)-reference.Vertices.Point3dAt(f.A));best=Math.Min(best,p.DistanceTo(actual));
                }
                if(best==double.MaxValue){if(missing<3)Console.WriteLine($"Missing native UV at {actual}, uv {uv}; reference faces {reference.Faces.Count}");missing++;}else {if(best>1&&max<1)Console.WriteLine($"Native UV error at {actual}, uv {uv}: {best} mm");max=Math.Max(max,best);}samples++;
            }
        }
        Console.WriteLine($"Remesh UVs: {samples} triangle centres, missing {missing}, largest physical error {max:0.####} mm");
        Assert(samples>0&&missing==0&&max<.5,"Native UVs changed across save/reopen/remesh");
    }
    public static void UnitsAndStock()
    {
        foreach(var unit in new[]{UnitSystem.Millimeters,UnitSystem.Meters,UnitSystem.Inches}) {
            using var doc=Document("units-"+unit,unit);double scale=RhinoMath.UnitScale(UnitSystem.Millimeters,unit);
            using var stock=BuildupCutChecks.Stock();stock.Transform(Transform.Scale(Point3d.Origin,scale));
            using var cut=Woodie.Geometry.TimberCut.Create(stock,new Plane(new Point3d(600*scale,0,0),Vector3d.XAxis),false,doc.ModelAbsoluteTolerance);
            var settings=new TimberSettings { Product=TimberProduct.SolidTimber,Resolution=8,EdgeResolution=4,Layers=new[]{new LayerDefinition(30),new LayerDefinition(60,false),new LayerDefinition(30)} };
            using var plan=Plan(cut.Geometry[0],doc,settings,stock,plane:Plane.WorldXY);plan.Render(CancellationToken.None);
            var ids=TimberBaker.Commit(doc,new[]{plan},null,CancellationToken.None);var bounds=doc.Objects.FindId(ids[0]).Geometry.GetBoundingBox(true);
            Near(bounds.Max.X,600*scale,Math.Max(1e-8,.001*scale));
            Console.WriteLine("Stock-aware native baking in "+unit);
        }
    }
    public static void MaterialMatrix()
    {
        using var doc=Document("materials");int count=0;
        foreach(var product in Enum.GetValues<TimberProduct>()) {
            var settings=new TimberSettings {Product=product,Resolution=16,EdgeResolution=8,Seed=3};
            var fixtures=GeometryGallery.Fixtures(settings.VirtualDepthMm);
            try {foreach(var fixture in fixtures) {
                using var plan=Plan(fixture.Geometry,doc,settings,guide:fixture.Guide);plan.Render(CancellationToken.None);
                Assert(File.Exists(plan.TexturePath),"Native material texture missing: "+product+" / "+fixture.Name);count++;
            }}finally{foreach(var fixture in fixtures)fixture.Dispose();}
            Console.WriteLine("Native material: "+product+" / 11 geometry textures");
        }
        Assert(count==88,"Native material coverage must include eight products by eleven geometry cases");
    }
    public static void KnotSection()
    {
        using var doc=Document("knot-section");using var stock=TimberExamples.Cutaway();
        using var cut=Woodie.Geometry.TimberCut.Create(stock,new Plane(new Point3d(840,0,0),Vector3d.XAxis),false,.001);
        var settings=new TimberSettings {Product=TimberProduct.SolidTimber,Seed=3,Resolution=1,EdgeResolution=.5};
        using var plan=Plan(cut.Geometry.Single(),doc,settings,stock,plane:Plane.WorldXY);plan.Render(CancellationToken.None);
        var ids=TimberBaker.Commit(doc,new[]{plan},new ObjectAttributes {Name="Knot section / 1 mm faces / 0.5 mm edges"},CancellationToken.None);
        Assert(doc.Objects.FindId(ids.Single()).Geometry is Brep b&&b.IsSolid,"Knot section lost its native capped solid");
        Assert(doc.WriteFile(doc.Path,new FileWriteOptions {SuppressDialogBoxes=true,IncludeBitmapTable=true,IncludeRenderMeshes=false}),"Could not save native knot section");
        Console.WriteLine("Native stepped knot section: "+plan.Side+"px; original Stock coordinates retained");
    }
    public static void Component()
    {
        IsolatedHost.LoadWoodie(typeof(PreviewComponent).Assembly.Location);GH_Document.EnableSolutions=true;
        using var doc=new GH_Document { Enabled=true };var preview=new PreviewComponent { Hidden=true };preview.CreateAttributes();doc.AddObject(preview,false);
        using var stock=BuildupCutChecks.Stock();((Param_Geometry)preview.Params.Input[0]).SetPersistentData(new GH_Brep(stock.DuplicateBrep()));doc.NewSolution(false);
        Assert(preview.IsBakeCapable,"Hidden preview must remain bake capable");
        var show=(Param_Boolean)preview.Params.Input[5];show.PersistentData.Clear();show.SetPersistentData(false);preview.ExpireSolution(false);doc.NewSolution(false);Assert(!preview.IsBakeCapable,"Show off should disable baking");
        show.PersistentData.Clear();show.SetPersistentData(true);preview.ExpireSolution(false);doc.NewSolution(false);Assert(preview.IsBakeCapable,"Bake did not recover after Show on");
        preview.ClearData();Assert(!preview.IsBakeCapable,"Cleared component retained stale bake snapshots");
    }
}
