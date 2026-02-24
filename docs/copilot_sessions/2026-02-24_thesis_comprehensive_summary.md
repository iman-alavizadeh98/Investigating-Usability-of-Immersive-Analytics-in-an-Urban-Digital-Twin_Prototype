# Master Thesis: Immersive Analytics in Urban Digital Twin
**Comprehensive Research & Technical Summary**

**Student**: Iman Alavi Zadeh (gusalavim@student.gu.se)  
**Advisor**: Dr Aris Alissandrakis (aris.alissandrakis@chalmers.se)  
**Institution**: Chalmers University of Technology  
**Submission Date**: February 24, 2026  
**Duration**: ~6 months (5–6 months prototype dev + evaluation)

---

## I. RESEARCH CONTEXT & MOTIVATION

### What is Immersive Analytics?
**Definition**: A research field investigating how immersive visualization and interaction technologies (VR, AR, spatial displays) can support analytical reasoning and decision-making with complex, multidimensional data.

**Key Insight**: By combining 3D immersive visualization with interaction techniques (gesture, embodied spatial navigation), immersive analytics aims to help users understand complex datasets better than traditional 2D desktop visualizations.

**Prior Work** (Skarbez et al. 2019): Immersive analytics is still fragmented; empirical research is limited. Open challenges:
- How do immersive interfaces actually support sensemaking?
- Which interaction techniques work best for different data types?
- How do we evaluate immersive analytics systematically?

### What is an Urban Digital Twin?
**Definition**: An integrated digital representation of a city that combines:
- **Geometry**: 3D building models, infrastructure, terrain
- **Semantics**: Building attributes (use type, owner, energy class), infrastructure metadata
- **Dynamic Data**: Real-time or historical sensor streams (traffic, climate, energy, pollution)

**Key Insight**: Digital twins promise rich spatial and semantic context for urban analysis, but at city scale, the data becomes extremely dense and complex—hard to understand in 2D dashboards or traditional GIS interfaces.

### The Research Gap
Prior work has shown:
- ✅ Immersive interfaces can help analyze time-oriented abstract data (Reski et al. 2020, 2023, 2024)
- ✅ Immersive VR interfaces are feasible for urban digital twins (Bunea & Dobre 2024; Reski et al. 2025 — Urban Climate InteracTable)

But:
- ❌ Limited empirical understanding of how immersive analytics specifically supports **semantic understanding** of **dense, spatio-temporal urban data**
- ❌ Unclear how immersive interfaces help with **exploratory analysis** in **urban/smart planning contexts**
- ❌ No systematic evaluation of usability and sensemaking for city-scale digital twins

### This Thesis Addresses
**Research Question** (Implicit): *How do immersive analytics interfaces integrated into a city-scale digital twin support users' understanding and exploratory analysis of dense, spatio-temporal urban data relevant to urban planning?*

---

## II. PROJECT GOALS & SCOPE

### Primary Goal
**Investigate how immersive analytics interfaces integrated into a city-scale digital twin support users' semantic understanding of dense, time-oriented urban data.**

To enable this investigation, the project will:
1. **Design and implement** a digital twin prototype of Gothenburg (city in Sweden)
2. **Serve as experimental platform** for immersive data analysis and user evaluation

### Four Sub-Goals (Technical)

#### Goal 1: Data Integration
- **What**: Gather and integrate heterogeneous urban datasets (GIS, LiDAR, maps, property data)
- **Source**: Public authorities (Lantmäteriet, Göteborgs Stad)
- **Output**: 3D representation of urban environment with spatial + semantic context
- **Scope**: Not a complete city model; rather a **representative, scalable prototype** suitable for interactive exploration
- **Challenge**: Data availability and access — some datasets may be restricted or require institutional access

#### Goal 2: Data Processing Pipeline
- **What**: Transform heterogeneous datasets into interactive 3D representation
- **Tools**: Game engines (Unity or Unreal), GIS standards (CityGML), custom processing components
- **Objective**: Develop deeper understanding of pipeline mechanics (not just "drag-and-drop")
- **Challenge**: Balance exploratory learning with core research — avoid scope creep into pure engine development

#### Goal 3: Temporal Data Integration
- **What**: Populate digital twin with spatio-temporal urban datasets (traffic, energy, climate, etc.)
- **Type of Data**: Historical + preprocessed data (NOT real-time streams, NOT newly collected)
- **Challenge**: Data cleaning, preprocessing, temporal alignment, and cross-source integration
- **Expected Difficulties**: Misaligned timestamps, different coordinate systems, missing values

