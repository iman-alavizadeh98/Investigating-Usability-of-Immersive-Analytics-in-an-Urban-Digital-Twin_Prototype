# Buildings Pipeline: Modular Dataset Analysis

**Date**: May 11, 2026  
**Status**: Complete - Analysis & Translation Ready

## Overview

Modular pipeline for analyzing and preprocessing the Swedish **Byggnad (Buildings)** dataset. Designed to be reusable for similar geographic/administrative datasets from other regions or countries.

**Focus**: Data understanding and standardization, NOT mesh generation yet.

---

## Architecture

```
BuildingsPipeline (inherits BasePipeline)
├── config.py
│   ├── Swedish → English field translations
│   ├── Building type classifications (from Lantmäteriet PDF)
│   ├── Primary purpose categories
│   ├── Collection level definitions
│   └── Dataset metadata
│
├── pipeline.py
│   ├── load()     → Read GeoPackage, preserve raw Swedish names
│   ├── validate() → Check geometry, CRS, completeness
│   ├── preprocess() → Translate fields, standardize values
│   └── export()   → Save processed GeoPackage + metadata
│
└── __init__.py    → Package marker
```

---

## Key Concepts

### 1. Reusability Through Configuration

Instead of hardcoding translations, all mappings are in `config.py`:

```python
FIELD_TRANSLATIONS = {
    "objektidentitet": "object_id",
    "andamal1": "primary_purpose",
    # ... extensible for other datasets
}

BUILDING_TYPES = {
    "Bostad": {"en": "Residence", ...},
    "Industri": {"en": "Industrial", ...},
    # ... all 7 types from Lantmäteriet PDF
}
```

**To adapt for another Swedish dataset:**
- Swap `config.py` translations
- Reuse the same pipeline structure
- Only modify field/value mappings

### 2. Swedish ↔ English Preservation

**Raw data** → Keeps all Swedish field names (objektidentitet, andamal1, etc.)
**Processed data** → Adds English translations + standardized categories

Example:
```
Swedish field:     objekttyp → "Bostad"
English field:     object_type_en → "Residence"
Category field:    object_type_category → "Building used for residential purposes..."
```

### 3. Validation Before Transformation

Pipeline validates first, then transforms:

1. Check CRS (should be EPSG:3006 - Swedish national grid)
2. Check geometry validity
3. Verify required fields exist
4. Report null/missing percentages
5. **Then** apply translations

This ensures we know data quality before processing.

---

## Dataset Structure (From Lantmäteriet PDF)

### Building Object Types (Table 4)

| Swedish | English | Code | Min Size |
|---------|---------|------|----------|
| Bostad | Residence | 2061 | 15 m² |
| Industri | Industrial | 2062 | 15 m² |
| Samhällsfunktion | Public facility | 2063 | 15 m² |
| Verksamhet | Business | 2064 | 15 m² |
| Ekonomibyggnad | Farm building | 2065 | 15 m² |
| Komplementbyggnad | Ancillary | 2066 | 15 m² |
| Övrig byggnad | Other | 2067 | 15 m² |

### Primary Purposes (andamal1)

Organized by building type with detailed translations:

**Residential:**
- Småhus friliggande → Single-family detached
- Småhus radhus → Row house
- Flerfamiljshus → Multi-family apartment

**Industrial:**
- Metall- eller maskinindustri → Metal/machinery
- Textilindustri → Textile
- Trävaruindustri → Wood products

**Public Facilities:** 25+ categories (hospitals, schools, fire stations, etc.)

### Collection Level (insamlingslage)

How building location was determined:

- **fasad** → Facade: Precise edge-of-building measurement
- **takkant** → Roof edge: Measurement at roof line
- **illustrativt läge** → Schematic: Building shown but not surveyed
- **ospecificerad** → Unspecified

---

## Dataset Metadata (From PDF)

| Property | Value |
|----------|-------|
| Name (SV) | Byggnad, vektor |
| Name (EN) | Buildings, vector |
| Authority | Lantmäteriet (Swedish Land Survey) |
| Version | 1.6 (2023-02-01) |
| CRS (Plane) | SWEREF 99 TM |
| CRS (Height) | RH 2000 |
| Coverage | Sweden nationwide |
| Minimum size | 15 m² |
| Completeness | ~96% |
| Position accuracy | 0.02–50 meters |
| Update frequency | Continuous (municipalities) + Periodic (Lantmäteriet) |

