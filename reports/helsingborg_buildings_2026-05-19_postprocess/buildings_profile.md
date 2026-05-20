# Buildings (Byggnad) - Postprocess Snapshot

**Type**: GeoDataFrame

## Summary

| Metric | Value |
|--------|-------|
| Rows | 43,981 |
| Columns | 25 |
| Memory | 45.4 MB |
| CRS | EPSG:3006 |
| Duplicate rows | 0 |
| Duplicate object_id | 0 |

## Missing Values

| Column | Missing | Missing % |
|--------|---------|-----------|
| `position_uncertainty_height_m` | 8,207 | 18.7% |
| `building_name_primary` | 43,764 | 99.5% |
| `building_name_secondary` | 43,968 | 100.0% |
| `building_name_tertiary` | 43,981 | 100.0% |
| `secondary_purpose` | 43,268 | 98.4% |
| `tertiary_purpose` | 43,980 | 100.0% |
| `quaternary_purpose` | 43,981 | 100.0% |
| `quinary_purpose` | 43,981 | 100.0% |

## Columns

| Name | Type | Non-Null | Null % | Unique | Meaning |
|------|------|----------|--------|--------|---------|
| `object_id` | str | 43,981 | 0.0% | 43981 | Swedish source: objektidentitet |
| `version_valid_from` | datetime64[ms, UTC] | 43,981 | 0.0% | 5521 | Swedish source: versiongiltigfran |
| `position_uncertainty_plan_m` | float64 | 43,981 | 0.0% | 15 | Swedish source: lagesosakerhetplan |
| `position_uncertainty_height_m` | float64 | 35,774 | 18.7% | 13 | Swedish source: lagesosakerhethojd |
| `original_organisation` | str | 43,981 | 0.0% | 3 | Swedish source: ursprunglig_organisation |
| `object_version` | int32 | 43,981 | 0.0% | 10 | Swedish source: objektversion |
| `object_type_number` | int32 | 43,981 | 0.0% | 7 | Swedish source: objekttypnr |
| `object_type` | str | 43,981 | 0.0% | 7 | Swedish source: objekttyp |
| `collection_level` | str | 43,981 | 0.0% | 3 | Swedish source: insamlingslage |
| `building_name_primary` | str | 217 | 99.5% | 131 | Swedish source: byggnadsnamn1 |
| `building_name_secondary` | str | 13 | 100.0% | 12 | Swedish source: byggnadsnamn2 |
| `building_name_tertiary` | object | 0 | 100.0% | 0 | Swedish source: byggnadsnamn3 |
| `house_number` | int32 | 43,981 | 0.0% | 642 | Swedish source: husnummer |
| `main_building_flag` | bool | 43,981 | 0.0% | 2 | Main building flag (converted to boolean) |
| `primary_purpose` | str | 43,981 | 0.0% | 27 | Swedish source: andamal1 |
| `secondary_purpose` | str | 713 | 98.4% | 7 | Swedish source: andamal2 |
| `tertiary_purpose` | str | 1 | 100.0% | 1 | Swedish source: andamal3 |
| `quaternary_purpose` | object | 0 | 100.0% | 0 | Swedish source: andamal4 |
| `quinary_purpose` | object | 0 | 100.0% | 0 | Swedish source: andamal5 |
| `geometry` | geometry | 43,981 | 0.0% | 43981 |  |
| `object_type_en` | str | 43,981 | 0.0% | 7 | English label for object_type |
| `object_type_category` | str | 43,981 | 0.0% | 7 | High-level category for object_type |
| `primary_purpose_en` | str | 43,981 | 0.0% | 27 | English label for primary_purpose |
| `primary_purpose_category` | str | 43,981 | 0.0% | 1 | High-level category for primary_purpose |
| `collection_level_en` | str | 43,981 | 0.0% | 3 | English label for collection_level |

## Numeric Statistics

| Column | Min | Max | Mean | Std |
|--------|-----|-----|------|-----|
| `position_uncertainty_plan_m` | 0.03 | 50.00 | 0.23 | 0.36 |
| `position_uncertainty_height_m` | 0.03 | 10.00 | 3.57 | 2.89 |

## Categorical Distributions

### object_id

- `0000b2a0-ffbd-47a1-a33b-654af6e10fa4`: 1
- `00040e05-a0a5-457c-927a-970aa40e2d61`: 1
- `00049943-bca0-4a51-a055-083d3e902de3`: 1
- `0005c6b6-dad4-4206-947d-1f08ecddba5d`: 1
- `0006458c-6f72-49b9-9da0-d900f6cee910`: 1
- ... and 5 more

### original_organisation

- `Lantmäteriet`: 25767
- `Helsingborg`: 18133
- `Kommunsamverkan`: 81

### object_type

- `Komplementbyggnad`: 26413
- `Bostad`: 12526
- `Övrig byggnad`: 2106
- `Industri`: 1137
- `Samhällsfunktion`: 1078
- ... and 2 more

### collection_level

