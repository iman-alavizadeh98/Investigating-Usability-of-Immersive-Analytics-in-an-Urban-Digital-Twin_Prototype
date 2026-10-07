# Urban data analysis scenarios for evaluating an immersive urban digital twin (central Helsingborg)

Scope note: these notes combine (a) published Swedish/European sources and (b) a read-only check of the project's own data, done 2026-10-07. Project-data results are marked **[project data]**. How they were computed:
- 2026 municipal election, Valmyndigheten delivery in `Raw_data/1-Helsingborig/election/`, loaded with `Src/pipelines/election/loader.py`.
- Each district's share of area inside the SCB/building rectangle (E 355,285–361,855, N 6,207,503–6,218,191).
- DeSO values from the SCB delivery (`Tab11_DeSO_2023` median income, `Tab4_DeSO_2024` country of birth), area-weighted onto voting districts.
- Spearman correlations across the 60 districts lying fully (>99 %) inside the rectangle.

These are ecological (area-level) figures, not individual-level ones. The SCB delivery is licensed FUK (research and education only). Check the licence before quoting the income and country-of-birth figures outside the thesis. The election results themselves are public.

---

## 1. What is known in Sweden about neighbourhood income, education, country of birth and age vs. turnout and party shares (with Helsingborg specifics)

### Takeaway
Swedish register data shows the same strong pattern every time:
- Turnout rises with income and education, and is lower among the foreign-born and the young.
- Turnout is lowest, and varies most, in areas with large socioeconomic challenges (SCB area type 1). There it fell 67 → 58 % between 2018 and 2022, while type-5 areas stayed at about 91–93 %.

Party patterns:
- S and V are stronger among the foreign-born and in lower-income areas.
- M, L, C and KD are stronger among the highly educated and in higher-income areas.
- SD is stronger among Swedish-born men with lower or middle education, and in areas outside the central town. That gives SD a weak, non-linear relation to income inside a city.

The project's 2026 Helsingborg data reproduces all of these patterns strongly (turnout vs DeSO median income ρ = 0.92), so they can serve as ground truth for tasks.

### Cited Findings

