# Data scenarios for the evaluation — what they are, how the data was processed, and the math behind them

**Date:** 2026-10-08
**Scope:** the data behind the 9 visualization views and 7 guided scenarios of the expert evaluation in the Helsingborg digital twin. It covers:
- where every number comes from;
- every transformation, with formulas;
- how values become heights and colours;
- how the expected answers were computed.

**Related documents:**
- session guide: [docs/EVALUATION_SESSION.md](../EVALUATION_SESSION.md);
- package integration report: [2026-10-08_helsingborg_analytics_package_integration.md](2026-10-08_helsingborg_analytics_package_integration.md);
- decisions: [dasymetric estimate](../decisions/2026-10-07_dasymetric-grid-to-district.md), [rank scales and tiled columns](../decisions/2026-10-08_rank-scales-tiled-columns-raised-buildings.md).

> **Licence note.** The SCB statistics are licensed for research and education only (FUK). This report contains aggregate figures and district names, no tables of values. Do not publish derived tables without checking the licence.

---

## 1. Summary

The evaluation compares three kinds of urban data in one 3D city model of central Helsingborg:

- **Population** (SCB, 2024): age, sex, country of birth, education, population change.
- **Income** (SCB, 2023): households by income quartile, total disposable income, median income.
- **Votes** (Valmyndigheten, municipal election 2026): turnout and vote share per party.

They come on **different spatial units** (grid squares, DeSO areas, voting districts) and in **different years**. The processing keeps every value on its original unit where possible and moves data between units only through one documented step: the population/income estimate per voting district. It also marks every derived value as derived or estimated.

The study accepts the year differences, as decided by the user: the thesis evaluates the system, not the data. Every legend states the year.

---

## 2. The data sources

| Dataset (Swedish → English) | Provider | Spatial unit | Year | Used variables (examples) |
|---|---|---|---|---|
| *Befolkning* → Population | SCB via SLU GET | SCB grid (*Ruta*) 250 m / 1 km; DeSO | 2024 | `Totalt`, age groups (`Alder_*`), sex (`Man`, `Kvinnor`), country of birth (`Sverige`, `Norden_uto`, `EU_utom_No`, `Ovriga_var`), education 25–64 (`Forgymn` … `Eftergymn3`), population change (DeSO: `F_Till`, `F_Fran`, `Inv`, `Utv`, `Fodda`, `Doda`) |
| *Inkomster* → Income | SCB via SLU GET | SCB grid; DeSO | 2023 | households 20+ (`Totalt`), income quartiles (`Kvartil1–4`), sum of economic standard (`Tot_CDISP0`, grid only), median income (`MedianInk`, DeSO only) |
| *Röster per distrikt* + *Valdistrikt* → Votes per voting district + boundaries | Valmyndigheten | voting district (*valdistrikt*) | 2026 | votes per party, valid/invalid votes, votes cast, eligible voters |
| *Byggnad* + heights → Buildings | Lantmäteriet + own height pipeline | building footprint | footprints 2025, roof heights 2018 | footprint, height, building type (residential, industrial, …) |

**Economic standard** (*ekonomisk standard*) is SCB's household income measure: disposable income per consumption unit. It adjusts household income for household size and composition.

**Coverage.**
- The SCB data and the buildings cover the same **70 km² rectangle** of central Helsingborg.
- The election data covers the **whole municipality (425 km²)**.
- Of the 92 voting districts, 60 lie (almost) fully inside the rectangle; the others are partly or entirely outside.

---

## 3. The spatial units

### 3.1 SCB grid cells (*Ruta*)

SCB publishes grid statistics on squares. In built-up areas the squares are 250 m, elsewhere 1 km. Each cell has a 13-digit ID: the easting (6 digits) and northing (7 digits) of its **south-west corner** in SWEREF 99 TM (EPSG:3006).

**Geometry rebuilt from the ID.** For an ID $c$ and size $s$:

$$E = \lfloor c / 10^7 \rfloor, \quad N = c \bmod 10^7, \quad \text{cell} = [E, E+s] \times [N, N+s].$$

This undoes the cut at the delivery edge: 21 cells were clipped by the delivery rectangle while their values describe the whole cell. It also removes millimetre noise in the delivered polygons.

