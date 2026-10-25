# Natural materials and engineered bamboo — research notes

Starting visual presets (not species-wide specifications): bamboo diameter **80 mm**, wall **7 mm**, node spacing **270 mm**; log diameter **200 mm**, optional bark **5 mm**. Straight lines define base-to-tip. Seed is the root specimen identity; Natural Variation is a normalized control with moderate default. Variation zero keeps nominal anatomy, taper and nodes without optional irregularity.

## Natural bamboo (Moso reference)

Geometry and appearance share the same resolved node positions: hollow internodes, culm walls, external node ridges and internal diaphragms. Material boundaries close with annular end rims; the hollow interior stays empty.

Anatomy grounding (vascular bundles, node-region fibre changes):

- Speichinger et al., KIT publications: https://publikationen.bibliothek.kit.edu/1000193323/181346290
- Tsuyama et al., PLOS ONE: https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0290732

Node colour treatment and decorative ridge emphasis are **independent appearance adaptations**, not validated reproductions of measured culms.

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

Log bark is the outer material region (default 5 mm) and fresh cuts reveal timber. Moso bamboo uses hollow internodes, annular end rims, external ridges, internal diaphragms, graded vascular bundles and node fibre changes. Node positions and colour are shared between geometry and appearance and are appearance adaptations of the cited research. See stock-architecture.md.
