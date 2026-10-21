# Later Larsson work relevant to Woodie

Research checked 21 October 2026. This is a source review and implementation recommendation, not a code change or a measured improvement to Woodie.

## Scope

Woodie's [README](../README.md) identifies **Procedural Texturing of Solid Wood with Knots (2022)** as its appearance reference and describes the implementation as a calibrated C# approximation for Rhino/Grasshopper. The supplied DOI, **[MiGumi (2025)](https://doi.org/10.1145/3763304)**, concerns CNC joint geometry, so its fit optimization does not directly improve Woodie's texture model.

## Useful developments after the 2022 texture paper

| Work | Established contribution | Potential use in Woodie |
| --- | --- | --- |
| [Learned Inference of Annual Ring Pattern of Solid Wood (2024)](https://doi.org/10.1111/cgf.15074) | Infers a continuous growth-time field from annotated exterior photographs, then renders annual rings and orientation-dependent fine detail. | A future workflow for matching a particular real wood block; orientation-dependent fine detail is also a useful direction for procedural preview quality. |
| [The Mokume Dataset and Inverse Modeling of Solid Wood Textures (2025)](https://mokumeproject.github.io/) | Provides 190 wood samples with photographs, ring annotations and CT data; reconstructs a consistent 3D growth field from exterior observations, then synthesizes volumetric colour through procedural or neural methods. | Best near-term research reference for calibrating procedural appearance and checking cross-section consistency. These uses are recommendations, not results already demonstrated in Woodie. |

The 2024 method uses a generic neural field trained on synthetic data and fitted to surface annotations; adopting the complete approach would add a substantial inference workflow. See the [primary paper](https://diglib.eg.org/server/api/core/bitstreams/2f38b766-a79e-4e71-9470-cc85ee10af77/content).

Mokume's procedural route supports point-wise evaluation, while its neural cellular automaton is more resource intensive. This makes the procedural route the more plausible starting point for Woodie's existing preview architecture. The authors provide [code](https://github.com/marialarsson/mokumeproject); its release status should be checked before assuming that the paper's entire pipeline is available. See also the [authors' institutional publication page](https://rgl.epfl.ch/publications/Larsson2025Mokume).

## Recommended next experiment

Keep the current runtime and build an offline comparison against photographed longitudinal, end-grain and oblique sections. Use these references to tune ring spacing, earlywood/latewood transitions, colour and orientation-dependent detail, then compare the resulting presets in Rhino. Treat each lamella as its own material region when assessing engineered products; the papers' solid-wood reconstructions do not themselves establish an engineered-timber model. This is a proposed experiment and requires visual and performance validation before a claim of improvement.

## If the intended method was MiGumi

MiGumi represents flat-end milling operations and jointly adjusts mating geometry. It does not preserve assembly sequencing automatically: its limitations discuss a joint becoming unassemblable after optimization. An assembly-path check is therefore a useful engineering addition, but is explicitly future work rather than an already delivered successor method. See [MiGumi, sections 3–4 and 7.1](https://arxiv.org/html/2510.13168v1).

The later **[Helical Box Joints (SIGGRAPH 2026)](https://s2026.conference-schedule.org/presentation/?id=pos_270&sess=sess431)** introduces a joint family supporting translational, rotational and twist-in assembly, with separate functional and aesthetic parameters. The conference description reports validation using 3D-printed prototypes. This establishes an extension of joint-design possibilities, not evidence of a generally improved MiGumi optimizer or CNC manufacturability for those helical joints. No general successor optimizer was verified in this review.