- `Fasad`: 24735
- `Ospecificerad`: 16492
- `Takkant`: 2754

### building_name_primary

- `Rönnowska skolan`: 12
- `Högastensskolan`: 10
- `Fredriksdals friluftsmuseum`: 7
- `Nanny Palmkvistskolan`: 7
- `Ringstorpsskolan`: 6
- ... and 5 more

### building_name_secondary

- `Söderskolan`: 2
- `Pålsjö park`: 1
- `Fyrklöverns montessori`: 1
- `Olympiahuset`: 1
- `Borgmästarskolan`: 1
- ... and 5 more

### building_name_tertiary


### primary_purpose

- `Komplementbyggnad;`: 26413
- `Bostad;Småhus friliggande`: 6469
- `Bostad;Flerfamiljshus`: 2392
- `Övrig byggnad;`: 2106
- `Bostad;Småhus radhus`: 1837
- ... and 5 more

### secondary_purpose

- `Verksamhet;`: 652
- `Bostad;Flerfamiljshus`: 56
- `Samhällsfunktion;Skola`: 1
- `Samhällsfunktion;Busstation`: 1
- `Samhällsfunktion;Sporthall`: 1
- ... and 2 more

### tertiary_purpose

- `Verksamhet;`: 1

### quaternary_purpose


### quinary_purpose


### object_type_en

- `Ancillary building`: 26413
- `Residence`: 12526
- `Other building`: 2106
- `Industrial`: 1137
- `Public facility`: 1078
- ... and 2 more

### object_type_category

- `Small building attached to dwelling (garage, shed, etc., >15 kvm)`: 26413
- `Building used for residential purposes (single/multi-family, >15 kvm)`: 12526
- `Building with other purpose (colonist hut, shelter, tower, etc., >15 kvm)`: 2106
- `Building containing manufacturing or processing of products (>15 kvm)`: 1137
- `Building for public community services (>15 kvm)`: 1078
- ... and 2 more

### primary_purpose_en

- `Komplementbyggnad;`: 26413
- `Bostad;Småhus friliggande`: 6469
- `Bostad;Flerfamiljshus`: 2392
- `Övrig byggnad;`: 2106
- `Bostad;Småhus radhus`: 1837
- ... and 5 more

### primary_purpose_category

- `Other`: 43981

### collection_level_en

- `Fasad`: 24735
- `Ospecificerad`: 16492
- `Takkant`: 2754

## Sample Rows

```
                                  object_id        version_valid_from  position_uncertainty_plan_m  position_uncertainty_height_m original_organisation  object_version  object_type_number        object_type collection_level building_name_primary building_name_secondary building_name_tertiary  house_number  main_building_flag            primary_purpose secondary_purpose tertiary_purpose quaternary_purpose quinary_purpose                                                                                                                                                                                                                                geometry      object_type_en                                                   object_type_category         primary_purpose_en primary_purpose_category collection_level_en
16309  0000b2a0-ffbd-47a1-a33b-654af6e10fa4 2011-03-21 14:51:27+00:00                         0.50                            2.5          Lantmäteriet               1                2061             Bostad    Ospecificerad                   NaN                     NaN                   None             1               False  Bostad;Småhus friliggande               NaN              NaN               None            None                                                                                                MULTIPOLYGON (((360046.028 6209970.205, 360047.068 6209979.386, 360035.901 6209980.653, 360034.86 6209971.473, 360046.028 6209970.205)))           Residence  Building used for residential purposes (single/multi-family, >15 kvm)  Bostad;Småhus friliggande                    Other       Ospecificerad
10268  00040e05-a0a5-457c-927a-970aa40e2d61 2024-04-03 13:52:08+00:00                         0.10                           10.0           Helsingborg               1                2066  Komplementbyggnad    Ospecificerad                   NaN                     NaN                   None           425               False         Komplementbyggnad;               NaN              NaN               None            None                                                                                                MULTIPOLYGON (((359343.424 6209764.138, 359342.298 6209766.369, 359340.513 6209765.468, 359341.64 6209763.236, 359343.424 6209764.138)))  Ancillary building      Small building attached to dwelling (garage, shed, etc., >15 kvm)         Komplementbyggnad;                    Other       Ospecificerad
35718  00049943-bca0-4a51-a055-083d3e902de3 2011-03-21 14:50:47+00:00                         0.03                            2.5          Lantmäteriet               1                2066  Komplementbyggnad            Fasad                   NaN                     NaN                   None             3               False         Komplementbyggnad;               NaN              NaN               None            None  MULTIPOLYGON (((358020.46 6215999.944, 358021.641 6216000.152, 358021.661 6215999.826, 358022.67 6216000.022, 358020.154 6216011.784, 358019.113 6216011.624, 358019.256 6216011.215, 358018.085 6216010.856, 358020.46 6215999.944)))  Ancillary building      Small building attached to dwelling (garage, shed, etc., >15 kvm)         Komplementbyggnad;                    Other               Fasad
```