**1 km cells as remainders.** A 1 km cell overlaps the 250 m cells inside it, but SCB gives it only the population *outside* those cells. Drawn as a full square, it would cover the 250 m cells with a wrong, small value. Each 1 km cell is therefore drawn as its square minus every 250 m cell:

$$R_{1\text{km}} = S_{1\text{km}} \setminus \bigcup_{j} S_{250,j}.$$

Example: a 1 km cell with seven 250 m cells inside has area $1 - 7 \cdot 0.0625 = 0.5625$ km².

**Result:** 501 cells (487 × 250 m, 14 × 1 km, 7 of them remainders). They tile without overlap, which is what lets the columns fit together "like a puzzle".

**Unit ID:** `ruta:<size>_<ID>`, e.g. `ruta:250_3572506212750`. The size is part of the key, because a 250 m cell and a 1 km cell can share a corner and therefore an ID.

### 3.2 DeSO areas

DeSO (*demografiska statistikområden*) are SCB's statistical neighbourhoods, with 700–2,700 residents each.
- **Geometry:** used as delivered. 68 areas have geometry; 2 were cut away completely by the delivery rectangle and are left out.
- **Values:** describe the whole DeSO, even where its polygon is cut.
- **The only source of median income.**

### 3.3 Voting districts (*valdistrikt*)

- **Districts:** 92 in Helsingborg, typically 1,000–1,700 eligible voters each.
- **Collection district** (*uppsamlingsdistrikt*, code `128300`): votes counted centrally, such as late postal votes. It holds 2,067 votes (2.2 %) but has no area, so it is not drawn. Its votes still count in the municipal totals.

### 3.4 Buildings and links between units

Every building is linked to the grid cell, DeSO area and voting district that contain its **representative point**. That is a point guaranteed to lie inside the footprint, unlike the centroid, which can fall outside an L- or U-shaped building. A building outside every populated grid cell (industry, harbour, parks) gets no grid link. Link counts:

| Link | Buildings linked | Not linked |
|---|---|---|
| building → grid cell | 40,043 | 3,938 |
| building → DeSO area | 43,981 | 0 |
| building → voting district | 43,981 | 0 |

**Place names.** Grid cells and DeSO areas have no names, only codes. For the info panel, each one gets the **name of the voting district containing its representative point**, e.g. "250 m grid cell · in Eneborg V". These names are for orientation only; they are not used in any calculation.

---

## 4. Processing pipeline

```
Raw data (shapefiles, xlsx, GeoJSON, GeoPackage)
   │  Src/pipelines/population, income, election   load + check + HTML views (no changes to values)
   ▼
Unity package builder  (Src/Scripts/Unity/build_unity_package.py)
   │  statistics.py  grid + DeSO layers and their data
   │  election.py    voting-district layer and election data
   │  links.py       building links, place names, district estimates (dasymetric)
   │  visualizations.py  views + scenarios (checked against the package)
   ▼
StreamingAssets/cities/helsingborg/   (git-ignored; holds licensed data)
   ▼
Unity runtime: scales → heights/colours, buildings, legends, info panel, study log
```

Every derived value is computed in Python and written to the package, with its method in the variable's description. The runtime only maps values to heights and colours. It never changes values.

---

## 5. Data manipulation, step by step

The shortcuts are labelled D1–D8 as in the evaluation research report. Section 5.4 (election variables) involves only exact arithmetic.

### 5.1 Shares: normalised over the sub-groups (D3)

SCB protects privacy with the **Cell Key Method**, which adds random noise of −3 to +3 to small counts. As a result, sub-groups rarely add up to the published total: on 366–416 of 492 grid rows they differ, by at most 12 people. Shares are therefore computed over the **sum of the sub-groups**, not over the published total:

$$\text{share}_k = 100 \cdot \frac{x_k}{\sum_j x_j}.$$

This guarantees that shares within a table add up to 100 %. It is equivalent to one-margin raking of the parts to their own sum.

Examples:
- share of children $= 100 \cdot (x_{0-6} + x_{7-15}) / \sum \text{age groups}$;
- share born abroad $= 100 \cdot (1 - x_{\text{Sweden}} / \sum \text{birth regions})$;
- share of households in the lowest income quartile $= 100 \cdot q_1 / (q_1+q_2+q_3+q_4)$.

### 5.2 Missing values are not zero (D4)