---

## Usage

### Basic Pipeline Run

```bash
python Src/scripts/run_buildings_pipeline.py
```

Outputs:
- `Processed_data/buildings_processed.gpkg` → Cleaned GeoPackage with English fields
- `Processed_data/buildings_metadata.json` → Translation mappings + classifications
- `Processed_data/buildings_summary.json` → Statistics

### With Data Profiling

```bash
python Src/scripts/run_buildings_pipeline.py --profile
```

Additional outputs:
- `Processed_data/buildings_profile.md` → Markdown table (schema + stats)
- `Processed_data/buildings_profile.html` → Interactive HTML profile

### Custom Paths

```bash
python Src/scripts/run_buildings_pipeline.py \
  --input path/to/custom.gpkg \
  --output custom_output_dir \
  --profile
```

---

## Python API

```python
from pipelines.buildings.pipeline import BuildingsPipeline

config = {
    "input_gpkg": "Raw_data/byggnad_gpkg/byggnad_sverige.gpkg",
    "output_dir": "Processed_data",
}

pipeline = BuildingsPipeline(config=config)
report = pipeline.run()

# Access processed data
print(pipeline.data.head())
print(pipeline.validation_report)
```

---

## Extending to Other Datasets

### Template for New Pipeline

Create `Src/pipelines/[dataset_name]/`:

```
[dataset_name]/
├── __init__.py
├── config.py           ← Customize translations & classifications
├── pipeline.py         ← Reuse/adapt pipeline structure
└── README.md           ← Document the dataset
```

**Example: Population (befolkning) dataset**

```python
#config.py
FIELD_TRANSLATIONS = {
    "Rutstorl": "grid_cell_size_m",
    "Ruta": "grid_cell_id",
    "Alder_0_6": "age_0_6",
    # ... population-specific fields
}

# pipeline.py
class PopulationPipeline(BasePipeline):
    def __init__(self, ...):
        super().__init__(...)
        self.field_translations = FIELD_TRANSLATIONS
        # ... population-specific config
```

---

## Quality Assurance

### Validation Checks

- ✓ CRS correctness (EPSG:3006)
- ✓ Geometry validity (no null, invalid, or self-intersecting)
- ✓ Required fields present
- ✓ Data completeness metrics
- ✓ Field coverage percentages

### Exported Validation Report

In `buildings_metadata.json`:

```json
{
  "validation_report": {
    "total_buildings": 2547283,
    "fields": 23,
    "crs": "EPSG:3006",
    "geometry_types": ["Polygon", "MultiPolygon"],
    "issues": [],
    "andamal1_null_pct": 17.5
  }
}
```

---

## Output Files

### buildings_processed.gpkg
GeoPackage with translated fields + new columns:
- Original Swedish names preserved
- English translations (object_type_en, primary_purpose_en, etc.)
- Standardized categories (object_type_category, primary_purpose_category)
- All geometry validated

### buildings_metadata.json
Complete reference documentation:
- All field translations
- Building type classifications
- Primary purpose categories
- Collection level definitions
- Dataset metadata from Lantmäteriet

### buildings_summary.json
Quick statistics:
- Total buildings
- Geometry validity count
- Building type distribution
- Purpose category distribution

### buildings_profile.md / .html
Data analysis reports (if `--profile` used):
- Column-by-column analysis
- Null value percentages
- Unique value counts
- Sample rows
- Memory usage

---

## Next Steps

1. **Run the pipeline** on the full Byggnad dataset
2. **Analyze outputs** to understand data quality
3. **Design mesh-specific preprocessing** if needed later
4. **Adapt this pipeline** for other datasets (Population, Employment, Income, etc.)
5. **Integrate with analytics** when ready

**Key Decision**: Mesh generation is separate. Focus now is on understanding and standardizing the data.

---

**References**:
- Lantmäteriet PRODUKTBESKRIVNING: Byggnad Nedladdning, vektor (v1.6, 2023-02-01)
- Swedish national grid: SWEREF 99 TM + RH 2000
- Minimum building size: 15 m²
- Data quality: ~96% complete, 0.02–50m positional accuracy
