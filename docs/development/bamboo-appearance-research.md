# Bamboo culm appearance — volumetric / procedural research

Date: 2026-11-01. Status: investigation note; recommended shader experiment applied in `BambooMaterialModel` (cellular + ExpDec1 occupancy). Primary sources reviewed for graphics, anatomy, and datasets usable by Woodie’s point-wise `Sample`.

## Scope

Woodie’s softwood path follows Larsson et al. (2022) *Procedural Texturing of Solid Wood with Knots* (growth-time field + knots); later ring-inference / Mokume work is summarised in [`docs/larsson-later-work.md`](../larsson-later-work.md). Bamboo stock uses a separate monocot model in `BambooMaterialModel` (`src/Woodie/Stock/NaturalMaterial.cs`): Perlin-threshold “bundles”, a density ramp outer→inner, species skin RGB, and soft node banding. Contact-sheet inspection shows that end-grain does **not** match real culm radials (scattered, size-graded vascular bundles in parenchyma).

This note asks: what **primary** research (graphics + anatomy/materials) exists that can improve procedural bamboo appearance—radial vascular-bundle field, skin, nodes, longitudinal fibre—under Woodie’s runtime constraint of cheap point-sample evaluation (no heavy neural nets at preview time)?

Related geometry notes: [`bamboo-culm-segment-distribution.md`](bamboo-culm-segment-distribution.md) (internode spacing); [`natural-materials-research.md`](natural-materials-research.md) (species presets).

## Softwood (Larsson path) vs bamboo (monocot) anatomy for shading

| Feature | Softwood / Larsson-style volumetric wood | Bamboo culm wall (monocot) |
| --- | --- | --- |
| Growth model | Secondary thickening; cambium; **annual rings** as isosurfaces of a growth-time field; knots as branch strands merged into the stem field | **No cambium / no annual rings**; primary culm wall fixed at elongation; tissue = **vascular bundles in parenchyma** (Grosser & Liese, 1971; Liese, 1998; Dixon & Gibson, 2014) |
| Dominant radial pattern | Concentric earlywood/latewood | Bundle **number density and fibre volume fraction rise toward the outer wall**; bundles become **smaller** outward; open/semi-open morphology changes with depth (Dixon & Gibson, 2014; Xu et al., 2021; Tsuyama et al., 2023; Shang et al., 2024) |
| “Defects” / discontinuities | Knots distort rings | **Nodes**: external ridge + internal **diaphragm**; vessels **interconnect / bend** (no rays for radial transport in internodes) (Liese preservation anatomy; Shima et al., 2016; Palombini et al., 2019; Peng et al., 2021) |
| Outer surface | Bark (optional) separate from timber | Thin **silicified epidermis** (~15 µm) + cortex (hypodermis / transitional layers) with wax; hard outer shell (Yoshimura et al., 2025; Dixon & Gibson, 2014) |
| Longitudinal look | Ring-cut / quarter-cut grain from the same 3D field | Strongly **axial** fibre streaks; end-grain shows discrete dark flower-shaped bundles; long-grain shows fine parallel streaks without tree rings (Liu et al. wood shaders **explicitly out of scope** for bamboo — see below) |

**Implication for Woodie:** Reusing ring/knot machinery will not produce bamboo. The Larsson analogue for bamboo is a **radially graded discrete-bundle field** (plus thin skin and node cross-links), not a growth-time ring field.

## What exists (works by category)

### (a) Procedural / inverse graphics

