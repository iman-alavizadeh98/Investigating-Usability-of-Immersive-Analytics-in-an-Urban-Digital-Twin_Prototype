# Decision: repeated building IDs are merged, not dropped

**Date:** 2026-10-06
**Status:** Adopted (code changed; outputs not regenerated yet)
**Affects:** `Src/pipelines/buildings/postprocess.py`, `run_buildings_pipeline.py --postprocess`

## Context

The postprocess snapshot must have one row per `object_id`: the LiDAR height
pipeline joins on it and Unity identifies buildings by it. The old postprocess
assumed repeated IDs were older versions and kept the "newest" row.

Checked against the raw Gothenburg, Helsingborg and Fortuna deliveries:

- every repeated ID has a single version, so "newest" was an arbitrary pick;
- the rows are non-overlapping pieces of one building (Helsingborg: 1,384 of
  1,634 groups touch and form one polygon);
- they differ only in position uncertainty and collection level.

The product description (v1.6) supports this reading. It defines
`objektidentitet` as a globally unique UUID (Table 5), and since 2011 it stores
each building's map geometry and register data as one object (§2.2.1). Several
geometry rows under one UUID are therefore pieces of one register building.

Keeping one row deleted 6.3% of Helsingborg's footprint area (19% in
Gothenburg). The user wants unique, clean data without fixing records by hand.

## Chosen option

The postprocess runs three steps:

1. Drop exact duplicate rows.
2. Drop older versions, if any.
3. **Merge** the remaining pieces of each building.

A merge does four things:

- unions the geometry (`make_valid` + `union_all`, stored as a MultiPolygon);
- takes the attributes from the largest piece;
- sets `position_uncertainty_*` to the maximum over the pieces (conservative);
- records `source_part_count` and `collection_level_mixed`.

Every merged building is listed in `buildings_postprocess_parts.csv`. The step
fails if any ID is still repeated, and it reports any unexpected column that
differs between pieces.

## Alternatives considered

- **Keep the largest piece** (`--duplicate-strategy keep_largest`). Simple, but
  it still loses area and parts of buildings disappear from the 3D model. Kept as
  an option.
- **Keep the pieces as separate rows with a part index.** This preserves
  per-piece accuracy and lets each piece get its own LiDAR height, but every
  consumer would need a composite key. Rejected for now: the user asked for one
  row per building.
- **Drop all repeated IDs.** Loses whole buildings.

## Consequences

- Footprints are complete, and IDs are unique and stable (the original UUID).
- A merged building gets one height from the LiDAR pipeline, even if its pieces
  differ in height (for example a low wing next to a tower).
- Per-piece accuracy is reduced to a worst-case value. The full range stays in
  the parts CSV.
- Gothenburg outputs made before 2026-10-06 are missing building pieces. Do
  not reuse them as reference data.