#### Goal 4: Evaluation (Core Research)
- **What**: Empirical user study evaluating immersive analytics approaches for urban data analysis
- **Research Question**: How do immersive interfaces support users' **understanding** and **exploratory analysis** of urban spatio-temporal data?
- **Methodology**: Mixed-methods approach
  - **Qualitative**: Semi-structured interviews, participant observations, think-aloud protocols
  - **Quantitative**: Task completion time, accuracy on analytical questions, standardized usability questionnaires (e.g., SUS, NASA-TLX), system log data
- **Participants**: Urban planners, data analysts, or researchers with experience in data exploration
- **Metrics**: Compare user performance + experience in immersive interface vs. baseline (e.g., 2D desktop visualization)
- **Challenge**: Designing appropriate evaluation tasks and baselines; securing participants; prototype must be mature enough to evaluate

---

## III. RESEARCH METHODOLOGY & Approach

### Overall Strategy
**Prototype-driven research**: Build a digital twin as experimental platform → conduct empirical evaluation → report findings on immersive analytics effectiveness

### Four Implementation Phases

#### Phase 1: Data Acquisition & Preprocessing (Weeks 1–8)
**Goal**: Gather and prepare urban datasets for 3D reconstruction

**Tasks**:
1. Identify and validate available data sources (Lantmäteriet, Göteborgs Stad, etc.)
2. Select geographic extent (~1–3 km² area of Gothenburg with complete data coverage)
3. Standardize coordinate reference systems (likely SWEREF99 TM)
4. Process GIS layers (buildings, roads, land use)
5. Process LiDAR point clouds → extract building heights, terrain elevation
6. Align and merge datasets into consistent spatial framework
7. Data cleaning and basic quality assurance

**Output**: Processed GeoJSON (buildings), GeoTIFF (terrain), metadata describing coverage and quality

**Challenge**: Heterogeneous formats, coordinate system confusion, missing data

#### Phase 2: City Model Generation & System Implementation (Weeks 9–24)
**Goal**: Build 3D interactive city model in game engine; test scalability and performance

**Tasks**:
1. Set up real-time 3D framework (Unity or Unreal)
2. Implement terrain rendering from elevation data
3. Generate building geometry from footprints + heights (extrusion-based)
4. Design LOD (level-of-detail) strategy for performance (simplified models at distance)
5. Develop data import pipeline (automated conversion from GIS → game engine formats)
6. Version control and documentation

**Output**: Playable city model in game engine; interactive 3D viewport

**Challenge**: Performance at scale; balancing visual fidelity with computational cost; learning curve for 3D engine

#### Phase 3: Integration of Spatio-Temporal Data & Immersive Interaction (Later)
**Goal**: Add time-varying urban data and design immersive interaction techniques

**Tasks**:
1. Select and integrate one or more spatio-temporal datasets (e.g., traffic counts, energy consumption, temperature)
2. Map data values to spatial entities (buildings, road segments, districts)
3. Design temporal navigation interface (e.g., time sliders, play/pause, animation)
4. Implement immersive interaction modality:
   - Option A: Desktop-based 3D navigation (mouse + keyboard)
   - Option B: Virtual reality (HMD-based) with gestural input
5. Design visualization encoding (color, height, particle effects, etc.) for data attributes

**Output**: Interactive prototype supporting exploratory analysis of one or more urban datasets

**Challenge**: Meaningful visual encoding; intuitive temporal controls; ensuring data is readable in immersive context

#### Phase 4: Evaluation Design & Analysis (Later)
**Goal**: Conduct empirical user study; measure usability and sensemaking

**Tasks**:
1. Define evaluation tasks (e.g., "Identify traffic hotspots," "Find buildings with high energy use")
2. Recruit participants (target: urban planners, data analysts, researchers)
3. Design within-subjects or between-subjects study (immersive vs. baseline)
4. Conduct user sessions: task performance, interviews, questionnaires
5. Collect system logs (interaction patterns, dwell times, etc.)
6. Analyze qualitative data (thematic analysis of interviews)
7. Analyze quantitative data (task accuracy, efficiency, usability scores)

**Output**: Research findings on effectiveness of immersive analytics for urban data understanding

**Challenge**: Statistical power; participant recruitment; designing fair baselines; interpreting qualitative data

---

