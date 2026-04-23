# CLAUDE.md

## Purpose
This file defines the **development rules, architecture expectations, and delivery standards** for this project.

The project is a **city-scale digital twin of Gothenburg** used as a platform for immersive analytics and urban data exploration. The focus of this file is **development only**.

Do **not** use this file for thesis prose, literature review writing, citation formatting, or general academic drafting unless a task explicitly asks for development-facing technical documentation.

---

## Development Scope
Work in this project should support one or more of the following:

1. **Urban data ingestion**
   - GIS layers
   - building footprints
   - terrain/elevation data
   - LiDAR-derived data
   - map layers from public authorities
   - spatiotemporal urban datasets such as traffic, climate, or energy data

2. **Data preprocessing and harmonization**
   - coordinate reference system alignment
   - schema cleaning and normalization
   - entity matching between datasets
   - temporal alignment of observations
   - aggregation to buildings, roads, grids, or districts

3. **3D city model generation**
   - terrain mesh generation
   - building mesh generation
   - semantic tagging of spatial entities
   - scalable level-of-detail handling
   - export/import workflows for engine-ready assets

4. **Digital twin runtime**
   - loading and rendering the city model
   - streaming or chunking large scenes
   - metadata lookup and hover/select interactions
   - linking spatial objects to analytical datasets
   - timeline and temporal playback controls

5. **Immersive analytics interface**
   - desktop 3D mode and/or VR mode
   - navigation and camera control
   - spatial selection tools
   - filtering and layer toggles
   - time navigation widgets
   - comparison views for locations/time periods
   - interaction logging for later analysis

6. **Evaluation support features**
   - reproducible scenarios
   - stable task configurations
   - participant-safe builds
   - metrics logging
   - exportable session results

---

## Primary Goal
Build a **robust, explainable, and evaluation-ready prototype**, not an overengineered platform.

Priority order:

1. **Correctness and reproducibility**
2. **Clear data lineage**
3. **Usable interaction design**
4. **Performance sufficient for testing**
5. **Extensibility where it is low-cost**
6. **Visual polish only after the above are stable**

When tradeoffs appear, prefer the option that keeps the prototype:
- easier to validate,
- easier to document,
- easier to debug,
- easier to use in evaluation,
- easier to reproduce on another machine.

---

## Non-Goals
Unless explicitly requested, avoid spending major effort on:

- full-city perfection
- photorealism for its own sake
- complex custom engine work without research value
- real-time sensor integration
- production cloud infrastructure
- unnecessary microservices
- large refactors that do not improve the prototype's research utility
- academic chapter writing

---

## Working Principles

### 1. Keep the pipeline observable
Every transformation step should be inspectable.

Prefer:
- intermediate outputs
- small validation scripts
- summary tables
- explicit schema definitions
- logged assumptions

Avoid black-box transformations with no traceability.

### 2. Preserve semantics
Do not reduce data to geometry only.
Whenever possible, preserve:
- source identifiers
- building or road IDs
- category/type labels
- timestamps
- units
- CRS information
- aggregation method
- provenance metadata

### 3. Build for iteration
The prototype should allow repeated import, correction, rebuild, and retesting.

Prefer:
- configuration-driven paths
- scripts over manual clicking
- deterministic exports
- restartable preprocessing steps

### 4. Separate concerns
Keep these layers distinct:
- raw data
- cleaned/intermediate data
- engine-ready assets
- runtime logic
- evaluation/logging outputs
- reports and documentation

### 5. Avoid silent assumptions
If you assume:
- CRS,
- unit conversion,
- timestamp resolution,
- missing value handling,
- mesh simplification thresholds,
- aggregation logic,

then write it down in code comments and project documentation.


### 6. Treat Swedish datasets as first-class inputs
Many source datasets, file names, layer names, attributes, labels, and free-text values in this project are in **Swedish**.

Required behavior:
- preserve the **original Swedish names and values** in raw and intermediate data;
- create **English translations or aliases** for developer-facing documentation, configs, reports, and runtime labels when helpful;
- do not silently replace the original source terminology with English if traceability would be lost;
- when introducing translated field names, document the mapping from **original Swedish -> English alias**.

For every important dataset, record:
- original dataset name;
- English translation;
- source authority;
- original field names used;
- translated field names or UI labels used.

Preferred pattern:
- raw data keeps original names;
- processed data may add English aliases;
- docs should include both Swedish and English on first mention.

Example:
- `Byggnad` -> `Building`
- `Ortnamn` -> `Place names`
- `Markhöjdmodell` -> `Terrain elevation model`