**National turnout (register-based, SCB Demokratistatistik)**
- 2022 turnout by education: post-secondary 93 %, upper-secondary 85 %, pre-secondary (förgymnasial) 74 %. Turnout fell in all education groups since 2018, most among those with pre-secondary education only. — [SCB press release "Ny rapport visar grupperna där valdeltagandet sjönk mest" (report *Deltagande i de allmänna valen 2022*, published 2023-06-05)](https://www.scb.se/pressmeddelande/ny-rapport-visar-grupperna-dar-valdeltagande-sjonk-mest/)
- "The higher the income, the greater the propensity to vote." Turnout fell in all income groups, most in the lowest. The native/foreign-born gap is largest at ages 20–30. — [SCB press release, 2023](https://www.scb.se/pressmeddelande/ny-rapport-visar-grupperna-dar-valdeltagande-sjonk-mest/)
- 2022 study of 8.2 million eligible voters (600,000 foreign citizens), *Delat deltagande*, Demokratistatistik 2023:2. — [SCB 2023, PDF](https://scb.se/contentassets/048c2c293c404f3e899e91b844b6b9c2/studie_om_valdeltagande_i_de_allmanna_valen_2022_utifran_ett_integrations-_och_segregationsperspektiv_ju2022-03548.pdf)
  - Turnout among Swedish citizens: 86 %. Among foreign citizens voting in municipal/regional elections: 30 %.
  - Turnout fell 7 pp among foreign-born Swedish citizens, 9 pp among foreign citizens, and only 1 pp among Swedish-born people with two Swedish-born parents.
  - Among the foreign-born, turnout is highest for those born in the Nordic countries and lowest for those born in Europe outside the EU/Nordics. It fell most among people born in Africa and Asia.
  - People under 30 vote less, and so do people over about 75–80.
- Same SCB 2022 study, area perspective. DeSO areas are grouped into five area types by a socioeconomic index. — [SCB 2023](https://scb.se/contentassets/048c2c293c404f3e899e91b844b6b9c2/studie_om_valdeltagande_i_de_allmanna_valen_2022_utifran_ett_integrations-_och_segregationsperspektiv_ju2022-03548.pdf)
  - Share of population per area type: 5 % type 1, 8 % type 2, 22 % type 3, 53 % type 4, 12 % type 5.
  - Turnout change 2018 → 2022: type 1 fell from 67 % to 58 %; type 5 from 93 % to 91 %.
  - Range of DeSO turnout within each type: type 1 from 26 % to 80 % (median 57 %); type 2 from 36 % to 87 %; type 5 from 80 % to 97 %. Spread is much larger in the disadvantaged types.
  - Composition of type-1 electorates: only 26 % are Swedish-born with Swedish-born parents; 43 % are foreign-born Swedish citizens and 20 % foreign citizens.
  - In logistic models that control for individual resources (education, income) and family situation, the area-type effect largely disappears. A small extra effect remains for type 5.
  - Of all DeSO areas, 10 had significantly higher turnout than expected from their composition and about 194 lower. All the "higher" areas, and most of the "lower" ones, are in municipal central towns.
- SD support and education (SCB party preference survey, PSU, May 2022), for comparison with education grids. — [SCB PSU May 2022](https://www.scb.se/hitta-statistik/statistik-efter-amne/demokrati/partisympatier/partisympatiundersokningen-psu/pong/statistiknyhet/partisympatier-maj-2022/)
  - SD: higher among men, ages 50–64, the Swedish-born, and those with pre-secondary or upper-secondary education.
  - M: higher among the Swedish-born and those with 3+ years of post-secondary education.
  - S: higher among the foreign-born, ages 65+, and those with pre-secondary education.
  - V: higher among women, ages 18–29, and those with post-secondary education.
  - MP, C and L: higher among the more educated.

**Party geography**
- 2022 Riksdag election: SD got close to 25 % in rural municipalities and just over 16 % in metropolitan municipalities, a gap of almost 9 pp. In 2010 the gap was about 1 pp. Within municipalities, SD tends to grow most in outer areas outside the central town. Left parties (V, MP, S, C) did relatively better in metropolitan areas in 2022, right parties relatively better outside them. — [Öhrvall, R. (2022) "En klyfta mellan stad och land?", in Bolin et al. (eds.) *Snabbtänkt 2.0* / Vändpunkter, DEMICOM-rapport 51, Mittuniversitetet (PDF via IFN)](https://ifn.se/media/xvalkjt5/2022-öhrvall-en-klyfta-mellan-stad-och-land.pdf)
- 2022 national results: SD 20.5 % (+3 pp, second-largest party), M 19.1 %. — [Valforskningsprogrammet, *Väljare och val* ch. 2 "Hur valet 2022 slutade" (GU, 2024)](https://gu.se/sites/default/files/2024-05/VoV_kap2.pdf)
- Municipality-level analysis of SD support across 290 municipalities (from the search summary; full text not opened).
  - Support is concentrated in southern Sweden.
  - Positively related to low education, the 24–44 age share, a rising share of European immigrants, low income and crime.
  - Negatively related to the share of non-European immigrants.
  - — [Masaryk Univ. publication record](https://muni.cz/en/research/publications/956046)
- A local rise in the insider–outsider income gap, and the share of "vulnerable insiders", is associated with larger SD gains. (Dal Bó, Finan, Folke, Persson & Rickne, *Economic Losers and Political Winners: Sweden's Radical Right*; the page could not be opened, finding from the search summary.) — [Harvard PE group page](https://valveresearchgroup.hms.harvard.edu/pegroup/publications/economic-losers-and-political-winners-swedens-radical-right)

**Helsingborg facts**
- 2022 municipal council election, Helsingborg (1283): S 31.20 % (27,591 votes), M 23.68 %, SD 21.47 %, V 5.43 %; eligible voters 116,564; turnout 76.88 %. — [Valmyndigheten, protocol KF 2022 Helsingborg](https://resultat.val.se/protokoll/protokoll_Val_20220911_1283_KF.pdf)
- Police list of vulnerable areas, Region Syd (dated 2025-12-01, dnr A034.946/2025). In Helsingborg it lists **Dalhem / Drottninghög / Fredriksdal** (utsatt område, "vulnerable area") and **Söder** (utsatt område). No Helsingborg area is classed "särskilt utsatt" (especially vulnerable). Planteringen is not on the list. — [Polisen, region_syd.pdf](https://polisen.se/siteassets/dokument/organiserad_brottslighet/utsatta-omraden/region_syd.pdf)
- **Caution on a frequently quoted figure.** "Drottninghög 23.62 % turnout, Furutorp-Högaborg 28.24 %" comes from the **January 2020 municipal referendum on selling Öresundskraft**, not from a general election.
  - Overall participation in that referendum: 50.6 %. Rydebäck and Råå were highest, above 68 %. 96.37 % voted no.
  - Do not use these as general-election ground truth.
  - — [SVT Helsingborg "Valet i siffror"](https://www.svt.se/nyheter/lokalt/helsingborg/valet-i-siffror)
- **[project data] 2026 municipal election, district level.** 92 districts plus 1 collection district. Turnout 57.6–93.4 % (median 78.9 %). Municipal result: S 29.9 %, M 25.5 %, SD 18.7 %, V 9.5 %, Helsingborgspartiet 1.0 %. — `docs/data-analysis/2026-10-07_helsingborg_election_2026_profiling.md` (Valmyndigheten data)
- **[project data] Which neighbourhoods lie inside the 70 km² model rectangle** (by voting-district name):
  - **Fully inside (60 districts):**
    - Drottninghög V/Ö
    - Dalhem S, Dalhem-Källstorp
    - Fredriksdal V/M/Ö
    - Söder
    - Planteringen S
    - Tågaborg N/M/C/S/Ö
    - Råå N
    - Ramlösa N/Ö, Ramlösabrunn
    - Centrum S/Ö
    - Högaborg V/Ö, Furutorp, Eneborg V/M/Ö
    - Mariastaden S/Ö
    - Gustavslund V
    - Husensjö
    - Oceanhamnen
    - Raus S
    - Rosengården V/C/Ö
    - and others
  - **Partly inside (13):** Centrum V, Pålsjöbaden, Mariastaden N/V, Margaretaplatsen, Gustavslund Ö, Gustav Adolf, Råå S, Högasten, Planteringen N, Björka-Väla, Bårslöv V, Brohult.
  - **Outside (19):** Påarp V/Ö, Rydebäck N/S/M/Ö, Laröd V/Ö, Hittarp-Domsten, Ödåkra V/Ö, Kattarp N/S, Allerum, Mörarp V/Ö, Bårslöv Ö, Gantofta, Vallåkra-Ottarp.
  - So **Påarp and Rydebäck are not in the model.** Drottninghög, Planteringen (S), Söder, Tågaborg and Råå (N) are.
  - The profiling report's 59/17/16 split used a different threshold.
- **[project data] Extremes among the 60 fully covered districts, 2026:**
  - **Turnout, lowest:**
    - Furutorp 57.6 %
    - Eneborg M 58.5 %
    - Högaborg V 59.0 %
    - Söder 60.2 %
    - Högaborg Ö 60.5 %
    - Fredriksdal Ö 60.7 %
    - Drottninghög V 65.2 %
    - Drottninghög Ö 67.6 %
  - **Turnout, highest:**
    - Gustavslund V 90.6 %
    - Ramlösa Ö 90.3 %
    - Ramlösa N 90.2 %
    - Husensjö 89.6 %
    - Ramlösabrunn 89.5 %
    - St Jörgens plats 88.6 %
  - **Area-weighted DeSO median income:**
    - lowest: Drottninghög Ö ≈206k SEK, Högaborg V ≈217k, Söder ≈218k
    - highest: Ramlösa Ö ≈411k, Husensjö ≈403k, Ramlösa N ≈392k, Humlegården ≈389k
  - **Foreign-born share (DeSO-weighted):**
    - lowest: Råå N ≈8 %, Ramlösa N ≈12 %, St Jörgens plats ≈12 %
    - highest: Högaborg V ≈59 %, Drottninghög Ö ≈57 %, Fredriksdal Ö ≈57 %, Söder ≈56 %
  - **M:** highest in Tågaborg N 46.6 %; lowest in Drottninghög Ö 4.0 %.
  - **V:** highest in Fredriksdal Ö 33.7 %, Drottninghög Ö 32.2 %, Furutorp 30.0 %; lowest in St Jörgens plats 2.0 %, Ramlösa Ö 2.0 %.
  - **SD:**
    - highest: Ättekulla Ö 26.5 %, Rosengården V 25.3 %, Lussebäcken 25.1 %, Ättekulla N 24.6 %
    - lowest: Furutorp 6.8 %, Fredriksdal Ö 10.0 %
    - also relatively low in the richest districts: Tågaborg N 13.4 %, Gustavslund V 14.9 %
- **[project data] Spearman ρ across the 60 districts:**

  | Variable | vs median income | vs foreign-born share |
  |---|---|---|
  | Turnout | +0.92 | −0.93 |
  | M | +0.90 | −0.87 |
  | L | +0.82 | −0.83 |
  | C | +0.81 | −0.79 |
  | S | −0.83 | +0.85 |
  | V | −0.91 | +0.91 |
  | SD | +0.22 | −0.33 |
  | Helsingborgspartiet | ≈0 | ≈0 |

- **[project data] Inner vs outer municipality**, weighted by votes cast:

  | Area | Turnout | SD | M | S | V |
  |---|---|---|---|---|---|
  | Districts inside the rectangle | 75.5 % | 17.6 % | 22.4 % | 32.7 % | 11.6 % |
  | Districts outside it | 85.0 % | 22.6 % | 33.2 % | 22.2 % | 3.5 % |

  - Outer villages are SD-strong: Påarp Ö 32.5 %, Kattarp S 31.9 %, Mörarp Ö 31.4 %.
  - Outer coastal suburbs are M-strong: Hittarp-Domsten 52.9 %, Laröd V 47.7 %.
  - This matches Öhrvall's "SD grows most outside the central town".

### Inferences
- **Ground-truth patterns usable for task checking** (all confirmed in project data):
  1. Turnout is higher where median income is higher, and lower where the foreign-born share is higher. Both relations are very strong.
  2. V and S shares go the opposite way to M, L and C.
  3. SD does **not** follow income linearly. It is low in high-foreign-born, low-income districts, highest in middle-income districts with a mostly Swedish-born population, and somewhat lower in the richest inner districts.
  4. Outer, mostly rural districts have higher SD and M shares and higher turnout than the inner city. Most of them are outside the model, though.
- Pattern 3 is a good **"surprise" task**: users who expect "low income → SD" will be wrong. That makes it a realistic test of whether the system supports checking assumptions rather than confirming them.
- **Ecological-fallacy caution for task wording.** The SCB regressions show area effects shrink once individual resources are controlled for. Tasks should say "districts with lower median income have lower turnout", not "poorer people vote less in Helsingborg".
- **Foreign citizens in the electorate.** Foreign citizens can vote in municipal elections (SCB reports their turnout separately at 30 %). So municipal-election turnout in high-migrant districts mixes in this low-turnout group. This is one reason 2026 municipal turnout in Furutorp or Söder can be near 58–60 %.
- **Eligible voters vs residents.** The median ratio is 0.80 [project data]. A height encoding of "eligible voters" and one of "residents" will differ, most in districts with many children or many non-eligible residents.
- **Expected stability.** The 2022 national pattern (type-1 area median ≈57 %) and the Helsingborg 2026 lowest districts (≈58–61 %) are of the same magnitude. That supports using 2022 literature as the reference for 2026 data. This is an inference; the 2026 national turnout analysis by SCB is not yet published.

### Gaps
- No 2022 district-level party results for Helsingborg were extracted. The protocol PDF gives municipal totals only, so a 2022 vs 2026 district comparison has not been done. Valmyndigheten's 2022 per-district file would be needed for a "change over time" task.
- A search snippet claimed the 2018 municipal election had its lowest Helsingborg turnout in Drottninghög V (63 %) and Drottninghög Ö (66 %). The source (a Centerpartiet "Demokratirapport" PDF) returned 404, so this is **unverified**.
- A snippet that "19 % turnout in western Drottninghög" referred to the 2019 EU election (SVT). It was not opened, so it is unverified.
- Exact turnout by income quintile (numbers, not just direction) and by birth region for 2022 sit in SCB tables that were not extracted.
- Rules for foreign citizens' municipal voting rights (residence requirements) were not re-verified this session on val.se.
- No Helsingborg-specific academic study of electoral geography was found. Elisabeth Högdahl's municipality-commissioned book on the north/south divide is mentioned in a search summary, but was not opened.
- The DeSO-to-district area weighting assumes values are spread evenly over each DeSO. See §5, the "data-error spotting" scenario about Oceanhamnen.

---

## 2. Residential segregation indicators in Swedish cities and whether computing or visualising them is a reasonable immersive-analytics task

### Takeaway
The Swedish official toolkit (Delmos → Boverket Segregationsbarometern) uses two measures:
- a **socioeconomic index (SEI)** per RegSO, classified into **five area types**;
- an **inequality index (ojämlikhetsindex)**, a dissimilarity index between income quintile 1 and quintile 5 households, per municipality.

Both can be approximated from the project's 250 m grid: income quartiles, education, country of birth. A dissimilarity or "concentration" view is a reasonable advanced IA task, but answers depend strongly on the spatial unit (MAUP). Tasks should therefore ask users to *compare* or *locate*, not compute an index by hand.

### Cited Findings
- **The two measures in Segregationsbarometern** (Boverket report 2024:18, *Boendesegregationens utveckling*, June 2024, by H. Jonsson & J. Kihlberg). — [Boverket 2024:18](https://segregationsbarometern.boverket.se/app/uploads/2024/09/2024-18-Boendesegregationens-utveckling.pdf)
  - The **socioekonomiskt index** (socioeconomic index), measured per RegSO, combines three indicators:
    1. the share of people with low economic standard;
    2. the share with pre-secondary education;
    3. the share with long-term social assistance (≥10 months) and/or unemployment over 6 months.
  - The **ojämlikhetsindex** (inequality index) measures how unevenly the lowest (Q1) and highest (Q5) income quintiles live relative to each other in a municipality or county, on a 0–100 scale.
- Area types are **relative**: the cut-offs come from the national mean and standard deviation each year. A change in type does not necessarily mean an absolute change. — [Boverket 2024:18](https://segregationsbarometern.boverket.se/app/uploads/2024/09/2024-18-Boendesegregationens-utveckling.pdf)
- RegSO counts by type, 2022 (of 3,363 RegSO; 5,984 DeSO). About 13.4 % of the population lives in types 1–2, and 64.2 % in types 4–5. — [Boverket 2024:18](https://segregationsbarometern.boverket.se/app/uploads/2024/09/2024-18-Boendesegregationens-utveckling.pdf)

  | Area type | RegSO | Share |
  |---|---|---|
  | 1 (large challenges) | 160 | 4.8 % |
  | 2 | 289 | 8.6 % |
  | 3 (mixed) | 855 | 25.4 % |
  | 4 | 1,770 | 52.6 % |
  | 5 (very good conditions) | 289 | 8.6 % |

- **Trend 2012–2022:** smaller municipalities, which started with lower inequality-index values, saw rising segregation. Larger municipalities, which started higher, saw a decrease. — [Boverket 2024:18](https://segregationsbarometern.boverket.se/app/uploads/2024/09/2024-18-Boendesegregationens-utveckling.pdf)
- **Scale effect (MAUP).** DeSO areas were built with 700–2,700 residents, most around 1,700. A larger area such as a whole district "tends to appear more mixed" than a block or small housing area. Analysis at RegSO level can hide DeSO areas with completely different conditions inside the same RegSO. — [Boverket 2024:18](https://segregationsbarometern.boverket.se/app/uploads/2024/09/2024-18-Boendesegregationens-utveckling.pdf)
- Area types are a RegSO classification; the index and the types are published in Segregationsbarometern. — [SCB Statistikdatabasen, IntGr5Socio table](https://www.statistikdatabasen.scb.se/pxweb/en/ssd/START__AA__AA0003__AA0003X/IntGr5Socio/)
- Boverket has compared the police list with the area types (document not opened). — [Boverket/Segregationsbarometern note](https://segregationsbarometern.boverket.se/app/uploads/2022/06/Skillnader-och-likheter-mellan-polisens-lista-och-omradestyperna.pdf)
- SCB used DeSO area types to show that turnout is lowest and most dispersed in type 1–2 areas (see §1). — [SCB 2023](https://scb.se/contentassets/048c2c293c404f3e899e91b844b6b9c2/studie_om_valdeltagande_i_de_allmanna_valen_2022_utifran_ett_integrations-_och_segregationsperspektiv_ju2022-03548.pdf)

### Inferences
- **Feasible with current project data** (not computed yet):
  - **(a)** A dissimilarity index between Q1 and Q4 households across 250 m cells, from `Tab11_Ruta_2023` (Kvartil1–4). It approximates Boverket's Q1/Q5 index, but uses quartiles, not quintiles.
  - **(b)** Dissimilarity between Swedish-born and "rest of world"-born residents across cells, from `Tab4`.
  - **(c)** A simplified SEI proxy per cell: the Q1 share plus the pre-secondary education share (`Tab10`, ages 25–64). The third Boverket indicator (social assistance and unemployment) is **not** in the delivery.
  - Standard formula: D = ½ Σ |aᵢ/A − bᵢ/B|. Isolation and concentration measures can be derived the same way.
- **Good IA task forms:**
  - "Find the cluster of cells where low-income households are concentrated. Is it the same place as the cluster of residents born outside Europe?" Expected yes: the Söder / Drottninghög / Fredriksdal / Högaborg belt, consistent with §1 project data and the police list.
  - "Does the picture change if you switch from 250 m cells to DeSO?" This is a MAUP demonstration with a verifiable answer: the DeSO view should look more mixed.
  - Computing D by hand in VR is not a realistic usability task. Precompute it and let users find or compare it.
- **Area types are relative and national.** Labelling a Helsingborg cell "type 1" from project data would be an approximation. The official RegSO types would have to be downloaded from Boverket or SCB to be authoritative.

### Gaps
- Official RegSO area types and SEI values for Helsingborg's RegSO areas were not retrieved. They are available in Segregationsbarometern and SCB, but the values were not extracted here.
- No national or Helsingborg values of the ojämlikhetsindex were extracted. Boverket's chart shows all 290 municipalities, but numbers were not read off.
- No source was fetched for isolation or concentration indices (beyond D) in Swedish practice.

---

## 3. Typical urban-analytics scenarios used in digital-twin / immersive-analytics demos and evaluations

### Takeaway
Urban visual-analytics and IA systems are usually evaluated with a small set of task types:
- lookup of an extreme value or outlier;
- comparison of two areas or periods;
- correlation or co-location of two variables;
- impact or decision tasks ("where should X go");
- temporal pattern finding.

Results consistently show a **speed vs accuracy trade-off** between colour-coded encodings on the 3D model (faster) and plots or glyphs (more accurate, better for complex tasks). Chen et al.'s linked / embedded / mixed view typology is the standard vocabulary for 2D-in-3D design.

### Cited Findings
- **Chen, Wang, Sun, Gao, Chen, Pan, Qu & Wu (2017)**, "Exploring the design space of immersive urban analytics", *Visual Informatics* 1(2):132–142. — [arXiv 1709.08774](https://arxiv.org/pdf/1709.08774); [UMN record](https://experts.umn.edu/en/publications/exploring-the-design-space-of-immersive-urban-analytics/)
  - Proposes a model of immersive urban analytics, and a typology for combining 2D and 3D: **linked views, embedded views, mixed views**.
  - Gives guidelines for choosing between them by visual geometry and spatial distribution.
  - Notes that head-mounted displays add 3D context and presence to urban visual analytics.
- **Mota, Ferreira, Silva et al. (IEEE VIS 2022)**, "A Comparison of Spatiotemporal Visualizations for 3D Urban Analytics". — [arXiv 2208.05370](https://arxiv.org/abs/2208.05370)
  - Users "identify extreme values on building surfaces over time".
  - Compared spatial juxtaposition, temporal juxtaposition, linked view and embedded view.
  - Colour-coded designs were faster; plot-based designs (linked, embedded) were more accurate.
  - As tasks get harder, plot-based designs preserve time and accuracy better.
- **Ferreira, Lage, Doraiswamy, Vo, Wilson, Werner, Park & Silva (VAST 2015)**, "Urbane: A 3D framework to support data driven decision making in urban development". — [KPF UI project page](https://ui.kpf.com/ieeevast-urbane)
  - Designed with architects to explore several data layers in 3D.
  - Assesses the impact of a new development on the "character and value of a neighbourhood".
  - This is the archetype of the "decision / impact" scenario.
- **Friedl-Knirsch, Pointecker, Pfistermüller, Stach, Anthes & Roth (2024)**, "A Systematic Literature Review of User Evaluation in Immersive Analytics", *Computer Graphics Forum* 43(3), DOI 10.1111/cgf.15111. — [EG Digital Library](https://diglib.eg.org/handle/10.1111/cgf15111); [FH OÖ record](https://pure.fh-ooe.at/en/publications/a-systematic-literature-review-of-user-evaluation-in-immersive-an/)
  - The first PRISMA review of IA evaluation practice.
  - Per the search summary, graph analysis is the most common task type in IA studies.
  - The full text was blocked (403), so no proportions were extracted.
- **Maiullari, Nägeli, Rundenå & Thuvander (2023)**, "Gothenburg Digital Twin. Modelling and communicating the effect of temperature change scenarios on building demand", *J. Phys.: Conf. Ser.* 2600 032006 (CISBAT 2023). — [Chalmers full text](https://research.chalmers.se/publication/539110/file/539110_Fulltext.pdf)
  - A Swedish city-DT viewer: LOD1 buildings from LiDAR heights, coloured by energy-demand class (kWh/m²·yr), with a user-selectable climate scenario.
  - The authors say a test of use by decision-makers is still future work, i.e. no user evaluation was reported.

### Inferences
- **Scenario families that map onto these task types**, using the project's own data. The full catalogue is in §5.
  1. **Lookup / extreme:** "Which voting district has the lowest turnout?"
  2. **Compare A/B:** "Tågaborg N vs Drottninghög Ö: how do they differ on all variables?"
  3. **Correlate / co-locate:** "Is turnout lower where income is lower?"
  4. **Outlier / exception:** "Find a district where SD is unexpectedly low or high given its income."
  5. **Distribution / cluster:** "Where do young adults (20–24, 25–44) concentrate relative to the centre?"
  6. **Decision / priority:**
     - "Where should a new preschool or elderly-care unit go, given the age structure?"
     - "Which buildings or districts should be prioritised for a turnout campaign or energy renovation?"
  7. **Temporal (future):** time playback of energy or weather.
- Mota et al.'s result suggests including at least one task that is **harder in colour-only mode** (multi-attribute, or reading a precise value). That way glyph or plot features have a measurable benefit.
- Decision tasks (family 6) are engaging but have no single correct answer. Pair them with explicit criteria (e.g. "the 250 m cell with the most 0–6-year-olds within the model") to make them verifiable.

### Gaps
- No IA user study using **census or election data on a 3D city model** was found in this search round. Most retrieved IA studies use graphs or generic data, which supports the novelty of the thesis but leaves no direct task template.
- Proportions of task types and measures (SUS, NASA-TLX, time, accuracy) from the 2024 review could not be extracted (403).
- A 2026 arXiv paper on occlusion-free lensing for 3D urban analytics, with a between-subjects study, appeared in results but was not read: [arXiv 2602.03743](https://arxiv.org/pdf/2602.03743).

---

## 4. Energy and weather scenarios (if data arrives) and Swedish public sources

### Takeaway
The standard energy-DT scenarios are:
- colouring buildings by energy intensity (kWh/m²·yr);
- comparing building types or ages;
- relating demand to outdoor temperature / heating degree days;
- seasonal or daily playback with a winter peak.

Public Swedish sources exist for weather (SMHI, CC BY 4.0, open API) and for building energy performance (Boverket *energideklarationer*: free API but needs a signed agreement), plus municipal totals (SCB EN0203). No open, building-level **metered** electricity or district-heating data for Helsingborg was found. Öresundskraft open data is unverified.

### Cited Findings
- **SMHI open data:** licence Creative Commons Attribution 4.0 SE. Commercial use and derivatives are allowed; you must name SMHI as source and indicate changes. Only documented APIs may be used, and mass downloading is to be avoided. — [SMHI "Villkor för användning"](https://www.smhi.se/data/om-smhis-data/villkor-for-anvandning)
  - SMHI's open meteorological observations come at 1-minute, hourly, daily and monthly resolution, with no API key (via the wetterdienst documentation, a secondary source). — [wetterdienst SMHI docs](https://wetterdienst.readthedocs.io/en/latest/data/provider/smhi/observation/index.html)
- **Boverket energy declarations** (energideklarationer). — [Boverket "API för energideklarationer"](https://www.boverket.se/sv/om-boverket/oppna-data/publikt-api-for-energideklarationer/); [Boverket open data](https://www.boverket.se/sv/om-boverket/oppna-data/)
  - A free API returns, per building: energy class (A–G), declaration ID, energy performance (primary-energy number) and specific energy use.
  - Access requires signing an agreement with Boverket.
  - Boverket's open-data terms allow copying, distribution and processing with attribution.
- **Energy declarations in a Swedish city DT** (Gothenburg). — [Maiullari et al. 2023](https://research.chalmers.se/publication/539110/file/539110_Fulltext.pdf)
  - Data used: HVAC type, number of floors, heated floor area and energy carrier.
  - EPC coverage is "comprehensive" for residential buildings but incomplete for non-residential, where gaps were imputed from the company registry and the 50 nearest buildings.
  - Findings:
    - Gothenburg's baseline (TMY 2018) building final demand is 4,263 GWh/yr, peaking in January (680 GWh) and lowest in August (118 GWh).
    - Under RCP 8.5 (2050), demand drops up to 18 % in April and September, and summer demand rises up to 1.5 %.
    - Multifamily houses are most climate-sensitive (−4.4 / −8.3 / −12.4 % for RCP 2.6 / 4.5 / 8.5); offices least (−1.2 to −4.8 %).
    - Daily cooling demand on hot days can rise up to 300 %.
- **SCB Kommunal och regional energistatistik (EN0203)** (municipal and regional energy statistics): annual electricity and district-heating production and final energy use per municipality and county, by consumer category. It is produced for Energimyndigheten (the Swedish Energy Agency) and used for municipal energy planning. — [SCB EN0203](https://www.scb.se/hitta-statistik/statistik-efter-amne/energi/energibalanser/kommunal-och-regional-energistatistik/)
- **Region Skåne** has a background report on electricity use and power demand per municipality (not opened). — [Region Skåne, elfakta om kommunerna (PDF)](https://utveckling.skane.se/siteassets/verksamhetsomraden/miljo-och-klimat/dokument/region-skane_elanvandning-och-effektbehov_bilaga_elfakta-om-kommunerna.pdf)
- **Öresundskraft** (Helsingborg's municipal energy company): electricity, district heating, district cooling and biogas. It raised the district-heating price 8 % from 2025-01-01. The company has co-hosted a seminar on open data with Metry, but no open consumption dataset was found. — [Öresundskraft blog](https://oresundskraft.se/blogg/oresundskraft-genomfor-prishojning-pa-fjarrvarme-for-2025); [Mynewsdesk Öresundskraft](https://www.mynewsdesk.com/se/oresundskraft/tag/fjaerrvaermesamarbete)

### Inferences
- **Candidate energy and weather scenarios** (expected patterns are inferred from the Gothenburg DT and basic building physics; verify on the actual data):
  - **E1 Seasonal playback:** "Play the year. When is total demand highest?" Expected: January–February peak, July–August minimum.
  - **E2 Temperature link:** "Does daily consumption go up when SMHI temperature goes down?" Expected: a strong negative relation in the heating season and a flat one in summer, i.e. a heating-degree-day relation. Hourly or daily SMHI data supports this.
  - **E3 Building type:** "Which building purpose has the highest kWh/m²?" or "Which responds most to cold days: residential multifamily or offices?" Expected from Gothenburg: multifamily is more temperature-sensitive than offices.
  - **E4 Building age or height vs energy class:** colour by EPC class and use height as storeys; find old, large, low-class buildings.
  - **E5 Peak-load mapping:** "Which block has the highest peak hour on the coldest day?" This needs hourly metered data, which is not publicly available.
  - **E6 Renovation targeting (decision):** combine EPC class F–G, building size and an area-income layer for an energy-poverty angle. Mark the income join as ecological.
- **District heating caveat.** For district-heated buildings, electricity data excludes most space heating. Electricity consumption would then **under-represent** heating demand, and the HDD relation would be weak. Whether a delivered electricity dataset includes district heating must be checked. This is inferred, not verified for Helsingborg.
- **Smallest realistic energy add-on:** SMHI hourly temperature for the nearest Helsingborg station (open), plus Boverket EPC values per building (agreement needed). This supports E3, E4 and E6 without any utility data. E1, E2 and E5 need metered time series.

### Gaps
- No open, building-level or grid-level metered electricity or heat data for Helsingborg was found. Öresundskraft open data availability is **unverified**.
- The exact SMHI station ID and period for Helsingborg was not looked up. The "Helsingborg A" name is not verified.
- Whether Boverket also offers a bulk open download (not just the API under agreement), and its exact licence text, were not checked.
- Helsingborg's EPC coverage rate is unknown.

---

## 5. Which scenarios best exercise each system capability: a scenario catalogue with expected answers

### Takeaway
A balanced evaluation set should contain:
- 1–2 simple lookups (colour on buildings or areas);
- 1 bivariate correlation (height + colour);
- 1 composition task (stacked-age or radial glyphs);
- 1 A/B comparison;
- 1 "surprise" or outlier task (SD vs income);
- 1 decision task with explicit criteria;
- optionally 1 time-playback task, once time-series data exists.

The expected answers below come from the 2026 project data and agree with the Swedish literature in §1–2.

### Cited Findings
- Colour-coded designs are faster; plot-based designs (linked and embedded) are more accurate, especially for complex tasks. — [Mota et al., IEEE VIS 2022](https://arxiv.org/abs/2208.05370)
- Linked / embedded / mixed 2D–3D views. — [Chen et al. 2017](https://arxiv.org/pdf/1709.08774)
- Patterns used as ground truth (turnout ~ income and education; SD, M, V and S group patterns; type-1 turnout around 57–58 %). — [SCB 2023](https://scb.se/contentassets/048c2c293c404f3e899e91b844b6b9c2/studie_om_valdeltagande_i_de_allmanna_valen_2022_utifran_ett_integrations-_och_segregationsperspektiv_ju2022-03548.pdf); [SCB PSU May 2022](https://www.scb.se/hitta-statistik/statistik-efter-amne/demokrati/partisympatier/partisympatiundersokningen-psu/pong/statistiknyhet/partisympatier-maj-2022/); [Öhrvall 2022](https://ifn.se/media/xvalkjt5/2022-öhrvall-en-klyfta-mellan-stad-och-land.pdf)
- Helsingborg vulnerable areas: Dalhem/Drottninghög/Fredriksdal and Söder. — [Polisen 2025-12-01](https://polisen.se/siteassets/dokument/organiserad_brottslighet/utsatta-omraden/region_syd.pdf)

### Inferences
**Scenario catalogue.** The answers are [project data] results for the 60 fully covered districts or the grid; recheck them after the export pipeline is final.

**S1 – Lookup, lowest turnout (building / district colouring)**
- Question: "Which voting district in the model has the lowest turnout?"
- Answer: Furutorp, 57.6 %. Next are Eneborg M (58.5 %) and Högaborg V (59.0 %).
- Highest: Gustavslund V, 90.6 %.

**S2 – Correlation, turnout vs income (height + colour)**
- Encoding: colour = turnout, height = DeSO median income (or the reverse).
- Question: "Are low-turnout districts also low-income?"
- Answer: yes, strongly (ρ ≈ 0.92). Plausible to users and consistent with SCB 2023.
- Variant: turnout vs foreign-born share (ρ ≈ −0.93).

**S3 – Outlier / surprise, SD vs income**
- Question: "Is SD strongest in the lowest-income districts?"
- Answer: **no**. SD is lowest in Furutorp (6.8 %) and Fredriksdal Ö (10.0 %), highest in middle-income districts (Ättekulla Ö 26.5 %, Rosengården V 25.3 %, Lussebäcken 25.1 %), and lower again in the richest (Tågaborg N 13.4 %). ρ(SD, income) is only 0.22.
- This tests whether users verify rather than assume. It is backed by PSU (SD weaker among the foreign-born) and by the municipality-level study (negative relation with the non-European immigrant share).

**S4 – A/B comparison (block comparison)**

| District | Turnout | Median income | Foreign-born | Largest parties |
|---|---|---|---|---|
| Tågaborg N | 86.2 % | ≈379k | ≈15 % | M 46.6 %, S 15.9 %, V 2.7 % |
| Drottninghög Ö | 67.6 % | ≈206k | ≈57 % | S 44.8 %, V 32.2 %, M 4.0 % |

- Question: "Compare Tågaborg N and Drottninghög Ö on all variables."
- A milder pair: Råå N vs Söder.
- Age-structure differences should be read from the grid glyphs; they were not computed here.

**S5 – Composition glyphs (radial or stacked)**
- Question: "Which neighbourhood has the largest share of residents born outside Europe?"
- Data: `Tab4` grid, with categories Sweden / Nordic / EU / rest of world.
- Expected: the Söder–Drottninghög–Fredriksdal–Högaborg belt. At DeSO level the foreign-born (all categories) share peaks at ≈56–59 %.
- Verify the "rest of world" category specifically on the grid.

**S6 – Age distribution (stacked age bars)**
- Questions: "Where do young adults (20–24) live relative to the centre?" and "Where are children 0–6 concentrated?"
- Ground truth must be computed from `Tab1` (not done here). No Helsingborg-specific source was found.
- Treat "young adults near the centre and in rental areas" as a **hypothesis to verify, not ground truth**.

**S7 – Decision with explicit criteria**
- Question: "Choose the 250 m cell for a new preschool: the most 0–6-year-olds among cells with no school building within 500 m."
- Data: age grid plus building purpose. The answer is computable and therefore verifiable.
- Elderly-care variant: the most residents aged 65+.

**S8 – Segregation / scale (DeSO vs grid toggle)**
- Question: "Does the low-income concentration look the same at 250 m and at DeSO level?"
- Expected: the DeSO view looks more mixed (Boverket MAUP note). This also exercises the layer toggle.

**S9 – Data-error spotting**
- Question: "Find a district whose election profile does not match its neighbourhood statistics."
- Candidate: Oceanhamnen.
  - Its election profile looks like the affluent districts: M 30.7 %, V 8.1 %, turnout 75.8 %.
  - Its area-weighted DeSO foreign-born share (≈46 %) looks like the low-income districts.
- This probably comes from the DeSO polygon not matching the voting district (area weighting). It is a useful "is this real or an artefact?" task.
- Further caveats to brief participants on:
  - the 2.2 % collection-district votes, which cannot be mapped;
  - edge-cut DeSO areas keep whole-area values;
  - the 1,000 m remainder cells.

**S10 – Inner vs outer (only partly possible)**
- Question: "Is SD stronger outside the city core?"
- Answer: yes, 22.6 % outside the rectangle vs 17.6 % inside. But the outer districts are **not in the 3D model**, so this needs a 2D map or overview.
- Possibly exclude it from 3D tasks.

**T1–T3 – Time playback (future)**
- T1/T2: energy and weather scenarios E1 and E2 in §4.
- T3: election change 2022 → 2026, once 2022 district data is loaded. Note that district boundaries may change between elections.
- No time series exists today. `Tab5_DeSO` (moves, births, deaths) is a single-period flow, not a time series.

**Capability → best scenarios**

| Capability | Best scenarios |
|---|---|
| Building / area colour | S1, S5 |
| Height + colour | S2, S3 |
| Stacked age glyphs | S6, S7 |
| Radial composition glyphs | S5, and party composition in S4 |
| A/B comparison | S4, S8 |
| Layer toggle and unit switch | S8, S9 |
| Time playback | T1–T3 |

**Framing advice**
- State the data years (election 2026, population 2024, income 2023) in the task brief.
- Phrase all correlation tasks at area level, to avoid the ecological fallacy.
- Avoid stigmatising wording about named neighbourhoods. Compare by district name and variable, not by "problem area" labels.

### Gaps
- The age-structure ground truth (S6, S7) has not been computed. It needs a short script over `Tab1_Ruta_2024` and building purpose data.
- Education-based expected patterns (`Tab10`) were not checked against turnout in project data. The literature direction is clear (higher education → higher turnout, more M, L, C and MP, less SD).
- No published IA study with a near-identical task set was found to benchmark difficulty or time.
