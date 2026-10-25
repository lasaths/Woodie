namespace Woodie.Appearance;

// Existing archive identifiers are permanent. Append new products only.
public enum TimberProduct { CLT = 0, ThreeS = 1, GL = 2, KertoQ = 3, KVH = 4, SolidTimber = 5, Plywood = 6, ParallelLVL = 7, Log = 8, Bamboo = 9,
    BambooLumber = 10, BambooLumberSide = 11, BambooPanel = 12 }
public enum TimberLook { Natural = 0, CleanPale = 1 }

public static class TimberProducts
{
    public static readonly TimberProduct[] MenuOrder = { TimberProduct.SolidTimber, TimberProduct.KVH, TimberProduct.GL,
        TimberProduct.CLT, TimberProduct.ThreeS, TimberProduct.Plywood, TimberProduct.ParallelLVL, TimberProduct.KertoQ };
    // Engineered bamboo (split-and-machined strips, see BambooConstruction) stays out of MenuOrder like Log/Bamboo, so the eight-product fixtures are unchanged.
    public static readonly TimberProduct[] BambooOrder = { TimberProduct.BambooLumber, TimberProduct.BambooLumberSide, TimberProduct.BambooPanel };
    public static string Label(this TimberProduct product) => product switch {
        TimberProduct.SolidTimber => "Solid timber", TimberProduct.GL => "Glulam", TimberProduct.ThreeS => "Three-layer panel",
        TimberProduct.ParallelLVL => "LVL — parallel", TimberProduct.KertoQ => "Kerto-Q",
        TimberProduct.BambooLumber => "Bamboo lumber — plain-pressed", TimberProduct.BambooLumberSide => "Bamboo lumber — side-pressed",
        TimberProduct.BambooPanel => "Layered bamboo panel", _ => product.ToString() };
    public static bool TryParse(string text, out TimberProduct product)
    {
        text = text?.Trim();
        foreach (var value in MenuOrder.Concat(BambooOrder))
            if (string.Equals(text, value.Label(), StringComparison.OrdinalIgnoreCase) || string.Equals(text, value.ToString(), StringComparison.OrdinalIgnoreCase))
            { product = value; return true; }
        // Legacy Settings accepted enum values supplied as numeric text.
        return Enum.TryParse(text, true, out product) && Enum.IsDefined(typeof(TimberProduct), product);
    }
}