### 7. Be typo-tolerant and language-aware during data handling
Swedish source data may contain:
- spelling variation,
- abbreviations,
- OCR-like mistakes,
- inconsistent capitalization,
- mixed Swedish/English labels,
- municipality-specific naming conventions.

Required behavior:
- never assume labels are clean just because they look official;
- use cautious matching for field names and categorical values;
- prefer explicit normalization tables over ad hoc string replacements;
- log suspicious values, near-duplicates, unknown categories, and unmatched records;
- preserve the original raw value whenever a cleaned value is introduced.

When cleaning or matching text fields:
- document the original value;
- document the cleaned/normalized value;
- document the rule or reason;
- flag uncertainty instead of pretending the match is correct.

If confidence is low, do **not** invent certainty. Mark the issue and continue with the safest fallback.

### 8. Build from the current project state first
Prefer extending what already exists in the repository and prototype before proposing a larger architecture or requiring more data.

Current default attitude:
- use the datasets, scripts, scene structure, and processed assets already available;
- improve the existing pipeline incrementally;
- only request additional data when the current data is truly insufficient for the task.

Do not block progress by demanding ideal data too early.

### 9. Insufficiency protocol
If the available data is not sufficient:
- say so clearly;
- state exactly **what is missing**;
- state **why it matters**;
- state the **smallest additional data** needed;
- suggest a fallback using the current data, if one exists.

Do not hallucinate missing attributes, translations, semantics, or spatial precision.
Do not claim a dataset supports something unless that support is visible in the data or documented.

The default should be:
- build with what exists,
- note limitations,
- ask for or recommend the minimum extra data only when necessary.

---

## Recommended Project Structure
Adapt as needed, but keep a similar separation:

```text
project-root/
├─ data/
│  ├─ raw/
│  ├─ external/
│  ├─ interim/
│  ├─ processed/
│  └─ metadata/
├─ docs/
│  ├─ architecture/
│  ├─ setup/
│  ├─ decisions/
│  ├─ data-analysis/
│  ├─ experiments/
│  └─ troubleshooting/
├─ reports/
│  ├─ data-quality/
│  ├─ preprocessing/
│  ├─ integration/
│  ├─ performance/
│  └─ evaluation-support/
├─ scripts/
│  ├─ ingest/
│  ├─ preprocess/
│  ├─ validate/
│  ├─ export/
│  └─ analysis/
├─ src/
│  ├─ core/
│  ├─ data/
│  ├─ runtime/
│  ├─ ui/
│  ├─ analytics/
│  ├─ logging/
│  └─ utils/
├─ engine/
│  └─ [Unity_or_other_runtime_project]
├─ tests/
├─ configs/
├─ notebooks/
└─ CLAUDE.md
```

If the actual stack differs, preserve the same intent.

---

## Required Documentation
Every substantial development task should leave behind usable documentation.

Minimum expectation:

### For every new component
Create or update documentation covering:
- purpose
- inputs
- outputs
- dependencies
- assumptions
- failure cases
- how to run it
- how to verify it worked

### For every important decision
Create a short decision note in `docs/decisions/`:
- context
- chosen option
- alternatives considered
- consequences

### For every dataset
Document in `data/metadata/` or `docs/data-analysis/`:
- original dataset name
- English translation of the dataset name
- source
- access date
- format
- CRS
- spatial extent
- temporal extent
- original field names used
- translated field names or aliases used
- known gaps
- license/usage constraints
- preprocessing applied
- target entities after integration

---

## Data Analysis Documentation and Reports
This project must include **development-facing data analysis documentation**, not only code.

Any time data is explored, transformed, or integrated, produce a short report.

### Required report types

#### 1. Data profiling report
For each dataset, document:
- row/feature count
- geometry types
- column names and types
- Swedish field names and proposed English aliases
- missing values
- duplicate identifiers
- value ranges
- timestamp coverage
- categorical distributions
- suspected anomalies
- spelling variations or likely typos that affect processing

#### 2. Cleaning report
Describe:
- what was removed
- what was repaired
- what was imputed
- what was standardized
- which Swedish labels/fields were translated or aliased to English
- which spelling variants or likely typos were normalized
- what remains unresolved

#### 3. Integration report
Describe:
- which datasets were joined
- join keys or spatial join method
- matching rate
- unmatched records
- spatial tolerance used
- temporal aggregation/alignment method
- quality risks introduced by the merge

#### 4. Visualization-readiness report
Before sending data into the runtime, document:
- target spatial entity type
- target temporal granularity
- units after conversion
- normalization/scaling used
- performance implications
- expected UI mapping

