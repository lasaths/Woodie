# Bamboo culm segment distribution (nodes and internodes)

Date: 2026-10-31. Status: **Implemented in `NaturalFeatures` (RIN–RCIL logistic + basal window)**; primary sources reviewed.

Goal: replace equal/repetitive node spacing with botanically plausible internode-length profiles for Grasshopper/Rhino geometric modelling of bamboo stock.

## Executive summary

A bamboo culm is a hollow (or nearly hollow) cylinder divided into **internodes** by **nodes**. Each node combines an external ridge with an internal **diaphragm** that stiffens the tube against bending ovalisation (Shima et al., 2016; Dixon and Gibson, 2014). Internode lengths are **not** uniform along height. Across many measured woody bamboos, lengths are typically **shortest near the base**, **longest around mid-culm** (often slightly below half height), then **shorten toward the tip**, giving a right-skewed length-vs-node-index curve (Banik, 1991, as cited in Tan et al., 2024; Chaowana et al., 2021; Ghavami and Marinho, 2005). Relative-height studies of *Phyllostachys edulis* place the longest internode near relative height **0.47–0.52** (Yang and Tang, 2021). Cumulative length vs relative node index follows a **sigmoid** (“slow–fast–slow”) pattern that size-normalises across culms; third-order / logistic / modified-Brière fits outperform power laws (Inoue et al., 2012, 2017; Tan et al., 2024). Diameter and wall thickness generally **decrease from base to tip**, with species-dependent rates (Ghavami and Marinho, 2005; Chaowana et al., 2021; Lorenzo et al., 2020). Branching height and sterile basal nodes are **genus- and species-specific** and must not be hardcoded from one taxon. For Woodie, prefer a **parameterised relative-length curve** (species preset + seed jitter) over equal spacing; treat absolute centimetres as species/stock parameters, not universal constants.

## Anatomy (measured / textbook primary)

| Feature | Definition (as used in sources) | Source |
| --- | --- | --- |
| Culm | Aboveground woody stem; final outer diameter set at sprouting (no secondary thickening like trees) | Chaowana et al., 2021; Dixon and Gibson, 2014 |
| Internode | Hollow cylindrical segment between consecutive nodes; cells mostly axially oriented | Chaowana et al., 2021; Liese, 1998 (INBAR TR 18) |
| Node | External ridge + internal wooden partition (**diaphragm**) between adjacent internodes; cells more transversely interconnected | Chaowana et al., 2021; Shima et al., 2016 |
| Diaphragm | Stiff transverse plate acting as a ring stiffener against cross-section ovalisation under bending | Shima et al., 2016 |
| Taper | Outer diameter and wall thickness typically decrease from base toward tip; some tropical species are nearly columnar then taper near the tip | Chaowana et al., 2021; Ghavami and Marinho, 2005 |

Node count is largely fixed before or at shoot emergence; subsequent height growth is mainly **internode elongation**, not addition of new nodes (Yang and Tang, 2021, citing Chinese culm-form literature; Tan et al., 2024).

## Internode length along height

### Qualitative pattern (widely reported)

Measured profiles commonly show:

1. Short internodes at the base.
2. Progressive lengthening to a maximum near mid-culm (often slightly below half height).
3. Progressive shortening toward the tip.

Banik (1991) plotted internode length against serial number for ten species and reported a **right-skewed** curve (steeper on the basal side, flatter toward the tip); Tan et al. (2024) restate and confirm that pattern for three Chinese taxa. Shima et al. (2016) state the same qualitative shape for wild moso and note similar profiles for madake and hachiku. Chaowana et al. (2021) report the increase–decrease pattern for five Thai construction species (*Dendrocalamus asper*, *D. sericeus*, *D. membranaceus*, *Thyrsostachys oliveri*, *Phyllostachys makinoi*). Ghavami and Marinho (2005) report the same for Brazilian *Guadua angustifolia* (basal short → central maximum → upper decrease). Korean *Bambusa vulgaris* measurements likewise show mid-culm internodes longer than basal and apical ones (Hakim et al., 2018).

**Caveat (species / sampling):** A Philippine *Guadua angustifolia* study that bins only **bottom / middle / top** reports mean internode lengths **20.93 / 31.69 / 36.35 cm** (still increasing into the top third) with significant height-level differences (Alipon et al., 2025). That does **not** contradict mid-culm peaks in full serial profiles; coarse three-bin averages can hide tip shortening. Do not treat three-bin means as a continuous spacing curve.