## IV. TECHNICAL APPROACH & DESIGN DECISIONS

### Game Engine Choice: **Unity** (Not Unreal)
**Rationale**:
- Lighter memory footprint; faster development iteration for solo developer
- Strong C# scripting support; faster prototyping than C++
- Excellent asset store for data visualization plugins
- Strong community for VR/spatial applications

**Unreal Cons**: Heavier, steeper learning curve for non-engine-specialist

### Data Sources: **Swedish Open/Institutional Data Only**
**Why**:
- No Street View (privacy, licensing complexity)
- Use authoritative government sources: Lantmäteriet (national land survey), Göteborgs Stad (city municipality)
- Cleaner, more reliable for urban planning context
- Better licensing for academic research

**Expected Data Types**:
- **Building footprints** (polygons with address, use type)
- **LiDAR-derived heights** (point clouds or elevation model)
- **Terrain/DEM** (digital elevation model)
- **Property registry** (owner, year built, energy class)
- **Thematic data**: traffic sensors, energy meters, building characteristics
- **Infrastructure**: roads, utilities, districts

### Architecture: Hybrid (Tools + Custom Development)
**~60% Existing Libraries**:
- GDAL (GIS data reading/writing, coordinate transforms)
- Fiona (GeoJSON handling)
- Rasterio (raster/DEM processing)
- CityGML standards (semantics and interoperability)
- Unity built-in terrain system, mesh generation

**~40% Custom Code** (for learning):
- Custom GIS → game engine importers
- Point cloud rendering (efficiency optimization)
- Immersive interaction controllers
- Data visualization encodings

**Rationale**: Use mature tools for infrastructure; write custom code for domain-specific challenges (GIS-to-graphics pipeline, immersive UX design)

### Scope: Start Small, Iterate
**Initial Prototype**: 1–3 km² of Gothenburg (~100–500 buildings)
- Not full city
- Chosen area has complete data coverage
- Feasible for solo developer in 5–6 months
- Sufficient for meaningful user evaluation

**Future**: Can scale to larger city area once pipeline is validated

### LOD (Level-of-Detail) Strategy
**Why LOD Matters**: At city scale, you can't render every building at full detail without graphics overload.

**Proposed Approach**:
- **LOD 0** (closest): Full building geometry + interior spaces (if included)
- **LOD 1** (medium distance): Exterior only, simplified mesh, texture baked
- **LOD 2** (far distance): Single colored box, minimal geometry
- **Switch distances**: Determined by frame rate budget (target: 60 fps minimum, 90 fps for VR)
- **Implementation**: Unity's LODGroup component + frustum culling

### Interaction Modality (Phase-Dependent)
**Phase 2–3 (Prototype)**: Desktop-based 3D navigation (WASD + mouse)
- Reason: Hardware-agnostic, faster iteration, sufficient for initial evaluation

**Phase 3+ (Later, if mature)**: Virtual Reality (HMD-based)
- Gestural input, first-person navigation
- Spatial embodiment expected to enhance understanding of urban scale
- Requires mature prototype + VR hardware (Valve Index, Meta Quest, etc.)

### Interior Geometry: Optional Complexity
**Question**: Should buildings have explorable interiors?

**Approaches**:
1. **Closed boxes only** (simplest): Buildings are opaque solids; no interior
2. **Hollow interiors** (medium): Thin-shell buildings; players can walk through empty space
3. **Procedurally divided interiors** (medium-hard): Automatic floor generation (assume 3m/floor); simple wall divisions
4. **Hand-crafted interiors** (hardest): Author specific buildings in detail (CAD import, manual modeling)

**Decision**: TBD after 1-week learning sprint; likely approach #2–3 for key buildings, #1 for rest

### Data Pipeline: Python vs QGIS
**Question**: Which tool for batch processing GIS data?

**Approaches**:
- **Python** (GDAL/Fiona): Scriptable, reproducible, scalable, good for automation
- **QGIS** (GUI): Visual exploration, interactive error detection, built-in plugins, less code-heavy
- **Hybrid**: Use QGIS for validation/exploration; Python for reproducible pipeline

**Decision**: TBD after 1-week learning sprint; likely hybrid approach

---

## V. CHALLENGES & MITIGATION STRATEGIES

### Technical Challenges

**Challenge 1**: Data availability & access
- **Mitigation**: Early contact with data providers; identify backup datasets; scope project to publicly available data only

