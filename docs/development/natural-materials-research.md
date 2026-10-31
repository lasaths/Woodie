# Natural materials and engineered bamboo — research notes

Starting visual presets (not species-wide specifications): log diameter **200 mm**, optional bark **5 mm**. Straight lines define base-to-tip. Seed is the root specimen identity; Natural Variation is a normalized control (default 0.8) for specimen spread. Variation zero keeps nominal anatomy, taper and nodes without optional irregularity.

## Natural bamboo species library

**Bamboo Stock** `Species` accepts one of three construction trade species. All share the same hollow-culm generator; presets only change starting diameter, wall, node spacing, taper and **cured** skin colour (dried poles, not living green). Explicit Diameter/Wall/Node Spacing/Taper inputs still override the preset. **Log Stock** is a separate component for softwood stems. Engineered bamboo strips still come from an internal Moso source culm.

| Species | Botanical | Ø mm | Wall mm | Node mm | Notes |
|---------|-----------|------|---------|---------|-------|
| Moso (default; `Bamboo` alias) | *Phyllostachys edulis* | 80 | 7 | 270 | European structural poles |
| Guadua | *Guadua angustifolia* | 120 | 12 | 310 | Latin American construction |
| Asper / Betung | *Dendrocalamus asper* | 140 | 15 | 450 | Large SE Asian structural culms |

Geometry and appearance share the same resolved node positions: hollow internodes, culm walls, external node ridges and internal diaphragms. Material boundaries close with annular end rims; the hollow interior stays empty.

**Node spacing is not equal.** Internode lengths follow a size-independent RIN–RCIL logistic (Inoue et al. 2012/2017; Tan et al. 2024 logistic family): `Node Spacing` sets the mean segment count for the stock length; species `CulmInternodes` / `PeakRin` window the stock onto a mature culm curve. Variation 0 uses a basal window (typical structural pole: short internodes near the cut base, lengthening toward mid-culm). Variation above 0 picks a seeded window along the culm and applies soft multiplicative jitter (bounded under the ~12% among-culm IL CV from Li et al. 2025). See `docs/development/bamboo-culm-segment-distribution.md` when present on the branch.

Anatomy grounding (vascular bundles, node-region fibre changes):

- Speichinger et al., KIT publications: https://publikationen.bibliothek.kit.edu/1000193323/181346290
- Tsuyama et al., PLOS ONE: https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0290732
- Tan et al., 2024, Frontiers in Ecology and Evolution: https://doi.org/10.3389/fevo.2024.1440494

Node colour treatment, decorative ridge emphasis and cured skin colours are **independent appearance adaptations**, not validated reproductions of measured culms.

## Engineered bamboo

Process chain: culm → longitudinal split → machined strip → orientation and adhesive → finished stock. Machining removes source material; it does not regenerate unrelated grain. Preserve each strip’s source region, fibre orientation and retained node structure.

Supported constructions (initial):

- Plain-pressed and side-pressed laminated lumber
- Layered bamboo panels (including cross-oriented cores) via existing Layer/Buildup

Representative recipes follow the laminated-lumber size-effect study and manufacturer constructions; provenance is recorded next to each recipe:

- Lorenzo et al., size effect on compressive strength of laminated bamboo lumber: https://discovery.ucl.ac.uk/10170654/3/Lorenzo_Size%20effect%20on%20the%20compressive%20strength%20of%20laminated%20bamboo%20lumber.pdf

Deferred: scrimber, flattened-culm products, explicit user-authored manufacturing sequences.

## Randomness

Resolve features once per specimen. Use separate deterministic streams for geometry and appearance. Correlate shape changes smoothly along the specimen. Pieces from the same source stay correlated; independent sources vary independently. Changing resolution, remeshing or moving a cut must not reshuffle features.

## Implementation note: natural stock

Log bark is the outer material region (default 5 mm) and fresh cuts reveal timber. Bamboo species use hollow internodes, annular end rims, external ridges, internal diaphragms, graded vascular bundles and node fibre changes. Node positions and colour are shared between geometry and appearance and are appearance adaptations of the cited research. See stock-architecture.md.
