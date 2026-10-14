# Evaluation session — expert walkthrough (desktop now, VR next)

**Status (2026-10-08):** implemented and run end to end in the Editor (desktop). VR is the next step, and the session tools are built so that it can reuse them.

## Study frame

- **Participants:** 6–8 domain experts, one at a time.
- **Session:** at most **30 minutes** of system use.
- **Format:** guided scenarios with checkable answers, think-aloud and questionnaires. There is no timed benchmark: the group is too small for statistics, so results are reported descriptively.
- **Focus:** usability and the value of immersive, multi-layer visualization. That includes showing four variables at once, height + colour, buildings in context and the DeSO view. Data accuracy is not the focus. The data years differ (2023 / 2024 / 2026); this is stated in the legends and the briefing.

## Session plan (≈ 30 min)

| Min | Part | Notes |
|---|---|---|
| 0–3 | Briefing, consent | Purpose, think-aloud, "the system is tested, not you". Data are real area statistics (SCB, Valmyndigheten) and estimates; values describe areas, not people. |
| 3–6 | **T0 Training** (VR: after the locked-in tutorial) | Navigate, hover, select, compare two areas as A/B. No answer. |
| 6–24 | **E1–E5** (~3–4 min each) | Participant reads the scenario, compares the areas A/B, answers the choice questions (desktop: facilitator clicks them; VR: on the Task tab) and the open questions aloud; facilitator codes the open answers (covered / partly / not covered) and records confidence (1–5). |
| 24–28 | Questionnaires | SUS (10 items) and UEQ-S (8 items), on paper or a form. |
| 28–30 | Short interview | What was easiest/hardest; which view was most useful; would VR help, and where? |

## Scenarios

Defined in `configs/visualizations/evaluation/scenarios.json`; views in the same folder.

**Showcase views** (free exploration, demos; not in the scenario file):

| Id | View | Data |
|---|---|---|
| X1 | `x1_population`: grid height + colour = population density | 1 dataset |
| X2 | `x2_income_population`: grid height = population density, colour = mean income | 2 datasets |
| X3 | `x3_origin_income_turnout`: grid height = born abroad, colour = mean income; buildings = district turnout | 3 datasets, 2 unit types |

**All views** (2026-10-08):
- full extrusion: columns tile edge to edge;
- rank scales with a minimum height;
- buildings always stand on the columns.

Decision: `docs/decisions/2026-10-08_rank-scales-tiled-columns-raised-buildings.md`.

### Scenarios E1–E5 (since 2026-10-09; replace S1–S6)

The purpose is not only a correct number: the scenarios test whether
municipal experts can explore, compare and interpret multidimensional
spatial data in the digital twin while recognising the limits of the data.
Each scenario shows its text (verbatim from the study design), a **data
block** (variable · unit · spatial unit · year) and its sub-questions.

| Id | View (preset) | Spatial unit | Encodings | Areas A/B | Sub-questions |
|---|---|---|---|---|---|
| T0 | `x1_population` | 250 m grid | density (height + colour) | participant | training, no answer |
| E1 | `e1_density_income` | 250 m grid | height = population density 2024; colour = mean income (economic standard) 2023; buildings = context | auto: density quantiles 0.85 / 0.35 (cells ≥ 100 residents), counterbalanced | which has higher density · higher income · open: what is relevant to investigate |
| E2 | `e2_deso_income_born_abroad` | DeSO | height = median income 2023; colour = born abroad 2024 | auto: median-income quantiles 0.8 / 0.2, counterbalanced | higher income · higher born-abroad share · open: what it suggests · open: what cannot be concluded |
| E3 | `e3_grid_profile` | 250 m grid + voting districts | up = density; down = aged 65+; colour = mean income; buildings = district turnout 2026 | auto: quantiles 0.8 / 0.3 of rank(density) + rank(65+), counterbalanced | stronger density + 65+ combination · higher income · open: what turnout adds · open: what is still needed |
| E4 | `e4_sd_born_abroad` | voting districts | colour = SD share 2026 (rank); height = born abroad (estimate) | A: participant picks a high-SD district; B: auto, median SD share | (check: A in top quarter of SD share) · higher SD share · higher born-abroad share · pattern consistent or mixed · open: individual voters |
| E5 | `e5a_v_born_abroad` ⇄ `e5b_s_born_abroad` | voting districts | colour = V or S share, **same fixed 0–50 % scale and palette**; height = born abroad (estimate) in both | A: participant picks a V-strong district; B: auto, S well above its municipal result (0.85 quantile of `share_s_diff`) | (check: A in top quarter of V share) · in A, which party is relatively stronger · same in B · higher born-abroad share · open: spatial relation · open: individual voters |

