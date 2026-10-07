# Visual Encodings for Multivariate Urban / Geospatial Statistics in 3D and VR City Models

Scope: evidence on which encodings work (and fail) for statistical data shown in an immersive city model, mapped to what the Helsingborg digital twin engine already renders (choropleth surfaces with linear/log/diverging/quantile scales; height surfaces and extrusions; bar/stacked-bar/radial glyphs on cell centroids; buildings coloured via building->cell association; picking + info panels; A/B side-by-side of two cells). Missing in the engine: animation/temporal playback, categorical encoding, multiple legends, VR interaction.

Source-verification note: entries marked (fetched) were read in full text or abstract during this research session. Entries marked (canonical, not re-fetched) are well-known papers cited from their DOI/URL; their claims are stated at the level of their published abstracts, but the report writer should re-check details before quoting numbers.

## 1. Choropleth vs prism / extruded maps vs 3D bars vs glyphs: accuracy, comparison, pattern detection (incl. VR-specific results)

### Takeaway
Across desktop and VR studies there is a stable trade-off: height (prism/extrusion/bars) gives more accurate value reading than colour, while flat colour choropleths are faster to read; 3D costs come from occlusion and perspective foreshortening. The best VR result so far is not "pick one" but letting the user move between choropleth, prism and bar views (Tilt Map).

