using Rhino;
using Rhino.Geometry;
using Rhino.Render;
namespace Woodie.Baking;

internal sealed class NativeUvGeometry : IDisposable
{
    internal GeometryBase Geometry { get; }
    internal Mesh Samples { get; }
    internal MeshingParameters Meshing { get; }
    internal TextureMapping Mapping { get; }
    NativeUvGeometry(GeometryBase geometry, Mesh samples, TextureMapping mapping, MeshingParameters meshing = null) { Geometry = geometry; Samples = samples; Mapping = mapping; Meshing = meshing; }
    internal static NativeUvGeometry Create(GeometryBase source, UnitSystem units, double paddingMm, CancellationToken ct, double gridAngle = Math.PI / 180)
    {
        ct.ThrowIfCancellationRequested();
        if (source is Mesh original)
        {
            var output = original.DuplicateMesh();
            try
            {
                if (!MeshCharts(output, RhinoMath.UnitScale(units, UnitSystem.Millimeters), paddingMm, ct)) throw new ArgumentException("This mesh cannot be UV-mapped without changing its topology. Supply a mesh with existing UV seams, or explicitly unweld the required seams upstream.");
                if (output.Vertices.Count != original.Vertices.Count || output.Faces.Count != original.Faces.Count) throw new ArgumentException("Baking would change mesh topology; add UV seams upstream.");
                var samples = output.DuplicateMesh(); samples.Faces.ConvertQuadsToTriangles(); samples.Normals.ComputeNormals(); return new NativeUvGeometry(output, samples, TextureMapping.CreateSurfaceParameterMapping());
            }
            catch { output.Dispose(); throw; }
        }
        var geometry = source.Duplicate();
        TextureMapping mapping = null;
        try
        {
            // Use Rhino's own packed surface UV layout, which survives render-mesh regeneration.
            using var scratch = RhinoDoc.CreateHeadless(null); scratch.ModelUnitSystem = units;
            var id = scratch.Objects.Add(geometry); var obj = scratch.Objects.FindId(id);
            if (obj == null) throw new ArgumentException("Rhino could not prepare the native geometry for baking.");
            var box = geometry is Extrusion profile ? ExtrusionBox(profile) : new Box(geometry.GetBoundingBox(true));
            bool boxed = geometry is Extrusion e && e.IsSolid && box.X.Length > 0 && box.Y.Length > 0 && box.Z.Length > 0;
            if (boxed)
            {
                // An enclosing cube avoids nonuniform normal scaling folding curved walls onto end faces.
                double side = Math.Max(box.X.Length, Math.Max(box.Y.Length, box.Z.Length));
                var cubePlane = box.Plane; cubePlane.Origin = box.Center; var extent = new Interval(-side / 2, side / 2); box = new Box(cubePlane, extent, extent, extent);
            }

            mapping = boxed ? TextureMapping.CreateBoxMapping(box.Plane, box.X, box.Y, box.Z, true) : TextureMapping.CreateSurfaceParameterMapping();
            if (boxed)
            {
                // TextureSpace is available in the supported Rhino 8.20 host; SDK reference remains 8.0.
                var space = typeof(TextureMapping).GetProperty("TextureSpace") ?? throw new NotSupportedException("Native extrusion baking requires Rhino 8.20 or later.");
                space.SetValue(mapping, Enum.Parse(space.PropertyType, "Divided"));
                var projection = typeof(TextureMapping).GetProperty("Projection"); projection.SetValue(mapping, Enum.Parse(projection.PropertyType, "ClosestPoint"));
            }
            if (mapping == null || obj.SetTextureMapping(1, mapping) == 0) throw new ArgumentException("Could not prepare the native texture mapping.");
            var parameters = new MeshingParameters(MeshingParameters.Default) { TextureRange = MeshingParameterTextureRange.PackedScaledNormalized, RefineGrid = true, GridAngle = gridAngle, MinimumEdgeLength = 0, MaximumEdgeLength = 30 / RhinoMath.UnitScale(units, UnitSystem.Millimeters), SimplePlanes = false };
            obj.SetRenderMeshParameters(parameters);
            if (obj.CreateMeshes(MeshType.Render, parameters, false) == 0) throw new ArgumentException("Rhino could not create native render UVs.");
            var samples = new Mesh();
            foreach (var mesh in obj.GetMeshes(MeshType.Render)) { using (mesh) { if (boxed) mesh.SetTextureCoordinates(mapping, Transform.Identity, false, true); samples.Append(mesh); } }
            samples.Faces.ConvertQuadsToTriangles(); samples.Normals.ComputeNormals();
            if (samples.Faces.Count == 0 || samples.Faces.Count > 200000) { samples.Dispose(); throw new ArgumentException("Native baking supports up to 200,000 render triangles per object. Simplify the geometry."); }
            if (geometry is Brep b) foreach (var face in b.Faces) face.ClearMaterialChannelIndex();
            return new NativeUvGeometry(geometry, samples, mapping, parameters);
        }
        catch { geometry.Dispose(); mapping?.Dispose(); throw; }
    }
    static Box ExtrusionBox(Extrusion extrusion)
    {
        // UV projection follows the physical profile, independently of the timber/guide frame.
        var plane = extrusion.GetPathPlane(0); using var profile = extrusion.Profile3d(0, .5);
        var parameters = profile.DivideByCount(128, true); var points = parameters.Select(t => { plane.RemapToPlaneSpace(profile.PointAt(t), out var p); return p; }).ToArray();
        double mx = points.Average(p => p.X), my = points.Average(p => p.Y), xx = 0, xy = 0, yy = 0;
        foreach (var p in points) { double x = p.X - mx, y = p.Y - my; xx += x * x; xy += x * y; yy += y * y; }
        double angle = .5 * Math.Atan2(2 * xy, xx - yy); var axis = plane.XAxis * Math.Cos(angle) + plane.YAxis * Math.Sin(angle); axis.Unitize();
        var frame = new Plane(plane.Origin, axis, Vector3d.CrossProduct(plane.ZAxis, axis));
        return new Box(frame, extrusion.GetBoundingBox(frame));
    }
    static bool MeshCharts(Mesh mesh, double unitToMm, double paddingMm, CancellationToken ct, double gridAngle = Math.PI / 180)
    {
        int count = mesh.Vertices.Count; var used = new bool[count]; foreach (var face in mesh.Faces) foreach (int id in face.IsQuad ? new[] { face.A, face.B, face.C, face.D } : new[] { face.A, face.B, face.C }) used[id] = true; var roots = Enumerable.Range(0, count).ToArray();
        int Root(int i) { while (roots[i] != i) { roots[i] = roots[roots[i]]; i = roots[i]; } return i; }
        foreach (var f in mesh.Faces) { int root = Root(f.A); foreach (int i in f.IsQuad ? new[] { f.B, f.C, f.D } : new[] { f.B, f.C }) roots[Root(i)] = root; }
        var charts = new List<(int[] Indices, Point2d[] UV, double Width, double Height)>();
        foreach (var group in Enumerable.Range(0, count).GroupBy(Root))
        {
            ct.ThrowIfCancellationRequested(); var indices = group.Where(i => used[i]).ToArray(); if (indices.Length == 0) continue; var local = indices.Select((id, i) => (id, i)).ToDictionary(t => t.id, t => t.i); using var piece = new Mesh();
            foreach (int i in indices) piece.Vertices.Add(mesh.Vertices.Point3dAt(i));
            foreach (var f in mesh.Faces) if (local.ContainsKey(f.A)) { if (f.IsQuad) piece.Faces.AddFace(local[f.A], local[f.B], local[f.C], local[f.D]); else piece.Faces.AddFace(local[f.A], local[f.B], local[f.C]); }
            piece.Faces.ConvertQuadsToTriangles(); piece.Normals.ComputeNormals(); var points = piece.Vertices.ToPoint3dArray(); Plane.FitPlaneToPoints(points, out var plane);
            Point2d[] uv;
            if (points.All(p => Math.Abs(plane.DistanceTo(p)) * unitToMm < .000001)) uv = points.Select(p => { plane.RemapToPlaneSpace(p, out var q); return new Point2d(q.X, q.Y); }).ToArray();
            else
            {
                using var unwrap = new MeshUnwrapper(piece); if (!unwrap.Unwrap(MeshUnwrapMethod.LSCM) || piece.TextureCoordinates.Count != indices.Length) return false;
                double worldArea = 0, uvArea = 0;
                foreach (var f in piece.Faces) { var a = piece.Vertices.Point3dAt(f.A); var b = piece.Vertices.Point3dAt(f.B); var c = piece.Vertices.Point3dAt(f.C); worldArea += Vector3d.CrossProduct(b - a, c - a).Length / 2; var u = piece.TextureCoordinates[f.A]; var v = piece.TextureCoordinates[f.B]; var w = piece.TextureCoordinates[f.C]; uvArea += Math.Abs((v.X - u.X) * (w.Y - u.Y) - (v.Y - u.Y) * (w.X - u.X)) / 2; }
                if (!(uvArea > 0)) return false; double scale = Math.Sqrt(worldArea / uvArea); uv = Enumerable.Range(0, indices.Length).Select(i => new Point2d(piece.TextureCoordinates[i].X * scale, piece.TextureCoordinates[i].Y * scale)).ToArray();
            }
            double minU = uv.Min(p => p.X), minV = uv.Min(p => p.Y), width = uv.Max(p => p.X) - minU, height = uv.Max(p => p.Y) - minV;
            if (width < height) { uv = uv.Select(p => new Point2d(p.Y - minV, p.X - minU)).ToArray(); (width, height) = (height, width); } else uv = uv.Select(p => new Point2d(p.X - minU, p.Y - minV)).ToArray();
            charts.Add((indices, uv, width, height));
        }
        double pad = paddingMm / unitToMm;
        double side = Math.Max(charts.Max(c => c.Width) + pad, Math.Sqrt(charts.Sum(c => (c.Width + pad) * (c.Height + pad))) * 1.5); var result = new Point2f[count];
        double x = pad / 2, y = pad / 2, row = 0;
        foreach (var chart in charts.OrderByDescending(c => c.Height))
        {
            if (x + chart.Width + pad / 2 > side) { x = pad / 2; y += row + pad; row = 0; }
            if (y + chart.Height + pad / 2 > side) side = y + chart.Height + pad / 2;
            for (int i = 0; i < chart.Indices.Length; i++) result[chart.Indices[i]] = new Point2f((float)(x + chart.UV[i].X), (float)(y + chart.UV[i].Y));
            x += chart.Width + pad; row = Math.Max(row, chart.Height);
        }
        mesh.TextureCoordinates.Clear(); foreach (var uv in result) mesh.TextureCoordinates.Add(uv.X / side, uv.Y / side);
        return mesh.SetSurfaceParametersFromTextureCoordinates();
    }
    public void Dispose() { Geometry.Dispose(); Samples.Dispose(); Mapping.Dispose(); }
}