Plant Biosystems work on twelve species notes that node density is **not always** “dense near both ends and sparse in the middle”; the **position of maximum internode length varies among species** (Shi et al., 2021).

### Measured series and summary statistics

#### *Phyllostachys edulis* / *P. pubescens* (moso) — development stages

Tan et al. (2024), open-access *Frontiers in Ecology and Evolution*, Table 1 (means ± SD; internodes numbered base → tip):

| Stage | Approx. mean culm height | Mean internode count | Mean IL (cm) | Max IL (cm) | Serial no. of longest IL |
| --- | --- | --- | --- | --- | --- |
| G1 | 0.91 m | 30.0 | 2.31 | 7.30 | 5.3 |
| G5 | 5.10 m | 41.4 | 11.92 | 28.57 | 16.4 |
| G10 | 11.04 m | 57.7 | 18.68 | 30.23 | 27.7 |

As height increases, the **longest internode’s serial index moves upward** (linear in culm length in their Fig. 3). At mature-ish G10, the longest internode is near the **middle of the node sequence**, not the tip.

Yang and Tang (2021), Zhejiang *P. edulis*, *n* = 159 culms, 9 431 nodes: node counts concentrate at **53–67**; 95% CI of mean node count **(58.1, 60.4)**; internode length vs **relative height** fits a **parabola**; longest-node relative height stays near **0.47–0.52** across diameter classes; central diameter falls about **10% per 10% relative-height increase** (linear taper in relative height).

China-wide survey of moso internode length **at 1.5 m** height: range **17–34 cm**, mean **24.39 ± 2.86 cm**, **CV = 11.72%** (Li et al., 2025 abstract). That CV is **among-site / among-culm** variation at a fixed height, not within-culm segment jitter—but it is a citeable order of magnitude for environmental scatter of absolute length.

#### *Guadua angustifolia*

| Source | What was measured | Internode pattern | Diameter / wall |
| --- | --- | --- | --- |
| Ghavami and Marinho (2005) | Whole culms, Brazil | Short base → max mid → shorter top; cubic regression of length vs node index (figure + regression in paper) | Outer *D*: ~**101.9 mm** base → **31.2 mm** tip (mean **79.6 mm**); wall *t* mean **10.3 mm**, decreasing base→tip with high scatter (1 mm instrument resolution noted) |
| Alipon et al. (2025) | Philippines, three height bins | Bottom / middle / top IL **20.93 / 31.69 / 36.35 cm** | Diameter **8.66 / 7.52 / 5.00 cm**; wall **15.49 / 8.89 / 7.11 mm** |

Ghavami and Marinho also give regression forms (paper Fig. 6–7): outer diameter vs distance from base *z* (m) approximately  
`D = −0.4804 z² + 1.8716 z + 98.101` (mm), and a cubic for wall thickness vs *z*. Use these only for *Guadua*-like structural stock, not as universal bamboo defaults.

#### Multi-species construction bamboos (Thailand)

Chaowana et al. (2021), five three-year-old species, ISO-aligned sampling of bottom / middle / top thirds: **internode length increased bottom→middle then decreased toward top**; outer diameter and wall thickness decreased toward the tip; *D. asper* had the largest diameter, wall and internodes among the five; *D. membranaceus* was relatively **columnar**, tapering mainly near the top. Exact per-species centimetre tables are in their figures (OA MDPI PDF); this note does not invent digitised point values from those plots.

#### *Bambusa vulgaris*

Hakim et al. (2018): internode length longer in mid-section than bottom or top; outer diameter decreases base→tip; wall thickness decreases base→tip; mid-culm cavity (inner diameter) larger than at ends.

### Mathematical models (published)

Mark: these are **fits to measured relative geometry**, not growth-process simulators unless the paper claims that.

