# Decision: rank scales, tiled columns and buildings on top of extrusions

**Date:** 2026-10-08 · **Status:** implemented · **Trigger:** user review of the evaluation views

## Context

The user reviewed the first evaluation views and reported five problems:
1. buildings stayed on the ground, hidden inside extruded columns;
2. inset columns left gaps between neighbouring areas, which breaks immersion;
3. some columns looked like a cube standing in the middle of another cube. These were glyph bars sized for 250 m cells, standing inside 1 km cells, and inset columns inside their own cells;
4. with min–max scales many areas looked flat or almost identical, because a few extreme values (tiny cells, outliers) stretched the range;
5. the colour maps seemed to have too few shades.

## Decisions

1. **Buildings always stand on raised units** (`VisualizationManager.raiseBuildingsOnExtrusions`, default on). When a view has an upward HeightSurface:
   - building layers on the ground are made to follow it;
   - a view without a building layer gets an uncoloured follower layer, using the association `buildings_to_<layer>`.

   The follow now finds the building's own unit in the followed layer, even when the building colour comes from another layer (e.g. colour by voting district, columns by grid cell). Before this fix such buildings were silently not lifted.
2. **Full extrusion for all evaluation views.** Every unit is a column with its exact footprint, so neighbouring columns touch like puzzle pieces. 1 km cells are already exported as the remainder around their 250 m cells, so columns never overlap. Glyph bars are no longer used in the evaluation views; S6 became a two-sided column.
3. **Rank scales (`ScaleType.Rank = 4`) for colour and height.**
   - Each value maps to its position among all valid values (interpolated between neighbours; ties share their mean position).
   - Skewed data then uses every shade and every height evenly.
   - Order is exact; distances between values are not. The legend therefore shows the real minimum, **median** and maximum.
4. **Minimum visual height** (`height.minimumVisualHeight`): the lowest valid value still rises a little (12–20 m), so units with data never look flat or missing. Units without data stay flat.
5. **More shades.** These come from 3 (the rank scale uses all 256 samples of continuous palettes), not from new palettes.

## Alternatives considered

- **Percentile (2–98) domains with linear scales:** still bunch skewed data. Kept for other uses.
- **Quantile classes:** stepped, at most 12 classes, which reads as fewer shades.
- **Log scales:** not defined at 0 (many shares are 0) and hard to explain to experts.

## Consequences

- **Reading values.** Heights and colours show **order** (higher / lower than neighbours). Exact values come from the info panel, which shows the value, its unit and its rank in words. This fits the guided tasks, which ask about order and association, not magnitudes.
- **Legends** read "rank of N values" and show the median.
- **Partial districts.** In S2 and S3 the 32 districts without income estimates have no column, and their buildings stay on the ground.
- **Inset extrusion and glyphs** remain available in the engine, but the evaluation presets don't use them.
