using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.FileIO;
using Rhino.Render;
using Woodie.Appearance;
using Woodie.Rendering;
using System.Drawing;
using static Woodie.Checks.Program;
namespace Woodie.Checks;
internal static class PackedBakeProbe
{
    sealed record Chart(double Ax,double Bx,double Cx,double Ay,double By,double Cy)
    {
        public Point2d Map(double u,double v)=>new(Ax+Bx*u+Cx*v,Ay+By*u+Cy*v);
    }
    public static void Run()
    {
        var fixtures=GeometryGallery.Fixtures(27);var failures=new List<string>();
        try {
        foreach(var fixture in fixtures.Where(f=>f.Geometry is Mesh)) {
            using var mesh=((Mesh)fixture.Geometry).DuplicateMesh();int vertices=mesh.Vertices.Count,faces=mesh.Faces.Count;
            bool ok=MeshCharts(mesh);
            Console.WriteLine($"{fixture.Name}: unwrap {ok}, vertices {vertices}->{mesh.Vertices.Count}, faces {faces}->{mesh.Faces.Count}, UVs {mesh.TextureCoordinates.Count}");
            Assert(ok && vertices==mesh.Vertices.Count && faces==mesh.Faces.Count,"Unwrap changed source mesh topology");
            var originalMesh=(Mesh)fixture.Geometry;
            for(int i=0;i<vertices;i++)Assert(mesh.Vertices.Point3dAt(i)==originalMesh.Vertices.Point3dAt(i),"Mesh charting moved a vertex");
            for(int i=0;i<faces;i++)Assert(mesh.Faces[i].Equals(originalMesh.Faces[i]),"Mesh charting changed face indices");
            using(var reconstructed=mesh.DuplicateMesh())using(var surfaceMapping=TextureMapping.CreateSurfaceParameterMapping()) {
                reconstructed.SetTextureCoordinates(surfaceMapping,Transform.Identity,false,true);
                for(int i=0;i<vertices;i++){Near(reconstructed.TextureCoordinates[i].X,mesh.TextureCoordinates[i].X,.000001);Near(reconstructed.TextureCoordinates[i].Y,mesh.TextureCoordinates[i].Y,.000001);}
            }

            using var meshDoc=RhinoDoc.CreateHeadless(null);meshDoc.ModelUnitSystem=UnitSystem.Millimeters;
            var meshId=meshDoc.Objects.AddMesh(mesh);using var paintMesh=mesh.DuplicateMesh();
            if(fixture.Name.StartsWith("04")) {
                Paint(meshDoc,meshDoc.Objects.FindId(meshId),paintMesh,"mesh-04");
                Assert(meshDoc.WriteFile(Path.Combine(Output,"native-packed-mesh-04.3dm"),new FileWriteOptions { SuppressDialogBoxes=true,IncludeRenderMeshes=false,IncludeBitmapTable=true }),"Could not save native mesh proof");
            }

        }
        foreach(var fixture in fixtures.Where(f=>f.Geometry is not Mesh)) {
            using var doc=RhinoDoc.CreateHeadless(null);doc.ModelUnitSystem=UnitSystem.Millimeters;
            var id=doc.Objects.Add(fixture.Geometry,new ObjectAttributes());var obj=doc.Objects.FindId(id);
            var originalType=obj.Geometry.GetType();
            using var brep=fixture.Geometry switch { Brep b=>b.DuplicateBrep(),Extrusion e=>e.ToBrep(),Surface s=>s.ToBrep(),_=>throw new Exception() };
            using var mapping=TextureMapping.CreateSurfaceParameterMapping();obj.SetTextureMapping(1,mapping);
            var first=new MeshingParameters(MeshingParameters.Default) { TextureRange=MeshingParameterTextureRange.PackedScaledNormalized, MaximumEdgeLength=100 };
            obj.SetRenderMeshParameters(first);obj.CreateMeshes(MeshType.Render,first,false);
            var charts=new Dictionary<int,Chart>();int referenceVertices=0;
            Visit(obj,brep,(face,mesh,f)=> {
                referenceVertices+=3;
                if(charts.ContainsKey(face.FaceIndex))return;
                var ids=new[]{f.A,f.B,f.C};var uv=ids.Select(i=>mesh.TextureCoordinates[i]).ToArray();
                var st=ids.Select(i=> { face.ClosestPoint(mesh.Vertices.Point3dAt(i),out double u,out double v);return new Point2d(face.Domain(0).NormalizedParameterAt(u),face.Domain(1).NormalizedParameterAt(v)); }).ToArray();
                double du=st[1].X-st[0].X,dv=st[1].Y-st[0].Y,eu=st[2].X-st[0].X,ev=st[2].Y-st[0].Y,det=du*ev-dv*eu;
                if(Math.Abs(det)<1e-12)return;
                (double a,double b,double c) Fit(double x,double y,double z) {double b=((y-x)*ev-(z-x)*dv)/det,c=((z-x)*du-(y-x)*eu)/det;return(x-b*st[0].X-c*st[0].Y,b,c);}
                var x=Fit(uv[0].X,uv[1].X,uv[2].X);var y=Fit(uv[0].Y,uv[1].Y,uv[2].Y);charts.Add(face.FaceIndex,new Chart(x.a,x.b,x.c,y.a,y.b,y.c));
            });
            Assert(charts.Count==brep.Faces.Count,"Native UV chart count mismatch: "+fixture.Name);
            if(fixture.Name.StartsWith("01") || fixture.Name.StartsWith("02")) {
                using var combined=new Mesh();foreach(var m in obj.GetMeshes(MeshType.Render)){using(m)combined.Append(m);}
                Paint(doc,obj,combined,fixture.Name[..2]);
            }

            var file=Path.Combine(Output,"native-packed-"+fixture.Name[..2]+".3dm");Assert(doc.WriteFile(file,new FileWriteOptions { SuppressDialogBoxes=true,IncludeRenderMeshes=false,IncludeBitmapTable=true }),"Could not save packed proof");
            using var opened=RhinoDoc.OpenHeadless(file);var loaded=opened.Objects.FindId(id);Assert(loaded.Geometry.GetType()==originalType,"Packed mapping changed object type");
            var second=new MeshingParameters(loaded.GetRenderMeshParameters()) { MaximumEdgeLength=37,RefineGrid=true,SimplePlanes=false };
            loaded.SetRenderMeshParameters(second);loaded.CreateMeshes(MeshType.Render,second,false);
            int samples=0,bad=0;double max=0;
            Visit(loaded,brep,(face,mesh,f)=> {
                foreach(int i in new[]{f.A,f.B,f.C}) {
                    face.ClosestPoint(mesh.Vertices.Point3dAt(i),out var u,out var v);var expected=charts[face.FaceIndex].Map(face.Domain(0).NormalizedParameterAt(u),face.Domain(1).NormalizedParameterAt(v));
                    var actual=mesh.TextureCoordinates[i];double error=Math.Sqrt(Math.Pow(actual.X-expected.X,2)+Math.Pow(actual.Y-expected.Y,2))*2048;
                    max=Math.Max(max,error);if(error>2)bad++;samples++;
                }
            });
            Console.WriteLine($"{fixture.Name}: {originalType.Name}, charts {charts.Count}, packed UV errors {bad}/{samples}, max {max:0.###} px, reference vertices {referenceVertices}");
            if(bad>0)failures.Add(fixture.Name);
        }}finally{foreach(var f in fixtures)f.Dispose();}
        Assert(failures.Count==0,"Packed native UVs changed after remesh: "+string.Join(",",failures));
    }
    static bool MeshCharts(Mesh mesh)
    {
        int count=mesh.Vertices.Count;var roots=Enumerable.Range(0,count).ToArray();
        int Root(int i){while(roots[i]!=i){roots[i]=roots[roots[i]];i=roots[i];}return i;}
        foreach(var f in mesh.Faces){int root=Root(f.A);foreach(int i in f.IsQuad?new[]{f.B,f.C,f.D}:new[]{f.B,f.C})roots[Root(i)]=root;}
        var charts=new List<(int[] Indices,Point2d[] UV,double Width,double Height)>();
        foreach(var group in Enumerable.Range(0,count).GroupBy(Root)) {
            var indices=group.ToArray();var local=indices.Select((id,i)=>(id,i)).ToDictionary(t=>t.id,t=>t.i);using var piece=new Mesh();
            foreach(int i in indices)piece.Vertices.Add(mesh.Vertices.Point3dAt(i));
            foreach(var f in mesh.Faces)if(local.ContainsKey(f.A)){if(f.IsQuad)piece.Faces.AddFace(local[f.A],local[f.B],local[f.C],local[f.D]);else piece.Faces.AddFace(local[f.A],local[f.B],local[f.C]);}
            piece.Faces.ConvertQuadsToTriangles();piece.Normals.ComputeNormals();var points=piece.Vertices.ToPoint3dArray();Plane.FitPlaneToPoints(points,out var plane);
            Point2d[] uv;
            if(points.All(p=>Math.Abs(plane.DistanceTo(p))<.000001))uv=points.Select(p=>{plane.RemapToPlaneSpace(p,out var q);return new Point2d(q.X,q.Y);}).ToArray();
            else {
                using var unwrap=new MeshUnwrapper(piece);if(!unwrap.Unwrap(MeshUnwrapMethod.LSCM)||piece.TextureCoordinates.Count!=indices.Length)return false;
                double worldArea=0,uvArea=0;
                foreach(var f in piece.Faces){var a=piece.Vertices.Point3dAt(f.A);var b=piece.Vertices.Point3dAt(f.B);var c=piece.Vertices.Point3dAt(f.C);worldArea+=Vector3d.CrossProduct(b-a,c-a).Length/2;var u=piece.TextureCoordinates[f.A];var v=piece.TextureCoordinates[f.B];var w=piece.TextureCoordinates[f.C];uvArea+=Math.Abs((v.X-u.X)*(w.Y-u.Y)-(v.Y-u.Y)*(w.X-u.X))/2;}
                if(!(uvArea>0))return false;double scale=Math.Sqrt(worldArea/uvArea);uv=Enumerable.Range(0,indices.Length).Select(i=>new Point2d(piece.TextureCoordinates[i].X*scale,piece.TextureCoordinates[i].Y*scale)).ToArray();
            }
            double minU=uv.Min(p=>p.X),minV=uv.Min(p=>p.Y),width=uv.Max(p=>p.X)-minU,height=uv.Max(p=>p.Y)-minV;
            if(width<height){uv=uv.Select(p=>new Point2d(p.Y-minV,p.X-minU)).ToArray();(width,height)=(height,width);}else uv=uv.Select(p=>new Point2d(p.X-minU,p.Y-minV)).ToArray();
            charts.Add((indices,uv,width,height));
        }
        double side=Math.Max(charts.Max(c=>c.Width)+10,Math.Sqrt(charts.Sum(c=>(c.Width+10)*(c.Height+10)))*1.5);var result=new Point2f[count];
        double x=5,y=5,row=0;
        foreach(var chart in charts.OrderByDescending(c=>c.Height)) {
            if(x+chart.Width+5>side){x=5;y+=row+10;row=0;}
            if(y+chart.Height+5>side)side=y+chart.Height+5;
            for(int i=0;i<chart.Indices.Length;i++)result[chart.Indices[i]]=new Point2f((float)(x+chart.UV[i].X),(float)(y+chart.UV[i].Y));
            x+=chart.Width+10;row=Math.Max(row,chart.Height);
        }
        mesh.TextureCoordinates.Clear();foreach(var uv in result)mesh.TextureCoordinates.Add(uv.X/side,uv.Y/side);
        return mesh.SetSurfaceParametersFromTextureCoordinates();
    }
    static void Paint(RhinoDoc doc,RhinoObject obj,Mesh mesh,string name)
    {
        mesh.Faces.ConvertQuadsToTriangles();mesh.Normals.ComputeNormals();
        double required=0;
        foreach(var f in mesh.Faces) {
            var ids=new[]{f.A,f.B,f.C};var normal=Vector3d.CrossProduct(mesh.Vertices.Point3dAt(f.B)-mesh.Vertices.Point3dAt(f.A),mesh.Vertices.Point3dAt(f.C)-mesh.Vertices.Point3dAt(f.A));normal.Unitize();double resolution=Math.Abs(normal.Z)>.9?2:1;
            for(int j=0;j<3;j++) {
                int a=ids[j],b=ids[(j+1)%3];var ua=mesh.TextureCoordinates[a];var ub=mesh.TextureCoordinates[b];double length=Math.Sqrt(Math.Pow(ua.X-ub.X,2)+Math.Pow(ua.Y-ub.Y,2));
                if(length>1e-10)required=Math.Max(required,mesh.Vertices.Point3dAt(a).DistanceTo(mesh.Vertices.Point3dAt(b))/length/resolution);
            }
        }
        int side=Math.Max(64,((int)Math.Ceiling(required)+6+15)/16*16);Assert(side<=8192,"Proof texture exceeds 8192 pixels");
        var pixels=new int[side*side];var panel=new TimberPanel(TimberProduct.SolidTimber,1800,500,27,3);int inset=3;
        foreach(var f in mesh.Faces) {
            var a=mesh.Vertices.Point3dAt(f.A);var b=mesh.Vertices.Point3dAt(f.B);var c=mesh.Vertices.Point3dAt(f.C);
            Point2d Uv(int i){var p=mesh.TextureCoordinates[i];return new Point2d(inset+p.X*(side-2*inset),side-inset-p.Y*(side-2*inset));}
            var ua=Uv(f.A);var ub=Uv(f.B);var uc=Uv(f.C);double det=(ub.Y-uc.Y)*(ua.X-uc.X)+(uc.X-ub.X)*(ua.Y-uc.Y);if(Math.Abs(det)<1e-12)continue;
            int x0=Math.Max(0,(int)Math.Floor(Math.Min(ua.X,Math.Min(ub.X,uc.X)))),x1=Math.Min(side-1,(int)Math.Ceiling(Math.Max(ua.X,Math.Max(ub.X,uc.X))));
            int y0=Math.Max(0,(int)Math.Floor(Math.Min(ua.Y,Math.Min(ub.Y,uc.Y)))),y1=Math.Min(side-1,(int)Math.Ceiling(Math.Max(ua.Y,Math.Max(ub.Y,uc.Y))));
            var n=Vector3d.CrossProduct(b-a,c-a);
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++) {
                double wa=((ub.Y-uc.Y)*(x+.5-uc.X)+(uc.X-ub.X)*(y+.5-uc.Y))/det;
                double wb=((uc.Y-ua.Y)*(x+.5-uc.X)+(ua.X-uc.X)*(y+.5-uc.Y))/det,wc=1-wa-wb;
                if(wa<-.0000001||wb<-.0000001||wc<-.0000001)continue;
                var p=new Point3d(wa*a.X+wb*b.X+wc*c.X,wa*a.Y+wb*b.Y+wc*c.Y,wa*a.Z+wb*b.Z+wc*c.Z);
                pixels[y*side+x]=panel.Col(p.X,p.Y,p.Z,n.X,n.Y,n.Z);
            }
        }
        // Extend chart edges into the unused atlas space for texture filtering.
        for(int pass=0;pass<3;pass++) {
            var previous=(int[])pixels.Clone();
            for(int y=1;y<side-1;y++)for(int x=1;x<side-1;x++)if(previous[y*side+x]==0) {
                int index=y*side+x;
                if(previous[index-1]!=0)pixels[index]=previous[index-1];
                else if(previous[index+1]!=0)pixels[index]=previous[index+1];
                else if(previous[index-side]!=0)pixels[index]=previous[index-side];
                else if(previous[index+side]!=0)pixels[index]=previous[index+side];
            }
        }
        string file=Path.Combine(Output,"native-packed-texture-"+name+".png");PngEncoder.WriteAtomic(file,side,side,pixels,CancellationToken.None);
        using var material=new Material { Name="Woodie native packed proof "+name,DiffuseColor=Color.White };
        using var texture=new Texture { FileName=file,Enabled=true };
        double scale=(side-2d*inset)/side;texture.UvwTransform=Transform.Translation(inset/(double)side,inset/(double)side,0)*Transform.Scale(Point3d.Origin,scale);
        Assert(material.SetBitmapTexture(texture),"Could not assign native packed texture");int indexMaterial=doc.Materials.Add(material);doc.Bitmaps.AddBitmap(file,false);
        var attributes=obj.Attributes.Duplicate();attributes.MaterialIndex=indexMaterial;attributes.MaterialSource=ObjectMaterialSource.MaterialFromObject;doc.Objects.ModifyAttributes(obj,attributes,true);
        Console.WriteLine($"Painted {name}: {side} x {side} pixels, one native {obj.Geometry.GetType().Name}");
    }
    static void Visit(RhinoObject obj,Brep brep,Action<BrepFace,Mesh,MeshFace> visit)
    {
        foreach(var mesh in obj.GetMeshes(MeshType.Render)) {
            using(mesh) {
                mesh.Faces.ConvertQuadsToTriangles();Assert(mesh.TextureCoordinates.Count==mesh.Vertices.Count,"Native mesh has no UVs");
                foreach(var f in mesh.Faces) {
                    var a=mesh.Vertices.Point3dAt(f.A);var b=mesh.Vertices.Point3dAt(f.B);var c=mesh.Vertices.Point3dAt(f.C);
                    var p=new Point3d((a.X+b.X+c.X)/3,(a.Y+b.Y+c.Y)/3,(a.Z+b.Z+c.Z)/3);
                    BrepFace best=null;double distance=double.MaxValue;
                    foreach(var face in brep.Faces)if(face.ClosestPoint(p,out var u,out var v)){double d=face.PointAt(u,v).DistanceToSquared(p);if(d<distance){distance=d;best=face;}}
                    Assert(best!=null,"No native face found");visit(best,mesh,f);
                }
            }
        }
    }
}
