# Decision: move grid statistics to voting districts through buildings (dasymetric)

**Date:** 2026-10-07 · **Status:** implemented (`Src/pipelines/unity_package/links.py`)

## Context

The evaluation compares population, income and voting. The three come on different units:
- votes per voting district (*valdistrikt*);
- population and income on the SCB grid (250 m / 1 km);
- median income only per DeSO area.

DeSO areas and voting districts are about the same size but nest only partly: 34 of 68 DeSO areas lie ≥ 90 % inside one district. Moving numbers between two sets of similar-sized, overlapping areas would split large parts of every area by guesswork.

## Decision

**Voting districts are the shared unit for comparisons with votes.** Votes stay exact. Grid counts are moved to the districts through the buildings:

1. **Weights.** Each residential building (`object_type_category = Residential`) gets the weight
   `footprint area × floors`, with `floors = max(1, round(height / 3.0 m))`. The storey height is set in the city config (`estimates.storeyHeightM`).
2. **Allocation.** Each grid count (population, households, income sum, born abroad, 65+, lowest income quartile, ...) is split over the residential buildings of its cell in proportion to the weights. The building belongs to the cell containing its representative point.
3. **Fallbacks.**
   - A cell with no residential building spreads over all its buildings.
   - A cell with no building at all is assigned whole to the district containing the cell's representative point.
4. **Aggregation.** Building estimates are summed per voting district. Shares and means are computed **after** summing (e.g. mean income = Σ income sum / Σ households). A median is never moved.
5. **Coverage rule.** Districts less than 99 % inside the area covered by SCB data get **no** estimate (`estimates.minCoveragePct`). Their population would be incomplete while their votes are complete.

DeSO data is kept on DeSO areas as its own layer, for median income and population change. It is not joined to the votes.

## Alternatives considered

| Option | Why not |
|---|---|
| DeSO as the shared unit (move votes to DeSO) | Votes, the most sensitive variable, would all become estimates; the overlap is coarse. |
| Area-weighted interpolation grid → district | Assumes people are spread evenly over a cell, including parks and industry; the documented weak baseline. |
| 100 m grid as the weight | Only total population exists at 100 m; it is used instead as the independent check. |
| Each dataset on its own unit, linked only through buildings | Fine for looking things up, but no comparison of variables on one unit. Building links are exported anyway. |

## Consequences

- **Accuracy.** Building estimates summed into SCB's 100 m cells reproduce SCB's own 100 m totals with **R² = 0.76** (2,212 cells, mean error 15 persons per cell). Lwin & Murayama (2009) report R² ≈ 0.95 against true building populations for the same method; our check is against perturbed grid data, not true values.
- **Plausibility.** Median eligible voters per estimated resident is **0.81** (about 0.8 expected, because minors cannot vote).
- **Coverage.** 60 of 92 districts get estimates. The others show election data only.
- **Labelling.** Every estimated variable is labelled `estimate` in its display name, and `[method: dasymetric_estimate]` in the layer definition.
- **Ecological caveat.** Results describe areas, not individual voters.
- **Mixed years.** Population is from 2024, income from 2023 and the election from 2026. This is accepted and stated in every legend.