| Model | Variables | Claim | Species scope | Source |
| --- | --- | --- | --- | --- |
| Relative internode number → relative cumulative length (**RIN–RCIL**) | `RIN = i / N`, `RCIL = Σ_{k≤i} L_k / H` | Sigmoid (“slow–fast–slow”); better than power law | *P. edulis* / *pubescens* | Inoue et al., 2012 |
| Universal sigmoid RIN–RCIL | Same | One sigmoid can describe three *Phyllostachys* of different sizes | *P. edulis*, *P. nigra* var. *henonis*, *P. nigra* | Inoue et al., 2017 |
| Modified Brière (MBE) sigmoid vs logistic / power / cubic | RIN–RCIL | Lowest mean RMSE among compared equations | *P. edulis*, *P. iridescens*, *Pseudosasa amabilis* | Tan et al., 2024 |
| Parabola of internode length vs relative height | Relative height ∈ [0,1] | Longest node ~ half height | Zhejiang *P. edulis* | Yang and Tang, 2021 |
| Polynomials of IL, *D*, *t* vs node index or height | Absolute mm / m | Engineering regressions for one *Guadua* sample set | *G. angustifolia* | Ghavami and Marinho, 2005 |
| Mechanics / ovalisation optimality | Normalised node index `x = n/N` | Uneven spacing (longer mid, shorter ends) as lightweight stiffener layout; cubic fit to normalised height; parabolic fit to normalised IL | Primarily moso; similar profiles cited for madake / hachiku | Shima et al., 2016 |

**Inference for modelling (not a measured fact):** Differentiating a fitted RIN–RCIL sigmoid yields a relative internode-length weight per node that automatically shortens toward both ends when the sigmoid has the usual mid-culm steep region. That is the cleanest size-independent way to drive node placement on a stock of length *H*.

## Diameter and wall thickness along the culm

**Measured facts:**

- Outer diameter decreases from base to tip in *Guadua* (near-linear / mild quadratic; Ghavami and Marinho, 2005), moso (Dixon and Gibson, 2014: lower-internode OD ~16.8→12.4 cm over their sampled lower 15 internodes), Thai multi-species sets (Chaowana et al., 2021), and Italian cultivated *P. edulis* (Ferrarese et al., 2024: diameter and wall-thickness decrease along height; geometry broadly similar to Asian moso).
- Wall thickness decreases base→tip in the same studies; absolute values and scatter are high (instrument and natural ovality).
- Relative density / fibre packing often **increases** toward the tip even as wall thins (Alipon et al., 2025 for *Guadua*; Dixon and Gibson, 2014 for moso mechanics context).
- Lorenzo et al. (2020) 3D-scanned *Guadua*, moso and *Bambusa oldhamii*: wide geometric variability; diameter and thickness reduction along length confirmed; **no simple visual-grade pattern** that collapses all variation—parameterise per culm or per species preset, do not hardcode one taper for all bamboo.

**Inference:** For hollow-segment visualisation, couple a relative IL curve with independent relative-*D* and relative-*t* curves (linear taper is often enough for preview; use published *Guadua* polynomials only when the stock is Guadua-like).

## Branching vs sterile basal nodes

| Taxon / group | Branching vs basal nodes (as stated in primary botanical sources) | Source |
| --- | --- | --- |
| *Phyllostachys* (mature stature) | Lower nodes of mature culms typically **without** buds/branches; upper nodes branch promptly; juvenile/small culms may branch from more nodes | McClure, 1957 (USDA / Flora) |
| *Dendrocalamus* subg. *Dendrocalamus* | Culms often **branch from lower / basal nodes**; 1–3 dominant branches | Flora of China |
| *Dendrocalamus* subg. *Sinocalamus* | Basal nodes **without** branches; mid-culm branches more equal | Flora of China |
| Species examples | *D. tibeticus*: branching from ~**9th–14th** node; *D. bambusoides*: from ~**4th** node; *D. sikkimensis*: buds/branches absent from **lower quarter** of culm | Flora of China; Kew GrassBase |

Clear bole / “first branch height” is therefore a **species (and maturity) parameter**, not a fixed fraction of height for “bamboo” in general. Structural stock cut from the lower culm may show **sterile node rings** without branch scars even when mid-culm stock would show them.

## What to parameterise vs hardcode

| Quantity | Recommendation | Evidence |
| --- | --- | --- |
| Relative IL profile shape (base short → mid long → tip short) | Default **hardcoded curve family** (sigmoid RIN–RCIL or parabolic relative height), with optional species presets | Inoue 2012/2017; Tan 2024; Yang 2021; Chaowana 2021 |
| Absolute mean IL, node count, max IL | **Parameterise** (species + DBH/height class) | Yang 2021; Tan 2024 Table 1; Li 2025 CV |
| Peak location (relative height of longest IL) | Parameterise; default ~**0.5** for moso-like; allow shift (Shi 2021: species vary) | Yang 2021; Shi 2021 |
| Diameter / wall taper | Parameterise; linear relative taper OK as default | Ghavami 2005; Chaowana 2021; Lorenzo 2020 |
| Branch-start node index | Parameterise per genus/species; default “no branches” for lower-stock materials | McClure 1957; Flora of China |
| Within-culm random jitter | Soft parameter; no single published CV for residual after removing the height trend was found as a universal constant | Open gap (below) |

