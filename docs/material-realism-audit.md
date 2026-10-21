# Material realism audit

Checked 21 October 2026 against the current source and primary product references. This is a construction and appearance-model audit, not a new viewport acceptance run. Existing geometry/pixel checks do not establish a match to photographed timber. No renderer settings, installed GHA or golden fixtures were changed for this audit.

The eight presets distinguish their basic constructions, but they cannot all be described as matching real products across thicknesses and curvature. The strongest matches are the default 27 mm three-layer panel and 45 mm Kerto-Q layups. Species, surface grade, finish and lighting remain unspecified; Natural and Clean/Pale are procedural looks.

| Preset | Current construction | Assessment |
| --- | --- | --- |
| Solid timber | One continuous section, no manufactured joints | Appropriate basic construction. Shared softwood grain is a generic appearance; arbitrary bowed geometry does not establish that the section can be manufactured by bending. |
| KVH | One section with finger joints | Represents the finger-jointed KVH variant. KVH also exists without finger joints; the preset does not cover both. |
| Glulam | Parallel finger-jointed laminations, fitted to depth with a 45 mm ceiling | Plausible straight construction. Curvature does not select thinner laminations, so bowed examples are not representative manufacturing models. |
| CLT | Seven layers, L,L,C,L,C,L,L; 30/30/30/20/30/30/30 mm at 200 mm | A real double-outer-layer construction, not an orientation bug. Scaling this same seven-layer recipe to every depth does not reproduce the manufacturer's thickness-dependent range. |
| Three-layer panel / 3S | L,C,L; 9/9/9 mm at 27 mm | Valid default. Thin panels use equal thirds, which differ from the cited NOVATOP products. |
| Plywood | Odd count of alternating continuous veneers, nominal 1.5 mm | Correct generic orientation. Thickness is birch-like while palette/grain are shared with softwood LVL; no particular plywood species/grade is reproduced. Veneers are fitted to total depth, rather than enforcing exact nominal thickness. |
| LVL — parallel | Longitudinal continuous veneers, nominal 3 mm | Correct basic distinction from plywood and Kerto-Q. Idealized continuous sheets omit factory veneer joints; colour is not separately calibrated to a named product. |
| Kerto-Q | Fixed 15-veneer sequence, three cross veneers | Matches the referenced 45 mm Q-panel sequence. At other depths it stretches these same 15 veneers instead of selecting the corresponding real layup. |

## Concrete discrepancies

1. **Kerto-Q thickness selection:** `src/Woodie/Appearance/TimberLayups.cs:38` hard-codes the 45 mm sequence, then lines 43–44 scale it. A 27 mm part becomes fifteen 1.8 mm veneers; Metsä's Q-panel table specifies nine veneers. At 75 mm the renderer produces fifteen 5 mm veneers instead of twenty-five veneers and the corresponding cross-layer positions. Select documented layups by thickness, with an explicit generic fallback for unsupported depths. [Metsä Kerto LVL manual, Table 7, printed page 5](https://www.metsagroup.com/globalassets/metsa-wood/attachments/kerto-lvl-manual/en/metsa_kertolvl_mechanical_properties.pdf).

2. **Thin 3S layer ratios:** `TimberLayups.cs:32` uses equal thirds below 27 mm. For example, 21 mm becomes 7/7/7; NOVATOP lists 6/9/6. Its 19 mm panel is 6/7/6, whereas Woodie uses equal thirds. The default 27 mm 9/9/9 recipe is valid, as are several thicker recipes using 9 mm faces. Use product tables if retaining this manufacturer reference. [NOVATOP SWP thickness table](https://novatop-system.com/product/swp/).

3. **Curved glulam lamination sizing:** `TimberLayups.cs:34` receives no curvature information. The 120 mm example therefore has three 40 mm laminations in both straight and bowed forms. Swedish Wood distinguishes straight 45 mm laminations from generally thinner, approximately 33 mm laminations for curved products. Radius and fabrication method need to inform a representative curved layup; 33 mm is not a universal solution for every bend. The current guide mapper establishes grain continuity, not manufacturing feasibility. [Swedish Wood, About glulam](https://www.swedishwood.com/building-with-wood/about-glulam/).

4. **Plywood identity and appearance:** `TimberLayups.cs:21` defaults to 1.5 mm veneers, while `src/Woodie/Appearance/TimberPalette.cs:26` shares the Kerto-Q palette with plywood. WISA lists 1.5 mm birch veneers and thicker spruce veneers (2.0, 2.6 and 3.2 mm). The present combination is a generic visual approximation. To claim a particular real appearance, choose a reference species/grade and calibrate the face grain, colour and exposed bond lines together. [WISA veneer information](https://www.wisaplywood.com/specifying-wisa/about-veneer/).

5. **CLT thickness selection:** `TimberLayups.cs:29` preserves one 200 mm recipe at all depths. HASSLACHER documents this 200 mm seven-layer option and defines its double top layers, but also lists different layer counts and thickness recipes. Preserve the valid default while selecting representative recipes for other section depths. [HASSLACHER CLT brochure, printed pages 8 and 14](https://www.hasslacher.com/data/_dateimanager/broschuere/HNT-Brettsperrholz-EN.pdf).

## Other reference checks

- KVH can be finger-jointed or non-finger-jointed. Woodie's jointed variant is valid; joint contrast should remain restrained rather than implying every joint is a dark seam. [KVH technical brochure](https://www.kvh.eu/fileadmin/downloads/new/international/KVH_KVH-Duo-Triobalken_2020-03_GB_print_200506.pdf), [Stora Enso KVH](https://www.storaenso.com/en/products/mass-timber-construction/building-products/kvh).
- Metsä describes 3 mm peeled softwood veneers, with all veneers parallel for S-beam/T-stud. This supports the basic parallel-LVL construction, without making Woodie's generic continuous-sheet preset a reproduction of those products. [Metsä Kerto LVL environmental declaration, product information](https://www.metsagroup.com/contentassets/f8ff384a208d4786959efa4bca9fab2c/epd-document_kerto-lvl_epd-ies-0026805.pdf).
- Glue colour and visibility depend on adhesive/product. For example, HASSLACHER specifies a white glue line for its cited CLT. A single generic seam-darkening treatment is not enough to establish product-specific edge realism. The current plywood/LVL dark bond lines also require close photographic comparison before calling their width realistic.
- The cut-aware appearance work documented in [appearance.md](appearance.md) uses a qualitative Hinoki reference, explicitly without fitting a species model. That supports checking grain behaviour; it does not validate spruce, birch or all engineered timber products photographically.

## Remaining visual acceptance

After the construction discrepancies are addressed, compare each product with a named species, grade and adhesive reference using equally scaled longitudinal, end-grain and exposed-layer views. Review both appearances and straight/bowed pairs. Keep geometry, UV, seed and cache checks separate from this visual judgement. A bowed sheet preview must also be distinguished from a prediction of moisture-driven deformation or a fabrication specification.

This audit does not certify the installed Rhino preview or Mac display. No Rhino restart, installation or package publication was performed.
