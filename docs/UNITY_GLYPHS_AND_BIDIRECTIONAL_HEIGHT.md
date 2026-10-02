# Unity glyphs, derived anchors and bidirectional height

How to use the glyph marks (`BarGlyph`, `StackedBarGlyph`, `RadialGlyph`),
`DerivedAnchors` targets, multi-variable bindings and bidirectional height in
the `UrbanAnalytics` runtime. Added 2026-10-02. Scales and palettes:
[UNITY_SCALES_AND_PALETTES.md](UNITY_SCALES_AND_PALETTES.md).

## Where it lives (`Unity/City_Digital_Twin/Assets/Scripts/UrbanAnalytics/Visualization/`)

| File | Role |
|---|---|
| `Anchors/DerivedAnchors.cs` | `DerivedAnchorSet`: one anchor per spatial unit |
| `Model/HeightBinding.cs` | `ResolvedHeightBinding`, `HeightExtent`, `HeightExtentMath`: up / signed / two-sided heights (HeightSurface and BarGlyph) |
| `Model/SegmentBinding.cs` | multi-variable Segments encoding → per-segment values, colours, legend |
| `Renderers/GlyphGeometry.cs` | `GlyphMath` (pure), `GlyphMeshAccumulator` (boxes, wedges), `GlyphRendering` (root, placement, chunks) |
| `Renderers/BarGlyphRenderer.cs`, `StackedBarGlyphRenderer.cs`, `RadialGlyphRenderer.cs` | the three glyph marks |
| `Renderers/HeightSurfaceRenderer.cs`, `HeightSurfaceMeshBuilder.cs` | now bidirectional-capable |
| `Model/VisualizationSpec.cs` | `GlyphSettings`, `BidirectionalHeightSettings`, `DataBinding.TryGetMultiple` |
| `Assets/Tests/Editor/UrbanAnalytics/GlyphAndHeightTests.cs` | EditMode tests |

## Derived anchors (`Target.Kind = DerivedAnchors`)

- `Target.LayerId` = the spatial layer to anchor on (e.g. `ruta_250`);
  `Mapping` must be `Direct`, and every data layer used must target that
  spatial layer.
- One anchor per unit, at the unit's **centroid as stored in the spatial-layer
  package** (`geometry.json`), converted by `SpatialReferenceManager` and placed
  on the rendered layer root, i.e. on the analytical surface plane.
- **Typical size** = median of √(bounding-box area) over all units (250 m for
  `ruta_250`). Glyph sizes are fractions of it, so glyphs are uniform and do not
  shrink in clipped boundary cells.
- Placement: `urbanContextPlacement` (`Fixed`, or `FollowHeightSurface` with
  `sourceVisualizationLayerId` = an earlier HeightSurface layer) works for glyph
  layers as for buildings.

## Marks

All glyphs: one mesh per ~60k vertices with per-unit semantic ranges and a
triangle → unit ID map (ready for picking), shaded vertex-colour material
(roof/top = exact colour, sides darker). Units without data get **no** glyph;
zero values get a flat glyph, so "zero" and "no data" stay distinguishable.

### BarGlyph
- **Height**: the layer's Height encoding(s), see *Bidirectional height*.
  Linear domains are **extended to zero** so bar length ∝ value.
- **Colour**: optional single-variable Color encoding (any scale/palette);
  otherwise up/down colours (bidirectional) or `glyph.defaultColor`.
- Size: square footprint, `glyph.widthFraction` × typical size (default 0.5).

### StackedBarGlyph
- **Segments** encoding, `data.mode = Multiple`, 2+ variables stacked bottom →
  top in declared order. Colours: the encoding's palette (default `tableau10`;
  a sequential palette suits ordered groups such as age bands).
- Total height = Segments scale on the **sum of segments** (Linear only, zero
  baseline) × `height.maximumVisualHeight` of the Segments encoding. Each
  segment's share of the height = its share of the sum. (SCB perturbs small
  counts, so the sum need not equal a published total.)