Wording rules: all values describe **areas**, never people. E4/E5 make no
assumption about the direction of any relationship; the expected answers
come from the packaged data at runtime. E5 never says that immigrants vote
for V or S.

### How areas and expected answers are derived (`StudyAnswerKey`)

Nothing is chosen by hand and no answer is stored in the scenario file. When
**Show view** is pressed, the session picks the comparison areas by the
scenario's `pair` rule and puts them into slots A/B (highlighted, shown on
the comparison panel). At **Submit**, every sub-question's expected answer is
computed for the areas actually in A and B:

- **Eligible units:** valid values for every variable involved; 250 m cells
  only (`ruta:250_…`) with ≥ 100 residents in E1/E3 (small cells are noisy);
  districts with a born-abroad estimate in E4/E5.
- **Pick:** units sorted by score (value, or the sum of two percentile
  ranks), the unit at round(q · (n − 1)). A negative quantile leaves the slot
  to the participant (Compare tool / C).
- **Counterbalancing:** for half of the participant ids (FNV hash of
  participant + scenario) A and B are swapped, so "A" is not always the higher
  one. Logged.
- **compare:** "about the same" if |A − B| ≤ tolerance (5 % of the larger
  value for density and income; 2 pp for born-abroad shares; 1 pp for SD
  share), else the larger one.
- **compareCombined (E3):** percentile rank of density + percentile rank of
  65+ share among eligible cells; tolerance 0.1.
- **relativeParty (E5):** relative strength = district share / municipal
  share (municipal share = share − `share_*_diff`); tolerance 0.1.
- **pattern (E4):** Spearman ρ over all eligible districts; |ρ| ≥ 0.5 =
  consistent (positive or negative), else mixed.
- **slotCheck:** the participant's own pick lies in the top quarter of the
  variable (logged, not asked).
- **open:** answered aloud; the facilitator's code (covered / partly / not
  covered) is logged; no correctness.

Every computed answer is logged with its basis (values, ρ, counts), so it can
be checked afterwards. The scenario file is validated against the package by
the Python builder (`check_scenario`) and by the EditMode test
`StudyScenarioTests.ScenarioFile_ReferencesExistingPresetsAndVariables`.

