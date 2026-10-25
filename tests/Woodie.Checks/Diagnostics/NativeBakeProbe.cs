using Rhino;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Render;
using Woodie.Appearance;
using Woodie.Geometry;
using Woodie.Rendering;
using static Woodie.Checks.Program;
namespace Woodie.Checks;

// Feasibility gate: native objects must reconstruct the atlas after remeshing, without Woodie.
internal static class NativeBakeProbe
{
    public static void Run(bool continuousCharts=false,bool rayProjection=false)
    {
        using var doc=RhinoDoc.CreateHeadless(null);
        var fixtures=GeometryGallery.Fixtures(27);
        var failures=new List<string>();
        try {
            foreach(var fixture in fixtures.Where(f=>new[]{"01","02","06","08"}.Contains(f.Name[..2]))) {
                using var part=PreparedPart.Create(fixture.Geometry,new TimberSettings { Resolution=8 },1,.001,guide:fixture.Guide);
                var pages=TextureAtlas.Prepare(part,new TimberSettings { Resolution=8 });
                try {
                    Assert(pages.Count==1,"Probe fixture unexpectedly requires multiple pages");
                    using var chartMesh=continuousCharts?SurfaceCharts(fixture.Geometry):pages[0].Mesh.DuplicateMesh();
                    using var mapping=TextureMapping.CreateCustomMeshMapping(chartMesh);
                    if(continuousCharts && rayProjection) { var projection=typeof(TextureMapping).GetProperty("Projection");projection.SetValue(mapping,Enum.Parse(projection.PropertyType,"Ray")); }
                    TextureAtlas.BakeMissing(pages,CancellationToken.None);
                    using var material=new Material { Name="Native bake mapping probe",DiffuseColor=System.Drawing.Color.White };
                    Assert(material.SetBitmapTexture(pages[0].TexturePath),"Could not assign probe texture");
                    int materialIndex=doc.Materials.Add(material); doc.Bitmaps.AddBitmap(pages[0].TexturePath,false);
                    var id=doc.Objects.Add(fixture.Geometry,new ObjectAttributes { MaterialIndex=materialIndex,MaterialSource=ObjectMaterialSource.MaterialFromObject });
                    var obj=doc.Objects.FindId(id);
                    Assert(obj.SetTextureMapping(1,mapping)!=0,"Could not assign mapping");
                    var file=Path.Combine(Output,"native-bake-probe-"+(continuousCharts?(rayProjection?"chart-ray-":"charts-"):"triangles-")+fixture.Name[..2]+".3dm");
                    Assert(doc.WriteFile(file,new FileWriteOptions { SuppressDialogBoxes=true,IncludeRenderMeshes=false,IncludeBitmapTable=true }),"Could not save mapping probe");
                    using var reopened=RhinoDoc.OpenHeadless(file);
                    var loaded=reopened.Objects.FindId(id);
                    Assert(loaded.Geometry.GetType()==obj.Geometry.GetType(),"Native object type changed");
                    using var savedMapping=loaded.GetTextureMapping(1);
                    Assert(savedMapping!=null,"Missing saved mapping");
                    Assert(savedMapping.TryGetMappingMesh(out var savedMesh),"Mapping mesh did not survive reopen");
                    savedMesh.Dispose();
                    using var nativeBrep=loaded.Geometry is Extrusion e?e.ToBrep():((Brep)loaded.Geometry).DuplicateBrep();
                    var meshes=Mesh.CreateFromBrep(nativeBrep,
                        new MeshingParameters(MeshingParameters.Default) { RefineGrid=true,MaximumEdgeLength=100 });
                    using var controlMesh=chartMesh.DuplicateMesh();
                    controlMesh.TextureCoordinates.Clear();
                    foreach(var v in controlMesh.Vertices)controlMesh.TextureCoordinates.Add(v.X/1800,v.Y/500);
                    using var control=TextureMapping.CreateCustomMeshMapping(controlMesh);
                    int samples=0,bad=0,controlBad=0;double maxError=0,controlMax=0;
                    foreach(var mesh in meshes) {
                        using(mesh) {
                            mesh.Faces.ConvertQuadsToTriangles(); mesh.Normals.ComputeNormals();
                            mesh.SetTextureCoordinates(savedMapping,Transform.Identity,false,true);
                            foreach(var f in mesh.Faces) {
                                var a=mesh.Vertices.Point3dAt(f.A);var b=mesh.Vertices.Point3dAt(f.B);var c=mesh.Vertices.Point3dAt(f.C);
                                var centre=new Point3d((a.X+b.X+c.X)/3,(a.Y+b.Y+c.Y)/3,(a.Z+b.Z+c.Z)/3);
                                var normal=Vector3d.CrossProduct(b-a,c-a);normal.Unitize();
                                savedMapping.Evaluate(centre,normal,out var expected);
                                var ua=mesh.TextureCoordinates[f.A];var ub=mesh.TextureCoordinates[f.B];var uc=mesh.TextureCoordinates[f.C];
                                double du=((ua.X+ub.X+uc.X)/3-expected.X)*pages[0].Width;
                                double dv=((ua.Y+ub.Y+uc.Y)/3-expected.Y)*pages[0].Height;
                                double error=Math.Sqrt(du*du+dv*dv);maxError=Math.Max(maxError,error);if(error>2)bad++;samples++;
                            }
                            mesh.SetTextureCoordinates(control,Transform.Identity,false,true);
                            foreach(var f in mesh.Faces) {
                                var a=mesh.Vertices.Point3dAt(f.A);var b=mesh.Vertices.Point3dAt(f.B);var c=mesh.Vertices.Point3dAt(f.C);
                                var centre=new Point3d((a.X+b.X+c.X)/3,(a.Y+b.Y+c.Y)/3,(a.Z+b.Z+c.Z)/3);
                                var normal=Vector3d.CrossProduct(b-a,c-a);normal.Unitize();control.Evaluate(centre,normal,out var expected);
                                var ua=mesh.TextureCoordinates[f.A];var ub=mesh.TextureCoordinates[f.B];var uc=mesh.TextureCoordinates[f.C];
                                double du=((ua.X+ub.X+uc.X)/3-expected.X)*pages[0].Width, dv=((ua.Y+ub.Y+uc.Y)/3-expected.Y)*pages[0].Height;
                                double error=Math.Sqrt(du*du+dv*dv);controlMax=Math.Max(controlMax,error);if(error>2)controlBad++;
                            }
                        }
                    }
                    Console.WriteLine($"  {fixture.Name}: {bad}/{samples} remeshed triangle centres differ by >2 texels; max {maxError:0.###} px");
                    Console.WriteLine($"    Continuous UV control: {controlBad} mismatches; max {controlMax:0.###} px");
                    Assert(controlBad==0,"Continuous mapping control failed; the probe needs investigation");
                    if(bad>0) failures.Add(fixture.Name+": "+bad+"/"+samples+" samples");
                    doc.Objects.Delete(id,true);
                } finally { foreach(var page in pages)page.Dispose(); }
            }
        } finally { foreach(var fixture in fixtures)fixture.Dispose(); }
        Assert(failures.Count==0,"Native mapping is not safe after remeshing: "+string.Join("; ",failures));
    }
    static Mesh SurfaceCharts(GeometryBase geometry)
    {
        using var brep=geometry is Extrusion extrusion?extrusion.ToBrep():((Brep)geometry).DuplicateBrep();
        var result=new Mesh();int columns=(int)Math.Ceiling(Math.Sqrt(brep.Faces.Count));int rows=(brep.Faces.Count+columns-1)/columns;
        foreach(var face in brep.Faces) {
            using var single=face.DuplicateFace(false);
            foreach(var mesh in Mesh.CreateFromBrep(single,new MeshingParameters(MeshingParameters.Default) { RefineGrid=true,MaximumEdgeLength=50 })) {
                using(mesh) {
                    mesh.TextureCoordinates.Clear();
                    foreach(var vertex in mesh.Vertices) {
                        Assert(face.ClosestPoint(vertex,out double u,out double v),"Surface parameter lookup failed");
                        mesh.TextureCoordinates.Add((face.FaceIndex%columns+face.Domain(0).NormalizedParameterAt(u))/columns,
                            (face.FaceIndex/columns+face.Domain(1).NormalizedParameterAt(v))/rows);
                    }
                    result.Append(mesh);
                }
            }
        }
        result.Normals.ComputeNormals();return result;
    }

}