## Modeling recommendations for Woodie (inference, citeable)

Clear mark: **recommendations** for implementation, grounded in the sources above.

1. **Do not use equal spacing.** Equal internodes contradict every full-profile study cited here and the mechanical rationale in Shima et al. (2016).

2. **Place nodes by a normalised relative-length curve.**  
   - Choose target stock length *H* (Rhino/GH units after Geometry Units conversion) and node count *N* (or mean IL ≈ *H* / *N*).  
   - Build weights `w_i` from either:  
     - (A) difference of a fitted **RIN–RCIL sigmoid** at `i/N` and `(i−1)/N` (Inoue / Tan family), or  
     - (B) a **parabola** peaking at relative height `p ∈ [0.45, 0.55]` with positive lengths at both ends (Yang and Tang shape).  
   - Set `L_i = H · w_i / Σ w`, then cumulative sums as node ring positions along the stock axis.

3. **Species presets** (absolute scales are examples from papers, not Woodie defaults until productised):  
   - *Phyllostachys* / moso-like: *N* ~ 50–65 for full culms; peak near half height; use RIN–RCIL sigmoid.  
   - *Guadua*-like: allow longer mid/upper IL; optional Ghavami *D(z)*, *t(z)* if hollow section is shown.  
   - *Dendrocalamus asper*-like: large diameter/wall; still mid-peak IL (Chaowana).  
   - Columnar types (*D. membranaceus*-like): milder diameter taper.

4. **Seed-based jitter.** No primary source gave a residual CV after detrending the along-culm profile that is safe to hardcode as “the” bamboo jitter. **Inference:** apply multiplicative noise `L_i ← L_i · (1 + ε_i)` with `ε_i` from a zero-mean distribution, then **re-normalise** to preserve *H*. Bound `|ε|` using among-culm absolute-length CV as an **upper** guide (~**0.12** at fixed height for Chinese moso at 1.5 m; Li et al., 2025)—i.e. keep typical jitter **well below** that if the goal is botanical irregularity without destroying the mid-culm peak. Prefer documenting the bound as a UI parameter with that citation as the ceiling rationale.

5. **Stock as a culm fragment.** Commercial poles are often basal or mid sections (Lorenzo et al., 2020 used ~2.8 m bottom sections). Slice the relative curve to the relative-height window that corresponds to the stock (e.g. basal third only → mostly rising IL, few tip-short internodes). Equal spacing is still wrong inside a fragment.

6. **Branch scars / sterile nodes.** Drive branch geometry from a separate “first branching node” parameter; leave basal rings sterile for *Phyllostachys*-like materials.

7. **Reject silent quality loss.** If atlas/bake or segment count would force equal fake nodes, prefer fewer honest segments or an explicit “schematic equal” mode labelled as non-botanical—aligned with Woodie’s reject-oversized-atlas rule for materials.

## Open gaps / what was not found

- A single open tabulated **full serial IL list** (every node centimetre) for *Guadua* / *Dendrocalamus* suitable to paste as a Woodie lookup without digitising figures.
- A published **within-culm residual CV** after removing the systematic height trend (for jitter calibration).
- Banik (1991) primary PDF was not retrieved in this pass; the right-skew claim is cited via Tan et al. (2024) and secondary mechanical literature—prefer locating Banik’s original plots before treating species-specific peak positions as authoritative.
- Liese INBAR Technical Report 18 (*The Anatomy of Bamboo Culms*, 1998/2002) is the standard anatomy monograph; this note uses open papers that cite it rather than a full re-read of the 208-page book.
- Italian *P. edulis* geometry paper (Ferrarese et al., 2024) confirms qualitative similarity to Asian moso but full numeric tables were not open-fetched here.
- Exa MCP was not available in this agent session; searches used Cursor WebSearch/WebFetch against publisher and repository URLs.

## References

