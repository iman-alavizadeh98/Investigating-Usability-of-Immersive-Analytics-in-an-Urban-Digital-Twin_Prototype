# Unity visualization scales and colour palettes

How a data value becomes a colour (or a height) in the Unity `UrbanAnalytics`
runtime, and how to configure it. Added 2026-10-02.

## Where it lives

All paths are under `Unity/City_Digital_Twin/Assets/`.

| File | Role |
|---|---|
| `Scripts/UrbanAnalytics/Visualization/Model/VisualizationSpec.cs` | `NumericScaleSpec` (scale settings), `ColorEncodingSettings` (palette / gradient), enums `ScaleType`, `ScaleDomainMode`, `DivergingCenterMode` |
| `Scripts/UrbanAnalytics/Visualization/Model/VisualizationScaleUtility.cs` | `ResolvedNumericScale` (value → [0, 1]) and `VisualizationScaleUtility.Resolve` (builds it from data) |
| `Scripts/UrbanAnalytics/Visualization/Model/ColorPalette.cs` | `ColorPalette`, `ColorPaletteLibrary` (built-in presets) |
| `Scripts/UrbanAnalytics/Visualization/VisualizationLegendInfo.cs` | `ForColorEncoding`: the single legend builder used by every renderer |
| `Scripts/UrbanAnalytics/Data/DataLayer.cs` | `TryGetNumericValues`: all valid values of a variable |
| `Tests/Editor/UrbanAnalytics/ScaleAndPaletteTests.cs` | EditMode tests (22) |

## Pipeline

```
value ──scale──▶ t ∈ [0,1] ──(reverse)──▶ palette or custom gradient ──▶ Color32
                      └──────────────────────────────▶ height = t × maximumVisualHeight
```

The scale is resolved once per layer from the variable's valid, finite values
(no-data units are excluded). The same `Normalize` result drives colour and
height. Units without a value always get the encoding's **no-data colour** /
zero height; scales never see them.

## Scale settings (`NumericScaleSpec`)

### Domain: which input range is used (`domainMode`)

| Value | Meaning |
|---|---|
| `DataMinMax` (0) | Smallest to largest valid value. Sensitive to outliers. |
| `Manual` (1) | `manualMinimum` – `manualMaximum`. Use for reproducible study conditions across datasets. |
| `Percentile` (2) | `lowerPercentile` – `upperPercentile` (default 2–98) of the valid values. Values outside are clamped to the ends. Percentile definition = linear interpolation (same as `numpy.percentile`). |

### Type: how the domain maps to [0, 1] (`type`)

| Value | Mapping | Notes |
|---|---|---|
| `Linear` (0) | `(v − min) / (max − min)` | Default; old specs without `type` are Linear. |
| `Log` (1) | `log10` | Needs a positive domain. A data-driven domain whose minimum is ≤ 0 starts at the smallest positive value instead; values ≤ 0 clamp to the low end and are counted in `ClampedLow`. A manual minimum ≤ 0 is an error. |
| `Diverging` (2) | centre → 0.5 | Centre from `divergingCenter` (`divergingCenterMode = Manual`) or the data median (`DataMedian`). **Symmetric:** the range becomes centre ± the larger of the two distances, so equal distances above and below the centre look equally strong. The legend shows that symmetric range. **Not allowed for Height** (a centre at half height would read as "medium"; signed heights need the future bidirectional-height mark). |
| `Quantile` (3) | stepped | `quantileClasses` (2–12) classes with (about) equal unit counts. Class *i* of *N* maps to *i / (N − 1)*. Ignores `domainMode`: the classes come from the whole distribution. Ties can leave a class empty. |

`ResolvedNumericScale` also reports `ClampedLow` / `ClampedHigh` (valid values
outside the range) and a short `Description` used by the legend.

## Palettes (`ColorEncodingSettings.paletteId`)

Set `paletteId` to a library ID; leave it **empty** to use the custom Unity
`Gradient` field (previous behaviour). An unknown ID is an error, so a typo
cannot silently fall back to another colour map. `reverse` flips any palette.

| Kind | IDs | Use for |
|---|---|---|
| Sequential | `viridis`, `magma`, `blues`, `ylorrd`, `ylgnbu` | ordered values, low → high |
| Diverging | `rdbu`, `brbg`, `puor` | values around a meaningful centre (with a Diverging scale) |
| Categorical | `tableau10`, `set2`, `okabe_ito` | unordered categories (no blending; `GetCategory(i)` wraps) |

Sequential/diverging palettes interpolate linearly in sRGB between evenly
spaced stops; diverging palettes have an odd number of stops so the neutral
colour sits exactly at 0.5. `okabe_ito`, `viridis` and the ColorBrewer
sets are colour-blind friendly.

**Pairing check.** `ColorPaletteLibrary.CheckCompatibility` warns (console) on
poor pairings: sequential palette + Diverging scale, diverging palette +
non-diverging scale, categorical palette + any numeric scale.

**Categorical status.** The categorical palettes and `GetCategory` exist, but no
renderer encodes a categorical (string) variable yet, because no categorical
dataset or categorical scale exists in the runtime. They are ready for that.

**Sources / licences.** ColorBrewer 2.0 (Cynthia Brewer, Apache 2.0):
blues, ylorrd, ylgnbu, rdbu, brbg, puor, set2. matplotlib (CC0): viridis,
magma, sampled at 9 points. Tableau 10 (Tableau Software). Okabe & Ito (2008).

## Legend

`VisualizationLegendInfo.ForColorEncoding` samples 256 colours through
`scale.LegendPositionToNormalized` and the encoding, so the legend is exactly
what is drawn on the map: stepped for quantile (point-filtered texture),
reversed if the encoding is. The unit line shows the scale, e.g.
`SEK · 2-98 percentile`. Min/max labels show the scale range (for Diverging
the symmetric range, for Quantile the data range).

## Example (Inspector or JSON)

```json
"scale": { "domainMode": 2, "lowerPercentile": 2, "upperPercentile": 98, "type": 0 },
"color": { "paletteId": "viridis", "reverse": false }
```

Diverging around the median:
`"scale": { "domainMode": 2, "type": 2, "divergingCenterMode": 1 }`, `"color": { "paletteId": "rdbu" }`.

## How to verify

1. EditMode tests: Unity Test Runner → EditMode → `UrbanAnalytics.Tests`, or
   with the Editor open: `unity command run_tests --mode EditMode --filter UrbanAnalytics.Tests`.
2. In play mode the console logs each layer's scale through the legend; a
   palette/scale mismatch logs a warning; an unknown palette or invalid scale
   settings throw with the layer named.

## Failure cases

| Situation | Behaviour |
|---|---|
| No valid values for the variable | throws (`no valid values to build a scale from`) |
| Percentile bounds not `0 ≤ lower < upper ≤ 100` | throws |
| Log with no positive value / manual min ≤ 0 | throws |
| Quantile classes outside 2–12 | throws |
| Diverging scale on Height | throws `NotSupportedException` |
| Unknown `paletteId` | throws |
| All values equal (min = max) | every unit maps to 0.5 |