**Info panel** (click an area or a building):
- **Header:** a readable name (district name; "250 m grid cell · in Eneborg V"; a building's type and district).
- **First, large:** the values the current view shows, each with its unit, the channel (height, colour, building colour) and its rank in words ("higher than 72 % of areas"). For a building, these are the values of its cell and district.
- **Below:** folded detail sections, opened by clicking a heading.
- **Footer:** codes and ids, small.

Expected answers are never shown to the participant. Because they are
computed at runtime, they follow data changes automatically.

## Pattern study 2D vs 3D (P1–P3, since 2026-10-14)

A second, separate study: three open pattern-finding questions, first on 2D
maps (`Processed_data/Helsingborg_Final/study/study_2d_maps.pdf`), then with
the headset on the 3D table. **20 minutes with the system, then a 10-minute
interview**; fixed order 2D → 3D, about 2 minutes per scenario. Design,
questions and expected answers: `configs/study/pattern_scenarios.json` and
`study_facilitator_handbook.pdf` (same folder as the PDF).

**Selecting it.** The PC study panel lists the studies before Start: **Pattern
study 2D/3D (P1–P3)** (default) and **Evaluation E1–E5**. Each has its own view
catalog and scenario file (`StudySession.studies`):

| Study | Views (package) | Scenarios | Authored in |
|---|---|---|---|
| Pattern 2D/3D | `visualizations/pattern/catalog.json` | `visualizations/pattern/scenarios.json` | `configs/visualizations/pattern/` (generated by `Src/Scripts/Unity/make_pattern_study_views.py` from `configs/study/pattern_scenarios.json`) |
| Evaluation E1–E5 | `visualizations/catalog.json` | `visualizations/scenarios.json` | `configs/visualizations/evaluation/` |

The package builder copies extra sets from the city config's
`visualizationSets` (`"pattern": "configs/visualizations/pattern"`) into
`visualizations/<name>/`.

**Views** (height = variable A, colour = variable B):

| Id | Unit | Height (A, rank scale) | Colour (B, 5 equal-count classes, oranges) | n (both values) |
|---|---|---|---|---|
| T0 | DeSO | residents 2024 | share aged 65+ 2024 | practice, no answer |
| P1 | DeSO | median income 2023 (`deso_income.median_income`) | employment rate 20–64 2023 (`deso_employment.employment_rate_20_64`, new) | 68 (ρ +0.92) |
| P2 | DeSO | median income 2023 | share aged 0–15 2024 (`deso_population.share_children_0_15`) | 68 (ρ −0.08) |
| P3 (since 2026-10-14, "Adult education and SFI") | **250 m grid cells with ≥ 50 residents** | share born abroad 2024 (`ruta_population.share_born_abroad`) | share with ≥ 3 years post-secondary education, ages 25–64, 2024 (`ruta_population.share_post_secondary_3y`) | 357 (ρ −0.71); **144 cells grey = no data** (1 km cells and cells under 50 residents) |
| S (optional, after P3) | 250 m grid | up = density, down = aged 65+ (the evaluation view `e3_grid_profile`) | mean income; buildings = district turnout | free exploration, not timed or scored |

P3 was voting-district turnout × estimated share born abroad (ρ −0.94, n 60)
until the user replaced it on 2026-10-14 with a grid scenario. The earlier 2D
PDFs are kept in `Processed_data/Helsingborg_Final/study/study/`.

**Unit filter (P3).** A layer's `unitFilter` (spec; `UnitFilterSettings`:
id prefix and/or a minimum of a variable) makes the other units no data in
the 3D view: no-data colour, left out of quantile classes and rank scales,
counted in the legend's "No data". The 2D builder applies the same rule from
the scenario's `unitFilter` in `configs/study/pattern_scenarios.json`
(`study.py: unit_filter_mask`), so both show the same cells and the same
classes (checked: 3D class bounds 19.6 / 27.1 / 35.0 / 43.5 %, 2D legend 20 /
27 / 35 / 43 %). Reason: shares of a handful of residents are extreme (e.g. 3 of
4 residents → 75 %). The info panel still shows a filtered cell's values.

**Showcase S (optional).** After P3, if time allows: the board explains only
**Copy** (toolbar Copy or A, click an area; carry the copy with the grip) and
**Compare** (toolbar Compare, click two areas; the Compare panel). Untimed, no
answers; copies, comparisons and tool changes are logged as usual (`vr_copy`,
`vr_compare`, `vr_tool`). F5 / Finish ends the session. Defined in
`pattern_scenarios.json` → `showcase` (3D only; the 2D builder ignores it).

