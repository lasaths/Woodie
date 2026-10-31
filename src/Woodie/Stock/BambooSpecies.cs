namespace Woodie.Stock;

/// <summary>
/// Construction-ready culm presets for Bamboo Stock. Dimensions are mid-culm starting points, not species-wide
/// specifications. Skin colours are cured/dried pole tones (not living green). Provenance notes sit next to each preset.
/// </summary>
public enum BambooSpeciesId { Moso = 0, Guadua = 1, Asper = 2 }

public readonly struct BambooSpeciesPreset
{
    public BambooSpeciesId Id { get; init; }
    public string Name { get; init; }
    public string[] Aliases { get; init; }
    public double Diameter { get; init; }
    public double Wall { get; init; }
    /// <summary>Mean internode length (mm) used to choose segment count for a stock length.</summary>
    public double Spacing { get; init; }
    public double Taper { get; init; }
    public double[] Skin { get; init; }   // cured exterior RGB
    /// <summary>Typical mature full-culm internode count for RIN windowing (Yang and Tang 2021; Tan et al. 2024).</summary>
    public int CulmInternodes { get; init; }
    /// <summary>Relative internode number of the longest internode on a mature culm (~0.47–0.52 for moso).</summary>
    public double PeakRin { get; init; }
}

public static class BambooSpecies
{
    // shortcut: three structural trade species only; upgrade PeakRin / CulmInternodes from measured tables when calibrated.
    public static readonly BambooSpeciesPreset[] All =
    {
        // Moso — Phyllostachys edulis; European structural poles ~Ø70–80 / wall 7 (ISO 22156 case studies).
        new() { Id = BambooSpeciesId.Moso, Name = "Moso", Aliases = new[] { "bamboo", "moso", "phyllostachys edulis", "p. edulis" },
            Diameter = 80, Wall = 7, Spacing = 270, Taper = 5, Skin = new[] { 176.0, 148, 92 }, CulmInternodes = 60, PeakRin = 0.48 },
        // Guadua — Latin American construction; BioResources mature culms ~Ø105–148, wall ~12–22, internodes ~310 mm.
        new() { Id = BambooSpeciesId.Guadua, Name = "Guadua", Aliases = new[] { "guadua", "guadua angustifolia", "g. angustifolia" },
            Diameter = 120, Wall = 12, Spacing = 310, Taper = 8, Skin = new[] { 158.0, 138, 100 }, CulmInternodes = 55, PeakRin = 0.50 },
        // Dendrocalamus asper (Betung) — SE Asian structural culms Ø80–200, wall 11–20, internodes 400–500 mm.
        new() { Id = BambooSpeciesId.Asper, Name = "Asper", Aliases = new[] { "asper", "dendrocalamus", "dendrocalamus asper", "d. asper", "betung" },
            Diameter = 140, Wall = 15, Spacing = 450, Taper = 10, Skin = new[] { 188.0, 162, 108 }, CulmInternodes = 50, PeakRin = 0.48 },
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
    /// <summary>Menu label: name plus starting dimensions (recognition, not colour alone).</summary>
    public static string MenuLabel(BambooSpeciesPreset p) =>
        FormattableString.Invariant($"{p.Name} · Ø{p.Diameter:0} · wall {p.Wall:0}");
    public static string Summary(BambooSpeciesPreset p) =>
        FormattableString.Invariant($"{p.Name} · cured · Ø{p.Diameter:0} · wall {p.Wall:0} · nodes {p.Spacing:0}");
}