**Challenge 2**: Heterogeneous data formats
- **Mitigation**: Define canonical intermediate formats (GeoJSON, GeoTIFF); write robust conversion pipelines; validate data quality frequently

**Challenge 3**: Performance at scale
- **Mitigation**: Aggressive LOD strategy; frustum culling; spatial indexing; profile early and often

**Challenge 4**: Coordinate system confusion
- **Mitigation**: Establish local coordinate origin from day 1; document all transforms; validate alignment visually in QGIS and Unity

**Challenge 5**: Temporal data alignment
- **Mitigation**: Understand temporal granularity upfront (hourly? daily?); handle missing timestamps; clearly document assumptions

### Research Challenges

**Challenge 6**: Appropriate evaluation methodology
- **Mitigation**: Study design informed by prior immersive analytics research (Reski et al., Skarbez et al.); mixed-methods approach (qual + quant); recruit real user groups (urban planners, not just students)

**Challenge 7**: Prototype maturity
- **Mitigation**: Iterative development; conduct formative evaluation early; be prepared to simplify features if prototype stability is at risk

**Challenge 8**: Participant recruitment
- **Mitigation**: Establish partnerships with planning departments or universities early; offer transparent info about study demands

**Challenge 9**: Generalizability
- **Mitigation**: Document design decisions and rationale; conduct evaluation on multiple representative datasets if possible; acknowledge limitations

### Project Management Challenges

**Challenge 10**: Scope creep
- **Mitigation**: Define clear phase boundaries; focus on core research questions; avoid deep dives into engine development or GIS theory unless directly supporting research

**Challenge 11**: Timeline pressure
- **Mitigation**: 1-week learning sprint to validate pipeline before committing to months of development; fortnightly milestones; clear go/no-go criteria

---

## VI. EXPECTED OUTCOMES & CONTRIBUTIONS

### Prototype Deliverables
1. **City-scale digital twin** of Gothenburg (1–3 km²) integrating GIS, LiDAR, and temporal data
2. **Reusable data pipeline** (Python + game engine tools) for converting Swedish geospatial data to interactive 3D
3. **Immersive analytics interface** supporting exploratory analysis of urban spatio-temporal data
4. **Open-source codebase** and documentation for reproducibility

### Research Contributions
1. **Empirical evidence** on how immersive analytics interfaces support semantic understanding of dense urban data
2. **Design guidelines** for immersive analytics in urban digital twin contexts
3. **Evaluation methodology** tailored to immersive urban analysis
4. **Case study** demonstrating feasibility + challenges of immersive analytics for smart city planning

### Academic Contributions
- Bridges immersive analytics research (Skarbez et al., Reski et al.) with digital twin research (Boje et al.)
- Addresses identified research gaps in city-scale immersive analytics (Bunea & Dobre, Reski et al. 2025)
- Contributes to growing field of XR for urban informatics

---

## VII. RELEVANT PRIOR WORK & POSITIONING

### Immersive Analytics Research
- **Skarbez et al. (2019)**: Foundational review; identifies fragmentation and need for empirical studies ← **This thesis directly responds**
- **Reski et al. (2020, 2023, 2024)**: 3D radar charts, gestural interaction, temporal reasoning in VR ← **We adopt similar evaluation methods; extend to contextual urban data**

### Digital Twin Research
- **Boje et al. (2020)**: Semantic frameworks for digital twins; identifies challenges in scalability and integration ← **Our pipeline addresses integration**
- **Iordanidis & Georgiadis (2025)**: GIS + Unreal Engine integration ← **Similar technical approach; we use Unity**

### Urban Digital Twins + VR
- **Bunea & Dobre (2024)**: VR interface for urban twins using CAVE/HMD ← **We adopt similar immersive modality; extend evaluation**
- **Reski et al. (2025) — Urban Climate InteracTable**: Immersive exploration of urban climate in 3D city ← **Similar immersive + urban context; we broaden to multiple data types**

### This Thesis Positioning
**Unique**:
- First systematic evaluation of immersive analytics for **semantic understanding** of **dense, spatio-temporal urban data**
- Grounded in Swedish urban context (Gothenburg); uses open government data
- Combines learning (custom pipeline development) with research (evaluation)
- End-to-end: from data to interactive immersive prototype to user study

---

## VIII. TIMELINE & MILESTONES

