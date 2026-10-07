# Let Helsingborg's voting map test immersion

Build the evaluation around the 2026 municipal election joined to SCB income and population. That data already contains strong, checkable patterns. Across the 60 voting districts fully inside the model, turnout tracks DeSO median income at **Spearman ρ ≈ 0.92** and foreign-born share at **ρ ≈ −0.93**. The Sweden Democrats (SD) break the simple "low income → SD" expectation (**ρ ≈ 0.22**). These patterns give task answers without inventing data. The recommended study has two tracks. The first is a **timed benchmark of six task types** (lookup, two-area difference, find extreme, region summary, two-variable co-location, composition). It runs on *seeded* versions of the real layers, so every answer is known. Participants do it in a **within-subjects Desktop-3D vs Quest 3S comparison** (n ≈ 16–24, about 75 minutes). The second track is a shorter **real-data scenario walk** with think-aloud, which brings in ecological validity, a "surprise" task and an artefact-spotting task. Two encoding rules follow from the literature. **Use height plus the same variable's colour (a "coloured prism") when accuracy matters, and flat colour for overview, because height is more accurate and colour is faster.** Use the existing A/B panel with its B − A column for exact comparison. Five pieces of engineering block a valid study, not the data: interaction logging, a task runner, export of the Helsingborg layers into Unity, multiple legends, and categorical encoding. VR input and a tabletop mode for the Quest 3S come next. Electricity and weather fit later as a temporal extension. A transparent, synthetic energy-signature model driven by open SMHI temperatures makes that extension possible even if no metered data ever arrives.

## Helsingborg's 2026 data already contains testable ground truth

The prototype's data covers a 70 km² rectangle of central Helsingborg in EPSG:3006. Each source uses its own spatial unit and reference year, so every task has to name both. The SCB delivery (*Statistik på ruta*, statistics on grid squares) has **population tables for 2024 and income tables for 2023**. The grid mixes **250 m cells in built-up areas with 1,000 m cells that hold only the remainder population**. DeSO areas use the 2025 codes ([SCB profiling](../docs/data-analysis/2026-10-07_helsingborg_scb_profiling.md)). The election data has 92 voting districts (*valdistrikt*) plus a collection district (*uppsamlingsdistrikt*). The collection district holds 2.2 % of the votes and cannot be mapped ([election profiling](../docs/data-analysis/2026-10-07_helsingborg_election_2026_profiling.md)). Of the 43,981 buildings, **4,084 lie in no populated cell** (industry, harbour, parks). This matters for building colouring.

Three data facts constrain the task designs:

- **The grid has no median income.** It has only quartile counts and a sum of disposable income per consumption unit. The median exists per DeSO (`MedianInk`, 198,693–411,400 SEK).
- **The tables cover different cell sets.** Each covers 483–494 of 501 cells, so a missing cell means "no data", not zero.
- **Sub-groups do not add up to totals.** They differ from the totals on most rows, by at most 12 people, because of SCB's disclosure protection ([SCB profiling](../docs/data-analysis/2026-10-07_helsingborg_scb_profiling.md)).

The SCB delivery is FUK-licensed (research and education only). Exports must stay out of git, and thesis figures need a licence check before anything is published.

| Dataset (Swedish → English) | Unit | Year | Count in model area | Main task use |
|---|---|---|---|---|
| *Röster per distrikt* → votes per voting district | valdistrikt | 2026 | 60 fully inside, 13 partly | turnout, party shares, winner |
| *Befolkning, Tab1/Tab4* → population by age / country of birth | Ruta 250/1000 m, DeSO | 2024 | 492 cells, 70 DeSO | composition glyphs, decision tasks |
| *Inkomster, Tab11* → households by income quartile | Ruta, DeSO | 2023 | 494 cells, 70 DeSO | income surfaces; median only for DeSO |
| *Tab10* → education, ages 25–64 | Ruta | 2024 | 483 cells | optional socioeconomic proxy |
| *Byggnad* → buildings with LiDAR height and purpose | footprint | 2018 LiDAR | 43,981 | context, colouring, decision criteria |

