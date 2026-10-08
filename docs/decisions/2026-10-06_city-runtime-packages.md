# Decision: one generated runtime package per city; buildings as GBLD v3 + attribute table

**Date:** 2026-10-06
**Status:** Adopted. Code and tests are done; the package has not yet been built from Helsingborg data.
**Affects:** `Src/Scripts/Unity/build_unity_package.py`, `Src/pipelines/unity_package/`, `configs/cities/`, Unity `ProjectManager`, `UrbanContextManager`, `BuildingPackage.cs`, `EntityInfo`, `Assets/Scenes/Helsingborg.unity`

## Context

- The project moved from Gothenburg to Helsingborg, and it should work for any city that is fed the same kinds of data.
- `project_manifest.json` was written by hand. Its origin (Gothenburg) and layer list had to be edited manually.
- The building binary (GBLD v2) had no writer in the repo. Its numeric IDs could not carry Lantmäteriet UUIDs, and it held no attributes and no holes.
- The user wants to see the building information in Unity: name, type, purpose, house number and so on.
- The two old Python exporters were quick test tools, and the user asked for them to be removed.

## Chosen option

**City config, not code.**
- Everything specific to a city lives in `configs/cities/<city>.json`: its id and name, CRS, input files, the package folder and a **fixed origin**.
- The origin is set by hand, never derived from the data, so positions and logs stay comparable between runs. This is the same rule as the analytical grid anchor.
- The Python code and the pipeline scripts have no city defaults.

**One package per city.**
- Each city's package lives in `StreamingAssets/cities/<city>/`. The whole `cities/` folder is git-ignored: the packages are generated from licensed data (FUK), so they are rebuilt, never committed. The pre-commit hook rejected the first attempt to commit one (24 MB attributes file).
- The build script writes the layers and then **generates** `project_manifest.json` from the `layer.json` files present in the package. Nothing is hand-edited, and entries can never be stale.
- Paths in a manifest are relative to its own folder. `ProjectManager.ResolvePackagePath` resolves them, so the old root-level package still works.
- A scene picks its city through `ProjectManager`'s manifest path.

**Buildings are an `urbanContext` entry.** It is written to `urban_context/buildings/`:
- `geometry.bin`, GBLD **v3**:
  - string IDs `building:<object_id>`;
  - height, ground elevation and a has-height flag;
  - polygons with holes, as float32 x/z relative to the city origin. Exterior rings are CCW and holes CW.
- `attributes.json`: the same column-wise layout as data-layer values files, with typed arrays and a `valid` mask, so Unity's existing data model fits.
- `layer.json`: the **field list** (id, label, group, type, unit, description), plus counts, provenance and checksums.

**The info panel is driven by the field list.**
- Adding a field, for example an address once address data exists, needs only a change to `BUILDING_FIELDS` in Python and a rebuild. No C# change is needed.
- Missing values are not shown. A building without a measured height shows "Height measured: No" and the reason, not "0 m".

## Alternatives considered

- **Buildings as a spatial layer** (`geometry.json`, flat polygons). This would reuse the spatial-layer loader. It was rejected because it means about 45k polygons in JSON, with no extrusion and no building identity in the urban-context model.
- **Extending GBLD v2.** Rejected: numeric IDs, no holes and no room for attributes.
- **Attributes inside the binary.** Rejected: fields would be fixed in the format, so each new field would need a format change and C# work.
- **Origin computed from the data.** Rejected: it moves whenever the data changes.

## Consequences

- Building a city package:
  ```
  python Src/Scripts/Unity/build_unity_package.py --config configs/cities/<city>.json
  ```
- float32 coordinates relative to the origin keep millimetre precision only within about 16 km. The exporter refuses data farther from the origin, so a large city needs an origin near its centre.
- **The building → Ruta association is no longer baked into the binary.** It will be computed when the Ruta layer of the city is exported, as part of the next step (data layers).
- `SampleScene` and the root-level Gothenburg package still load through the legacy path. They are historical, and nothing new is added to them.

## Update 2026-10-07: analytical layers, associations and views

The package now also holds analytical layers. They come from the config sections `statistics`, `election`, `estimates` and `visualizations` (build steps `analytics` and `visualizations`):

```
spatial_layers/{ruta,deso,valdistrikt}/   layer.json + geometry.json (MultiPolygon, hidden until a view uses them)
data_layers/<id>/                         layer.json + values.json (DataLayerFileDto)
associations/buildings_to_<layer>/        layer.json + pairs.json (sourceIds[] → targetIds[])
visualizations/                           catalog.json, presets, scenarios.json (copied from configs/visualizations/<set>/)
analytics_report.json                     counts, coverage, dasymetric validation
```

- **Associations (done).** The building → Ruta link above is now computed by Python (point inside the footprint), with links to DeSO and voting districts as well. `AssociationPackageLoader` loads them after the buildings.
- **Views** are authored in the repository, because they hold no data. The build checks every layer, variable and association a view names against the package, and fails if one is missing.
- **Coordinate precision.** Spatial layers keep EPSG:3006 doubles in JSON and are converted at load. Voting districts reach about 30 km from the origin, which float32 at 1 unit = 1 km still resolves to about 2 mm. The 16 km limit applies only to the float32 metre offsets in the building binary.
- **Details:** `docs/data-analysis/2026-10-08_helsingborg_analytics_package_integration.md`, `docs/decisions/2026-10-07_dasymetric-grid-to-district.md`.
