# Explainable data shortcuts for the Helsingborg digital twin: disaggregation, interpolation, imputation and synthesis of urban statistical, energy and weather data

Scope note: These notes collect citable methods that a master thesis can use as declared shortcuts. The thesis evaluates an immersive analytics system, so the aim is defensible and explainable data, not maximum accuracy. Research date is 2026-10-07. Each claim has a link. Where I took a paper's content from background knowledge and did not re-read the full text, the citation says "DOI verified via Crossref". That means the bibliographic record (title, venue, year, volume, pages) was checked against api.crossref.org, but the content summary was not re-checked against the full text. Each section separates **well-established methods** from **ad-hoc heuristics**.

---

## 1. Areal interpolation and dasymetric mapping: moving grid counts to voting districts (valdistrikt) and votes to DeSO

### Takeaway
Use **dasymetric (ancillary-weighted) areal interpolation** with a **building-based weight**. The weight is residential floor area, footprint × height ÷ storey height, or the 100 m population grid as a simpler alternative. It moves SCB grid counts to the 92 voting districts. Votes should normally **not** be pushed down to DeSO. If it is needed, use the same population or floor-area weights on the district→DeSO intersection, and flag DeSOs that are split between districts. Plain area weighting is the documented baseline and the least accurate method. Pycnophylactic interpolation is elegant, but it is unnecessary when building data exists.

### Cited Findings