The election–income patterns in Helsingborg match national register evidence closely enough to serve as **ground truth for tasks**. SCB reports that in 2022 turnout was 93 % among people with post-secondary education and 74 % among those with only pre-secondary education. "The higher the income, the greater the propensity to vote" ([SCB 2023 press release](https://www.scb.se/pressmeddelande/ny-rapport-visar-grupperna-dar-valdeltagande-sjonk-mest/)). Turnout in the most disadvantaged DeSO area type fell from **67 % to 58 %** between 2018 and 2022, while the most advantaged type stayed near 91–93 % ([SCB, *Delat deltagande*, 2023](https://scb.se/contentassets/048c2c293c404f3e899e91b844b6b9c2/studie_om_valdeltagande_i_de_allmanna_valen_2022_utifran_ett_integrations-_och_segregationsperspektiv_ju2022-03548.pdf)). Helsingborg's 2026 district turnout ranges from **57.6 % to 93.4 %** (median 78.9 %) ([election profiling](../docs/data-analysis/2026-10-07_helsingborg_election_2026_profiling.md)). The lowest values (Furutorp 57.6 %, Eneborg M 58.5 %, Högaborg V 59.0 %) match the national low-turnout type.

A read-only analysis of the project data on 2026-10-07 produced the following correlations. It area-weighted DeSO values onto the 60 districts lying more than 99 % inside the rectangle.

| Variable | vs DeSO median income | vs foreign-born share |
|---|---|---|
| Turnout | +0.92 | −0.93 |
| M | +0.90 | −0.87 |
| V | −0.91 | +0.91 |
| S | −0.83 | +0.85 |
| SD | +0.22 | −0.33 |

These figures are not yet in a committed report. Recompute them once the export pipeline is final.

The SD row is the most valuable one for evaluation:

- SD is **lowest** in low-income, high foreign-born districts: Furutorp 6.8 %, Fredriksdal Ö 10.0 %.
- SD is **highest** in middle-income districts: Ättekulla Ö 26.5 %, Rosengården V 25.3 %, Lussebäcken 25.1 %.
- SD **dips again** in the richest district, Tågaborg N (13.4 %).

This matches SCB survey evidence that SD sympathy is higher among the Swedish-born and those with pre-secondary or upper-secondary education ([SCB PSU May 2022](https://www.scb.se/hitta-statistik/statistik-efter-amne/demokrati/partisympatier/partisympatiundersokningen-psu/pong/statistiknyhet/partisympatier-maj-2022/)). It also matches the finding that SD grows most outside central towns ([Öhrvall 2022](https://ifn.se/media/xvalkjt5/2022-öhrvall-en-klyfta-mellan-stad-och-land.pdf)). A participant who assumes "poorer means more SD" will answer wrongly. The task therefore measures whether the system supports *checking* an assumption rather than confirming it.

The joined data also contains a **real artefact that makes a good critical-reading task**. Oceanhamnen votes like an affluent district (M 30.7 %, V 8.1 %, turnout 75.8 %), but its area-weighted DeSO foreign-born share is about 46 %. The likely cause is a DeSO polygon that does not match the voting district.

All tasks should be worded at area level ("districts with lower median income have lower turnout"), because SCB's own models show that the area effect largely disappears once individual resources are controlled for ([SCB 2023](https://scb.se/contentassets/048c2c293c404f3e899e91b844b6b9c2/studie_om_valdeltagande_i_de_allmanna_valen_2022_utifran_ett_integrations-_och_segregationsperspektiv_ju2022-03548.pdf)). The model area contains two areas on the police list of vulnerable areas: Söder and Dalhem/Drottninghög/Fredriksdal ([Polisen Region Syd, 2025](https://polisen.se/siteassets/dokument/organiserad_brottslighet/utsatta-omraden/region_syd.pdf)). Task text should therefore name districts and variables, never "problem areas".

## Ten scenarios cover every encoding the engine renders

Each task should come from an established taxonomy and have **one checkable answer**. Tilt Map is the strongest VR precedent. It grounded its tasks in Andrienko & Andrienko's elementary/synoptic split. It replaced binary "which is larger" questions with **numeric difference estimation**, because binary answers were near ceiling accuracy. It **highlighted targets** so that measured time covered reading, not searching ([Yang et al., Tilt Map](https://arxiv.org/abs/2006.14120)). Besançon et al. built their choropleth tasks from Roth's Identify / Compare / Rank / Associate objectives and added Summarize ([Besançon et al., OSF](https://osf.io/8rxwg/)). Urban digital-twin usability work uses the same escalation: lookup, then hot-spot finding, then integrating two sources ([Wang et al., CISBAT 2025](https://ual.sg/publication/2025-cisbat-dt-interfaces/2025-cisbat-dt-interfaces.pdf)).

### Benchmark tasks: seeded layers, highlighted targets, planted answers

These six tasks form the quantitative core. Each condition gets its own *parallel* target set, rotated across participants. The layers are real Helsingborg geometry carrying seeded values (see the data-shortcuts section). Answers are planted, and correct answers are balanced across low, middle and high ranges on a 0–100 scale.

| ID | Task type (taxonomy) | Question template | Encoding | Answer by construction | Engine status |
|---|---|---|---|---|---|
| B1 | Retrieve value (Identify, elementary) | "What is the value of the highlighted cell?" | Choropleth, 250 m grid; pick → info panel | Planted value | Supported |
| B2 | Compare two (Compare, elementary) | "By how much does cell A exceed cell B?" Pairs are near or far apart. | Coloured prism (inset extrusion + same-variable colour) | Planted difference in 20–40 / 40–60 / 60–80 bands | Supported |
| B3 | Find extreme (Rank, synoptic) | "Which cell in the outlined district has the highest value?" | Coloured prism; buildings in context mode | One planted maximum, 1.3–1.5× the runner-up | Supported (context mode is new) |
| B4 | Summarize region (Summarize, synoptic) | "Estimate the average over the outlined DeSO." | Choropleth vs prism | Region mean, coefficient of variation 40–60 % | Supported |
| B5 | Associate two variables | "In the outlined area, are high-X cells also high-Y?" | Height = X, colour = Y, two legends | Planted positive / none / negative association | Needs a second legend |
| B6 | Composition | "Which highlighted district has the largest share of group k?" | Stacked or radial glyph on districts or DeSO | Planted dominant segment | Supported |

### Real-data scenarios: expected answers from the 2026 data

These scenarios run in the exploration phase. Their answers come from real data, so they cannot be repeated across conditions without learning effects. Use them for think-aloud, insight counting and correctness checks rather than for the timed medium comparison.

| Priority | ID | Question | Encoding | Expected answer |
|---|---|---|---|---|
| Core | S1 | "Which district in the model has the lowest turnout?" | District choropleth (sequential); buildings coloured by district for orientation | Furutorp 57.6 %, then Eneborg M 58.5 % and Högaborg V 59.0 %. Highest is Gustavslund V, 90.6 %. |
| Core | S2 | "Are low-turnout districts also low-income?" | Height = DeSO median income on districts; colour = turnout; two legends | Yes, strongly (ρ ≈ 0.92). Variant: foreign-born share (ρ ≈ −0.93). |
| Core | S3 | "Is SD strongest in the lowest-income districts?" | Height = income; colour = SD share; info panel | **No.** Lowest in Furutorp (6.8 %), highest in middle-income Ättekulla Ö (26.5 %), lower in Tågaborg N (13.4 %). |
| Core | S4 | "Compare Tågaborg N and Drottninghög Ö on all variables." | A/B block compare (B − A column); radial party glyph | Turnout 86.2 vs 67.6 %; median income ≈ 379k vs ≈ 206k; M 46.6 vs 4.0 %; V 2.7 vs 32.2 %. |
| Core | S9 | "Find a district whose vote profile contradicts its neighbourhood statistics." | Diverging map of district minus municipal mean; picking | Oceanhamnen. It is a DeSO/district mismatch artefact, not a real finding. |
| Second | S5 | "Where are residents born outside Europe concentrated?" | Stacked or radial glyph from Tab4 | The Söder–Drottninghög–Fredriksdal–Högaborg belt (DeSO share ≈ 56–59 %). Verify the "rest of world" category on the grid. |
| Second | S7 | "Pick the 250 m cell for a new preschool: most 0–6-year-olds among cells with no school within 500 m." | Stacked age glyph + building purpose (categorical) | Computable from Tab1 and building purpose. **Not computed yet.** |
| Second | S8 | "Does low-income concentration look the same at 250 m and DeSO level?" | Layer/unit toggle, same palette and scale | No. The DeSO view looks more mixed, as Boverket notes about scale ([Boverket 2024:18](https://segregationsbarometern.boverket.se/app/uploads/2024/09/2024-18-Boendesegregationens-utveckling.pdf)). |
| Drop or defer | S6 | "Where do young adults (20–24) live?" | Stacked age glyph | Ground truth not computed. Treat it as a hypothesis, not as ground truth. |
| Drop | S10 | "Is SD stronger outside the core?" | — | Yes (22.6 % vs 17.6 %), but the outer districts are not in the 3D model. |

S4's figures use DeSO-weighted income, so recompute them after export. Two things need checking before S4 runs. The A/B tool copies a selected "block": confirm it accepts district polygons and not only grid cells. Also make sure the cell-pair B − A table shows each variable's year.

### Encoding rules the evidence supports

**Height for accuracy, colour for speed.** In the first controlled VR comparison, prism maps were significantly more accurate than choropleths for area comparison, and choropleths were significantly faster. 75 % of participants ranked the coloured prism best ([Tilt Map](https://arxiv.org/abs/2006.14120)). The engine's inset extrusion with the same variable on colour is that coloured prism, so make it the default for B2–B4. Inset columns should also reduce occlusion between neighbours, but no source tests this, so it is an inference.

**Expect low accuracy whenever two variables must be combined.** Besançon et al. found 3D choropleths were the most frequently ranked favourite. Tasks combining two variables still reached only about 50–65 % accuracy, and juxtaposed univariate maps were most accurate for single-variable questions ([Besançon et al.](https://arxiv.org/pdf/2005.00324)). For B5 and S2, use height for one variable and colour for the other. This is a superposition in Gleicher's sense ([Gleicher et al. 2011](https://doi.org/10.1177/1473871611416549)), and it needs two legends.

**Prefer explicit encoding for the correlation question.** Show a derived value, such as district minus municipal mean or a ratio, on a diverging scale. This is more defensible than a 3×3 bivariate palette, because Olson found readers judge correlation worse on bivariate maps than on two univariate maps ([Penn State GEOG 486](https://courses.ems.psu.edu/geog486/node/900)).

**Colouring buildings by cell value is fast but imprecise.** On 3D buildings, colour-coded designs were faster and plot-based linked or embedded designs more accurate, and the plot advantage grows with task complexity ([Mota et al. 2022](https://arxiv.org/abs/2208.05370)). Use building colouring for orientation and overview. Use the info panel and A/B table for accuracy. Label building colours "inherited from cell" so participants do not read a cell value as a building value.

**Keep data colours unlit.** Rendering technique measurably changes error rates for thematic colour in 3D city scenes ([Engel et al., VMV 2013](https://diglib.eg.org/handle/10.2312/PE.VMV.VMV13.025-032)). Render the tops of data surfaces so their colour matches the legend, and shade only the side walls. The glyph renderer already gives roofs the exact colour and darker sides ([glyph doc](../docs/UNITY_GLYPHS_AND_BIDIRECTIONAL_HEIGHT.md)). Extend the same rule to surfaces and columns.

**Categories need a qualitative palette.** The district winner and building purpose need a qualitative ColorBrewer-style palette ([Harrower & Brewer 2003](https://doi.org/10.1179/000870403235002042)). Show text labels alongside, because conventional Swedish party colours are not all colour-blind distinguishable.

**Legend text must be large.** VR reading studies on Quest-class headsets report preferred text heights near **41 ± 14 dmm** (mm at 1 m distance) ([Dingler et al.](https://arxiv.org/html/2004.01545v1)), and "good" ratings from about 14.6 dmm ([Virtual Visus](https://www.researchgate.net/publication/362905907_Virtual_Visus_-_Vision_Acuity_and_Text_Legibility_in_Virtual_Environments)). Start legends and panels at 25–30 dmm or more, and tune them in the Quest 3S pilot.

## A two-condition within-subjects study fits in 75 minutes

The dominant design in geo-immersive analytics is a **within-subjects lab study with 12–32 participants, two to four conditions, counterbalanced order and different but equivalent data per trial**. Two reference points:

- **Wagner Filho et al.** compared desktop and immersive space-time cubes with **20 participants, 14 trials and about 65 minutes**. They called that length "very long" ([Wagner Filho et al. 2020](https://arxiv.org/abs/1908.00580)).
- **Tilt Map Study 1** collected 72 responses per participant from 12 people in about 1.5 hours ([Tilt Map](https://arxiv.org/abs/2006.14120)).

Crossing two media with three encodings and six tasks would exceed 90 minutes. Pick one primary factor.

**Primary design (Option A): Desktop 3D vs Quest 3S.**
- Within-subjects, AB/BA counterbalanced, n = 16–24.
- Each condition runs B1–B6 twice, giving 12 trials, with a fixed encoding per task and a parallel seeded target set.
- Run the Quest condition **seated, as a tabletop miniature in front of the user**, not as room-scale flying over 70 km². Tilt Map, Kraus's VR miniature condition and Wagner Filho's desk-coupled cube all used this exocentric setup. It lowers both cybersickness and pointing-precision risk.

**Fallback design (Option B), if VR input slips: encodings on desktop.**
- Choropleth vs coloured prism vs glyph, Latin square, n ≈ 12, desktop only.
- This replicates Tilt Map Study 1 on a real, building-dense city. That is itself new, because all published prism/choropleth VR studies used isolated maps, not city context.

| Phase | Minutes | Content |
|---|---|---|
| Intro | 5 | Consent, demographics, VR experience, familiarity with Helsingborg, SSQ baseline |
| Training | 10 | Standard training scene for both media. Tilt Map added VR training after a VR-naive pilot participant struggled ([Tilt Map](https://arxiv.org/abs/2006.14120)). |
| Condition 1 | 15 + 5 | B1–B6 × 2, then SUS, NASA Raw TLX, confidence per task type, SSQ if VR |
| Break | 3 | — |
| Condition 2 | 15 + 5 | Parallel set, same questionnaires |
| Real-data walk | 10 | S1–S4 and S9 with think-aloud, in the participant's chosen medium (log the choice) |
| Close | 7 | Preference ranking, semi-structured interview |

Keep each continuous VR block under 25–30 minutes. Wagner Filho et al. found a negligible pre/post change in simulator sickness for immersive sessions of about 25 minutes ([Wagner Filho et al.](https://arxiv.org/abs/1908.00580)).

Add **two or three domain experts**, such as Helsingborg municipal planners, in a 20-minute free-exploration walkthrough. UrbanVR and the CISBAT urban-twin study used expert sessions of this kind ([UrbanVR](https://arxiv.org/abs/2107.00227); [Wang et al.](https://ual.sg/publication/2025-cisbat-dt-interfaces/2025-cisbat-dt-interfaces.pdf)). This also answers the grand-challenge critique of short studies run only with "easily accessible" students ([Ens et al., CHI 2021](https://par.nsf.gov/servlets/purl/10290646)).

| Measure | Instrument | When | Purpose |
|---|---|---|---|
| Completion time | Logged from target shown to answer confirmed | Every trial | Speed; colour vs height trade-off |
| Accuracy | Absolute error on 0–100 scale; correct/incorrect for picks | Every trial | Comparable across encodings |
| Usability | SUS ([Bangor et al. norms](https://doi.org/10.1080/10447310802205776)) | After each condition | Wagner Filho et al. found 82.3 (VR) vs 62.1 (desktop) |
| Workload | NASA Raw TLX | After each condition | Mental demand and frustration differed in VR |
| Sickness | SSQ ([Kennedy et al. 1993](https://doi.org/10.1207/s15327108ijap0303_3)) or VRSQ | Before the first VR block and after each VR block | Safety, confound control |
| Confidence and preference | 5-point Likert per task type; final ranking | Per condition / end | Tilt Map's subjective measures |
| Behaviour | Head/camera pose at 10 Hz, picks, legend and toggle use, A/B use | Continuous | Occlusion fighting, interaction counts |
| Insights | Count and type of insights (Saraiya et al.) | Real-data walk | Exploratory value ([Saraiya et al. 2005](https://faculty.cc.gatech.edu/~john.stasko/8001/saraiya05.pdf)) |
| Optional | UEQ-S; IPQ only if presence is a research question | End | The CISBAT urban twin scored perspicuity 0.75 against attractiveness 1.67 |

The literature makes five hypotheses plausible enough to test:

- **H1:** coloured prisms are more accurate than choropleths for B2–B4.
- **H2:** choropleths are faster.
- **H3:** Quest 3S and desktop do not differ significantly in accuracy, but VR scores higher on SUS and preference. This is the Wagner Filho pattern.
- **H4:** VR produces more head and camera movement.
- **H5:** B5 and S2/S3 have low accuracy in both media.

With n ≤ 24, report medians and means with bootstrap 95 % confidence intervals and effect sizes, as Besançon et al. did ([OSF](https://osf.io/8rxwg/)). Use Wilcoxon tests for the two media, Friedman tests for three encodings, and a mixed model when trials are nested. Treat VR experience and Helsingborg familiarity as covariates.

Novelty bias is the threat to watch most closely. Ens et al. warn that immersion causes a "short term bias towards over-acceptance" ([Ens et al.](https://par.nsf.gov/servlets/purl/10290646)). A higher VR SUS score therefore needs the behavioural and accuracy data next to it before it supports any claim.

## Four declared shortcuts make the data good enough

Synthetic or seeded data is an accepted choice in visualization evaluation. Schulz et al. argue that user studies need "representative and unconfounded visual stimuli" and that hand-picked real data can harm generality and privacy ([Schulz et al., BELIV 2016](https://doi.org/10.1145/2993901.2993907)). Tilt Map used real geographies but **generated new values for every question**. The generated values kept the spatial autocorrelation (Moran's I) of real population density, and the authors applied a root transform because the skewed originals made answers trivial ([Tilt Map](https://arxiv.org/abs/2006.14120)). This thesis measures usability, not data accuracy, so a hybrid is the most defensible choice: real geometry and aggregates, with planted patterns only where a task needs a known answer. The following register covers what is needed.

| ID | Shortcut | Method | Status | Applies to |
|---|---|---|---|---|
| D1 | Grid counts → buildings | Volumetric dasymetric: Pop_i = Pop_cell · RFA_i / Σ RFA_j over residential buildings, RFA = footprint × max(1, round(height / 3.0 m)) | Established; storey height and mixed-use factor (0.5) are declared parameters | Building colouring, district aggregation |
| D2 | Grid → voting district | Sum D1 building estimates by district (each building in exactly one district). Fallback: 100 m population grid as weight. Report area weighting only as a baseline. | Established | S2, S3 income on districts |
| D3 | Sub-groups vs totals | Rake sub-groups to the published total (one margin), or IPF for two | Established | All composition glyphs |
| D4 | Missing vs zero | Absent population cell = 0 residents. Absent cell in another table = NA. Zero households with positive income (30 cells) = mean undefined, flagged. Missing Tab10 cells = ratio-borrowed from DeSO, flagged "imputed". | Mixed (rules declared) | All layers |
| D5 | 1,000 m remainder cells | Draw as the square minus the overlapping 250 m cells | Required for correctness | Grid surfaces |
| D6 | Mixed years | Label 2023 / 2024 / 2026 on every layer and legend; no cross-year ratios without a caveat | Declared | All tasks |
| D7 | Seeded benchmark layers | Regenerate values per trial while keeping Moran's I; root-transform; plant targets; store seed and answer | Precedented (Tilt Map) | B1–B6 |
| D8 | Votes → DeSO (avoid) | If ever needed: assume uniform shares within each district, weight by adult population, flag DeSOs with < 90 % overlap | Ad hoc; ecological caveat | Not needed for core tasks |

D1 has strong evidence. Lwin & Murayama's volumetric method reached **R² ≈ 0.95** against true building populations, against about 0.80 for footprint area alone. Their best results came from allocating to residential buildings only ([Lwin & Murayama 2009](https://mail.cdema.org/virtuallibrary/images/A%20GIS%20Approach%20to%20Estimation%20of%20Building.pdf)). German national gridded mapping found that combining density and height "considerably increased mapping quality" ([Schug et al. 2021](https://doi.org/10.1371/journal.pone.0249044)).

D1 also comes with a cheap accuracy number for the thesis. Aggregate the building estimates back to SCB's 100 m grid and report R² and MAE against it. The 100 m grid is an independent, finer official layer.

D3 and D4 rest on SCB's own documentation:

- The Cell Key Method adds random noise of −3 to +3 to counts.
- "Reported totals are not always equal to the sum of their reported parts."
- Totals that users build themselves carry larger uncertainty.

Source: [SCB CKM FAQ](https://www.scb.se/dokumentation/statistikomraden-under-forandring/utveckling-av-rojandeskyddet/fragor-och-svar-om-rojandeskyddet-cell-key-method-ckm/). Raking follows Deming & Stephan's iterative proportional fitting ([Deming & Stephan 1940](https://doi.org/10.1214/aoms/1177731829)). Do not disaggregate income to buildings as if each building had its own value. Show the cell or DeSO mean and mark it "inherited from cell", to avoid false precision.

Document the shortcuts in four places:

1. **Parameters** go in a config under `configs/`: storey height, mixed-use factor, seeds, the Moran's I target and the answer bands.
2. **One decision note per shortcut** goes in `docs/decisions/`.
3. **A cleaning and integration report** goes in `docs/data-analysis/`, with counts before and after and the 100 m validation figure.
4. **Seeded layers and the planted-pattern register** (entity IDs, magnitude, task, correct answer) go in `Processed_data/`. They must not go in `reports/`, because the SCB licence forbids publishing derived values.

Every runtime value should carry these fields:

- `value_raw` (the original value);
- `ref_year`;
- `method`: `observed`, `raked_to_total`, `dasymetric_rfa`, `ratio_imputed_deso`, `seeded` or `planted`;
- `quality_flag`.

Expose `quality_flag` through a single "show data quality" toggle. Estimated values get desaturation or hatching; seeded or planted values get an outline and a "modelled, not measured" legend entry. This follows the cartographic uncertainty literature ([MacEachren et al. 2005](https://doi.org/10.1559/1523040054738936)). That literature also stresses task-centred evaluation of such encodings ([Kinkeldey et al. 2014](https://repos.hcu-hamburg.de/bitstream/hcu/758/1/How%20to%20Assess%20Visual%20Communication%20of%20Uncertainty%20A%20Systematic%20Review%20of%20Geospatial%20Uncertainty%20Visualisation%20User%20Studies.pdf)). Logging whether participants open the toggle turns data-quality awareness into a measurable interaction.

Tell participants plainly that benchmark values are scenario values, not real statistics. This removes prior-knowledge effects for local participants and avoids showing perturbed socioeconomic data about identifiable neighbourhoods as if it were real.

## Logging and legends must ship before the first participant

The rendering engine is ahead of the evaluation infrastructure. Three gaps stand out ([living context](../Project_livingContext.md)):

- **The Helsingborg statistics are not yet in Unity.** The population, income and election pipelines are still at the preprocess stage.
- **The Helsingborg scene has no legend and no visualization switcher.**
- **No interaction logging exists**, although `HoverChanged`, `SelectionChanged`, `ComparisonManager.Changed` and `VisualizationSwitcher.Changed` are named as ready hook points ([interaction doc](../docs/UNITY_DESKTOP_INTERACTION.md)).

Without logging, time and accuracy cannot be measured. The first priority is therefore desktop infrastructure that also supports the fallback design.

| Priority | Item | Why | Unlocks |
|---|---|---|---|
| P0-1 | Export Helsingborg layers to the Unity package: 250 m cells plus 1,000 m remainder cells, DeSO and voting districts, with derived variables (shares, mean income, district minus municipal mean) and the D1/D2 aggregates | No real-data task can run without them | S1–S9, B1–B6 |
| P0-2 | Study logging: session, participant, condition, task and trial IDs; target-shown and answer timestamps; answer; correctness; picks; preset, legend and toggle changes; A/B use; head/camera pose at 10 Hz; CSV/JSON export | Time and accuracy depend on it, and so does the behavioural analysis | All measures |
| P0-3 | Task runner: reads a trial list from config, highlights targets, collects answers (0–100 slider or pick), runs the timer, resets state between trials | Reproducible, counterbalanced trials | B1–B6 |
| P0-4 | Multiple legends, one per encoded channel, each showing variable, unit and year | Height and colour encoding different variables are uninterpretable without it | B5, S2, S3 |
| P0-5 | Categorical (string) encoding with a qualitative palette | District winner and building purpose | S1 variant, S7, S9 |
| P1-1 | Quest 3S: XR package, ray interactor reusing the existing ray-based picking services, world-space panels and legends at 25–30 dmm or more, seated tabletop miniature, snap rotation | Option A | Medium comparison |
| P1-2 | Context mode (desaturated or ghosted buildings when height encodes data); unlit data tops; top-down and oblique camera presets | Reduces occlusion and colour ambiguity | B3, S2, S3 |
| P1-3 | Data-quality toggle driven by `quality_flag` | Makes the shortcuts visible and loggable | Exploration phase |
| P2-1 | 2–4 small multiples on a flat shelf | VR small multiples work best flat when there are few of them ([Liu et al. 2020](https://ialab.it.monash.edu/~dwyer/papers/ImmersiveSmallMultiples.pdf)) | Comparisons across variables or time |
| P2-2 | Temporal scrubber and linked time-series plot | Only if energy or weather data arrives | E1–E4 |

**Quest performance.** Collider memory for building picking has not been measured. Run the Quest pilot with `pickBuildings = false` (cells only) until profiling shows the 3.3 M-triangle colliders fit ([interaction doc](../docs/UNITY_DESKTOP_INTERACTION.md)).

**Tilt Map's transitions.** Tilt Map's continuous choropleth–prism–bar transition has the strongest VR evidence. It was more accurate than side-by-side views and faster than toggling ([Tilt Map](https://arxiv.org/abs/2006.14120)). It is also a large build. The cheap approximation is a choropleth/prism toggle combined with a top-down/oblique camera preset, with the A/B table as the "bar view".

## Electricity and weather fit in as a temporal extension

Temporal data adds the one task family the current data cannot support, so plan for it even though nothing is promised. Before any energy data, the cheapest temporal scenario is **election change from 2022 to 2026**. It needs only Valmyndigheten's 2022 per-district file and a check for boundary changes. Helsingborg's 2022 municipal result (S 31.20 %, M 23.68 %, SD 21.47 %, turnout 76.88 %) ([Valmyndigheten 2022 protocol](https://resultat.val.se/protokoll/protokoll_Val_20220911_1283_KF.pdf)) differs enough from 2026 to make change maps worthwhile.

If metered electricity data never arrives, a **synthetic energy-signature model** is explainable and fast. Its form is the standard measurement-and-verification change-point model:

E_i(d) = A_i · [ b_type + k_type · max(0, 17 °C − T_out(d)) ]

([ACEEE 2007](https://www.aceee.org/files/proceedings/2007/data/papers/37_4_084.pdf)). The model's inputs:

- **Balance temperature.** +17 °C follows SMHI's degree-day (*graddagar*) convention ([SMHI graddagar](https://www.smhi.se/professionella-tjanster/hallbara-stader/smhi-energi-index-och-graddagar---normalarskorrigering-varme)).
- **Calibration.** k_type is calibrated to official Swedish intensities for premises, for example offices 110, schools 128 and food retail 138 kWh/m² for heating and hot water ([Energimyndigheten 2016](https://www.energimyndigheten.se/4ab47a/globalassets/statistik/bostader/energistatistik-for-lokaler-2016.pdf)).
- **Floor area.** A_i comes from D1.
- **Temperature.** Hourly temperatures (parameter 1) come from SMHI open data under CC BY 4.0, with attribution and a note of changes required ([SMHI terms](https://www.smhi.se/data/om-smhis-data/villkor-for-anvandning); [SMHI metobs API](https://opendata-download-metobs.smhi.se/api/version/1.0.json)).
- **Hourly shapes.** These can come from BDG2's hourly meters of 1,636 non-residential buildings (CC BY 4.0) ([Miller et al. 2020](https://doi.org/10.1038/s41597-020-00712-x)).

Two gaps remain. A verified Swedish **electricity-only** kWh/m² per building type was not found. The hot-water base share b_type is an undocumented heuristic. Both must be declared before use. Real per-building energy classes are available through Boverket's energy-declaration (*energideklaration*) API, but only after signing an agreement ([Boverket API](https://www.boverket.se/sv/om-boverket/oppna-data/publikt-api-for-energideklarationer/)).

| ID | Task | Encoding | Expected answer |
|---|---|---|---|
| E1 | "Play the year: when is total demand highest?" | Stepped scrubber; small multiples of 3–6 months | January–February peak, summer minimum. The Gothenburg twin shows 680 GWh in January vs 118 GWh in August ([Maiullari et al. 2023](https://research.chalmers.se/publication/539110/file/539110_Fulltext.pdf)). |
| E2 | "Does daily demand rise as temperature falls?" | Linked time-series plot in the info panel | Strong negative relation in the heating season, flat in summer. It holds by construction in the synthetic model. |
| E3 | "Which building type responds most to cold days?" | Buildings coloured by sensitivity; categorical type legend | Multifamily housing more than offices, as the Gothenburg climate scenarios show ([Maiullari et al.](https://research.chalmers.se/publication/539110/file/539110_Fulltext.pdf)) |
| E4 | "Find the building with an abnormal winter peak." | Embedded colour plus linked plot | A planted ×3 outlier, recorded in the register |

The encodings follow the evidence on temporal display:

- **Small multiples beat animation in accuracy for analysis**, while animation is engaging and error-prone ([Robertson et al. 2008](https://faculty.cc.gatech.edu/~stasko/papers/infovis08-anim.pdf)).
- **Plot-based linked views beat colour sequences on 3D buildings** ([Mota et al.](https://arxiv.org/abs/2208.05370)).

So use playback for overview and linked plots or small multiples for answers.

Synthetic data limits what can be claimed. E2 and E3 then test whether the system *reveals* a relation, not whether the relation exists, and the thesis should say so. One more caveat applies if real data arrives: in district-heated buildings, electricity excludes most space heating, so the temperature link would be weak. Check what any delivered dataset actually covers.

## Conclusion

The data turned out not to be the limit. A single municipal election joined to grid statistics produces correlations near ±0.9, a counter-intuitive party pattern and a real join artefact. Those three properties map onto confirmatory, surprise and critical-reading tasks, and few evaluation datasets offer that range. The open question is engineering. Logging, a task runner, legends and categorical encoding decide whether any of these scenarios can produce a measurable result. Building them first also keeps the desktop-only fallback study available if Quest input slips.

The split between seeded benchmark tasks and a real-data walk is the central design choice. It settles the tension between known ground truth and ecological validity without hiding either. It also turns the thesis's main novelty into an explicit contribution: no published immersive-analytics study tests census and election encodings inside a dense, building-level city model. Whether inset prisms among 44,000 buildings keep the accuracy advantage they show on isolated VR maps is a result worth reporting either way.
