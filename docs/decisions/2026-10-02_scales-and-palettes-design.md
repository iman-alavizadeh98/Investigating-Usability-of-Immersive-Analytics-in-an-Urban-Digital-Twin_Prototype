# Decision: numeric scales and a code-defined palette library

**Date:** 2026-10-02
**Status:** Adopted
**Affects:** `Unity/.../Visualization/Model/VisualizationSpec.cs`,
`VisualizationScaleUtility.cs`, `ColorPalette.cs`, `VisualizationLegendInfo.cs`,
`UI/VisualizationLegendView.cs`. Usage: [docs/UNITY_SCALES_AND_PALETTES.md](../UNITY_SCALES_AND_PALETTES.md)

## Context

Colour and height scales only supported the data min–max or a manual range.
With the test income data (−3,975,825 to 15,712,449 SEK) the min–max scale put
99.7% of the 3,919 cells into one tenth of the colour range: buildings showed
only 48 distinct colours. Colours came from one Unity `Gradient` per encoding
(max 8 keys), and legends copied that gradient, so they could not show stepped
scales.

## Decision

1. **Scale = domain × type.** `domainMode` picks the input range (DataMinMax,
   Manual, Percentile); `type` picks the mapping (Linear, Log, Diverging,
   Quantile). Linear is the default, so existing specs behave exactly as before
   (verified: identical colours on all 201,594 buildings).
2. **Diverging is always symmetric** about its centre, so the two sides are
   visually comparable. It was first rejected for Height; later the same day it
   became the signed bidirectional height (see
   [2026-10-02_glyph-and-bidirectional-encoding-rules.md](2026-10-02_glyph-and-bidirectional-encoding-rules.md)).
3. **Quantile is stepped** and uses the whole distribution (ignores the domain).
4. **Palettes are code-defined presets addressed by string ID**
   (`ColorEncodingSettings.paletteId`), not ScriptableObject assets. An empty ID
   keeps the custom `Gradient`.
5. **Legends get a pre-sampled colour ramp** built by one shared function
   (`VisualizationLegendInfo.ForColorEncoding`) that pushes legend positions
   through the same scale and palette as the renderers.

## Alternatives considered

- **Palettes as ScriptableObject assets.** Editable in the Inspector, but
  study scenarios are meant to move to external files (roadmap item 12), where
  a string ID is portable and an asset GUID is not. Presets are also published
  colour maps that should not be edited by accident. Custom colours remain
  possible through the gradient field.
- **Quantile as a domain mode.** Rejected: quantile classification changes the
  *output* (stepped), not only the input range.
- **Asymmetric diverging** (each side stretched to its own extent). Rejected:
  it exaggerates the shorter side.
- **Keep `Gradient` in the legend.** Cannot represent stepped classes and limits
  palettes to 8 keys.

## Consequences

- Scale maths is a pure function over `double[]`, covered by 22 EditMode tests
  (`Assets/Tests/Editor/UrbanAnalytics/`) — the first automated tests on the
  Unity side.
- `DataLayer.TryGetNumericValues` returns every valid value (3,919 for the
  current layer; cheap). A very large layer would want the sorted values cached.
- Categorical palettes exist but are not yet used by any renderer (no
  categorical variable or categorical scale in the runtime).
- `VisualizationLegendInfo` no longer exposes `Gradient` / `ReverseGradient`.