#### 5. Performance report
When data or rendering changes meaningfully, record:
- scene load time
- memory impact
- frame rate or rough performance observations
- bottlenecks
- mitigation attempts

### Report format
Reports can be markdown files in:
- `docs/data-analysis/`
- `reports/data-quality/`
- `reports/integration/`
- `reports/performance/`

Each report should be concise, reproducible, and decision-oriented.

Preferred filename style:

```text
YYYY-MM-DD_dataset-name_report-type.md
```

Example:

```text
2026-04-23_buildings_gpkg_profiling.md
2026-04-23_traffic_timeseries_integration.md
```

---

## Coding Standards

### General
- Prefer readable code over clever code.
- Keep functions focused and small.
- Use explicit names.
- Avoid hidden global state.
- Avoid magic numbers; move them to config.
- Add logging for pipeline-critical steps.
- Fail loudly on invalid assumptions.

### Python / preprocessing scripts
- Use type hints where practical.
- Use `pathlib` for paths.
- Use structured configs where possible.
- Keep notebooks exploratory; move stable logic to scripts/modules.
- Do not bury production logic only inside notebooks.

### C# / Unity runtime
- Separate data loading, scene generation, and interaction logic.
- Avoid monolithic manager classes when possible.
- Keep serialized inspector fields clear and documented.
- Do not hardcode dataset paths inside scene objects.
- Prefer scriptable configuration or external config files for data sources.

### Data integrity
- Never overwrite raw data.
- Write transformed outputs to new locations.
- Keep versioned exports when a transformation is important.

---

## Validation Expectations
Before considering a task complete, validate at the appropriate level.

### Data tasks
Validate:
- schema consistency
- CRS consistency
- geometry validity
- identifier uniqueness
- timestamp parse success
- row/feature counts before and after transformation

### 3D/model tasks
Validate:
- meshes load successfully
- coordinates are correctly aligned
- scale is plausible
- object metadata survives import
- entity picking resolves the correct object

### Runtime tasks
Validate:
- scene loads without manual fixes
- interactions produce expected results
- time controls affect the intended dataset only
- filters do not silently corrupt state
- session logs are written correctly

### Evaluation-support tasks
Validate:
- tasks can be reset
- user flows are stable
- logs export correctly
- builds can run on target hardware

---

## Logging and Reproducibility
All major workflows should generate enough information to be replayed or audited.

Minimum logging for data workflows:
- input file names
- run timestamp
- config used
- counts before/after
- warnings
- output path

Minimum logging for runtime/evaluation:
- session ID
- build version
- dataset version
- user actions relevant to analysis
- selected entities
- time range explored
- filter state changes
- export location

---

## Preferred Development Workflow
When implementing a new feature or data step:

1. Clarify the task and affected layer.
2. Identify inputs, outputs, and assumptions.
3. Check whether a simpler path already exists.
4. Implement the smallest useful version.
5. Validate with a tiny sample first.
6. Document what was done.
7. Produce or update a report if data changed.
8. Only then optimize or generalize.

---

## When Writing Documentation
Write for the future developer or researcher who opens the project months later and needs to answer:
- What is this?
- Why does it exist?
- What data does it use?
- How do I run it?
- How do I know it worked?
- What can I safely change?
- What should I not touch lightly?

Prefer:
- direct language
- short sections
- checklists when useful
- examples of commands or file paths
- explicit failure notes

---

## Task Prioritization Heuristic
When multiple development options exist, prefer the option that best supports:

1. usable prototype progress,
2. clean data integration,
3. stable interaction and logging,
4. evaluation readiness,
5. maintainability.

Do not chase architectural perfection too early.

---

## Definition of Done
A development task is done when:

- the code or asset works,
- the assumptions are documented,
- validation has been performed,
- outputs are reproducible,
- related docs are updated,
- a data-analysis/integration/performance report exists if relevant,
- another person could reasonably continue from the result.

---

## Default Behavior for AI Coding Assistance
When acting as a coding assistant on this project:

- focus on implementation, debugging, structure, and technical documentation;
- keep academic discussion out unless explicitly requested;
- prefer actionable outputs over abstract advice;
- suggest concrete folder locations for new files;
- mention risks to data quality, scale, and reproducibility;
- produce reports when data changes;
- avoid unnecessary complexity;
- keep the prototype aligned with the project’s core urban digital twin and immersive analytics goals;
- translate Swedish dataset names and important labels into English for developer clarity while preserving the original source terminology;
- handle Swedish source text cautiously and be alert to possible typos, spelling variation, and inconsistent field naming;
- build on the current prototype and available data first;
- if the data is insufficient, say exactly what is missing instead of inventing details.

