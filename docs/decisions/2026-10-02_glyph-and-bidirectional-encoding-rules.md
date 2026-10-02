# Decision: encoding rules for glyphs and bidirectional height

**Date:** 2026-10-02
**Status:** Adopted (changeable — each rule is in one place in code)
**Affects:** `Visualization/Renderers/{Bar,StackedBar,Radial}GlyphRenderer.cs`,
`GlyphGeometry.cs`, `Model/HeightBinding.cs`, `Model/SegmentBinding.cs`,
`Anchors/DerivedAnchors.cs`. Usage: [docs/UNITY_GLYPHS_AND_BIDIRECTIONAL_HEIGHT.md](../UNITY_GLYPHS_AND_BIDIRECTIONAL_HEIGHT.md)

## Context

The visualization model already declared BarGlyph / StackedBarGlyph /
RadialGlyph marks, DerivedAnchors targets, Positive/Negative height roles and
multi-variable bindings, but nothing rendered them. These marks are what study
participants will read values from, so the perceptual rules matter as much as
the geometry.

## Decisions

1. **Zero baseline for bar-like marks.** Linear scales of bars, stacked bars and
   radial glyphs always include 0, so length (or area) is proportional to the
   value. HeightSurface keeps its previous behaviour (no forced zero).
2. **Radial wedges are area-true:** radius = √normalized × max radius, so the
   wedge area, which the eye compares, is proportional to the value.
3. **Shared scales where marks are compared:** a radial glyph's wedges share one
   scale over all segments; two-sided heights share one scale over both
   variables; stacked bars scale the sum.
4. **Missing ≠ zero.** No data → no glyph; zero → a flat glyph (bar tile, radial
   hub). Two-sided marks need both values; stacked bars need every segment.
5. **Signed height** comes from a Diverging height scale (centre at the base,
   symmetric), so "above/below the median" needs no new data. **Two-sided**
   height uses the existing Positive/Negative roles.
6. **Anchors** are unit centroids from the spatial-layer package; glyph size is a
   fraction of the median unit size, so glyphs are uniform.
7. **Glyphs are flat-shaded meshes chunked like the city**, with triangle → unit
   maps, so picking (roadmap item 5) works for glyphs too.

## Alternatives considered

- **Radius ∝ value** (common in rose charts): overstates large values
  quadratically. Rejected.
- **Per-segment scales** in radial glyphs: shapes would not be comparable across
  wedges. Rejected.
- **Missing segment = 0** in stacked bars: misstates the total. Rejected.
- **Signed height for any scale type**: only a Diverging scale defines where
  zero height is. Rejected.
- **Per-glyph GameObjects**: 3,900 objects per layer; worse for Quest and
  picking. Rejected.

## Consequences

- Verified on real SCB 2023 data (population by age/sex, income): every bar
  height, stack boundary, wedge radius and sector matched an independent
  recomputation within 1 mm (see the 2026-10-02 changelog).
- Only one legend is shown at a time (roadmap item 6); with a surface layer
  first, a glyph layer's legend is hidden.
- Downward parts of bidirectional marks go below the analytical plane and are
  hidden by any ground geometry at that level.