| Phase | Duration | Key Milestones |
|-------|----------|---|
| **Learning Sprint** | Week 1 | Data sourced; Python/QGIS processed; Unity prototype with buildings/interiors. Document findings. |
| **Phase 1: Data Acq.** | Weeks 2–8 | All datasets acquired, cleaned, integrated. `data/processed/` complete. |
| **Phase 2: System Dev.** | Weeks 9–24 | Terrain + buildings rendering; LOD optimized; data pipeline automated; UI for interaction. |
| **Phase 3: Temporal Data** | *Weeks 18+* | Select datasets; temporal interface designed; visualization encodings tested. |
| **Phase 4: Evaluation** | *Weeks 20–26* | Study protocol finalized; participants recruited; user sessions conducted; data analyzed. |

---

## IX. STUDENT BACKGROUND & EXPERTISE

**Relevant Completed Courses**:
- **Computer Graphics** (DIT224) — 3D rendering, shaders, geometry
- **Game Engine Architecture** (DIT572) — Engine design, asset pipelines, performance
- **Data Science & AI** (DIT407) — Data preprocessing, analysis
- **Applied Machine Learning** (DIT867) — Could support data analysis tasks later
- **Game Research** (DIT248) — Research methods in game/immersive contexts
- **Current Trends in Gaming** (DIT468) — VR/AR/spatial computing

**Strengths**: Graphics, game engines, research methodology  
**Learning Goals This Sprint**: GIS data, coordinate systems, data pipelines

---

## X. KEY ASSUMPTIONS & OPEN QUESTIONS

### Assumptions
1. Latest Swedish open data is accessible without legal barriers
2. LiDAR data available at sufficient resolution (2m or better) for height extraction
3. Game engine (Unity) can handle 1–3 km² at interactive frame rates with LOD
4. Immersive VR hardware available for Phase 3+ evaluation (TBD)
5. Participants (urban planners, data analysts) available for user study

### Open Questions (To Resolve in Learning Sprint)
1. **Data**: Which datasets from Lantmäteriet/Göteborgs Stad are freely available? Any licensing restrictions for academic research?
2. **Pipeline**: Python + GDAL or QGIS? Or hybrid? What's the smoothest workflow from download → game engine?
3. **Interior Detail**: Feasible to procedurally generate interiors? Worth the complexity, or skip for Phase 1?
4. **LOD Strategy**: How many buildings render at 60 fps without optimizations? What LOD distances are appropriate?
5. **Coordinate Transforms**: How hard is SWEREF99 TM → local Unity origin? Any pitfalls?
6. **Evaluation Scope**: User study feasible in 6 months? Or defer to future work?

---

## XI. NOTES FOR FUTURE COLLABORATION

### Project Structure
```
Prototype/
├── docs/
│   ├── copilot_sessions/              ← Save all Copilot conversations here (as .md)
│   ├── thesis_proposal.pdf
│   ├── learning_sprint.md             ← Key learnings from Week 1
│   ├── research_findings.md           ← After evaluation
│   └── pipeline_guide.md
├── data/
│   ├── raw/                           ← Downloaded files
│   └── processed/                     ← Pipeline output
├── tools/
│   ├── data_processor_python.py
│   ├── data_processor_qgis_notes.txt
│   └── unity_exporter.py
├── unity_project/
│   ├── Assets/Scripts/DataImport/
│   ├── Assets/Data/
│   └── Scenes/
└── README.md
```

### For Copilot Context
- Student is **learning-focused**: balance between using existing tools and custom development for understanding
- **Performance-conscious**: target 60 fps (90 fps for future VR)
- **Scope-aware**: deliberately narrow 1-week sprint before committing to months of work
- **Documentation-oriented**: save session logs for reproducibility and team handoff
- **Graphics + Game Engine background**: Ask more technical questions about rendering optimization, less about basic game design
- **New to GIS**: Explain coordinate systems, projection transforms clearly; validate understanding frequently

### Recommended Conversation Starters (Future Sessions)
- "I've completed the learning sprint. Here are my findings on Python vs QGIS; which approach should I use for Phase 2?"
- "Phase 1 data acquisition is done. Ready to start Phase 2 city model generation. Let's design the LOD strategy."
- "I want to integrate traffic data into the digital twin. How should I encode spatio-temporal data in immersive 3D?"
- "Evaluation design is next. Help me design user tasks and metrics for measuring immersive analytics effectiveness."

---

**Document Version**: 1.0  
**Last Updated**: February 24, 2026  
**Status**: Ready for 1-week learning sprint