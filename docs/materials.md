# Timber materials

Woodie exposes eight products through Timber Material under Display → Woodie. These are procedural visual constructions; they do not specify or certify structural products.

| Menu label | Identifier / archive value | Construction | Virtual depth |
| --- | --- | --- | --- |
| Solid timber | SolidTimber / 5 | One continuous solid section; no manufactured joints | 120 mm |
| KVH | KVH / 4 | One solid section with finger joints | 120 mm |
| Glulam | GL / 2 | Parallel lamellas ≤45 mm, fitted to section | 120 mm |
| CLT | CLT / 0 | Representative seven-layer board layup L,L,C,L,C,L,L | 200 mm |
| Three-layer panel | ThreeS / 1 | Longitudinal face layers and cross-grain core | 27 mm |
| Plywood | Plywood / 6 | Odd, alternating continuous veneers; nominal 1.5 mm | 18 mm |
| LVL — parallel | ParallelLVL / 7 | All continuous veneers longitudinal; nominal 3 mm | 45 mm |
| Kerto-Q | KertoQ / 3 | 45 mm reference 15-veneer cross-banded/scarfed layup | 45 mm |

The first five archive values are unchanged. Open surfaces/meshes use virtual depth only for appearance; their input and output geometry stay open. Real solids use their measured physical depth.

Plywood uses the closest odd layer count, at least three; all veneers are fitted evenly to the depth. Parallel LVL uses the nearest positive layer count. Veneer Thickness changes the nominal thickness for these generic veneer products, not Kerto-Q. Generic sheets are continuous over the part, so there are no artificial scarf/clip seams.

## Reference constructions

[Metsä Wood's Kerto LVL environmental declaration](https://www.metsagroup.com/contentassets/f8ff384a208d4786959efa4bca9fab2c/epd-document_kerto-lvl_epd-ies-0026805.pdf) distinguishes products with parallel veneers from products containing cross veneers. [Kerto-Q product guidance](https://www.metsagroup.com/metsawood/products-and-services/products/kerto-lvl/q-panel/) describes its cross-banded construction. [WISA Birch Premium](https://www.wisaplywood.com/siteassets/documents/brochures/wisa-birch-premium-en-fs.pdf) describes cross-bonded plywood with continuous face veneers. Woodie's generic veneer dimensions are display defaults rather than a reproduction of a particular plywood specification.

The procedural grain and knot appearance is inspired by Maria Larsson et al., [Procedural Texturing of Solid Wood with Knots](https://doi.org/10.1145/3528223.3530081), ACM TOG 41(4), Article 45 (SIGGRAPH 2022). MIT notices are retained in THIRD-PARTY-NOTICES.md.

## Appearance and overrides

Natural retains the original softwood-oriented visual defaults. Clean/Pale sets earlywood colours (223,213,181) and (234,224,198), latewood (194,181,151), knot colour (130,110,80), and density 0.25 knots/metre. It does not change layer construction or lamella width. Explicit inputs override either look. Resolution is always measured in mm/pixel regardless of Rhino document units.

See [validation](validation.md) for the current pass/fail report. Numerical tests and actual viewport inspection are recorded separately; Mac acceptance remains pending.

## Known limitations

The 200 mm CLT and 45 mm Kerto-Q recipes are scaled to other depths instead of selecting manufacturer-specific thickness tables. Thin ThreeS panels use simplified ratios; the default 27 mm 9/9/9 layup is valid. Curved glulam does not select thinner laminations from bend radius. Plywood shares a softwood LVL palette rather than a species-specific birch/spruce appearance. These are documented limitations of this release, not claims of product accuracy. See the [source-backed material audit](material-realism-audit.md).