**Well-established methods (formulas)**
- **Area weighting** (Goodchild & Lam 1980). For a source zone s and a target zone t, the estimate is Ŷ_t = Σ_s Y_s · A(s∩t)/A(s). It assumes the variable is spread **uniformly** within each source zone. The method is volume-preserving: source totals are kept when all of a source's area is covered. Goodchild, M.F. & Lam, N.S.-N. (1980), "Areal interpolation: a variant of the traditional spatial problem", *Geo-Processing* 1:297–312. No DOI exists, and I could not verify an online copy, so this rests on the standard formulation repeated across the literature below.
- **Pycnophylactic (mass-preserving) interpolation** (Tobler 1979) builds a smooth density surface on a raster. The surface is adjusted iteratively so that each source zone's total is exactly preserved ("pycnophylactic" means volume-preserving). Neighbouring cells are smoothed, typically by averaging with neighbours, and values are kept non-negative. Target estimates are the surface summed over each target zone. Tobler, W.R. (1979), "Smooth Pycnophylactic Interpolation for Geographical Regions", *JASA* 74(367):519–530 — [doi:10.1080/01621459.1979.10481647](https://doi.org/10.1080/01621459.1979.10481647) (DOI verified via Crossref).
- **Dasymetric mapping with relative density weights** (Mennis 2003). Ancillary classes (for example land-cover classes) get relative densities from a sample of source zones. Population is then redistributed within each source zone in proportion to class density × class area. Mennis, J. (2003), "Generating Surface Models of Population Using Dasymetric Mapping", *The Professional Geographer* 55(1):31–42 — [doi:10.1111/0033-0124.10042](https://doi.org/10.1111/0033-0124.10042) (DOI verified via Crossref).
- **Eicher & Brewer (2001)** compared areal weighting with binary, three-class and limiting-variable dasymetric methods for interpolating US county data to target zones. In their evaluation the dasymetric methods beat area weighting, and the limiting-variable method performed best. Eicher, C.L. & Brewer, C.A. (2001), "Dasymetric Mapping and Areal Interpolation: Implementation and Evaluation", *Cartography and GIS* 28(2):125–138 — [doi:10.1559/152304001782173727](https://doi.org/10.1559/152304001782173727) (DOI verified via Crossref).
- **Zandbergen & Ignizio (2010)** compared dasymetric techniques that use different ancillary data for small-area population estimates: land cover, imperviousness, road density, nighttime lights and areal weighting. *CaGIS* 37(3):199–214 — [doi:10.1559/152304010792194985](https://doi.org/10.1559/152304010792194985) (DOI verified via Crossref).
- A secondary summary of related Ohio work (16 counties) reports overall population-estimate errors by ancillary data: **address points 4.9%, imperviousness 10.8%, land cover 11.6%, road density 13.3%, nighttime lights 18.6%, areal weighting 21.2%**. Address points performed significantly better. — reported via [WebSearch summary of Esri UC 2011 proceedings abstract a4092](https://proceedings.esri.com/library/userconf/proc11/abstracts/a4092.html). *Caveat:* the original page now redirects and I could not open it. The figures are probably from Zandbergen's 2011 address-point study, not the 2010 paper. Quote them only as "order-of-magnitude" evidence that ancillary-weighted methods roughly halve area-weighting error.
- **Volume-based weights beat area-only weights.** In German national gridded population mapping, "the combined use of density and height, i.e. volume, considerably increased mapping quality". It largely removed systematic underestimation in dense agglomerations and overestimation in rural areas. Adding building type helped at fine scale because "population is not redistributed to non-residential areas". — Schug, F., Frantz, D., van der Linden, S., Hostert, P. (2021), *PLOS ONE* 16:e0249044 — [doi:10.1371/journal.pone.0249044](https://doi.org/10.1371/journal.pone.0249044); [HU Berlin EOLab summary](https://eolab.geo.hu-berlin.de/publication/schug_2021/)

**Python implementation**
- PySAL's `tobler` package (geopandas-based) implements area-weighted interpolation (`area_interpolate`), raster-masked dasymetric interpolation and pycnophylactic interpolation. — [PySAL tobler project](https://pysal.org/tobler/). *Caveat:* the API page returned 404 during this research. Function names are from background knowledge, so check them against the installed version.

### Inferences
- **Grid → voting district (recommended).** First disaggregate each grid cell's count to the residential buildings inside it (Section 2). Then sum buildings by the district that contains each building's centroid. The method is volume-preserving by construction: the district total equals the sum over buildings, and each cell's total is fully redistributed. It is easier to explain than polygon-overlay weights. It also removes the "cut edge cell" problem, because a building belongs to exactly one district.
- **Simpler alternative.** Use the **100 m total-population grid** as the weight. For a 250 m or 1000 m cell c and a district d: Ŷ_{c→d} = Y_c · P100(c∩d) / P100(c), where P100(c∩d) is the 100 m population whose cell centroids fall in both c and d. This is a "binary/limiting-variable dasymetric" approach in the Eicher & Brewer sense. It is well established, and in this case it uses an official SCB population layer rather than modelled building weights.
- **Area weighting** should be reported as the naïve baseline. It allocates people to parks, harbour areas and industrial land.
- **Votes → DeSO.** Votes are counted per district, and only half the DeSOs sit ≥90% inside one district. Disaggregating party shares requires the assumption that **vote shares are uniform within a district**. This is the ecological-inference caveat. The defensible shortcut has four steps:
  1. Compute party *shares* per district.
  2. Weight each district∩DeSO piece by its estimated electorate. As a proxy, use adult population or residential floor area in that piece.
  3. Compute DeSO share = Σ_pieces (weight × district share) / Σ weight.
  4. Flag each DeSO with its "max overlap fraction". Mark DeSOs whose largest district share is below 90% as "estimated – mixed districts".
- **Use pycnophylactic interpolation only for continuous smooth surfaces.** It suits a smoothed income surface for a heatmap. It does not suit discrete counts when building data is available, because it ignores where people actually live. It is also slower and harder to explain to evaluators.

### Gaps
- I did not re-read the Eicher & Brewer or Zandbergen & Ignizio full texts, so per-method error tables are not quoted here.
- I found no published accuracy study specific to Swedish SCB grids → valdistrikt.

---

## 2. Disaggregating grid population or income to individual buildings

### Takeaway
The established method is the **"Volumetric" method** (Lwin & Murayama 2009). Population in a source zone is split across *residential* buildings in proportion to footprint area × number of floors, or to a LiDAR volume. It achieved R² ≈ 0.95 against true building populations in Tsukuba, compared with ≈ 0.80 for footprint area alone. Non-residential buildings are filtered out before allocation, and very small footprints can be filtered too.

### Cited Findings
- **Areametric method:** BP_i = CP · (BA_i / Σ_k BA_k). **Volumetric method:** BP_i = CP · (BA_i·BF_i / Σ_k BA_k·BF_k). Here BP_i is the building population, CP the census-zone population, BA the footprint area and BF the number of floors. The paper also gives equivalents for LiDAR digital volume models. — Lwin, K.K. & Murayama, Y. (2009), "A GIS Approach to Estimation of Building Population for Micro-spatial Analysis", *Transactions in GIS* 13(4):401–414 (full text read from [a hosted PDF copy](https://mail.cdema.org/virtuallibrary/images/A%20GIS%20Approach%20to%20Estimation%20of%20Building.pdf)). The commonly cited DOI, 10.1111/j.1467-9671.2009.01171.x, did **not** resolve in Crossref during this check, so cite by journal, volume and pages.
- Accuracy against actual building populations in Tsukuba: the Areametric method gave **R² ≈ 0.80** (0.7990–0.8004 across footprint filters). The Volumetric method gave **R² ≈ 0.946–0.949**, best 0.9488 with a 25 m² minimum-footprint filter. The smallest RMSE was also Volumetric with the 25 m² filter. The Areametric method agreed poorly for highly populated buildings (>50 persons) because of high-rise buildings. — [Lwin & Murayama 2009, Table 1 and text](https://mail.cdema.org/virtuallibrary/images/A%20GIS%20Approach%20to%20Estimation%20of%20Building.pdf)
- **Non-residential treatment:** the method allows "filtering by minimum footprint area and building use types, e.g. commercial, industrial, educational, and other building use types that are not occupied by residents". Their best results applied the Volumetric method "to residential buildings" only. — [Lwin & Murayama 2009](https://mail.cdema.org/virtuallibrary/images/A%20GIS%20Approach%20to%20Estimation%20of%20Building.pdf)
- Building type information prevents redistribution of population to non-residential areas, and volume weighting removes density-related bias. — [Schug et al. 2021, PLOS ONE](https://doi.org/10.1371/journal.pone.0249044)
- Other studies of volume-based dasymetric allocation to buildings report mean absolute errors of about **2.3 persons**, RMSE about **4.1 persons** and MAPE about **8.6%**. — [search-result summary of Remote Sensing 13:2835 (Politecnico di Bari)](https://iris.poliba.it/retrieve/dd89f8a6-ffcc-ccdd-e053-6605fe0a1b87/remotesensing-13-02835-v2-compressed.pdf). *Caveat:* this comes from the search snippet. I did not open the paper, so the study area and units are unverified.

### Inferences
**Recommended formula for this project (established method, project-specific parameters).**
- Residential floor area: RFA_i = footprint_area_i × floors_i, where floors_i = max(1, round(height_i / h_storey)).
- Use h_storey ≈ 3.0 m for residential buildings. This is an assumption: Swedish housing is commonly about 2.7–3.0 m floor-to-floor. **Document it as a config parameter.** I did not find an official Swedish source for it.
- Only buildings whose purpose is residential (bostad) get weight. Mixed-use buildings ("bostad + verksamhet") get a damping factor, for example 0.5. This damping factor is **ad hoc**: declare it.
- Pop_i = Pop_cell · RFA_i / Σ_{j ∈ cell, residential} RFA_j.

**Income.**
- Distribute *income sums* with the same weights. Allocated income then follows allocated population.
- Show a *median-like* or *mean* income per building as the cell's (or DeSO's) mean income, not as a building-specific value. Disaggregating income to buildings creates **false precision**, so mark it as "inherited from cell".

**Buildings in no populated cell (4,084 buildings).**
- These get population 0. This is justified because SCB grid cells exist only where people are registered.
- Flag them as "no registered residents (SCB)" rather than "unknown". If any such building is tagged residential, list it in a QA report. Possible reasons are new construction after the reference year, holiday homes or misclassification.

**Cells with residential population but no residential building inside.**
- Fall back to all buildings in the cell weighted by volume, or to area weighting.
- Log the cell ID. This is an **ad-hoc fallback**.

**Validation shortcut.** Aggregate the building estimates back to the **100 m grid** and compare them with SCB's 100 m totals (R², MAE). The 100 m grid is an independent finer layer, so this gives a cheap, thesis-reportable accuracy number.

### Gaps
- I found no Swedish building-level population validation study (for example against Lantmäteriet address or apartment registers). The 100 m grid comparison is the proposed substitute.
- I found no authoritative Swedish storey-height constant.

---

## 3. Handling SCB-specific gaps: perturbation, zero/non-zero inconsistencies, missing cells, 1000 m remainder cells, edge cells

### Takeaway
SCB protects register statistics with the **Cell Key Method (CKM)**, which adds random noise of −3…+3 to counts. As a result, "reported totals are not always equal to the sum of their reported parts". Zeros are not perturbed. The safest shortcuts follow from this:
- Treat the published **total** as authoritative.
- Rescale sub-groups proportionally (one-dimensional raking, or IPF when two margins exist).
- Treat absent cells as "no registered residents" for population.
- Mark derived per-household values in zero-household cells as undefined, not 0 or infinite.

### Cited Findings
- **CKM perturbation size:** each adjustment is drawn at random from −3, −2, −1, 0, 1, 2 and 3. SCB states that "in the vast majority of cases the added uncertainty is marginal compared with other sources of uncertainty". Components do not always sum to the reported totals. Values above zero are perturbed or left unchanged, which implies zeros stay zero. Users who build their own totals should know that "the new total may contain greater uncertainty". — [SCB, Frågor och svar om röjandeskyddet Cell Key Method (CKM)](https://www.scb.se/dokumentation/statistikomraden-under-forandring/utveckling-av-rojandeskyddet/fragor-och-svar-om-rojandeskyddet-cell-key-method-ckm/)
- SCB's open grid statistics page (Statistik på rutor) states that a statistical method protects individuals' data. As a result, "the reported totals are not always equal to the sum of their reported parts". The open grid products cover total population, population by five-year age class and population by sex. They come in 1 km INSPIRE/ETRS89 grids and SWEREF99 TM-adapted grids, with yearly files for 2015–2025. — [SCB, Statistik på rutor](https://scb.se/vara-tjanster/oppna-data/oppna-geodata/statistik-pa-rutor/)
- SCB decided that all register-based, fully enumerated individual statistics should evaluate CKM implementation. — [SCB CKM FAQ](https://www.scb.se/dokumentation/statistikomraden-under-forandring/utveckling-av-rojandeskyddet/fragor-och-svar-om-rojandeskyddet-cell-key-method-ckm/)
- **IPF / raking (Deming & Stephan 1940).** IPF adjusts a table so that its margins match known totals by cycling through the margins and rescaling. It is the classical "least squares adjustment of a sampled frequency table when the expected marginal totals are known". It underlies most synthetic-population tools. — Deming, W.E. & Stephan, F.F. (1940), *Annals of Mathematical Statistics* 11(4):427–444 — [doi:10.1214/aoms/1177731829](https://doi.org/10.1214/aoms/1177731829) (DOI verified via Crossref)

### Inferences
Rules below are labelled **[E] established** or **[H] ad-hoc heuristic**.

1. **Sub-groups vs total (perturbation) [E].** For a cell with total T and sub-groups g_k (for example age classes), use g_k' = g_k · T / Σ g_k. This is one-margin raking. When two margins exist, such as age × sex where only the margins are published, run IPF to fit both. Round with a controlled rounding step, for example largest remainder, if integers are required. Keep the raw values in the dataset, add *_adj* columns and log the per-cell discrepancy |T − Σ g_k|. Under CKM the expected discrepancy is a few units, so a large discrepancy signals a join or parse error rather than perturbation.
2. **Zero households but positive income sum (30 cells) [H].** These are probably CKM or table inconsistencies. Households and income may come from different tables or reference populations, and CKM perturbs each table independently. Do not divide (income per household is undefined). The options are:
   - (a) keep the income sum but mark "per-household value not computable"; or
   - (b) attribute the income to the population count if population > 0. Flag it either way.
   - Never silently set it to 0 or drop it.
3. **Missing cell vs zero [E/H].** SCB grid cells exist only where people live. For *population*, an absent cell = 0 registered residents. This is defensible because the cell universe is defined by presence of residents. For *other variables* (income, households) in a table with 9 fewer cells than the others, absent ≠ 0: treat it as **suppressed or not published** (NA). Impute only if needed, and only by **ratio borrowing [H]**: value_cell = population_cell × (variable / population) of the containing DeSO or the parent 1000 m area. Flag the result "imputed".
4. **1000 m remainder cells [E-adjacent].** A 1000 m cell holds only the population *outside* the 250 m cells it overlaps. Before any area-based step, build the **true support polygon** of each 1000 m cell: the 1 km square minus the union of the overlapping 250 m cells. Otherwise area weighting double-counts space. Building-based allocation avoids most of the problem if each building is assigned to exactly one cell, preferring the 250 m cell when it falls in both.
5. **Cut edge cells at the 70 km² study boundary [H].**
   - Option one: keep a cell's full value only if its centroid lies inside the AOI.
   - Option two (preferred): allocate via buildings inside the AOI using the full-cell denominator. The AOI then receives Pop_cell × (RFA inside AOI / RFA in whole cell), so out-of-AOI buildings must be loaded for the denominator, or the method falls back to the area fraction.
   - State the choice explicitly.
6. **Different reference years (2023/2024/2026) [H].** Do not adjust. Label every layer with its year in the UI and the lineage table. Do not compute cross-year ratios, such as votes 2026 per population 2024, without a caveat.

### Gaps
- I found no SCB methodology PDF specific to grid statistics that describes the mixed 250/1000 m grid ("blandad rutstorlek"), its CKM parameters or its income tables. Only the general CKM FAQ and the open-geodata page were found.
- Whether the project's grid income tables use CKM or older suppression is unverified. Check the delivery documentation (beskrivning) that comes with the purchased or downloaded SCB grid tables.
- The SCB open-geodata page did not state a licence explicitly in the fetched content. SCB open data is generally CC0, but this is from background knowledge and **was not verified here**.

---

## 4. Synthetic vs real vs "seeded" data for user studies

### Takeaway
Visualization-evaluation literature openly accepts **generative or synthetic data** for user studies. It gives representative, *unconfounded* stimuli with known ground truth and avoids privacy problems. Its weakness is ecological validity. A **hybrid** is the most defensible design for this thesis: real geometry plus real aggregates, with planted patterns only where a task needs a known answer.

### Cited Findings
- Schulz et al. argue that "user studies require the display of representative and unconfounded visual stimuli". They note that real data is often hand-picked, which "obscures the view of generality, impairs availability, and potentially violates privacy". Generative data models are often ad-hoc "side projects" that are neither reusable nor general-purpose. They call for more research on generative data models for validation and evaluation. — Schulz, H.-J. et al. (2016), "Generative Data Models for Validation and Evaluation of Visualization Techniques", *BELIV '16*, pp. 112–124 — [doi:10.1145/2993901.2993907](https://doi.org/10.1145/2993901.2993907); [KOPS record](https://kops.uni-konstanz.de/entities/publication/a964b580-a4f8-4b9b-a587-bde5fb3fa862)
- **Synthetic populations via IPF:** Beckman, Baggerly & McKay (1996) created synthetic household populations by fitting census marginals with IPF and then sampling households from microdata. This is the standard reference for IPF-based synthetic populations. — "Creating synthetic baseline populations", *Transportation Research Part A* 30(6):415–429 — [doi:10.1016/0965-8564(96)00004-3](https://doi.org/10.1016/0965-8564(96)00004-3) (DOI verified via Crossref)
- IPF itself — [Deming & Stephan 1940](https://doi.org/10.1214/aoms/1177731829)

### Inferences
| Option | Pros | Cons | Use in thesis |
|---|---|---|---|
| Real data only | Ecological validity; credible to domain experts | Unknown ground truth for task correctness; patterns may be trivial or absent; CKM noise | Free exploration, realism questions |
| Fully synthetic | Full control; known answers; privacy-free | Participants may notice an "unreal" city; weak external validity | Not recommended when real geometry exists |
| **Seeded/hybrid** (real geometry and aggregates, plus planted anomalies such as one block with an energy spike or a district with an atypical vote share) | Known ground truth for accuracy and time measures; still looks real | Must be declared; must not be presented as real findings | **Recommended** for task-based trials |

- **Seeding protocol [H]:**
  - Fix a random seed and keep a "planted pattern register" (entity IDs, magnitude, task it supports).
  - Generate the planted values by perturbing real-derived baselines, for example ×3 on one building's load. Do not use arbitrary values.
  - Keep a `source_flag` column (observed / estimated / synthetic / planted).
  - Hide the flags from participants during trials only if the study design requires it, and disclose this in the ethics and methods section.
- **Synthetic population at small scale.** Full IPF microsimulation is not needed for an IA evaluation. One-margin raking per cell (Section 3) plus building allocation (Section 2) is enough. Go to combinatorial optimisation or IPF-with-microdata only if individual agents are needed, which they are not here.

### Gaps
- I did not retrieve specific immersive-analytics or VR user-study papers that justify synthetic data. Schulz et al. 2016 is the general visualization reference found.

---

## 5. Synthetic building-level electricity and energy consumption

### Takeaway
A transparent **energy-signature (degree-day change-point) model** is easy to explain and to compute on 44,000 buildings in seconds. In it, annual energy intensity by building type comes from Swedish official statistics, scaled by floor area, and daily values follow outdoor temperature through E = base + k·max(0, T_bal − T_out). It is an established measurement-and-verification (M&V) formulation (ASHRAE change-point models). Hourly shape can come from a type-specific daily profile, for example normalised hourly profiles from BDG2 (CC BY 4.0).

### Cited Findings
- **Energy signature / change-point models.** Daily consumption is modelled as a linear combination of heating and cooling degree days at given base temperatures. Heating degree days are max(0, θ_h − θ_d) and cooling degree days max(0, θ_d − θ_c). The ASHRAE 5-parameter change-point model has a base load, a heating slope and a cooling slope plus two change points. — [arXiv 2607.25382 (Bayesian energy signature; summary of standard formulation)](https://arxiv.org/pdf/2607.25382)
- Change-point models were described by Kissock et al. (1998, 2003). They are used in the ASHRAE Inverse Modeling Toolkit (Kissock et al. 2001, ASHRAE RP-1050) and by US-EPA Energy Star. ASHRAE Guideline 14 defines 3-parameter heating (3PH), 3-parameter cooling (3PC) and 5-parameter (5P) models. — [ACEEE 2007 paper](https://www.aceee.org/files/proceedings/2007/data/papers/37_4_084.pdf); [IIR record of the IMT numerical algorithms](https://iifiir.org/en/fridoc/inverse-modelling-toolkit-imt-numerical-algorithms-22451); [arXiv 2607.25382](https://arxiv.org/pdf/2607.25382)
- **Swedish degree days (graddagar).** SMHI computes, for each day, the difference between +17 °C and the daily mean temperature when the mean is below +17 °C, and sums it per month or year. The assumption is that the heating system heats to +17 °C and the rest comes from "free energy" (sun, people, equipment). SMHI Energi-Index/Graddagar cover 310 locations. The plus package offers balance temperatures of +7, +9, +11, +13, +15 and +17 °C. — [SMHI web pages on Energi-Index och graddagar (via search summary)](https://www.smhi.se/professionella-tjanster/hallbara-stader/smhi-energi-index-och-graddagar---normalarskorrigering-varme); [SMHI RMK 26 Graddagsstatistik för Sverige](https://smhi.se/download/18.18f5a56618fc9f08e832ac39/1717802127744/RMK_26%20Graddagsstatistik%20f%C3%B6r%20Sverige..pdf). SMHI's commercial Energi-Index product is **not** open data. Compute degree days yourself from open SMHI temperature observations (Section 6).
- **Swedish energy-use intensities (official statistics), premises (lokaler) 2016.** Energy for space heating and hot water averaged **123 kWh/m²**, and **117 kWh/m²** in district-heated premises. Buildings from 1991–2015 used 111–112 kWh/m² and those from 1941–1960 used 136 kWh/m². By type of premises, all heating types, not temperature-corrected (Table 3.7):

  | Type (Swedish) | English | kWh/m² |
  |---|---|---|
  | Kontor och förvaltning | Offices | 110 ±2 |
  | Skolor | Schools | 128 ±5 |
  | Vård dygnet runt | 24-h care | 123 ±4 |
  | Övrig handel | Retail excluding food | 111 ±5 |
  | Livsmedelshandel | Food retail | 138 ±14 |
  | Hotell, restaurang | Hotels and restaurants | 133 ±8 |
  | Idrottsanläggningar | Sports facilities | 141 ±17 |
  | Varmgarage | Heated garages | 100 ±5 |
  | Övriga lokaler | Other premises | 122 ±10 |

  Temperature-corrected values (Table 3.9) are about 3–5 kWh/m² higher. In 2016 there were 157 million m² of heated premises area, and 68% was heated only by district heating. — Energimyndigheten, *Energistatistik för lokaler 2016* (Swedish Energy Agency, Energy statistics for non-residential premises 2016), ES 2017 — [PDF](https://www.energimyndigheten.se/4ab47a/globalassets/statistik/bostader/energistatistik-for-lokaler-2016.pdf)
- This report covers heating plus hot water. It also describes the split of operating electricity (driftel) into property electricity (fastighetsel, the building's own systems) and business electricity (verksamhetsel, tenant activities), but I found no per-type electricity table in the extracted text. — [same PDF](https://www.energimyndigheten.se/4ab47a/globalassets/statistik/bostader/energistatistik-for-lokaler-2016.pdf)
- Equivalent annual reports exist for multi-dwelling buildings (flerbostadshus), for example 2009–2014. — [Energistatistik för flerbostadshus 2014](https://www.energimyndigheten.se/49a8d5/globalassets/statistik/officiell-statistik/statistikprodukter/energistatistik-i-flerbostadshus/rapporter/energistatistik-for-flerbostadshus-2014.pdf)
- Boverket's public energy-declaration (energideklaration) "trend" reports give counts of declarations, not mean kWh/m² by type. Building types required to declare include buildings with tenancy rights and special buildings over 250 m². — [Boverket trend – energideklarationer 2026-09-01](https://www.boverket.se/contentassets/b3f82f985d514c499e7d674caf0fa90c/2026-09-01-trend---energideklarationer.pdf)
- **BDG2 (Building Data Genome 2):**
  - Scale: 3,053 meters, 1,636 *non-residential* buildings and 19 sites in North America and Europe, recorded **hourly** for 2016–2017 (about 53.6 M measurements).
  - Meter types: electricity, heating and chilled water, steam, solar, water and irrigation.
  - Metadata: primary space usage, floor area, year built, number of floors, EUI, heating type and occupancy.
  - Weather: air and dew temperature, cloud cover, precipitation, sea-level pressure and wind.
  - **Licence: CC BY 4.0 for the data, with metadata under a CC0 public-domain dedication.**
  - Miller, C. et al. (2020), "The Building Data Genome Project 2, energy meter data from the ASHRAE Great Energy Predictor III competition", *Scientific Data* 7:368 — [doi:10.1038/s41597-020-00712-x](https://doi.org/10.1038/s41597-020-00712-x); [PMC full text](https://pmc.ncbi.nlm.nih.gov/articles/PMC7591488); [GitHub](https://github.com/buds-lab/building-data-genome-project-2)
  - Note on sites: the fetched summary listed UK and Irish sites plus an anonymised site among the European ones. Its list also mistakenly included a Canadian site, so the exact European count is unverified.

### Inferences
**Model (established structure; parameters are declared assumptions).**

1. Floor area: A_i = footprint_i × floors_i (Section 2). This approximates heated area (Atemp). Declare it as an overestimate because it ignores unheated attics and garages.
2. Annual heating-and-hot-water intensity EUI_type comes from the table above for lokaler and from Energimyndigheten's flerbostadshus or småhus statistics for housing. Map the building "purpose" (ändamål) to Energimyndigheten categories with an explicit Swedish→English table.
3. Daily energy: E_i(d) = A_i · [ b_type + k_type · max(0, T_bal − T_out(d)) ], with T_bal = 17 °C following SMHI's graddagar convention.
   - Calibrate k_type so the annual sum matches the EUI in a normal year: k_type = (EUI_type − 365·b_type) / HDD_17(year).
   - b_type is the hot-water and base share. A common heuristic is 20–30% of annual heat for hot water in housing; **this is ad hoc and I found no source in this research**.
4. Electricity (separate from heat, because most Helsingborg buildings are probably district-heated): use E_el,i(h) = A_i · EUI_el,type · profile_type(h) / 8760-normalised.
   - The hourly profile shape comes from BDG2 meters of the same primary use (CC BY 4.0, cite it), normalised to mean 1.
   - Add small multiplicative noise, for example lognormal with σ = 0.1 and a fixed seed, so buildings are not identical.
   - EUI_el,type for Swedish premises is **a gap** (see below). A placeholder such as "about 50–80 kWh/m² for offices" would have to be cited before use.
5. Label every value `synthetic: energy-signature model v1`, and keep the parameter table in `configs/`.

**Why this is defensible.** Every number traces to an official mean intensity or an observed temperature series. The model form is the standard M&V energy signature. It stays explainable even though it does not reproduce real buildings.

### Gaps
- I found no verified Swedish **electricity-only** kWh/m² per premises type (fastighetsel + verksamhetsel) in a primary source. A search snippet claimed 5.4–6.5 kWh/m²/month (about 65–78 kWh/m²/yr) for premises, but it came from a trade-press page (fastighetstidningen.se) and **was not verified**. Check the Energimyndigheten STIL2 survey reports or the newer lokaler statistics.
- The Energimyndigheten lokaler figures are from 2016. Newer editions exist but were not fetched.
- I found no Swedish "standard load profiles" (typkurvor/typprofiler) in this research. Grid operators use them in settlement, but no open source was confirmed.
- I did not check IEA/EnergyPlus prototype buildings, the ASHRAE GEPIII Kaggle licence or other "ElectricityLoadProfiles" sources.

---

## 6. Weather data: SMHI open data and alternatives (licences verified)

### Takeaway
**SMHI Open Data (metobs API)** is the first choice. It is free, needs no API key, is licensed **CC BY 4.0** (SE), and provides hourly air temperature, wind, humidity, precipitation, global irradiance and more per station. **Open-Meteo** (CC BY 4.0, free non-commercial tier) and **ERA5 via the Copernicus CDS** (CC BY 4.0 since 2 July 2025) are gridded fallbacks with gap-free hourly series.

### Cited Findings
- **SMHI licence:** SMHI open data is licensed under Creative Commons Attribution 4.0 SE (CC BY 4.0). Copying, distribution and derivative works are allowed, "även för kommersiella ändamål" ("even for commercial purposes"). Attribution is required: users must name SMHI as the source and state whether the material was changed. Consequence-based weather warnings are an exception: they may not be modified. — [SMHI, Villkor för användning](https://www.smhi.se/data/om-smhis-data/villkor-for-anvandning)
- **SMHI metobs access:**
  - It provides 1-minute, hourly, daily and monthly observations from the station network through a REST API with no key or registration. The entry point is https://opendata-download-metobs.smhi.se/api.
  - History is split into a **corrected-archive** (quality-controlled, up to about 3 months ago) and **latest-months** (about the last 4 months, still under quality control). There are also latest-day and latest-hour periods.
  - Sources: [wetterdienst SMHI observation docs](https://wetterdienst.readthedocs.io/en/latest/data/provider/smhi/observation/index.html); [SMHI metobs API docs](https://opendata.smhi.se/apidocs/metobs)
- **SMHI metobs parameters** (from the live API index), including:

  | Key | Swedish name | English | Resolution |
  |---|---|---|---|
  | 1 | Lufttemperatur | Air temperature | Instantaneous, hourly |
  | 2 | Lufttemperatur | Air temperature | Daily mean |
  | 3 | Vindriktning | Wind direction | Hourly, 10-min mean |
  | 4 | Vindhastighet | Wind speed | Hourly, 10-min mean |
  | 6 | Relativ luftfuktighet | Relative humidity | Hourly |
  | 7 | Nederbördsmängd | Precipitation | Hourly sum |
  | 5 | Nederbördsmängd | Precipitation | Daily sum at 06 |
  | 9 | Lufttryck | Sea-level pressure | Hourly |
  | 10 | Solskenstid | Sunshine duration | Hourly |
  | 11 | Global irradians | Global irradiance | Hourly mean (radiation stations only) |
  | 16 | Total molnmängd | Total cloud cover | Hourly |
  | 19 / 20 | Lufttemperatur | Daily min / max temperature | Daily |
  | 21 | Byvind | Gust | Hourly max |
  | 22 | Lufttemperatur | Monthly mean temperature | Monthly |
  | 39 | Daggpunktstemperatur | Dew point | Hourly |
  | 45 | Lufttemperatur | Air temperature | 1-minute |

  — [SMHI metobs API index (version/1.0.json)](https://opendata-download-metobs.smhi.se/api/version/1.0.json)
- **Open-Meteo:** "The data obtained through the API is provided under the terms of the CC-BY 4.0 licence". The free non-commercial tier allows fewer than 10,000 calls per day, 5,000 per hour and 600 per minute. The terms page does not cover the upstream-source licences. — [Open-Meteo Terms](https://open-meteo.com/en/terms)
- **ERA5 / Copernicus:** on 2 July 2025 the "Licence to use Copernicus Products" for the Climate Data Store (CDS), Atmosphere Data Store (ADS) and EWDS was replaced by **CC BY 4.0**. — [ECMWF Forum announcement](https://forum.ecmwf.int/t/cc-by-licence-to-replace-licence-to-use-copernicus-products-on-02-july-2025/13464)
- **BDG2** also includes site weather (temperature, dew point, cloud, precipitation, pressure, wind) under CC BY 4.0, but for its own sites, not Sweden. — [Miller et al. 2020](https://doi.org/10.1038/s41597-020-00712-x)

### Inferences
- Use **SMHI parameter 1 (hourly temperature)** from the station nearest Helsingborg for the energy-signature model, and compute daily means and HDD₁₇ yourself.
  - **Station IDs were not looked up in this research.** List the stations via `/api/version/1.0/parameter/1.json` and pick the nearest Helsingborg station with a corrected archive covering the target year. If the Helsingborg station lacks the year, use the nearest one (for example Ängelholm or Landskrona, both unverified).
- Use Open-Meteo or ERA5 only if station gaps exceed what simple linear interpolation can bridge, for example gaps longer than 6 h. That threshold is **ad hoc**.
- **Attribution line (required by CC BY):** "Contains weather observations © SMHI, CC BY 4.0; values aggregated to daily means and gap-filled by linear interpolation (modified)."

### Gaps
- Station IDs and coverage years for Helsingborg were not verified.
- The upstream licences behind Open-Meteo's historical API, which mixes ERA5 with national weather-service models, were not checked individually.

---

## 7. Documenting the shortcuts transparently and showing estimated data in the IA interface

### Takeaway
Store a **per-value provenance or quality flag** (observed, adjusted, estimated by method X, imputed, synthetic, planted) and a lineage table per derived layer. Expose the flag in the interface with an uncertainty encoding drawn from the cartographic uncertainty-visualization literature (MacEachren et al. 2005; Kinkeldey et al. 2014). Bivariate encodings, such as hatching/texture, transparency or outline for "estimated", or a toggle, are the standard options. Kinkeldey et al. stress that evaluation should be task-centred, which matches an IA thesis.

### Cited Findings
- MacEachren et al. (2005) reviewed geospatial uncertainty visualization: what is known and the open challenges, such as understanding the components of uncertainty, representing it and evaluating whether representations help decisions. — "Visualizing Geospatial Information Uncertainty: What We Know and What We Need to Know", *CaGIS* 32(3):139–160 — [doi:10.1559/1523040054738936](https://doi.org/10.1559/1523040054738936) (DOI verified via Crossref)
- Kinkeldey, MacEachren & Schiewe (2014) systematically reviewed geospatial uncertainty-visualization user studies. They found that most research develops new depictions and only a small part evaluates them empirically. They stress "the importance of user tasks" and recommend task-centred typologies for systematic evaluation. — "How to Assess Visual Communication of Uncertainty? A Systematic Review of Geospatial Uncertainty Visualisation User Studies", *The Cartographic Journal* 51(4):372–386 — [doi:10.1179/1743277414Y.0000000099](https://reposit.haw-hamburg.de/handle/20.500.12738/13452); [HCU repository PDF](https://repos.hcu-hamburg.de/bitstream/hcu/758/1/How%20to%20Assess%20Visual%20Communication%20of%20Uncertainty%20A%20Systematic%20Review%20of%20Geospatial%20Uncertainty%20Visualisation%20User%20Studies.pdf)
- MacEachren et al. (2012) ran an empirical study of how visual variables (for example fuzziness, location, value, arrangement, size, transparency) are judged as intuitive uncertainty signifiers. — "Visual Semiotics & Uncertainty Visualization: An Empirical Study", *IEEE TVCG* 18(12):2496–2505 — [doi:10.1109/TVCG.2012.279](https://doi.org/10.1109/TVCG.2012.279). DOI verified via Crossref; the result details come from background knowledge, so verify them before quoting rankings.
- SCB itself warns users that self-built totals from perturbed cells may carry larger uncertainty. That supports carrying an "aggregated from perturbed cells" flag downstream. — [SCB CKM FAQ](https://www.scb.se/dokumentation/statistikomraden-under-forandring/utveckling-av-rojandeskyddet/fragor-och-svar-om-rojandeskyddet-cell-key-method-ckm/)
- Attribution and change-marking are *licence obligations* for SMHI (CC BY 4.0), Open-Meteo (CC BY 4.0), ERA5 (CC BY 4.0) and BDG2 (CC BY 4.0). — [SMHI villkor](https://www.smhi.se/data/om-smhis-data/villkor-for-anvandning); [Open-Meteo terms](https://open-meteo.com/en/terms); [ECMWF](https://forum.ecmwf.int/t/cc-by-licence-to-replace-licence-to-use-copernicus-products-on-02-july-2025/13464); [BDG2 paper](https://pmc.ncbi.nlm.nih.gov/articles/PMC7591488)

### Inferences
**Per-value schema [H, but standard practice].** For every runtime attribute, store:

| Field | Content |
|---|---|
| `value` | the value shown |
| `value_raw` | the original value, if any |
| `source_dataset` | source dataset (Swedish and English name) |
| `ref_year` | reference year |
| `method` | enum: `observed`, `raked_to_total`, `dasymetric_rfa`, `dasymetric_pop100`, `area_weighted`, `ratio_imputed_deso`, `energy_signature_v1`, `planted` |
| `quality_flag` | `observed`, `adjusted`, `estimated`, `imputed`, `synthetic`, `planted` |
| `uncertainty_note` | short free-text note |

Keep a lineage table per layer (inputs, parameters, run timestamp), consistent with the project's CLAUDE.md logging rules.

**UI mapping (suggested).**
- Observed values: solid colour.
- Estimated or disaggregated values: the same colour with a hatch, desaturation or transparency.
- Synthetic or planted values: a distinct outline or icon, plus a legend entry "modelled, not measured".
- A global "show data quality" toggle, so the encoding does not clutter the default view.
- Log whether participants turned the toggle on. This creates a measurable interaction for the IA evaluation.

**Recommended 2–4 shortcuts for the thesis (synthesis):**
1. **Building-based (volumetric) dasymetric disaggregation** of SCB grid population and income sums to residential buildings, then re-aggregation to valdistrikt/DeSO (Lwin & Murayama 2009; Schug et al. 2021; Mennis 2003). Validate against the 100 m grid.
2. **Raking sub-groups to published totals** (one-margin or IPF; Deming & Stephan 1940), justified by SCB's CKM documentation. Use explicit missing-vs-zero rules.
3. **Energy-signature synthetic energy model** (ASHRAE change-point form; Energimyndigheten intensities; SMHI CC BY 4.0 temperatures; optionally BDG2 hourly shapes), labelled as synthetic.
4. **Seeded and declared planted patterns** for task ground truth (Schulz et al. 2016), with per-value quality flags shown through an uncertainty encoding (MacEachren et al. 2005; Kinkeldey et al. 2014).

### Gaps
- I did not retrieve uncertainty-visualization studies specific to immersive or VR settings, so the guidance above is from 2D cartography.
- I found no published standard schema for per-value provenance flags in urban digital twins. The schema above is a proposal.
