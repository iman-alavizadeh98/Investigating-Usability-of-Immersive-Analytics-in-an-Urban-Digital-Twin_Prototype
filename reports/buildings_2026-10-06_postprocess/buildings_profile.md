# Buildings (Byggnad) - Postprocess Snapshot

**Type**: GeoDataFrame

## Summary

| Metric | Value |
|--------|-------|
| Rows | 43,981 |
| Columns | 33 |
| Memory | 32.1 MB |
| CRS | EPSG:3006 |
| Duplicate rows | 0 |
| Duplicate object_id | 0 |

## Missing Values

| Column | Missing | Missing % |
|--------|---------|-----------|
| `position_uncertainty_height_m` | 8,135 | 18.5% |
| `building_name_primary` | 43,764 | 99.5% |
| `building_name_secondary` | 43,968 | 100.0% |
| `building_name_tertiary` | 43,981 | 100.0% |
| `secondary_purpose` | 43,268 | 98.4% |
| `tertiary_purpose` | 43,980 | 100.0% |
| `quaternary_purpose` | 43,981 | 100.0% |
| `quinary_purpose` | 43,981 | 100.0% |
| `secondary_purpose_en` | 43,268 | 98.4% |
| `tertiary_purpose_en` | 43,980 | 100.0% |
| `quaternary_purpose_en` | 43,981 | 100.0% |
| `quinary_purpose_en` | 43,981 | 100.0% |

## Columns

| Name | Type | Non-Null | Null % | Unique | Meaning |
|------|------|----------|--------|--------|---------|
| `object_id` | str | 43,981 | 0.0% | 43981 | Swedish source: objektidentitet |
| `version_valid_from` | datetime64[ms, UTC] | 43,981 | 0.0% | 5521 | Swedish source: versiongiltigfran |
| `position_uncertainty_plan_m` | float64 | 43,981 | 0.0% | 15 | Swedish source: lagesosakerhetplan |
| `position_uncertainty_height_m` | float64 | 35,846 | 18.5% | 12 | Swedish source: lagesosakerhethojd |
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
| `object_type_en` | object | 43,981 | 0.0% | 7 | English label for object_type |
| `object_type_category` | object | 43,981 | 0.0% | 7 | Broad English category for object_type (Residential, Public, ...) |
| `primary_purpose_en` | object | 43,981 | 0.0% | 27 | English label for primary_purpose (e.g. 'Samhällsfunktion;Sjukhus' -> 'Hospital') |
| `primary_purpose_category` | object | 43,981 | 0.0% | 7 | Broad English category of the primary purpose's object type |
| `secondary_purpose_en` | object | 713 | 98.4% | 7 | English label for secondary_purpose |
| `tertiary_purpose_en` | object | 1 | 100.0% | 1 | English label for tertiary_purpose |
| `quaternary_purpose_en` | object | 0 | 100.0% | 0 | English label for quaternary_purpose |
| `quinary_purpose_en` | object | 0 | 100.0% | 0 | English label for quinary_purpose |
| `collection_level_en` | object | 43,981 | 0.0% | 3 | English label for collection_level |
| `main_building_flag_sv` | str | 43,981 | 0.0% | 2 | Swedish source: huvudbyggnad (Ja/Nej) |
| `footprint_area_m2` | float64 | 43,981 | 0.0% | 17320 | Footprint area in m² (EPSG:3006) |
| `source_part_count` | int64 | 43,981 | 0.0% | 9 | Postprocess only: number of source rows (pieces) merged into this building |
| `collection_level_mixed` | bool | 43,981 | 0.0% | 2 | Postprocess only: pieces had different collection levels |

## Numeric Statistics

| Column | Min | Max | Mean | Std |
|--------|-----|-----|------|-----|
| `position_uncertainty_plan_m` | 0.03 | 50.00 | 0.23 | 0.36 |
| `position_uncertainty_height_m` | 0.03 | 10.00 | 3.59 | 2.91 |
| `footprint_area_m2` | 0.00 | 40080.79 | 152.46 | 743.57 |
| `source_part_count` | 1.00 | 11.00 | 1.04 | 0.25 |

## Categorical Distributions

### object_id

- `15fc7757-1cf2-4d88-a12d-8d12ccc0c872`: 1
- `68307637-8623-45e4-8e2c-28d9b4cb5977`: 1
- `8527579f-be6a-4388-afab-5ad267c407ed`: 1
- `1b95930c-c15e-47ee-b52c-6a32124163a7`: 1
- `2b06ae56-834b-4762-91dc-a92196134904`: 1
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

- `Fasad`: 24721
- `Ospecificerad`: 16513
- `Takkant`: 2747

### building_name_primary

- `Rönnowska skolan`: 12
- `Högastensskolan`: 10
- `Nanny Palmkvistskolan`: 7
- `Fredriksdals friluftsmuseum`: 7
- `Ringstorpsskolan`: 6
- ... and 5 more

### building_name_secondary