### Cited Findings
- Yang et al. ran the first controlled comparison of choropleth, prism and coloured prism maps in VR (population density data). Participants were more accurate with prism maps but faster with choropleth maps; they preferred the coloured prism map (height + colour redundantly encoding the same value) but raised concerns about occlusion. The authors state this confirms earlier non-VR results showing the same speed/accuracy trade-off. (fetched) — [Yang, Dwyer, Marriott, Jenny, Goodwin, "Tilt Map: Interactive Transitions Between Choropleth Map, Prism Map and Bar Chart in Immersive Environments", IEEE TVCG 27(12), 2021](https://ialab.it.monash.edu/~dwyer/papers/TiltMap.pdf); also [ResearchGate record](https://www.researchgate.net/publication/342408363_Tilt_Map_Interactive_Transitions_Between_Choropleth_Map_Prism_Map_and_Bar_Chart_in_Immersive_Environments)
- Tilt Map links orientation to view: vertical map = choropleth, tilting morphs into prism map, near horizontal it becomes a 2D bar chart. The bar chart view was added specifically "to aid comparison tasks and alleviate 3D occlusion and perspective foreshortening". Study 2: Tilt Map beat side-by-side views on accuracy, beat toggling on time, and was preferred. Some participants answered using the intermediate transition states. (fetched) — [Yang et al. 2021](https://ialab.it.monash.edu/~dwyer/papers/TiltMap.pdf)
- Height (the visual variable of prism maps) is far more accurate for quantitative values than brightness (choropleth), but perspective foreshortening and oblique views distort and occlude prism maps. (search summary of the Tilt Map paper) — [Tilt Map PDF](https://ialab.it.monash.edu/~dwyer/papers/TiltMap.pdf)
- Illuminated choropleth maps (shading/soft shadows that treat classes as 3D prisms) let participants pick the denser of two adjacent units of the same class more accurately (0.75 +/- 0.09 vs 0.65 +/- 0.04 without illumination). (search snippet) — [Stewart & Kennelly, "Illuminated Choropleth Maps", Annals of the AAG 100(3), 2010](https://www.researchgate.net/publication/233375373_Illuminated_Choropleth_Maps)
- Besançon et al. (controlled experiment, 60 participants; second experiment 47) compared choropleth augmentations: glyphs, 3D extrusion, cartograms, juxtaposed maps, shading. Juxtaposed univariate maps were most accurate for single-variable questions; for questions combining two variables and aggregating over several regions, 3D choropleths and deformed cartograms were particularly accurate and much faster than juxtaposed maps. The paper highlights "the low accuracy of choropleth map tasks with multivariate data" and that large, sparsely populated areas dominate choropleths visually. A 3D prism variant over fine-grained population ("Popchart 3D Prism Map") performed worst for regional averaging. (fetched) — [Besançon, Cooper, Ynnerman, Vernier, "An Evaluation of Visualization Methods for Population Statistics Based on Choropleth Maps", arXiv:2005.00324, 2020](https://arxiv.org/pdf/2005.00324)
- 2D vs 3D charts in VR (J. of Visualization, 2024): 3D renditions of traditional charts reduced interpretation accuracy but improved long-term recall. (search summary; not fetched) — [A comparative study of 2D vs. 3D chart visualizations in virtual reality, J. Visualization 28(1), 2024, doi:10.1007/s12650-024-01033-6](https://link.springer.com/article/10.1007/s12650-024-01033-6)
- A 2012 study found 3D bars on 3D maps neither increased nor decreased reading accuracy relative to 2D bars on 2D maps when judging relative height and distance. (search snippet; primary source not identified) — [ResearchGate: Immersive visualization with bar graphics](https://www.researchgate.net/publication/342024449_Immersive_visualization_with_bar_graphics)
- Kraus et al. survey of immersive analytics with abstract 3D visualizations: results for 3D vs 2D are task dependent (e.g., 2D heat maps better for reading/comparing single items; 3D heat maps in VR showed lower error rates for some tasks). (search summary) — [Kraus et al., "Immersive Analytics with Abstract 3D Visualizations: A Survey", Computer Graphics Forum 2022](https://researchonline.rca.ac.uk/4950/1/Computer%20Graphics%20Forum%20-%202021%20-%20Kraus%20-%20Immersive%20Analytics%20with%20Abstract%203D%20Visualizations%20%20A%20Survey.pdf)
- Dübel et al. systematize 2D vs 3D presentation of spatial data (2D/3D reference space x 2D/3D attribute presentation) and list occlusion, clutter, distortion and scalability as the main perceptual/technical trade-offs. (search summary) — [Dübel, Röhlig, Schumann, Trapp, "2D and 3D Presentation of Spatial Data: A Systematic Review", IEEE VIS International Workshop on 3DVis, 2014](https://knowledge.hpi.de/handle/123456789/21511)
- Classic perceptual ranking: position/length judgements are more accurate than area, colour saturation or shading. (canonical, not re-fetched) — [Cleveland & McGill, "Graphical Perception", JASA 79(387), 1984, doi:10.1080/01621459.1984.10478080](https://doi.org/10.1080/01621459.1984.10478080)

### Inferences
- The engine's "height surface + same-variable colour" (redundant encoding, i.e. coloured prism) is the best-supported default for single-variable value reading in VR; it is already implementable with existing full/inset extrusion + choropleth.
- Inset extrusion (columns smaller than the cell) should reduce occlusion between neighbouring columns compared with full extrusion; this is a design inference, not tested in the sources found.
- Grid cells (250 m / 1000 m SCB squares) have equal area, which removes the "large sparse region dominates" choropleth bias that Besançon et al. document for administrative units; the bias remains for the 92 voting districts and DeSO areas.
- A Tilt-Map-like transition (choropleth <-> prism <-> 2D bar chart) is new implementation work but has the strongest VR evidence; a cheap approximation is an explicit toggle plus a camera preset that looks straight down (choropleth) vs oblique (prism).
- The existing A/B two-cell comparison panel effectively provides the "bar chart view" for exact comparison; extending it to N selected cells would approximate Tilt Map's bar view.

### Gaps
- No VR study found that compares glyph maps (bar/radial glyphs on centroids) against prisms or choropleths; glyph evidence is desktop-only.
- No study found on extrusions embedded among real 3D buildings (all prism/choropleth VR studies used isolated maps on a table/wall, not a city context).
- Exact numeric results of Tilt Map Study 1 (error magnitudes, n) were in figures not extracted.

## 2. Bivariate and multivariate encodings (e.g., income vs vote share)

### Takeaway
Bivariate colour schemes are readable for order but poor for judging correlation; juxtaposed univariate maps are more accurate but slower; mixing channels (height for one variable, colour for the other) is a well-used superposition with partial support. Gleicher's juxtaposition / superposition / explicit-encoding framework is the standard way to justify the choice.

### Cited Findings
- Olson (1981): readers could recognise order in bivariate maps, but judging correlation between two variables was harder than with two side-by-side univariate maps; bivariate legends are hard to memorise. (search summary citing Olson) — [Besançon et al. 2020](https://arxiv.org/pdf/2005.00324); [Penn State GEOG 486, Multivariate Choropleths](https://courses.ems.psu.edu/geog486/node/900)
- Besançon et al.: juxtaposed univariate maps were most accurate for single-variable questions and, together with "Bertillon" (colour + value-sized) choropleths, best when two variables had to be combined with geography; juxtaposed maps were much slower. For combining two variables, most techniques performed similarly. Authors note integrating information from two juxtaposed charts is error-prone, while superposition shows the influence of both variables. (fetched) — [Besançon et al. 2020](https://arxiv.org/pdf/2005.00324)
- Legend design for unclassed bivariate choropleths: legend presence did not affect identification of regional trends; but for questions about combined characteristics, a bivariate (2D) legend outperformed a 1D ordering legend. (search summary) — [Legend Designs for Unclassed, Bivariate, Choropleth Maps (ResearchGate)](https://www.researchgate.net/publication/250015597_Legend_Designs_for_Unclassed_Bivariate_Choropleth_Maps); [Strode et al., "Operationalizing Trumbo's Principles of Bivariate Choropleth Map Design", Cartographic Perspectives](https://cartographicperspectives.org/index.php/journal/article/download/1538/1819?inline=1)
- Gleicher et al. classify comparison designs into juxtaposition (side by side), superposition (overlaid in the same space) and explicit encoding (computing and showing the relationship/difference directly), and hybrids. (canonical, not re-fetched) — [Gleicher, Albers, Walker, Jusufi, Hansen, Roberts, "Visual comparison for information visualization", Information Visualization 10(4), 2011, doi:10.1177/1473871611416549](https://doi.org/10.1177/1473871611416549)
- Yang et al. observed users preferred the coloured prism (colour + height); when colour and height encode different variables this becomes a bivariate superposition, but that specific configuration was not tested in the VR study. (fetched; inference about bivariate use is mine) — [Yang et al. 2021](https://ialab.it.monash.edu/~dwyer/papers/TiltMap.pdf)

### Inferences
- For income vs vote share (different spatial units: grid/DeSO vs 92 voting districts), the defensible order is: (1) explicit encoding where possible (e.g., a computed derived value such as residual or ratio shown on one diverging scale) for the correlation question; (2) height = one variable, colour = other (already supported: extrusion + choropleth on the same areas) for "where are high-X and high-Y together"; (3) juxtaposition via two map copies / small multiples for accurate single-variable reading (needs new implementation: multiple map instances + second legend).
- Bivariate 3x3 colour grids are not supported (needs a 2D palette + 2D legend) and the evidence says they are weak for correlation judgements; low priority.
- Combining voting districts with grid data requires an areal join (district <-> cell/DeSO); this is a data-integration step with its own uncertainty, not a rendering choice.
- Multiple simultaneous legends become mandatory as soon as height and colour encode different variables; currently a blocker in the engine.

### Gaps
- No VR/3D-city study found on bivariate colour-plus-height maps with different variables per channel.
- No study found on glyph maps (stacked bars, radial) vs bivariate choropleths for socio-economic data in immersive settings.

## 3. Data inside 3D city models / digital twins: building colouring, embedding, occlusion

### Takeaway
3D urban visual analytics literature identifies occlusion by buildings as the core problem and shows a speed/accuracy trade-off between colour on building surfaces (fast) and plot-based linked/embedded views (accurate, scale better with task complexity). Embedded/situated visualisation is a recognised design space but the occlusion cost must be managed (transparency, height cut, top-down views, selection).

### Cited Findings
- Mota et al. compared four designs for time-varying data on 3D building surfaces: spatial juxtaposition (colour-coded snapshots), temporal juxtaposition (colour-coded sequence), linked view and embedded view (plots placed on the surfaces). Participants were more accurate with plot-based designs (linked, embedded) and faster with colour-coded designs; plot-based designs kept efficiency better as task complexity rose. Task design was informed by interviews with urban practitioners. (fetched abstract) — [Mota et al., "A Comparison of Spatiotemporal Visualizations for 3D Urban Analytics", IEEE TVCG (VIS 2022), doi:10.1109/TVCG.2022.3209474](https://arxiv.org/abs/2208.05370)
- Miranda et al. STAR on visual analytics for 3D urban data: separates the physical layer (buildings, streets) from the thematic layer (simulation/sensing/survey data); verticality and density of cities cause occlusion of analysis targets that "can impede or cause analysis tasks to fail"; recommends strategies for navigation and occlusion minimisation; catalogues papers at urbantk.org/survey-3d. (fetched abstract + search summary) — [Miranda et al., "The State of the Art in Visual Analytics for 3D Urban Data", Computer Graphics Forum 43(3), 2024, doi:10.1111/cgf.15112](https://arxiv.org/abs/2404.15976)
- Occlusion-free techniques exist for highlighting important geographic features in 3D urban environments (e.g., view-dependent deformation/ghosting). (search result; not read) — [Occlusion-Free Visualization of Important Geographic Features in 3D Urban Environments, ISPRS IJGI 5(8):138, 2016](https://doi.org/10.3390/ijgi5080138)
- Willett, Jansen and Dragicevic define situated data representations (displayed near the physical referent) and embedded data representations (displayed directly on/aligned with the referent objects). (canonical, not re-fetched) — [Willett, Jansen, Dragicevic, "Embedded Data Representations", IEEE TVCG 23(1), 2017, doi:10.1109/TVCG.2016.2598608](https://doi.org/10.1109/TVCG.2016.2598608)
- Practice example: Helsinki's semantic CityGML model drives the Helsinki Energy and Climate Atlas, a browser visualisation where every building can be queried for energy-related information and buildings are linked to registry IDs (GMLID, RATU, VTJ-PRT) to join data streams. (search summary of city pages) — [City of Helsinki, Helsinki 3D](https://hel.fi/en/decision-making/information-on-helsinki/maps-and-geospatial-data/helsinki-3d); [Helsinki Region Infoshare](https://hri.fi/en_gb/best-3d-model-in-the-world-improves-with-age)

### Inferences
- "Buildings coloured by cell value" (supported) is an embedded colour-coded encoding: expect fast but less accurate reading (Mota et al.), and note it is dasymetric only in appearance: the value is the cell's, not the building's. Label it as such in the UI to avoid an ecological-fallacy misreading.
- Lifting buildings to follow a height surface (supported) visually merges city geometry and data; tall real buildings add height noise to a height-encoded variable. Recommended mitigation: when height encodes data, render buildings as low-saturation context (or footprints / flattened) and reserve height for data. Needs a "context mode" toggle (small new implementation).
- Occlusion mitigations to consider (new implementation): building transparency/ghosting, a global height clamp, top-down camera preset, per-selection highlighting.
- Linked views (picking -> info panel / A-B panel) are already supported and are the "plot-based" side of the Mota trade-off; they should be the recommended route for accuracy tasks.

### Gaps
- No empirical study found specifically on dasymetric display of grid statistics onto buildings in VR.
- No published "city on a table" (tabletop miniature) vs human-scale comparison for statistical data was retrieved in this session; Yang et al. 2018 (section 4) is the closest evidence.
- Practice details of Virtual Singapore, Zurich and Herrenberg visual encodings were not retrieved.

## 4. Comparison techniques in immersive analytics (side-by-side, overlay, small multiples, shelves, multiview maps)

### Takeaway
VR small multiples on a "shelf" work; with few multiples a flat layout is fastest, with many a half-circle is preferred and full surround is disorienting. Users arranging multiple maps around themselves prefer a spherical-cap layout. Interactive transitions (Tilt Map) beat static side-by-side for accuracy.

### Cited Findings
- Liu, Prouzeau, Ens, Dwyer: small multiples in VR on a "shelves" metaphor (12 participants per study; bar chart and BIM datasets; coordinated brushing, filtering, rotation). With fewer multiples a flat layout performed best despite more walking; with more multiples the difference disappeared and users preferred a semi-circular layout; fully enclosing circular shelves were disorienting; quarter-circle was rated a good compromise between walking and rotating (6/12 and 8/12 participants in two conditions). (fetched) — [Liu, Prouzeau, Ens, Dwyer, "Design and Evaluation of Interactive Small Multiples Data Visualisation in Immersive Spaces", IEEE VR 2020](https://ialab.it.monash.edu/~dwyer/papers/ImmersiveSmallMultiples.pdf)
- Satriadi et al.: exploratory study (16 participants) of multiview map layouts in VR for search, comparison and route-planning; layouts fell into spherical, spherical-cap and planar geometries; participants preferred a spherical cap around themselves and often rearranged views during tasks. (search summary) — [Satriadi, Ens, Cordeil, Czauderna, Jenny, "Maps Around Me: 3D Multiview Layouts in Immersive Spaces", PACM HCI 4(ISS), 2020](https://ialab.it.monash.edu/~tobiasc/publication/satriadi-2020)
- Tilt Map (interactive transition) was more accurate than side-by-side arrangement and faster than toggling. (fetched) — [Yang et al. 2021](https://ialab.it.monash.edu/~dwyer/papers/TiltMap.pdf)
- Maps and Globes in VR: compared globe and flat/curved map projections in VR for geographic tasks (distance, area, direction). (canonical, not re-fetched; consult paper for which condition won per task) — [Yang, Jenny, Dwyer, Marriott, Chen, Cordeil, "Maps and Globes in Virtual Reality", Computer Graphics Forum 37(3), 2018, doi:10.1111/cgf.13431](https://ialab.it.monash.edu/~dwyer/papers/vrglobe.pdf)
- Gleicher's taxonomy (juxtapose / superpose / explicit difference) applies directly. (canonical) — [Gleicher et al. 2011](https://doi.org/10.1177/1473871611416549)

### Inferences
- Current A/B two-cell comparison = juxtaposition of values in a panel. Cheapest high-value extensions: (a) explicit difference map (A minus B, or district minus municipal mean) on a diverging scale - supported already by diverging scales, only needs a derived variable; (b) 2-4 small-multiple miniature maps on a flat shelf for comparing variables or (later) time steps - new implementation (multiple map instances, shared legend).
- For a city-scale model the "map copies" should be simplified (no buildings, choropleth/prism only) to keep Quest 3S rendering cost manageable - performance inference, not measured.

### Gaps
- Findings on Lee et al. and Quang Vinh Nguyen's immersive comparison work were not retrieved in this session.
- No study found on comparing two areas inside a single full-scale city model (vs miniature copies).

## 5. Temporal data on maps in 3D/VR (if electricity/weather time series arrive)

### Takeaway
For analysis, static small multiples beat animation in accuracy (animation is engaging but error-prone); the space-time cube is viable in VR and better accepted immersively than on desktop. For 3D urban temporal data, plot-based linked/embedded views beat colour animation-like juxtapositions on accuracy.

### Cited Findings
- Robertson et al.: in analysis contexts, small multiples were faster and more accurate than animation for trend comparison; animation was perceived as fun and was fastest in presentation contexts but produced many errors. (search summary) — [Robertson, Fernandez, Fisher, Lee, Stasko, "Effectiveness of Animation in Trend Visualization", IEEE TVCG 14(6), 2008, doi:10.1109/TVCG.2008.125](https://faculty.cc.gatech.edu/~stasko/papers/infovis08-anim.pdf)
- Mobile follow-up comparing animation vs small multiples for trends exists (Brehmer, Lee, Isenberg, Choe). (title only) — [arXiv:1907.03919](https://arxiv.org/pdf/1907.03919)
- Immersive space-time cube (trajectory data): matched desktop completion time and accuracy for most tasks; higher usability (SUS 82.3 vs 62.1), strongly preferred, lower workload, no discomfort in ~25-min sessions. (search summary) — [Wagner Filho, Stuerzlinger, Nedel, "Evaluating an Immersive Space-Time Cube Geovisualization for Intuitive Trajectory Data Exploration", IEEE TVCG 26(1), 2020](https://arxiv.org/pdf/1908.00580)
- Mota et al.: for time-varying data on buildings, linked and embedded plots were more accurate; colour-coded temporal juxtaposition was faster. (fetched abstract) — [Mota et al. 2022](https://arxiv.org/abs/2208.05370)

### Inferences
- If electricity/weather data arrive: implement (1) a timeline scrubber with discrete steps (animation for overview/presentation only), (2) a linked time-series plot in the info panel for selected cells/buildings (accuracy), (3) optional small multiples of 3-6 time steps. Space-time cube suits trajectories more than areal grid data; low priority.
- Temporal playback is not yet in the engine (new implementation).

### Gaps
- No VR study found comparing animation vs small multiples for areal (choropleth/prism) data specifically.

## 6. Categorical data (vote winner, building type), legends and text legibility on Quest-class HMDs

### Takeaway
Use a qualitative, colour-blind-safe palette with few classes (Okabe-Ito / ColorBrewer qualitative) for categories; for party winner a hue per party plus a lightness ramp for margin is the common cartographic practice. VR text needs to be large: reported comfortable sizes are well above desktop equivalents, and lower contrast requires larger text.

### Cited Findings
- Reading in VR (Oculus Go and Quest): preferred angular text size about 41 +/- 14 dmm overall ("dmm" = character height in mm at 1 m distance); preferred mean sizes differed by text length (short text ~27-32 dmm, medium/long ~16-18 dmm). (search summary) — [Dingler et al., "User Experience of Reading in Virtual Reality - Finding Values for Text Distance, Size and Contrast", arXiv:2004.01545](https://arxiv.org/html/2004.01545v1)
- "Virtual Visus" study: comfortable reading size rated 'good' at ~14.6 dmm; median ratings define comfortable and minimal acceptable sizes. (search summary) — [Virtual Visus - Vision Acuity and Text Legibility in Virtual Environments (ResearchGate)](https://www.researchgate.net/publication/362905907_Virtual_Visus_-_Vision_Acuity_and_Text_Legibility_in_Virtual_Environments)
- Lower contrast requires larger font size (contrast/size trade-off). (search summary) — [Dingler et al.](https://arxiv.org/html/2004.01545v1); see also [Text readability in AR: multivocal literature review, Virtual Reality 2024](https://link.springer.com/article/10.1007/s10055-024-00949-6)
- ColorBrewer provides sequential, diverging and qualitative schemes designed for maps, with colour-blind/print safety indicators. (canonical, not re-fetched) — [Harrower & Brewer, "ColorBrewer.org: An Online Tool for Selecting Colour Schemes for Maps", The Cartographic Journal 40(1), 2003, doi:10.1179/000870403235002042](https://doi.org/10.1179/000870403235002042)

### Inferences
- Note that the two text-size sources disagree in magnitude (14.6 dmm "good" vs ~41 dmm preferred); a conservative prototype target is >= ~20-30 dmm for legend/label text and larger for short labels; verify on the Quest 3S during pilot testing.
- Swedish party colours are conventional (e.g., S red, M blue); using party colours aids recognition but many are not colour-blind-distinguishable; pair colour with labels in the info panel and legend. (design inference; no study retrieved)
- Categorical encoding and multiple legends are missing in the engine and needed for vote-winner and building-type views.

### Gaps
- No peer-reviewed study retrieved on legend placement/design in VR maps (world-anchored vs hand-anchored vs head-locked).
- No Quest 3S-specific legibility data found (display resolution differs from Quest 1/Go used in studies).

## 7. Colour on lit/shaded 3D geometry (lit vs unlit materials)

### Takeaway
Shading changes perceived colour and can make colour-mapped values ambiguous; stepped (classed) colour maps read more accurately but interfere more with shape perception; lighting choice measurably affects error rates for thematic colour in 3D virtual environments.

### Cited Findings
- Engel, Semmo, Trapp, Döllner: user study of rendering techniques (shading, global illumination, stylisation) on thematic colour mapping in 3D virtual environments (city-like scenes); significant differences in error rate and completion time across rendering techniques and colour mappings for four perception tasks; the motivation is that depth-cue rendering alters colour values and creates ambiguity in the colour mapping. (search summary; detailed results not extracted) — [Engel et al., "Evaluating the Perceptual Impact of Rendering Techniques on Thematic Color Mappings in 3D Virtual Environments", VMV 2013, doi:10.2312/PE.VMV.VMV13.025-032](https://diglib.eg.org/handle/10.2312/PE.VMV.VMV13.025-032)
- Colour maps with strong lightness gradients disrupt shading-based shape perception; stepped colour maps were read more accurately but interfered more with surface-shape perception; isoluminant maps minimise interference with shading. (search summary of a TVCG 2024 study) — [IEEE TVCG 2024, doi:10.1109/TVCG.2024.3383336](https://doi.org/10.1109/tvcg.2024.3383336)
- Diverging colour maps for 3D surfaces should keep perceptual uniformity and work with shading. (canonical) — [Moreland, "Diverging Color Maps for Scientific Visualization"](https://www.kennethmoreland.com/color-maps/ColorMapsExpanded.pdf)
- Illuminated choropleths: adding shading/shadows to classed choropleths improved discrimination within classes. (search snippet) — [Stewart & Kennelly 2010](https://www.researchgate.net/publication/233375373_Illuminated_Choropleth_Maps)

### Inferences
- Data surfaces should use unlit (or ambient-dominant) colour for the value channel, with geometry cues provided by edges/outlines or mild separate shading on side walls only. Tops of extrusions unlit = colour matches legend. This is a shader/material change in Unity (small new implementation if not already unlit).
- Viridis-type maps vary in lightness by design; that conflicts with shading but aids value reading. Keep shading off the data-coloured faces rather than switching to isoluminant palettes.

### Gaps
- No VR-HMD-specific study found on colour fidelity on lit materials (HMD display gamut/tonemapping).

## 8. Which combinations suit which analysis tasks (recommendation catalogue for this prototype)

### Takeaway
Match encoding to task: colour for overview/pattern detection, height (+ redundant colour) for value estimation, linked plots/panels for exact comparison, explicit difference maps for comparison between variables/areas, small multiples for temporal or multi-variable comparison.

### Cited Findings
- Speed (colour) vs accuracy (height/plots) trade-off appears in both map studies and 3D urban studies. — [Yang et al. 2021](https://ialab.it.monash.edu/~dwyer/papers/TiltMap.pdf); [Mota et al. 2022](https://arxiv.org/abs/2208.05370)
- Juxtaposed univariate maps are most accurate for single variables; 3D choropleths fast and accurate for combined/aggregated questions. — [Besançon et al. 2020](https://arxiv.org/pdf/2005.00324)
- Small multiples beat animation for analysis. — [Robertson et al. 2008](https://faculty.cc.gatech.edu/~stasko/papers/infovis08-anim.pdf)
- VR small multiples: flat for few, half-circle for many. — [Liu et al. 2020](https://ialab.it.monash.edu/~dwyer/papers/ImmersiveSmallMultiples.pdf)

### Inferences (catalogue; S = supported now, N = needs new implementation)
| Task | Data example | Recommended encoding | Status |
|---|---|---|---|
| Overview / hot-spot detection | population density, median income | Choropleth on 250 m grid, quantile or percentile-domain scale, sequential viridis | S |
| Value estimation of one variable | income sum, population | Inset extrusion with redundant colour (coloured prism) + info panel on pick | S |
| Two-variable co-location | income (height) vs share 65+ (colour) | Height + colour superposition, two legends | S (render) / N (second legend) |
| Correlation-type judgement | income vs vote share | Explicit derived variable (ratio/residual) on diverging scale; or juxtaposed small maps | N (derived variable, multiples) |
| Composition per area | age groups, household income quartiles | Stacked-bar or radial glyphs on 1000 m cells (sparser than 250 m to limit clutter) | S |
| Compare two areas | cell A vs cell B | A/B panel (juxtaposed bars) + optional difference highlight | S / N |
| Categorical overview | election winner per district, building type | Qualitative palette (Okabe-Ito), margin as lightness | N |
| Context for data | buildings | Desaturated/ghosted buildings when height encodes data; buildings coloured via cell only for orientation | N (context mode) |
| Temporal (if data arrives) | electricity, weather | Linked time-series plot + stepped scrubber; small multiples for 3-6 steps; animation only for presentation | N |

- Prefer the 1000 m grid for glyphs and the 250 m grid for colour/height surfaces to manage clutter at ~70 km2 extent (inference).

### Gaps
- No direct evidence for any of these combinations inside a full-scale, building-dense VR city; this gap is itself a thesis contribution opportunity.
