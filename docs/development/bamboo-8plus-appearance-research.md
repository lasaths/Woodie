# Bamboo appearance → 8+ — research appendix

Date: 2026-11-05. Status: execution brief for volumetric-only epidermis/end-grain/node upgrades. Complements [bamboo-appearance-research.md](bamboo-appearance-research.md).

## Principle

One volumetric field. Side strings = outer samples of `SampleBundle`; end dots = same cells. No `Fract(arc)` epidermis hatch.

Measured pre-fix disconnect: `--bamboo-vol-compare` Pearson sheath↔Fract-string **r ≈ −0.01**.

## Facet → mechanism → code gap → move

### Cut continuity

- **Mechanism:** Internode VBs are strongly axial (Liese; Ahvenainen MFA ~11° in fibre). Same `(θ,t)` cell IDs on end and long cuts (Larsson discipline; Liu et al. 2016 exclude bamboo).
- **Code:** `SampleBundle` has no axial index; `rRef` fixed. Fract hatch broke visual continuity.
- **Move:** Delete Fract; side streak = `f(sheath)`. Target Pearson **r > 0.3**.

### End grain

- **Mechanism:** Dixon `V_vb` / `S_f`; Xu ExpDec1; Shang area 0.061–0.194 mm²; Tsuyama aspect; Speichinger pith/skin VB-free bands.
- **Code:** Flower SoftCaps OK; spacing floor 0.85 mm too coarse; SoftCap `SmoothStep(0.95, 0.22)` too soft; Late amber OK.
- **Move:** Spacing floor ~0.5 mm; SoftCap `SmoothStep(~0.20, ~0.045)`; raise outer sheathMix; Speichinger A/B.

### Side long-grain

- **Mechanism:** Photo strings = outer fibre sheaths through thin wax/silica cuticle (Liese; Yoshimura ~15 µm; PMC8271862 longitudinal cuts).
- **Code:** Fract hatch + 2.8 mm skin band; `SampleBundle` returns 0 for `tc > 0.995` so outer surface sheath was zero.
- **Move:** Query bundles at `min(r, ro − ε)`; streak from `Pow(sheath,k)` toward near-Skin dark; Ahvenainen split (sharp sheath / soft parenchyma).

### Colour mottling

- **Mechanism:** Cured tan/honey; UV quinones yellow then wash (PMC6030306). Fresh cured = fibre vs parenchyma + mild variance, not pepper.
- **Code:** Skin RGB + Fract blotch.
- **Move:** Near-base Skin blotch ±6–10%; sheath contrast carries mottle; nodeHalo wick on sheath.

### Nodes

- **Mechanism:** Peng/Gouin µCT — VB reorientation and interconnect; ~12.9 mm stack; ~95% conducting connectivity.
- **Code:** Ridge geometry + Gaussian bands + mild `angWarp`.
- **Move:** Stronger `angWarp`; `strandT *= (1 + c·nodeHalo)`; ring `Skin*0.88–0.92`. No geometric fringe in v1.

### Skin / sheen

- **Mechanism:** ~15 µm silicified epidermis + wax (Yoshimura; Liese). Speichinger skin ~1% wall.
- **Code:** 2.8 mm band; diffuse only.
- **Move:** Band ~0.35–0.5 mm; oriented `dot(n,L)` ~5–8% Skin lift.

## Citations

Dixon & Gibson 2014 · Xu 2021 · Shang 2024 · Tsuyama 2023 · Speichinger 2026 · Peng 2021 · Yoshimura 2025 · Liese · Ahvenainen 2017 · UV weathering PMC6030306.

See plan and [bamboo-appearance-research.md](bamboo-appearance-research.md) for full DOI list.