- `Söderskolan`: 2
- `Filbornakyrkan`: 1
- `Maria gymnasiesärskola`: 1
- `Fyrklöverns montessori`: 1
- `Centrumskolan`: 1
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
- `Bostad;Småhus friliggande`: 1
- `Samhällsfunktion;Sporthall`: 1
- `Samhällsfunktion;Busstation`: 1
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

- `Ancillary`: 26413
- `Residential`: 12526
- `Other`: 2106
- `Industrial`: 1137
- `Public`: 1078
- ... and 2 more

### primary_purpose_en

- `Ancillary building (unspecified)`: 26413
- `Detached house`: 6469
- `Apartment building`: 2392
- `Other building (unspecified)`: 2106
- `Terraced house`: 1837
- ... and 5 more

### primary_purpose_category

- `Ancillary`: 26413
- `Residential`: 12526
- `Other`: 2106
- `Industrial`: 1137
- `Public`: 1078
- ... and 2 more

### secondary_purpose_en

- `Business (unspecified)`: 652
- `Apartment building`: 56
- `Detached house`: 1
- `Sports hall`: 1
- `Bus station`: 1
- ... and 2 more

### tertiary_purpose_en

- `Business (unspecified)`: 1

### quaternary_purpose_en


### quinary_purpose_en


### collection_level_en

- `Facade`: 24721
- `Unspecified`: 16513
- `Roof edge`: 2747

### main_building_flag_sv

- `Nej`: 43833
- `Ja`: 148

## Sample Rows

```
                              object_id        version_valid_from  position_uncertainty_plan_m  position_uncertainty_height_m original_organisation  object_version  object_type_number    object_type collection_level building_name_primary building_name_secondary building_name_tertiary  house_number  main_building_flag                     primary_purpose secondary_purpose tertiary_purpose quaternary_purpose quinary_purpose                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 geometry  object_type_en object_type_category              primary_purpose_en primary_purpose_category secondary_purpose_en tertiary_purpose_en quaternary_purpose_en quinary_purpose_en collection_level_en main_building_flag_sv  footprint_area_m2  source_part_count  collection_level_mixed
0  15fc7757-1cf2-4d88-a12d-8d12ccc0c872 2017-10-18 15:46:07+00:00                         0.15                          10.00           Helsingborg               4                2061         Bostad            Fasad                   NaN                     NaN                   None             3               False  Bostad;Småhus med flera lägenheter               NaN              NaN               None            None  MULTIPOLYGON (((359811.201 6211983.499, 359805.569 6211983.228, 359805.741 6211979.633, 359804.493 6211979.573, 359804.598 6211977.376, 359802.039 6211977.253, 359802.388 6211969.973, 359804.947 6211970.095, 359805.062 6211967.689, 359804.064 6211967.641, 359804.198 6211964.844, 359801.639 6211964.722, 359801.988 6211957.441, 359804.547 6211957.564, 359804.681 6211954.767, 359806.329 6211954.847, 359806.456 6211952.179, 359811.29 6211952.411, 359811.162 6211955.079, 359812.81 6211955.158, 359812.212 6211967.641, 359813.211 6211967.689, 359812.622 6211979.962, 359811.374 6211979.903, 359811.201 6211983.499)))       Residence          Residential  Small house with several flats              Residential                 None                None                  None               None              Facade                   Nej             272.68                  1                   False
1  68307637-8623-45e4-8e2c-28d9b4cb5977 2017-05-03 09:42:44+00:00                         0.08                           0.25           Helsingborg               1                2067  Övrig byggnad          Takkant                   NaN                     NaN                   None           342               False                      Övrig byggnad;               NaN              NaN               None            None                                                                                                                                                                                                     MULTIPOLYGON (((358433.031 6214647.045, 358429.47 6214648.869, 358430.588 6214651.053, 358430.382 6214651.159, 358432.046 6214654.41, 358432.215 6214654.323, 358432.397 6214654.677, 358434.013 6214653.85, 358433.817 6214653.466, 358435.834 6214652.433, 358436.253 6214653.251, 358438.406 6214652.148, 358438.21 6214651.764, 358438.402 6214651.666, 358435.999 6214646.972, 358433.617 6214648.19, 358433.031 6214647.045)))  Other building                Other    Other building (unspecified)                    Other                 None                None                  None               None           Roof edge                   Nej              41.06                  2                   False
3  8527579f-be6a-4388-afab-5ad267c407ed 2017-03-30 14:08:07+00:00                         0.03                            NaN           Helsingborg               5                2067  Övrig byggnad            Fasad                   NaN                     NaN                   None           175               False                      Övrig byggnad;               NaN              NaN               None            None                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 MULTIPOLYGON (((357779.253 6215341.401, 357778.283 6215341.829, 357777.835 6215340.825, 357778.82 6215340.421, 357779.253 6215341.401)))  Other building                Other    Other building (unspecified)                    Other                 None                None                  None               None              Facade                   Nej               1.15                  1                   False
```