1. Alipon, M. A., et al. (2025). *Morphological, Anatomical, and Physical Properties of Iron Bamboo (Guadua angustifolia Kunt.) Grown in the Philippines*. Philippine Journal of Science. PDF: https://philjournalsci.dost.gov.ph/wp-content/uploads/2025/07/morphological_anatomical_and_physical_properties_of_iron_bamboo_.pdf  
2. Banik, R. L. (1991). Culm internode length vs serial number for multiple bamboo species (primary monograph; cited via Tan et al., 2024).  
3. Chaowana, K., Wisadsatorn, S., & Chaowana, P. (2021). Bamboo as a Sustainable Building Material—Culm Characteristics and Properties. *Sustainability*, 13(13), 7376. https://doi.org/10.3390/su13137376  
4. Dixon, P. G., & Gibson, L. J. (2014). The structure and mechanics of Moso bamboo material. *Journal of the Royal Society Interface*, 11(99), 20140321. https://doi.org/10.1098/rsif.2014.0321 · PMC: https://pmc.ncbi.nlm.nih.gov/articles/PMC4233722/  
5. Ferrarese, S., et al. (2024). Natural geometrical variations of Italian *Phyllostachys edulis* bamboo culms for construction purposes. *Advances in Bamboo Science*, 7, 100116. https://doi.org/10.1016/j.bamboo.2024.100116  
6. Flora of China. *Dendrocalamus* and subgenera treatments (basal vs mid-culm branching). http://www.efloras.org/florataxon.aspx?flora_id=2&taxon_id=109555  
7. Ghavami, K., & Marinho, A. B. (2005). Propriedades físicas e mecânicas do colmo inteiro do bambu da espécie *Guadua angustifolia*. *Revista Brasileira de Engenharia Agrícola e Ambiental*, 9(1), 107–114. http://www.scielo.br/pdf/rbeaa/v9n1/v9n1a16.pdf  
8. Hakim, L., et al. (2018). Morphology of culm and vascular bundles of Ampel bamboo (*Bambusa vulgaris*). *Journal of the Korean Wood Science and Technology*, 46(4), 315–. https://www.woodj.org/archive/view_article?pid=wood-46-4-315  
9. Inoue, A., et al. (2012). Mathematical expression for the relationship between internode number and internode length for bamboo, *Phyllostachys pubescens*. *Journal of Forestry Research*. https://doi.org/10.1007/s11676-012-0281-1  
10. Inoue, A., et al. (2017). Universal node distribution in three bamboo species (*Phyllostachys* spp.). *Trees*. https://doi.org/10.1007/s00468-017-1546-2  
11. Kew GrassBase. *Dendrocalamus sikkimensis* (lower-quarter sterile of branches). https://www.kew.org/data/grasses-db/www/imp02804  
12. Li, et al. (2025). Key Environmental Factors Affecting Internode Length of Moso Bamboo. *Scientia Silvae Sinicae* abstract (mean 24.39±2.86 cm at 1.5 m; CV 11.72%). http://www.linyekexue.net/EN/10.11707/j.1001-7488.LYKX20250346  
13. Liese, W. (1998/2002). *The Anatomy of Bamboo Culms*. INBAR Technical Report No. 18. AGRIS: https://agris.fao.org/search/en/records/65dddf387c7033e84be87dd0  
14. Lorenzo, R., Mimendi, L., Godina, M., & Li, H. (2020). Digital analysis of the geometric variability of Guadua, Moso and Oldhamii bamboo. UCL Discovery PDF: https://discovery.ucl.ac.uk/id/eprint/10086175/1/JCBM_v2.pdf  
15. McClure, F. A. (1957). The vegetative characters of the bamboo genus *Phyllostachys*… USDA / Biodiversity Heritage Library. https://www.biodiversitylibrary.org/part/147102 · GovInfo PDF: https://www.govinfo.gov/content/pkg/GOVPUB-A-PURL-gpo19320/pdf/GOVPUB-A-PURL-gpo19320.pdf  
16. Shi, P., et al. (2021). Variations in patterns of internode and branch lengths for several bamboo species. *Plant Biosystems*, 155(6). https://doi.org/10.1080/11263504.2020.1829729  
17. Shima, H., Sato, M., & Inoue, A. (2016). Self-adaptive formation of uneven node spacings in wild bamboo. *Physical Review E*, 93, 022406. https://doi.org/10.1103/PhysRevE.93.022406  
18. Tan, J., Liu, X., Li, Q., Ma, K., & Huang, W. (2024). Variation in internode length patterns: a data analysis of internode length and serial number in three bamboo species. *Frontiers in Ecology and Evolution*, 12, 1440494. https://doi.org/10.3389/fevo.2024.1440494  
19. Yang, F., & Tang, M. (2021). On the structure characteristics of culm form of *Phyllostachys edulis* in Zhejiang Province. *Journal of Zhejiang A&F University*, 38(6), 1289–1296. https://doi.org/10.11833/j.issn.2095-0756.20200796  