- **A unit missing from a table has no value.** SCB lists only units where people live, so missing does not mean 0.
- **Shares and means** with a zero denominator get no value.
- **30 grid cells have 0 households but a positive income sum**, a perturbation artefact. Their mean income is undefined and left empty:

$$\bar{I} = \frac{\text{Tot\_CDISP0}}{\text{households}}, \quad \text{undefined if households} = 0.$$

### 5.3 Density

$$\text{density} = \frac{\text{population}}{\text{area of the drawn cell (km}^2)}.$$

For 1 km remainder cells the area is the remainder area, so density is not diluted by the 250 m cells inside.

### 5.4 Election variables

Per voting district $d$:

$$\text{turnout}_d = 100 \cdot \frac{\text{votes cast}_d}{\text{eligible}_d}, \qquad
\text{share}_{p,d} = 100 \cdot \frac{v_{p,d}}{\text{valid votes}_d}.$$

- **Deviations** from the municipal result, in percentage points, for diverging maps: $\Delta_{p,d} = \text{share}_{p,d} - \text{share}_{p,\text{municipality}}$. The municipal value includes the collection district (municipal turnout 79.6 %).
- **Largest party:** $\arg\max_p \text{share}_{p,d}$ (S in 52 districts, M in 34, SD in 6).
- **Named parties:** the eight Riksdag parties and Helsingborgspartiet have their own variables. The 20 parties under 0.5 % of the municipal vote are summed into "other".

The election workbook was checked before use, and all checks pass:
- party votes add up to the valid votes;
- valid + invalid votes equal votes cast;
- the wide and long sheets agree;
- turnout recomputes exactly.

### 5.5 Estimates per voting district (D1 + D2, dasymetric)

**Problem.** Votes exist per district; population and income exist per grid cell. The two do not nest, so a cell can straddle a district border.

**Method.** Move the grid counts to the **residential buildings** of each cell, then sum the buildings per district. Each residential building $i$ gets a weight equal to its approximate floor area:

$$w_i = A_i \cdot \max\!\left(1,\ \operatorname{round}\!\left(\frac{h_i}{3\ \text{m}}\right)\right),$$

where $A_i$ is the footprint area and $h_i$ the measured height. 3 m is the assumed storey height, and a building without a measured height counts as 1 storey. A cell's count $X_c$ (people, households, income sum, …) is split in proportion to the weights:

$$x_i = X_c \cdot \frac{w_i}{\sum_{j \in \text{res}(c)} w_j}.$$

**Fallbacks.**
- A cell with no residential building splits over all its buildings (40 cells).
- A cell with no building at all is given whole to the district containing its centre (3 cells, 53 people).

**Aggregation.** Per district, the counts are summed, and shares and means are taken **after** summing (a ratio of sums, never an average of ratios):

$$\bar{I}_d = \frac{\sum_{i \in d} I_i}{\sum_{i \in d} H_i}, \qquad \text{share}_d = 100 \cdot \frac{\sum_{i\in d} x^{(k)}_i}{\sum_{i \in d} x^{(\text{all})}_i}.$$

A median can never be moved this way. The DeSO median income therefore stays on DeSO areas.

**Coverage rule.** A district gets estimates only if at least 99 % of its area lies inside the SCB rectangle. Otherwise its population would be incomplete while its votes are complete. 60 of 92 districts qualify.

**Checks.**

| Check | Result |
|---|---|
| People allocated to buildings | 115,842 of 115,895 (the rest: 53 people in 3 cells without buildings, assigned to districts directly) |
| Building estimates summed into SCB's own **100 m grid** (an independent, finer table) | $R^2 = 0.76$, mean absolute error 14.8 people per 100 m cell (2,212 cells) |
| Eligible voters per estimated resident (median over 60 districts) | 0.81 (≈ 0.8 expected, because minors cannot vote) |

The coefficient of determination is

$$R^2 = 1 - \frac{\sum_k (\hat{P}_k - P_k)^2}{\sum_k (P_k - \bar{P})^2},$$

with $P_k$ SCB's 100 m population and $\hat{P}_k$ the sum of the building estimates in that cell. A cell with an estimate but no SCB row counts as $P_k = 0$.

### 5.6 What was *not* changed

- **No invented values.** Nothing was synthesised or imputed beyond the rules above.
- **No years were adjusted:** population 2024, income 2023, election 2026.
- **The raw files were never modified;** every step reads them and writes new files.

