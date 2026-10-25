using System.Drawing;
using Rhino;
using Rhino.DocObjects;
using Rhino.Render;
namespace Woodie.Baking;

internal static class TimberBaker
{
    internal static List<Guid> Commit(RhinoDoc doc, IReadOnlyList<NativeBakePlan> plans, ObjectAttributes attributes, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (plans.Any(p => string.IsNullOrEmpty(p.TexturePath) || !File.Exists(p.TexturePath))) throw new InvalidOperationException("Full-quality bake textures have not finished.");
        int materialStart = doc.Materials.Count;
        var objects = new List<Guid>(); var materials = new List<RenderMaterial>(); var bitmaps = new List<string>(); uint undo = doc.BeginUndoRecord("Bake textured timber");
        try
        {
            foreach (var plan in plans)
            {
                ct.ThrowIfCancellationRequested();
                string name = "Woodie " + NativeBakePlan.Version + " " + plan.ImageHash;
                var renderMaterial = doc.RenderMaterials.FirstOrDefault(m => m.Name == name);
                if (renderMaterial == null)
                {
                    using var material = new Material { Name = name, DiffuseColor = Color.White, SpecularColor = Color.Black, Shine = .05 };
                    using var texture = new Texture { FileName = plan.TexturePath, Enabled = true };
                    if (!material.SetBitmapTexture(texture)) throw new InvalidOperationException("Rhino could not assign the timber texture.");
                    renderMaterial = RenderMaterial.CreateBasicMaterial(material, doc);
                    if (renderMaterial == null || !doc.RenderMaterials.Add(renderMaterial)) throw new InvalidOperationException("Rhino could not create the timber material.");
                    renderMaterial.Name = name; materials.Add(renderMaterial);
                }
                // Rhino can report bitmap names without paths; register/reuse by immutable image content.
                int registration = doc.Bitmaps.AddBitmap(plan.TexturePath, false);
                if (registration < 0)
                {
                    if (doc.Bitmaps.AddBitmap(plan.TexturePath, true) < 0) throw new InvalidOperationException("Rhino could not register the texture for embedding.");
                }
                else bitmaps.Add(plan.TexturePath);
                var att = attributes?.Duplicate() ?? new ObjectAttributes { LayerIndex = doc.Layers.CurrentLayerIndex };
                att.MaterialSource = ObjectMaterialSource.MaterialFromObject;
                att.SetUserString("WoodieTextureHash", plan.ImageHash); att.SetUserString("WoodieBakeVersion", NativeBakePlan.Version);
                if (string.IsNullOrEmpty(att.Name)) att.Name = "Woodie " + plan.Label;
                var id = doc.Objects.Add(plan.Geometry, att); if (id == Guid.Empty) throw new InvalidOperationException("Rhino could not add the native timber geometry to the selected layer."); objects.Add(id);
                var obj = doc.Objects.FindId(id);
                if (obj.SetTextureMapping(1, plan.Mapping) == 0) throw new InvalidOperationException("Rhino could not assign the native timber mapping.");
                if (plan.Meshing != null && !obj.SetRenderMeshParameters(plan.Meshing)) throw new InvalidOperationException("Rhino could not retain native UV render settings.");
                // Reuse the RDK material itself; Rhino may create per-object legacy simulation rows.
                if (renderMaterial == null || !doc.Objects.ModifyRenderMaterial(obj, renderMaterial)) throw new InvalidOperationException("Rhino could not retain the shared timber material.");
            }
            ct.ThrowIfCancellationRequested(); return objects;
        }
        catch
        {
            foreach (var id in objects.AsEnumerable().Reverse()) doc.Objects.Delete(id, true);
            foreach (var material in materials.AsEnumerable().Reverse()) doc.RenderMaterials.Remove(material);
            // RDK assignment also appends legacy display-material simulations.
            for (int index = doc.Materials.Count - 1; index >= materialStart; index--) doc.Materials.DeleteAt(index);
            foreach (string bitmap in bitmaps) doc.Bitmaps.DeleteBitmap(bitmap);
            throw;
        }
        finally { if (undo != 0) doc.EndUndoRecord(undo); doc.Views.Redraw(); }
    }
}