**Differences from the 2D maps** (closest renderer option, written down as
required):
- **Colour B** matches: the same five oranges (`study_oranges5`), 5 quantile
  classes with about the same number of areas each. Class bounds are computed
  in Unity (percentiles with linear interpolation, like pandas' default); the 2D
  bounds are rounded to 6 decimals, so a value exactly on a bound could fall
  into the neighbouring class.
- **Height A** is a continuous **rank scale** (taller = higher rank), not the 2D
  map's 5 blue classes: the renderer has no classed height. The legend names the
  variable, unit and the median.
- **No data:** a neutral grey column top and a legend row "No data (n areas)"
  (the 2D map hatches them; 3D has no hatching). Filtered grid cells have no
  column (flat, grey): no value for either variable.
- **Context:** buildings stand on the columns (the 2D map shows roads/water).

**Flow — one action per step:**
1. The VR tutorial runs first (5 steps, target under 3 min: welcome, point and
   select, read the view, move the table, ready). F8 skips it.
2. The **study board** (in VR, beside the participant, where the tutorial card
   was) shows the question **word for word**, what height and colour show
   (variable · unit · spatial unit · year) and one button. The facilitator's
   **F5** does the same: Start → show T0 → next scenario → … → finish
   (`StudySession.Advance`).
3. Participants **think aloud**; there are no multiple-choice parts and no
   typing. The facilitator writes the answers on the handbook's answer sheet.
4. **F9** at the PC when the participant states a pattern: the first press in a
   scenario logs `first_claim` (time to first claim), later ones `claim`.

**Logged** (in addition to the general events below): `session_start`
(`study`, `studyLabel`, `catalog`, `scenarioFile`), `scenario_start`,
`first_claim` / `claim` (scenario, number, seconds since the scenario's view was
shown), `scenario_end` (seconds, claims, `firstClaimSeconds`, interactions),
`scenario_done`, `visualization_applied`, hovers, selections, camera/head pose.

## Running a session

1. **Build the package** once, after any data change:
   ```bash
   python Src/Scripts/Unity/build_unity_package.py --config configs/cities/helsingborg.json
   ```
2. **Prepare the scene.** Open `Assets/Scenes/Helsingborg.unity`. On the `StudySession` object, set **Participant Id** (e.g. `P03`) and **Condition** (`desktop`).
3. **Screen.** Use at least 1920×1080, full screen. `[` / `]` change the UI size; F2 hides the study panel.
4. **Start.** Press Play, then **Start session** (bottom-left panel). For each scenario:
   - **Show view** loads the view, starts the timer and sets the comparison areas;
   - in E4/E5 the participant puts their own area into A (select + C; VR: Compare tool);
   - E5: **Show S – Social Democrats / Show V – Left Party** switches the matched views;
   - select the answers, code the open answers and set the confidence;
   - **Submit**. Use **Skip** if needed.
5. **Finish.** After the last scenario the panel shows the log path. Stopping Play mode also closes the log.

## Logs

Logs are written to `%USERPROFILE%/AppData/LocalLow/<company>/City_Digital_Twin/study_logs/` (`Application.persistentDataPath`):

- **`<session>.jsonl`:** one JSON object per line. Each line has `t` (UTC), `s` (seconds since start), `session`, `participant`, `condition`, `event` and fields. The events:
  - `session_start`: build, Unity version, scene, manifest, scenario file, screen size;
  - `scenario_start` / `scenario_skip`;
  - `pair_assigned`: spatial layer, A and B unit ids (or "(participant)"), swapped, the pick rule;
  - `view_switch` (E5): to which view, count, A/B ids;
  - `scenario_part`: per sub-question: kind, answer (or facilitator code), expected, correct, basis (values / ρ), A/B ids;
  - `scenario_answer` / `scenario_done`: correct and scored parts, confidence, seconds, interaction count, view switches, picked and final A/B ids;
  - `visualization_applied`: view id and number of legends;
  - `hover` (when the hovered entity changes) and `select`: entity and unit ids;
  - `comparison`: A/B slot entities;
  - `camera`: position and Euler angles, at `poseHz` (default 2 Hz);
  - `session_end`.
- **`<session>_answers.csv`:** one row per sub-question (`E1.density`, …): answer, expected, correct (1/0, empty for open questions), confidence, seconds, interactions.

The logs are research data about participants. Keep them out of git, and pseudonymise participant ids.

## What was built for this (2026-10-07/08)

**Data package** (`build_unity_package.py`):
- grid, DeSO and voting-district layers;
- population, income and election data;
- per-district estimates (decision note `docs/decisions/2026-10-07_dasymetric-grid-to-district.md`);
- building links;
- the views and scenarios, checked against the package at build time.

**Unity:**
- `AssociationPackageLoader`: building → unit links from the package.
- **Two-sided heights with independent scales** (`bidirectional.independentScales`), for up and down variables with different units.
- `LegendStackView`: one legend per encoded variable, with channel, unit and year.
- Layers hidden by default (DeSO, voting districts) are shown only while a view uses them.
- `StudySession` and `StudyLog`: the scenario panel and the logs.

Tests: Unity EditMode 60/60, Python `tests/test_unity_analytics.py` 5/5.

## Carrying over to VR

`StudySession`'s flow methods are public (`StartSession`, `ShowCurrentView`, `SelectOption`, `SetConfidence`, `Submit`, `Skip`). `LegendStackView.BuildEntries` can fill a world-space panel. `StudyLog` has no input or UI code. For VR:
- set **Condition** to `vr`;
- add a wrist or world panel calling the same methods;
- log head pose instead of the desktop camera; the `camera` event already logs `Camera.main`, which is the headset camera under an XR rig.

In S5, the downward part (share aged 65+) hangs below the ground plane. On the hologram table it can be seen by looking under the table, which is a VR-specific advantage worth asking experts about.