---

## 6. From values to heights and colours

### 6.1 Rank scale (used in every evaluation view)

With raw min–max scaling, a few extreme values (tiny cells, outliers) squeeze everything else into a few shades and nearly flat columns. The views therefore use a **percentile-rank scale**.

Let $v_{(0)} \le \dots \le v_{(n-1)}$ be the sorted valid values. A value $v$ maps to

$$r(v) = \frac{\operatorname{pos}(v)}{n-1} \in [0, 1],$$

where $\operatorname{pos}(v)$ is:
- the mean position of the values equal to $v$ (ties share a position);
- interpolated between neighbours when $v$ falls between two data values: $\operatorname{pos} = k + \frac{v - v_{(k)}}{v_{(k+1)} - v_{(k)}}$;
- 0 at the minimum and $n-1$ at the maximum.

Example: values $\{1, 2, 3, 4, 1000\}$ map to $\{0, 0.25, 0.5, 0.75, 1\}$. A linear scale would map 1–4 to almost 0.

**Trade-off:** the **order** is exact, the **distances** are not. The legend therefore shows the real minimum, **median** ($= v$ at $r = 0.5$) and maximum. Exact values are always in the info panel.

### 6.2 Height

$$h(v) = h_{\min} + r(v) \cdot (h_{\max} - h_{\min}),$$

with $h_{\max}$ = 200–300 m and $h_{\min}$ = 12–20 m in the presets. The 3D model uses 1 Unity unit = 1 km. Every unit with data rises at least $h_{\min}$; units without data stay flat.

**Two-sided columns** (S5, S6) put a second variable downward with its own scale:

$$\text{column} = [-h^{\downarrow}(u),\ h^{\uparrow}(v)].$$

The scales are independent because the two variables have different units, e.g. people/km² up and % down.

### 6.3 Colour

$$\text{colour}(v) = \text{palette}(r(v)).$$

The palettes are perceptual, sampled at 256 steps: viridis, YlGnBu, YlOrRd, magma, Blues. With the rank scale every step is used. A unit without data gets a grey no-data colour.

### 6.4 Columns and buildings

- **Columns:** each unit becomes a column with its **exact footprint** (full extrusion), so neighbouring columns touch.
- **Buildings:** each building is lifted by the top height of the column of *its own* unit in the extruded layer (via the building links), so it stands on top. Building colour can come from a different unit, e.g. district turnout on grid columns. The position always comes from the column the building stands on.

### 6.5 Rank in the info panel

The info panel states each value's rank among all units of its layer as a mid-rank percentile:

