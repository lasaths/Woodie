using Rhino.Geometry;

namespace Woodie.Stock;

/// <summary>
/// Construction-ready culm presets for Bamboo Stock. Diameters are trade ranges in mm, not species-wide
/// specifications. Skin colours are cured/dried pole tones (not living green). Provenance notes sit next to each preset.
/// </summary>
public enum BambooSpeciesId { Moso = 0, Guadua = 1, Asper = 2 }

public readonly struct BambooSpeciesPreset
{
    public BambooSpeciesId Id { get; init; }
    public string Name { get; init; }
    public string[] Aliases { get; init; }
    /// <summary>Lower bound of typical base diameter (mm).</summary>
    public double DiameterMin { get; init; }
    /// <summary>Upper bound of typical base diameter (mm).</summary>
    public double DiameterMax { get; init; }
    /// <summary>Mid-point of the diameter range; used as the modelled base diameter.</summary>
    public double Diameter => 0.5 * (DiameterMin + DiameterMax);
    public double Wall { get; init; }
    /// <summary>Mean internode length (mm) used to choose segment count for a stock length.</summary>
    public double Spacing { get; init; }
    public double Taper { get; init; }
    public double[] Skin { get; init; }   // cured exterior RGB
    /// <summary>Typical mature full-culm internode count for RIN windowing (Yang and Tang 2021; Tan et al. 2024).</summary>
    public int CulmInternodes { get; init; }
    /// <summary>Relative internode number of the longest internode on a mature culm (~0.47–0.52 for moso).</summary>
    public double PeakRin { get; init; }
    /// <summary>Grosser &amp; Liese free fibre strands outside the central sheath: 0 = Type I, 1 = Type III, 2 = Type IV.</summary>
    public int FreeFibreStrands { get; init; }
}

public static class BambooSpecies
{
    // shortcut: three structural trade species only; upgrade PeakRin / CulmInternodes from measured tables when calibrated.
    public static readonly BambooSpeciesPreset[] All =
    {
        // Moso — Phyllostachys edulis; European structural poles ~Ø70–80 / wall 7 (ISO 22156 case studies).
        // Type I (Phyllostachys): four sheaths, no free fibre strands (Grosser & Liese 1971).
        new() { Id = BambooSpeciesId.Moso, Name = "Moso", Aliases = new[] { "bamboo", "moso", "phyllostachys edulis", "p. edulis" },
            DiameterMin = 70, DiameterMax = 80, Wall = 7, Spacing = 270, Taper = 5, Skin = new[] { 204.0, 168, 98 }, CulmInternodes = 60, PeakRin = 0.48, FreeFibreStrands = 0 },
        // Guadua — Type III (one isolated fibre strand); BioResources mature culms ~Ø105–148.
        new() { Id = BambooSpeciesId.Guadua, Name = "Guadua", Aliases = new[] { "guadua", "guadua angustifolia", "g. angustifolia" },
            DiameterMin = 105, DiameterMax = 148, Wall = 12, Spacing = 310, Taper = 8, Skin = new[] { 188.0, 158, 108 }, CulmInternodes = 55, PeakRin = 0.50, FreeFibreStrands = 1 },
        // Dendrocalamus asper — Type IV (two isolated fibre strands).
        new() { Id = BambooSpeciesId.Asper, Name = "Asper", Aliases = new[] { "asper", "dendrocalamus", "dendrocalamus asper", "d. asper", "betung" },
            DiameterMin = 80, DiameterMax = 200, Wall = 15, Spacing = 450, Taper = 10, Skin = new[] { 210.0, 176, 118 }, CulmInternodes = 50, PeakRin = 0.48, FreeFibreStrands = 2 },
    };

    public static BambooSpeciesPreset Default => All[0];

    public static BambooSpeciesPreset For(BambooSpeciesId id) =>
        All.FirstOrDefault(p => p.Id == id) is { Name: not null } p ? p : Default;

    public static bool TryParse(string text, out BambooSpeciesPreset preset)
    {
        preset = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var key = text.Trim().ToLowerInvariant();
        // Drop a trailing " bamboo" so panel text like "Guadua bamboo" still matches.
        if (key.EndsWith(" bamboo", StringComparison.Ordinal)) key = key[..^" bamboo".Length].Trim();
        foreach (var p in All)
            if (p.Aliases.Any(a => a == key) || string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase))
            { preset = p; return true; }
        return false;
    }

    public static string KindHelp => "Set Species to Moso, Guadua or Asper.";
    /// <summary>Menu label: name plus diameter range (recognition, not colour alone).</summary>
    public static string MenuLabel(BambooSpeciesPreset p) =>
        FormattableString.Invariant($"{p.Name} · Ø{p.DiameterMin:0}–{p.DiameterMax:0} · wall {p.Wall:0}");
    public static string Summary(BambooSpeciesPreset p) =>
        FormattableString.Invariant($"{p.Name} · cured · Ø{p.DiameterMin:0}–{p.DiameterMax:0} · wall {p.Wall:0} · nodes {p.Spacing:0}");

    /// <summary>
    /// Modelled base diameter in mm: mid-point of an explicit Domain, otherwise the species trade range.
    /// A zero-width Domain sets an exact pole diameter.
    /// </summary>
    public static double ResolveDiameter(BambooSpeciesPreset species, Interval? domain)
    {
        double min = species.DiameterMin, max = species.DiameterMax;
        if (domain is { } d) {
            min = d.T0; max = d.T1;
            if (min > max) (min, max) = (max, min);
        }
        if (!(min > 0) || !(max > 0) || !double.IsFinite(min + max))
            throw new ArgumentException("Diameter must be a finite positive range in millimetres.");
        return 0.5 * (min + max);
    }
}