- A unit with any missing or negative segment gets no bar (counted in the log).

### RadialGlyph
- **Segments** encoding as above. Equal-angle wedges, clockwise from north in
  declared order, small gaps between wedges.
- One shared scale over the pooled values of all segments (any type except
  Diverging; Linear gets a zero baseline). **Radius = √normalized × max radius**,
  so wedge *area* ∝ value. Max radius = `glyph.radiusFraction` × typical size
  (default 0.45). Flat: `glyph.radialThickness` (default 0.002 = 2 m).
- A missing value leaves a gap; a neutral hub (no-data colour) marks every unit
  with data.

## Multi-variable binding

`DataBinding.mode = Multiple` (1) with a `variables` list. Read via
`DataBinding.TryGetMultiple` (needs ≥ 2 configured variables). Used by the
Segments channel. Single-variable channels (Color, Height) still need
`mode = Single` with exactly one variable.

## Bidirectional height (HeightSurface and BarGlyph)

| Mode | How to configure | Result |
|---|---|---|
| Unidirectional | one Primary Height encoding, non-diverging scale | upward (as before) |
| **Signed** | one Primary Height encoding with a **Diverging** scale | centre value at the base; above goes up, below goes down; ±max height at the symmetric range ends |
| **Two-sided** | one **Positive**-role and one **Negative**-role Height encoding (no Primary) | Positive variable up, Negative variable down, **one shared scale** over both variables' pooled values (Linear or Log); the Positive encoding's scale and max height apply to both |

- Two-sided units need **both** values; otherwise no mark (a missing side is
  never drawn as zero).
- Colour: a Color encoding colours the whole mark; otherwise the upward part uses
  `bidirectional.positiveColor` (default RdBu blue #2166ac) and the downward part
  `negativeColor` (RdBu red #b2182b), and the legend lists the two directions.
- HeightSurface methods: Full and Inset split the column at the base into an up
  and a down part. **SurfaceDisplacement** works for Signed (plateau at the
  signed level) but **not** Two-sided (one plateau cannot show two values).
  **DownwardExtrusion** rejects any bidirectional binding.
- Followers (buildings, glyphs) stand on the **upward** top (offset ≥ 0).
- Downward parts go below the analytical plane; scene ground geometry at that
  level hides them.

## Example (JSON)

```json
{ "id": "age_stack", "mark": 3,
  "target": { "kind": 2, "layerId": "ruta_250", "mapping": { "mode": 0 } },
  "encodings": [ { "channel": 4, "data": { "mode": 1, "variables": [
      { "dataLayerId": "population_age_2023", "variableId": "age_0_6" },
      { "dataLayerId": "population_age_2023", "variableId": "age_7_15" } ] },
    "scale": { "domainMode": 0, "type": 0 },
    "height": { "maximumVisualHeight": 0.3 },
    "color": { "paletteId": "viridis" } } ] }
```

Two-sided bars: two Height encodings (`"channel": 1`) with `"role": 1`
(Positive, e.g. `population_sex_2023.men`) and `"role": 2` (Negative, `women`).

## Failure cases (all throw with the layer named)

| Situation | Message gist |
|---|---|
| Segments not `Multiple` with ≥ 2 variables | needs Data Mode = Multiple |
| Stacked bar with a non-Linear scale | segments are shares of a linear total |
| Radial with a Diverging scale | radius cannot be negative |
| Positive without Negative (or vice versa), or mixed with Primary | bidirectional height needs both / cannot combine |
| Two-sided with a Diverging/Quantile scale | needs Linear or Log |
| Two-sided HeightSurface with SurfaceDisplacement | plateau has one level |
| Bidirectional HeightSurface with DownwardExtrusion | cannot show bidirectional |
| Data layer targets another spatial layer | targets X, anchors come from Y |
| FollowHeightSurface source not rendered yet | put the HeightSurface layer first |
