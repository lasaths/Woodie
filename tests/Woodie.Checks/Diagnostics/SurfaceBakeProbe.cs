using System.Drawing;
using Rhino;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Render;
using Woodie.Appearance;
using Woodie.Rendering;
using static Woodie.Checks.Program;
namespace Woodie.Checks;
internal static class SurfaceBakeProbe
{
    public static void Run()
    {
        using var doc=RhinoDoc.CreateHeadless(null);doc.ModelUnitSystem=UnitSystem.Millimeters;
        for(int i=0;i<13;i++) { using var existing=new Material { Name="Existing material "+i };doc.Materials.Add(existing); }
        using var original=new Box(Plane.WorldXY,new Interval(0,1200),new Interval(0,300),new Interval(0,120)).ToBrep();
        using var brep=original.DuplicateBrep();
        var directory=Path.Combine(Output,"native-surface-bake");Directory.CreateDirectory(directory);
        var panel=new TimberPanel(TimberProduct.SolidTimber,1200,300,120,3);
        using var parent=new Material { Name="Woodie surface bake proof",DiffuseColor=Color.White };
        int parentIndex=doc.Materials.Add(parent);
        foreach(var face in brep.Faces) {
            face.GetSurfaceSize(out var w,out var h);int width=Math.Max(2,(int)Math.Ceiling(w)),height=Math.Max(2,(int)Math.Ceiling(h));
            int[] pixels=new int[width*height];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++) {
                double u=face.Domain(0).ParameterAt((x+.5)/width),v=face.Domain(1).ParameterAt(1-(y+.5)/height);
                var point=face.PointAt(u,v);var n=face.NormalAt(u,v);
                pixels[y*width+x]=panel.Col(point.X,point.Y,point.Z,n.X,n.Y,n.Z);
            }
            string path=Path.Combine(directory,"face-"+face.FaceIndex+".png");PngEncoder.WriteAtomic(path,width,height,pixels,CancellationToken.None);
            using var child=new Material { Name="Woodie face "+face.FaceIndex,DiffuseColor=Color.White };
            Assert(child.SetBitmapTexture(path),"Face texture failed");int index=doc.Materials.Add(child);
            doc.Bitmaps.AddBitmap(path,false);face.MaterialChannelIndex=parent.MaterialChannelIndexFromId(doc.Materials[index].Id,true);
            Console.WriteLine($"Face {face.FaceIndex}: child {doc.Materials[index].Id}, channel {face.MaterialChannelIndex}, reference {parent.MaterialChannelIdFromIndex(face.MaterialChannelIndex)}");
        }
        Assert(doc.Materials.Modify(parent,parentIndex,true),"Parent material update failed");
        Console.WriteLine("Table parent "+doc.Materials[parentIndex].Name+": "+string.Join(",",Enumerable.Range(0,8).Select(i=>i+"="+doc.Materials[parentIndex].MaterialChannelIdFromIndex(i))));
        var id=doc.Objects.AddBrep(brep,new ObjectAttributes { MaterialIndex=parentIndex,MaterialSource=ObjectMaterialSource.MaterialFromObject });
        var renderParameters=new MeshingParameters(MeshingParameters.Default) { TextureRange=MeshingParameterTextureRange.UnpackedUnscaledNormalized };
        Assert(doc.Objects.FindId(id).SetRenderMeshParameters(renderParameters),"Could not persist native UV mesh settings");
        using var mapping=TextureMapping.CreateSurfaceParameterMapping();Assert(doc.Objects.FindId(id).SetTextureMapping(1,mapping)!=0,"Surface mapping assignment failed");
        string file=Path.Combine(directory,"joined-brep.3dm");
        Assert(doc.WriteFile(file,new FileWriteOptions { SuppressDialogBoxes=true,IncludeBitmapTable=true,IncludeRenderMeshes=false }),"Could not write surface proof");
        using(var model=File3dm.Read(file)) { var savedParent=model.AllMaterials.First(m=>m.Name==parent.Name); Console.WriteLine("Archive channels: "+string.Join(",",Enumerable.Range(0,8).Select(i=>i+"="+savedParent.MaterialChannelIdFromIndex(i)))); }
        using var opened=RhinoDoc.OpenHeadless(file);var loaded=opened.Objects.FindId(id);
        var native=(Brep)loaded.Geometry;
        Console.WriteLine($"Loaded parent {loaded.Attributes.MaterialIndex}, channels: {string.Join(",",native.Faces.Select(f=>f.MaterialChannelIndex))}");Assert(native.IsSolid && native.Faces.Count==6 && opened.Objects.Count==1,"Native joined geometry changed");
        var material=opened.Materials[loaded.Attributes.MaterialIndex];
        Console.WriteLine("Read parent "+material.Name+": "+string.Join(",",Enumerable.Range(0,8).Select(i=>i+"="+material.MaterialChannelIdFromIndex(i))));
        int samples=0,bad=0;double worst=0;
        Assert(loaded.GetRenderMeshParameters().TextureRange==MeshingParameterTextureRange.UnpackedUnscaledNormalized,"Native UV mode did not persist");
        var rebuiltParameters=new MeshingParameters(loaded.GetRenderMeshParameters()) { MaximumEdgeLength=55,RefineGrid=true };
        Assert(loaded.CreateMeshes(MeshType.Render,rebuiltParameters,false)>0,"Native remesh failed");
        var rebuilt=loaded.GetMeshes(MeshType.Render);Assert(rebuilt.Length==native.Faces.Count,"Native face mesh count changed");
        using var savedMapping=loaded.GetTextureMapping(1);
        foreach(var face in native.Faces) {
            var childId=material.MaterialChannelIdFromIndex(face.MaterialChannelIndex);
            Console.WriteLine($"Saved face {face.FaceIndex}: channel {face.MaterialChannelIndex}, material {childId}");Assert(childId!=Guid.Empty,"Lost face material channel");
            Assert(opened.Materials.FindId(childId)!=null,"Lost face material");
            using var mesh=rebuilt[face.FaceIndex];
            mesh.Faces.ConvertQuadsToTriangles();mesh.SetTextureCoordinates(savedMapping,Transform.Identity,false,true);
            foreach(var f in mesh.Faces) {
                var a=mesh.Vertices.Point3dAt(f.A);var b=mesh.Vertices.Point3dAt(f.B);var c=mesh.Vertices.Point3dAt(f.C);
                var point=new Point3d((a.X+b.X+c.X)/3,(a.Y+b.Y+c.Y)/3,(a.Z+b.Z+c.Z)/3);
                face.ClosestPoint(point,out var u,out var v);var uv=mesh.TextureCoordinates;
                double du=(uv[f.A].X+uv[f.B].X+uv[f.C].X)/3-face.Domain(0).NormalizedParameterAt(u);
                double dv=(uv[f.A].Y+uv[f.B].Y+uv[f.C].Y)/3-face.Domain(1).NormalizedParameterAt(v);
                double error=Math.Sqrt(du*du+dv*dv);worst=Math.Max(worst,error);if(error>1e-5)bad++;samples++;
            }
        }
        Console.WriteLine($"Surface UVs: {bad}/{samples} incorrect, max {worst}; native solid, materials and mapping reopened. {file}");
        Assert(bad==0,"Native surface UV mapping did not reconstruct face parameters");
    }
}