$$p(v) = 100 \cdot \frac{\#\{v_j < v\} + \tfrac{1}{2}\#\{v_j = v\}}{n},$$

shown as "higher than $p$ % of areas", or "highest/lowest of all areas".

### 6.6 How the expected answers were checked

Relations between two variables use the **Spearman rank correlation** $\rho$: the Pearson correlation of the ranks,

$$\rho = \frac{\sum_k (R_k - \bar R)(S_k - \bar S)}{\sqrt{\sum_k (R_k - \bar R)^2 \sum_k (S_k - \bar S)^2}},$$

where $R_k, S_k$ are the ranks of the two variables in unit $k$. $\rho$ near +1 means both rise together, near −1 means one rises as the other falls, and near 0 means no monotone relation. It matches the rank scales of the views, because it also looks only at order.

---

## 7. The scenarios

The tables use these abbreviations: **v** = value of, **est** = district estimate (5.5), **pp** = percentage points. Every relation described here is a pattern between **areas**, not individuals: high-income areas voting a certain way does not mean high earners vote that way.

### 7.1 Showcase views (free exploration; no question)

| View | Unit | Height | Colour | Buildings | Datasets |
|---|---|---|---|---|---|
| **X1 Population density** | grid | v(density 2024) | same | raised, neutral | 1 |
| **X2 Population + income** | grid | v(density 2024) | v(mean economic standard 2023) | raised, neutral | 2 |
| **X3 Born abroad + income + turnout** | grid | v(share born abroad 2024) | v(mean economic standard 2023) | raised, coloured by district turnout 2026 | 3, on two unit types |

### 7.2 Guided scenarios

| Id | View | Encodings | Question | Expected answer | How it was derived |
|---|---|---|---|---|---|
| **T0** | X2 | as X2 | Training: navigate, hover, select, compare | — | — |
| **S1** | Turnout by district | district colour = turnout; buildings coloured the same | Which district has the **lowest** turnout? | **Furutorp** (57.6 %) | Sorted turnout: Furutorp 57.6 %, Eneborg M 58.5 %, Högaborg V 59.0 %, Söder 60.2 %. Highest: Rydebäck S 93.4 %. Median 78.9 %. |
| **S2** | Income vs turnout | district height = est. mean income; colour = turnout | Do the lowest-income districts have lower or higher turnout? | **Lower** | 60 districts split into income thirds: median turnout 64.5 % (low), 77.0 % (middle), 86.0 % (high). $\rho = 0.91$. |
| **S3** | Income vs SD | height = est. mean income; colour = SD share | Where is the SD share highest? | **Middle-income districts** | Median SD share by income third: 15.1 % (low), **19.9 %** (middle), 17.2 % (high). The five highest SD shares sit at income ranks 18–31 of 60; the lowest are in the poorest districts (Furutorp 6.8 %). $\rho = 0.21$ (weak). A "surprise" task: tests whether the system helps check an assumption. |
| **S4** | DeSO | height = median income; colour = share born abroad | How do the two relate? | **Higher income where fewer are born abroad** | 68 DeSO areas, $\rho = -0.93$. Highest median income 411,400 SEK with 12.8 % born abroad; lowest 198,693 SEK with 60.2 %. |
| **S5** | Four variables | grid up = density; down = share 65+; colour = mean income; buildings = district turnout | Which description fits the densest cells? | **Low income, few residents 65+, low-turnout district** | The six densest cells (21,600–23,700 people/km²) all have below-median income (median cell: 354,941 SEK). Five of six have a below-median share of 65+ (median 17.6 %). Five of six lie in districts with about 60 % turnout (Söder, Eneborg, Högaborg). |
| **S6** | Children vs 65+ | grid up = share of children 0–15; down = share 65+ | How deep do the tall columns go? | **Shallow: where children are many, older residents are few** | 361 cells with ≥ 50 residents: $\rho = -0.63$. Median 65+ share is 26.4 % in the quarter of cells with the fewest children and 11.2 % in the quarter with the most. |

S1–S5 cover the main task types of geovisualization studies:
- find an extreme value (S1);
- judge an association with two encodings (S2, S4);
- check a counter-intuitive pattern (S3);
- read a multi-variable profile (S5, S6).

S5 is the four-variable view; S4 is the DeSO view.

---

## 8. Limitations and assumptions

| Limitation | Effect | Handling |
|---|---|---|
| Different years (2023/2024/2026) | Relations mix years | Accepted; every legend shows the year |
| SCB noise (±3 per count) | Small cells are noisy; shares in tiny cells are extreme | Shares over sub-group sums (5.1); rank scales stop tiny cells dominating |
| Cut edge cells and DeSO areas | Value describes more area than is drawn | Grid squares rebuilt from IDs; DeSO noted |
| District estimates are modelled | Assumes residential floor area is equally occupied; 3 m storeys | Checked against the 100 m grid ($R^2 = 0.76$); labelled "estimate"; only districts ≥ 99 % covered |
| Rank scales | Distances between values not shown by height/colour | Legend shows the median; the info panel shows exact values |
| Area-level patterns only | Results describe areas, not people | Task wording uses areas; noted in the briefing |
| Inferred meanings | Some field meanings (population change, quartile boundaries) are not confirmed by SCB | Marked "inferred" in the field dictionaries; not used in scenario answers |
| Election licence not stated in the files | Publishing results | Check before publishing |

---

## 9. Reproducing

```bash
# Build the city package (buildings, grid/DeSO/district layers, links, estimates, views)
python Src/Scripts/Unity/build_unity_package.py --config configs/cities/helsingborg.json
```

```bash
# Tests (synthetic data)
python tests/test_unity_analytics.py
```

- **Parameters:** `configs/cities/helsingborg.json` (storey height 3 m, coverage 99 %, minimum party share 0.5 %).
- **Views and scenarios:** `configs/visualizations/evaluation/`.
- **Full run report:** `<package>/analytics_report.json`.