| Work | Contribution | Woodie fit |
| --- | --- | --- |
| Larsson et al., 2022 — *Procedural texturing of solid wood with knots* ([SIGGRAPH](https://history.siggraph.org/learning/procedural-texturing-of-solid-wood-with-knots-by-larsson-ijiri-yoshida-huber-fredriksson-et-al/); [project](https://www.ma-la.com/procedural_knots.html)) | GPU-evaluable growth-time field + smooth-min knot merge for pine/spruce/CLT | Reference architecture for **point-wise** evaluation; **not** bamboo anatomy |
| Liu, Dong, Hašan, Marschner, 2016 — *Simulating the structure and texture of solid wood* ([TOG](https://doi.org/10.1145/2980179.2980255); [arXiv](https://ar5iv.labs.arxiv.org/html/1511.04224v2)) | Modular volumetric hardwood/softwood (rings, pores, rays, figure) | Explicitly: *“other woody plants used for lumber (such as bamboo) are entirely different in structure and are not within the intended scope”* |
| Larsson et al., 2024/2025 — ring inference; Mokume ([CGF](https://doi.org/10.1111/cgf.15074); [Mokume](https://mokumeproject.github.io/)) | Inverse / hybrid solid-wood calibration | Useful **offline** calibration pattern for wood; **no bamboo samples** in Mokume |
| Inverse procedural SVBRDF / node-graph pipelines (e.g. Hu et al., 2022 [TOG](https://doi.org/10.1145/3502431); Yale inverse procedural textures) | Fit general noise graphs to photos | Too heavy / generic for preview `Sample`; optional **offline** preset fitting only |
| Luo et al., 2009 — *Bamboo Simulation Based on Improvement Fractal Algorithm…* ([JSJKX](https://www.jsjkx.com/EN/Y2009/V36/I12/285)) | L-system / fractal **plant form** + displacement for node ridges | Geometry/silhouette, **not** culm-wall volumetric texture |
| Prusinkiewicz / cpfg L-systems ([algorithmicbotany](https://algorithmicbotany.org/lstudio/graph.pdf)) | Procedural plants, cylinder textures | Adjacent plant modelling; **no** bamboo mesostructure shader |

**Verdict on (a):** No peer-reviewed Larsson-equivalent paper for volumetric bamboo culm appearance was found (SIGGRAPH/TOG/EG/CGF/arXiv searches for procedural/volumetric/inverse bamboo texture returned wood papers that exclude bamboo, plus plant-form L-system work).

### (b) Quantitative anatomy usable in a point-sample shader

| Work | Contribution | Woodie fit |
| --- | --- | --- |
| Grosser & Liese, 1971 — *On the anatomy of Asian bamboos…* ([WST](https://doi.org/10.1007/BF00365061)) | VB types I–IV across 52 spp.; radial packing pattern | Species presets: *Phyllostachys* ~ type I; tropical *Bambusa*/*Dendrocalamus* often III/IV |
| Liese, 1998 — *The Anatomy of Bamboo Culms* (INBAR TR 18) ([AGRIS](https://agris.fao.org/search/en/records/65dddf387c7033e84be87dd0)) | Standard monocot culm monograph | Ground tissue fractions (~parenchyma / fibres / vessels); no radial rays |
| Dixon & Gibson, 2014 — *Structure and mechanics of Moso bamboo* ([J R Soc Interface](https://doi.org/10.1098/rsif.2014.0321); [PMC](https://pmc.ncbi.nlm.nih.gov/articles/PMC4233722/)) | Measured **V_vb(r/a)** and **S_f(r/a)**; density ~500–650 kg m⁻³ inner → ~900 kg m⁻³ outer examples; parenchyma solid fraction **0.22 ± 0.03** | Best single source for a **density / darkness ramp** keyed to normalised wall depth `t` |
| Xu et al., 2021 — *Intelligent analysis… Part II* fibre VF ([Ind. Crops Prod.](https://doi.org/10.1016/j.indcrop.2021.114164)) | Fibre volume fraction vs radial depth fits **ExpDec1** `y = A e^(−x/t) + y0` across 12 Chinese moso regions (mean R² **0.988**); recommend layer ≤ **0.4 mm** when sampling gradient | Prefer **exponential** occupancy vs Woodie’s near-linear Perlin threshold |
| Xu et al., bioRxiv 2021 — *Phyllostachys* VB characteristics ([bioRxiv](https://doi.org/10.1101/2021.12.01.470805)) | 29 *Phyllostachys* spp.: fibre VF **17.7–32.7%** (mean **25.5 ± 3.5%**); ExpDec1 mean **A ≈ 57.4**, **t ≈ 0.53** (high CV on `t`) | Species-family defaults for Moso-like presets |
| Shang / Forests 2024 — *Gradient variation… Moso VB* ([Forests](https://doi.org/10.3390/f15061023); DOI [10.3390/f15061023](https://doi.org/10.3390/f15061023)) | Bundle **locations** exponential in radial coord; single-bundle area **0.061–0.194 mm²**, fit `A = 0.19 − 8.17 e^(5.27 r̄)` (inner large → outer small); open → semi-open morphology | Drive **feature scale** (noise frequency / blob size) with `t` |
| Tsuyama et al., 2023 — *Quantitative morphological transformation…* ([PLOS ONE](https://doi.org/10.1371/journal.pone.0290732)) | Bundle **area occupation and count** max at epidermis side; **aspect ratio** ~**0.5** near epidermis → **>1** toward pith; VAE simulates radial shape morph | End-grain: outer dots small/dense; inner larger, more elliptical |
| Speichinger et al., 2026 — *Scale-bridging… Moso* ([Constr. Build. Mater.](https://doi.org/10.1016/j.conbuildmat.2026.146522); [KIT PDF](https://publikationen.bibliothek.kit.edu/1000193323/181346290)) | Image-based VB type detection (VB1/2/3 by fibre-cap count); radial descriptors; reconstruction + homogenisation; fibre-bundle area rises then drops beyond **r̃ ≈ 0.8** | Offline calibration + type mix; **not** a runtime neural shader |
| Hakim et al., 2018 — *Bambusa vulgaris* VB morphology ([JKWST](https://doi.org/10.5658/WOOD.2018.46.4.315)) | Outer circular → mid vertical oval → inner horizontal oval; density outer→inner; types III/IV basal | Supports Asper/Guadua-like tropical presets |
| Maulana et al., 2023 — *G. apus* strips ([JKWST](https://www.woodj.org/archive/view_article?pid=wood-51-4-309)) | Internode VB count/mm² outer/mid/inner **8.33 / 4.89 / 4.22**; fibre-sheath % **51.8 / 44.7 / 37.5**; density **0.85 / 0.73 / 0.62 g cm⁻³** | Concrete three-bin ramp for contact-sheet tuning |
| Yoshimura et al., 2025 — silicified epidermis ([Sci. Rep. / PMC](https://pmc.ncbi.nlm.nih.gov/articles/PMC12219570/)) | Epidermal silica layer ~**15 µm** thick | Skin must be a **thin** albedo band at preview scale, not ~14% of wall |
| Cui et al., 2022 — bamboo cortex µCT ([Cellulose](https://doi.org/10.1007/s10570-022-04534-5); preprint [10.21203/rs.3.rs-1131320/v1](https://doi.org/10.21203/rs.3.rs-1131320/v1)) | Cortex layers: epidermis / hypodermis / transitional / parenchyma before VBs | Justifies a short outer colour transition before dense bundles |

### (c) Datasets (CT, photos, micrographs)

| Dataset / paper | Content | Woodie fit |
| --- | --- | --- |
| Speichinger KIT — cross-sectional stripes ([KITopen](https://publikationen.bibliothek.kit.edu/1000190988)) | Radially cropped moso wall micrographs (inner→outer bounds) | **Best offline reference** for end-grain contact sheets |
| Speichinger KIT — YOLO VB detection set ([RADAR](https://radar.kit.edu/radar/en/dataset/fe5eamg8w89bx4ns); DOI [10.35097/fe5eamg8w89bx4ns](https://doi.org/10.35097/fe5eamg8w89bx4ns)) | 62 annotated images, VB-I/II/III | Offline stats / validation; not for runtime |
| du Plessis — *MicroCT of bamboo* ([Mendeley](https://doi.org/10.17632/z8z5w59c7n.1)) | 2 µm isotropic stack (80 kV); companion review [Acta Biomater. 2019](https://doi.org/10.1016/j.actbio.2018.12.014) | 3D fibre continuity check; heavy to ingest |
| Palombini et al., 2019 — node µCT + FEA ([J Mater Res](https://doi.org/10.1557/jmr.2019.117)) | Branching nodal vascular system, 5.6 µm voxels | Node-region appearance: cross-links, not axial-only streaks |
| Peng et al., 2021 — *3D Visualization of Bamboo Node’s Vascular Bundle* ([Forests](https://doi.org/10.3390/f12121799)) | Node conducting tissue connectivity ~**95%**; parenchyma/fibre/conducting **65.7 / 30.5 / 3.8%** in their *Q. tumidinoda* node volume | Node banding should **reorient** fibre, not only darken |
| Polymer Testing 2024 — multi-species node µCT ([doi](https://doi.org/10.1016/j.polymertesting.2024.108391)) | Bending VB thickness thin ~**125 µm** / thick ~**265 µm** across four spp. | Optional node feature scale |
| Ahvenainen et al., 2017 — microfibril orientation ([Plant Methods](https://doi.org/10.1186/s13007-016-0155-1)) | Spatially localised fibre orientation in moso | Future anisotropic specular (preview may stay diffuse) |

### Engineered laminated bamboo (appearance)

| Work | Contribution | Woodie fit |
| --- | --- | --- |
| Suriani et al., 2019 — EB surface typology ([J. Sci. Design](https://www.jstage.jst.go.jp/article/jsd/3/1/3_1_69/_pdf)) | Classifies engineered-bamboo **surfaces** by grain composition, colour treatment, pattern, exposed layers | Confirms plain-pressed vs side-pressed looks; **not** a volumetric model |
| Manufacturer / trade grain primers (MOSO process pages, etc.) | Horizontal = faces + nodes; vertical = edge grain lines | Already aligned with Woodie’s strip/laminate recipe note — keep **per-strip source region** from the culm field |

No peer-reviewed **procedural volumetric** model of laminated bamboo lumber grain was found; treat LBL as **oriented samples of the same culm field** after machining removes cortex (as `natural-materials-research.md` already states).

## Quantitative parameters extractable for a shader

Normalise wall depth as Woodie already does: `t = (r − r_inner) / (r_outer − r_inner)` with `t = 0` at lacuna / pith side and `t = 1` at epidermis. Dixon & Gibson use `r/a` in the same sense (inner→outer).

| Parameter | Suggested use in `Sample` | Citeable values |
| --- | --- | --- |
| Bundle / fibre **occupancy** vs `t` | Replace linear threshold ramp with exponential occupancy `φ(t)` | Xu ExpDec1 family (outer high); Phyllostachys mean fibre VF ~**25%** whole-wall; outer third often holds **50–80%** of bundles (Fujii via Rusch et al. review) |
| Bundle **number density** | Higher noise frequency + lower threshold as `t→1` | *G. apus* internode: **8.33 / 4.89 / 4.22 mm⁻²** (O/M/I) |
| Bundle **cross-section area** | Scale blob size `∝ √A(t)` | Shang: **0.061–0.194 mm²**; `A ≈ 0.19 − 8.17 exp(5.27 t)` (their `r̄`) |
| Bundle **aspect** (end-grain) | Mild ellipse stretch radial vs tangential | Tsuyama: aspect ~**0.5** outer → **>1** inner |
| Parenchyma vs fibre colour mix | Base wall colour from parenchyma; darken with occupancy | Dixon parenchyma relative density **0.22 ± 0.03** → pale foam matrix vs dense fibre caps |
| Macro density proxy (optional albedo) | Soft luminance from ρ*(t) | Inner ~**500–650 kg m⁻³**; outer example ~**888 kg m⁻³** at `r/a ≈ 0.82` (Dixon) |
| Skin / cortex thickness | Thin outer lerp to species `Skin` RGB | Anatomical epidermis ~**15 µm**; cortex is a few cell layers — at 1 mm preview texels treat as **≲ 2–5%** of wall (or ~0.1–0.3 mm absolute), **not** Woodie’s current `SmoothStep(0.86, 0.97, t)` (~14% of wall) |
| Node axial width | Already tied to `RidgeHalfWidth` / diaphragm | Keep geometry-shared nodes; add **tangential / radial fibre reorientation** near node (µCT: vessels interconnect) rather than only `Pal.Knot` darkening |
| Longitudinal fibre | Low-amplitude axial streak modulated by occupancy | Internodes: vessels “strongly axial”; nodes distort (Liese) |

**Code status (2026-11-01 follow-up):** `BambooMaterialModel` now uses a cellular bundle field with ExpDec1-style occupancy, Shang-range depth-dependent spacing, Tsuyama aspect, and a thin epidermis band. Contact-sheet A/B against Speichinger KIT stripes is still required before claiming Speichinger-level radials.

## Recommended next experiment (smallest incorporate-able change first)

Keep the existing `Sample(x,y,z)` architecture. Change **only** the wall branch of `BambooMaterialModel.Shade` (still procedural noise; no nets).

1. **Exponential occupancy.** Map `t → φ(t)` with an ExpDec1-style curve calibrated so whole-wall mean fibre-ish dark fraction ≈ **0.25** (Xu / Phyllostachys mean), rising sharply for `t ≳ 0.7`. Use `φ` to set Perlin threshold **and** blend weight into `Pal.Late`.
2. **Depth-dependent feature scale.** Scale the `(dy, dz)` noise frequency so outer features match ~**0.06–0.1 mm²** blobs and inner ~**0.15–0.2 mm²** (Shang area range), i.e. frequency rises as `t→1`.
3. **Thin skin.** Shrink skin/cortex lerp to a few percent of wall (or absolute mm), matching silicified epidermis + short cortex rather than a thick painted band (Yoshimura; Cui).
4. **Contact-sheet A/B.** Render end-grain / long-grain / node cuts against Speichinger KIT stripes (and one cured pole photo). Reject the change if end-grain still reads as “cloudy Perlin” instead of discrete graded dots.
5. **Defer:** Speichinger/Tsuyama neural detectors at runtime; Mokume-style inverse fitting; anisotropic specular from microfibril data; full LBL strip atlas (reuse the same field with strip UVs later).

This is a proposed experiment; it needs visual and performance validation before any claim of improvement.

## Gaps / what was not found

- **No Larsson-equivalent** peer-reviewed procedural / volumetric / inverse-graphics paper for bamboo culm appearance (or monocot stem solid texture) was found. Liu et al. (2016) **exclude** bamboo by design.
- No open **Mokume-like** annotated photo+CT appearance dataset for bamboo poles.
- Dixon & Gibson’s publisher HTML drops equation glyphs; the PDF transcript recovers `V_vb = 0.09 exp(k · r/a)` with `k ∈ {1.640…2.110}` by internode and `S_f = 0.226(r/a) + 0.710` (Eq. 3.6, R² 0.84). Prefer PDF digits over HTML. Shang HTML for area Eq. (5) is still ambiguous; use Table 1 endpoints until the PDF is checked.
- Measured **cured commercial pole RGB** (skin vs fresh green) remains an appearance adaptation in Woodie presets, not a spectro-photometric series found here.
- Banik (1991) and full Liese TR 18 were not re-read cover-to-cover in this pass (same caveat as the segment-distribution note).
- Exa MCP was not available in this agent session; searches used Cursor WebSearch/WebFetch against publisher, arXiv, KIT, and repository URLs.

## Larsson-parity upgrade cues (2026-11-01)

Goal: raise `BambooMaterialModel.Sample` toward Larsson softwood **clarity** (sharp coherent structure, orientation-dependent microdetail, readable anatomy). No bamboo Larsson paper exists. Cues below come only from anatomy/materials primary sources. `t = 0` at pith cavity. `t = 1` at epidermis. Do not invent missing digits.

**Shipped:** `woodie-15` flower `BundleField` (clover→skull), Dixon density albedo, multi-octave axial fibre, thin skin. **`woodie-16`:** Speichinger pith/skin VB-free bands, Dixon `S_f` lumen fade, Peng ~12.9 mm node reorientation, Ahvenainen MFA-split streaks. Remaining gap vs Larsson: Mokume-style photo/CT calibration; full-ring tiles still under-resolve flowers (~20 px wall).

### Cue → Sample change → citeable value

| Cue | Sample change | Citeable value |
| --- | --- | --- |
| Discrete sheath lobes (flower / clover / skull) | Replace single soft radial falloff with 2–4 fibre-cap lobes around twin vessel lumens. Cap count falls with `t`. | Grosser & Liese types I–IV; *Phyllostachys* type I ([DOI 10.1007/BF00365061](https://doi.org/10.1007/BF00365061)). Dixon: inner/mid = four sclerenchyma areas (clover); outer = two areas (skull) ([DOI 10.1098/rsif.2014.0321](https://doi.org/10.1098/rsif.2014.0321); [PMC4233722](https://pmc.ncbi.nlm.nih.gov/articles/PMC4233722/)). Shang: inner open (four sheaths) → mid/outer semi-open ([DOI 10.3390/f15061023](https://doi.org/10.3390/f15061023)). Speichinger VB1/VB2/VB3 = 4 / 2–3 / 1 fibre bundles ([DOI 10.1016/j.conbuildmat.2026.146522](https://doi.org/10.1016/j.conbuildmat.2026.146522); [KIT PDF](https://publikationen.bibliothek.kit.edu/1000193323/181346290)). |
| Lumen vs sheath contrast | Pale vessel cores only where `S_f` is low. Shrink lumen weight as `t→1`. | Dixon `S_f = 0.226(r/a)+0.710` (Eq. 3.6, R² 0.84); vessels shrink outward (same paper). |
| Occupancy ramp φ(t) | Keep exponential dark fraction. Target whole-wall mean near Phyllostachys fibre VF. | Dixon: `V_vb = 0.09 exp(k·r/a)`, `k = 1.640…2.110` by internode. Xu Part II: ExpDec1, mean R² **0.988**, layer ≤ **0.4 mm** ([DOI 10.1016/j.indcrop.2021.114164](https://doi.org/10.1016/j.indcrop.2021.114164)). Xu bioRxiv: fibre VF **17.69–32.65%**, mean **25.50±3.51%**; ExpDec1 mean **A = 57.44**, **t = 0.53** ([DOI 10.1101/2021.12.01.470805](https://doi.org/10.1101/2021.12.01.470805)). Speichinger: local fibre area fraction ~**10%** inner → max **77.62%** outer; volume average **24.11%**. Tsuyama: VB area occupation up to ~**70%** at epidermis side ([DOI 10.1371/journal.pone.0290732](https://doi.org/10.1371/journal.pone.0290732)). |
| Bundle size / spacing | `spacing ∝ √A(t)`. Smaller outer blobs. Raise number density outward. | Shang Table 1: single-VB area **0.061–0.194 mm²** (exponential radial decrease; verify Eq. (5) on PDF). Speichinger: domain area peaks ~**0.6 mm²** at `r̃≈0.2` then falls; volume-average number density **3.37 mm⁻²** (VB1/2/3: **0.7 / 2.14 / 0.53 mm⁻²**). Fibre-bundle area in a VB rises then drops past `r̃≈0.8`. |
| Bundle aspect (end-grain) | Ellipse stretch with `t`. | Tsuyama: aspect (tangential/radial) ~**0.5** near epidermis → **>1** toward pith. Xu bioRxiv: whole-wall length/width mean **1.226±0.091**. |
| Empty pith / skin bands | Zero bundle survive in innermost and outermost wall fractions. | Speichinger six layers (inner→outer relative thickness): **5.64% / 24.23% / 31.52% / 32.23% / 5.38% / 1.01%**; pith and skin have no VBs. |
| Density → soft albedo | Multiply parenchyma base luminance by ρ*(t) proxy before sheath lerp. | Dixon: inner specimens ~**500–650 kg m⁻³**; example outer flexure specimen `r/a = 0.817`, ρ* = **888 kg m⁻³**. Parenchyma solid fraction **0.22±0.03** → ~**330 kg m⁻³** at ρ_s = 1500. |
| Epidermis thickness | Absolute outer band. Do not paint a thick wall fraction as “skin”. | Yoshimura: silicified epidermal layer ~**15 µm**; silica wedges ~**8 µm** thick, disk head ~**10 µm**, ridge ~**5 µm** ([DOI 10.1038/s41598-025-06906-w](https://doi.org/10.1038/s41598-025-06906-w); [PMC12219570](https://pmc.ncbi.nlm.nih.gov/articles/PMC12219570/)). Speichinger skin layer **1.01%** of wall (VB-free). Preview may thicken for readability; keep << current soft cortex. |
| Node reorientation length | Near node: rotate fibre/streak frame toward tangential; raise cross-link share. Do not only darken with `Pal.Knot`. | Peng: axial VB long axis reorients near diaphragm; inner/outer sheaths become tangential; conducting network connectivity **94.77%**; node tissue **parenchyma 65.7% / fibres 30.5% / conducting 3.8%** (*Q. tumidinoda*) ([DOI 10.3390/f12121799](https://doi.org/10.3390/f12121799)). μCT: **10 µm** voxels; **1290** slices ⇒ ~**12.9 mm** axial stack covering sheath scar → diaphragm → nodal ridge. |
| Longitudinal streak character | Internode: sharp axial streaks on sheath. Soft / less oriented mottling on parenchyma. | Ahvenainen: fibre mean MFA **11±3°** (NNMF) / **11±8°** (ROI); parenchyma **65±10°** (NNMF) / **46±15°** (ROI) ([DOI 10.1186/s13007-016-0155-1](https://doi.org/10.1186/s13007-016-0155-1)). Dixon parenchyma brick aspect **1.9±0.4** (axial–tangential). Internode fibres “well aligned” with culm axis (Ahvenainen citing anatomy). |
| Species type preset | Moso / *Phyllostachys*: type I (four sheaths, no free fibre strands). Tropical presets: types III/IV. | Grosser & Liese 1971 classification (52 spp., 14 genera). |

### Priority list (top 5 by Larsson-clarity visual impact)

1. **Multi-lobe sheath + twin lumens** in `BundleField` (Grosser / Dixon clover→skull / Shang open→semi-open / Speichinger VB1→VB3). This is the end-grain “flower” that soft noise cannot fake.
2. **Harder occupancy + lumen fade with `S_f(t)`** (Dixon Eq. 3.6; Speichinger outer local AF to 77.62%; Tsuyama ~70% occupation). Sharp dots on pale parenchyma, not cloudy Perlin.
3. **VB-free pith/skin bands + Speichinger-style density zones** (5.64% / … / 1.01%; number density toward 3.37 mm⁻²). Clean outer shell and sparse inner wall.
4. **Node streak reorientation over ~mm–cm axial band** (Peng ~12.9 mm μCT node stack; tangential sheath near diaphragm; 94.77% conducting connectivity). Matches Larsson knot “structure”, not a stain.
5. **Orientation-dependent microdetail** (Ahvenainen MFA ~11° fibre vs ~65° parenchyma; Dixon parenchyma AR 1.9). Sharp axial needles in sheath; softer matrix mottling.

Defer for preview: runtime YOLO/VAE (Speichinger/Tsuyama); specular from MFA; digitizing Shang Eq. (5) coefficients until PDF check; photo-ML inverse fitting.

## References


1. Ahvenainen, P., et al. (2017). Spatially-localized bench-top X-ray scattering reveals tissue-specific microfibril orientation in Moso bamboo. *Plant Methods*, 13, 5. https://doi.org/10.1186/s13007-016-0155-1  
2. Cui, J., et al. (2022). Quantitative characterization of bamboo cortex structure based on X-ray microtomography. *Cellulose*. https://doi.org/10.1007/s10570-022-04534-5 · preprint: https://doi.org/10.21203/rs.3.rs-1131320/v1  
3. Dixon, P. G., & Gibson, L. J. (2014). The structure and mechanics of Moso bamboo material. *Journal of the Royal Society Interface*, 11(99), 20140321. https://doi.org/10.1098/rsif.2014.0321 · PMC: https://pmc.ncbi.nlm.nih.gov/articles/PMC4233722/  
4. du Plessis, A. (2021). MicroCT of bamboo (dataset). Mendeley Data. https://doi.org/10.17632/z8z5w59c7n.1  
5. du Plessis, A., & Broeckhoven, C. (2019). Looking deep into nature: A review of micro-computed tomography in biomimicry. *Acta Biomaterialia*. https://doi.org/10.1016/j.actbio.2018.12.014  
6. Grosser, D., & Liese, W. (1971). On the anatomy of Asian bamboos, with special reference to their vascular bundles. *Wood Science and Technology*, 5, 290–312. https://doi.org/10.1007/BF00365061  
7. Hakim, L., et al. (2018). Morphological characteristics of *Bambusa vulgaris* and the distribution and shape of vascular bundles therein. *Journal of the Korean Wood Science and Technology*, 46(4), 315–. https://doi.org/10.5658/WOOD.2018.46.4.315  
8. Hu, Y., et al. (2022). An inverse procedural modeling pipeline for SVBRDF maps. *ACM Transactions on Graphics*. https://doi.org/10.1145/3502431  
9. Larsson, M., et al. (2022). Procedural texturing of solid wood with knots. SIGGRAPH 2022. https://www.ma-la.com/procedural_knots.html · https://github.com/marialarsson/procedural_knots  
10. Larsson, M., et al. (2024). Learned inference of annual ring pattern of solid wood. *Computer Graphics Forum*. https://doi.org/10.1111/cgf.15074  
11. Larsson, M., et al. (2025). The Mokume dataset and inverse modeling of solid wood textures. https://mokumeproject.github.io/ · https://rgl.epfl.ch/publications/Larsson2025Mokume  
12. Liese, W. (1998/2002). *The Anatomy of Bamboo Culms*. INBAR Technical Report No. 18. AGRIS: https://agris.fao.org/search/en/records/65dddf387c7033e84be87dd0  
13. Liu, A. J., Dong, Z., Hašan, M., & Marschner, S. (2016). Simulating the structure and texture of solid wood. *ACM Transactions on Graphics*, 35(6). https://doi.org/10.1145/2980179.2980255 · arXiv: https://ar5iv.labs.arxiv.org/html/1511.04224v2  
14. Luo, Y., Wu, Z., & Guo, X. (2009). Bamboo simulation based on improvement fractal algorithm and displacement and texture mapping. *Computer Science* (JSJKX), 36(12). https://www.jsjkx.com/EN/Y2009/V36/I12/285  
15. Maulana, M. I., et al. (2023). Anatomical characteristics… *Gigantochloa apus* (vascular-bundle density table). *Journal of the Korean Wood Science and Technology*. https://www.woodj.org/archive/view_article?pid=wood-51-4-309  
16. Palombini, F. L., et al. (2019). Biomimetic systems… bamboo node… X-ray microtomography… *Journal of Materials Research*. https://doi.org/10.1557/jmr.2019.117  
17. Peng, G., et al. (2021). 3D visualization of bamboo node’s vascular bundle. *Forests*, 12(12), 1799. https://doi.org/10.3390/f12121799  
18. Polymer Testing authors (2024). A nondestructive method using X-ray micro computed tomography for variation analysis of bamboo nodes’ vascular bundle among species. *Polymer Testing*. https://doi.org/10.1016/j.polymertesting.2024.108391  
19. Rusch, F., et al. (2018). Anatomia de hastes adultas de bambu: uma revisão (cites Fujii outer-third bundle share). *Pesquisa Florestal Brasileira*. https://pfb.sede.embrapa.br/pfb/article/download/1530/695  
20. Shang, L., et al. (2024). The gradient variation of location distribution, cross-section area, and mechanical properties of Moso bamboo vascular bundles along the radial direction. *Forests*, 15(6), 1023. https://doi.org/10.3390/f15061023  
21. Speichinger, L., Förster, R., & Böhlke, T. (2026). Scale-bridging characterization and modeling of statistically inhomogeneous Moso bamboo. *Construction and Building Materials*. https://doi.org/10.1016/j.conbuildmat.2026.146522 · KIT PDF: https://publikationen.bibliothek.kit.edu/1000193323/181346290  
22. Speichinger, L., et al. Dataset for vascular bundle detection in Moso bamboo. https://doi.org/10.35097/fe5eamg8w89bx4ns · https://radar.kit.edu/radar/en/dataset/fe5eamg8w89bx4ns  
23. Speichinger, L., et al. Cross-sectional images of Moso bamboo mesostructure. https://publikationen.bibliothek.kit.edu/1000190988  
24. Suriani, et al. (2019). Surface appearance typology of engineered bamboo products. *Journal of the Science of Design*, 3(1). https://www.jstage.jst.go.jp/article/jsd/3/1/3_1_69/_pdf  
25. Tsuyama, T., Hamai, K., Kijidani, Y., & Sugiyama, J. (2023). Quantitative morphological transformation of vascular bundles in the culm of moso bamboo (*Phyllostachys pubescens*). *PLOS ONE*, 18(9), e0290732. https://doi.org/10.1371/journal.pone.0290732  
26. Xu, H., et al. (2021). Intelligent analysis technology of bamboo structure. Part II: The variability of radial distribution of fiber volume fraction. *Industrial Crops and Products*, 174, 114164. https://doi.org/10.1016/j.indcrop.2021.114164  
27. Xu, H., et al. (2021). Efficiently, accurately, intelligently dissecting bamboo: characteristic of vascular bundle in *Phyllostachys*. bioRxiv. https://doi.org/10.1101/2021.12.01.470805  
28. Yoshimura, et al. (2025). Biogenic design of silicious architectures on Moso bamboo culm. *Scientific Reports* (PMC). https://pmc.ncbi.nlm.nih.gov/articles/PMC12219570/  
