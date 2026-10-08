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
| 3–7 | **T0 Training** | Pan, orbit, zoom, hover, select, compare (C). No answer. |
| 7–22 | **S1–S5** (~3 min each) | Facilitator reads the question; participant answers aloud; facilitator records the answer and confidence (1–5) in the panel. |
| 22–24 | S6 (optional) | Only if time allows. |
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

| Id | View (preset) | Task type | Shows the system's ability to... |
|---|---|---|---|
| T0 | `x2_income_population` | training | navigation, picking, compare |
| S1 | `s1_turnout_districts`: turnout as district colour + buildings coloured by district | find extreme / lookup | colour on areas and buildings, picking an area |
| S2 | `s2_income_turnout`: height = estimated mean income, colour = turnout (voting districts) | association | height + colour, two legends |
| S3 | `s3_sd_income`: height = income, colour = SD share | association, counter-intuitive | checking an assumption |
| S4 | `s4_deso_income_origin`: DeSO height = median income, colour = share born abroad | association (DeSO) | a second unit; the only median-income source |
| S5 | `s5_four_variables`: grid up = density, down = 65+, colour = mean income; buildings = district turnout | multi-variable profile | **4 datasets at once**, 2 units, buildings in context |
| S6 | `s6_age_composition`: grid up = share of children 0–15, down = share aged 65+ | association (up vs down) | two-sided columns |

**Info panel** (click an area or a building):
- **Header:** a readable name (district name; "250 m grid cell · in Eneborg V"; a building's type and district).
- **First, large:** the values the current view shows, each with its unit, the channel (height, colour, building colour) and its rank in words ("higher than 72 % of areas"). For a building, these are the values of its cell and district.
- **Below:** folded detail sections, opened by clicking a heading.
- **Footer:** codes and ids, small.

The expected answers are in the scenario file and were computed from the package data. They are logged for scoring but never shown. Recompute them if the data changes: the build checks only that each expected answer is one of the options.

## Running a session

1. **Build the package** once, after any data change:
   ```bash
   python Src/Scripts/Unity/build_unity_package.py --config configs/cities/helsingborg.json
   ```
2. **Prepare the scene.** Open `Assets/Scenes/Helsingborg.unity`. On the `StudySession` object, set **Participant Id** (e.g. `P03`) and **Condition** (`desktop`).
3. **Screen.** Use at least 1920×1080, full screen. `[` / `]` change the UI size; F2 hides the study panel.
4. **Start.** Press Play, then **Start session** (bottom-left panel). For each scenario:
   - **Show view** loads the view and starts the timer;
   - the participant answers;
   - select the option and the confidence;
   - **Submit**. Use **Skip** if needed.
5. **Finish.** After the last scenario the panel shows the log path. Stopping Play mode also closes the log.

## Logs

Logs are written to `%USERPROFILE%/AppData/LocalLow/<company>/City_Digital_Twin/study_logs/` (`Application.persistentDataPath`):

- **`<session>.jsonl`:** one JSON object per line. Each line has `t` (UTC), `s` (seconds since start), `session`, `participant`, `condition`, `event` and fields. The events:
  - `session_start`: build, Unity version, scene, manifest, scenario file, screen size;
  - `scenario_start` / `scenario_answer` / `scenario_done` / `scenario_skip`: answer, expected, correct, confidence, seconds, interaction count;
  - `visualization_applied`: view id and number of legends;
  - `hover` (when the hovered entity changes) and `select`: entity and unit ids;
  - `comparison`: A/B slot entities;
  - `camera`: position and Euler angles, at `poseHz` (default 2 Hz);
  - `session_end`.
- **`<session>_answers.csv`:** one row per scenario: answer, expected, correct (1/0), confidence, seconds, interactions.

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
