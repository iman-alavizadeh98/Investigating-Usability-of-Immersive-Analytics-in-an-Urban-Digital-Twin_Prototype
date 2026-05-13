# Buildings (Byggnad) - Postprocess Snapshot

**Type**: GeoDataFrame

## Summary

| Metric | Value |
|--------|-------|
| Rows | 201,594 |
| Columns | 25 |
| Memory | 194.3 MB |
| CRS | EPSG:3006 |
| Duplicate rows | 0 |
| Duplicate object_id | 0 |

## Missing Values

| Column | Missing | Missing % |
|--------|---------|-----------|
| `position_uncertainty_plan_m` | 264 | 0.1% |
| `position_uncertainty_height_m` | 69,191 | 34.3% |
| `building_name_primary` | 200,467 | 99.4% |
| `building_name_secondary` | 201,562 | 100.0% |
| `building_name_tertiary` | 201,594 | 100.0% |
| `house_number` | 53 | 0.0% |
| `secondary_purpose` | 198,411 | 98.4% |
| `tertiary_purpose` | 201,582 | 100.0% |
| `quaternary_purpose` | 201,594 | 100.0% |
| `quinary_purpose` | 201,594 | 100.0% |

## Columns

| Name | Type | Non-Null | Null % | Unique | Meaning |
|------|------|----------|--------|--------|---------|
| `object_id` | str | 201,594 | 0.0% | 201594 | Swedish source: objektidentitet |
| `version_valid_from` | datetime64[ms, UTC] | 201,594 | 0.0% | 44253 | Swedish source: versiongiltigfran |
| `position_uncertainty_plan_m` | float64 | 201,330 | 0.1% | 37 | Swedish source: lagesosakerhetplan |
| `position_uncertainty_height_m` | float64 | 132,403 | 34.3% | 13 | Swedish source: lagesosakerhethojd |
| `original_organisation` | str | 201,594 | 0.0% | 20 | Swedish source: ursprunglig_organisation |
| `object_version` | int32 | 201,594 | 0.0% | 16 | Swedish source: objektversion |
| `object_type_number` | int32 | 201,594 | 0.0% | 7 | Swedish source: objekttypnr |
| `object_type` | str | 201,594 | 0.0% | 7 | Swedish source: objekttyp |
| `collection_level` | str | 201,594 | 0.0% | 4 | Swedish source: insamlingslage |
| `building_name_primary` | str | 1,127 | 99.4% | 650 | Swedish source: byggnadsnamn1 |
| `building_name_secondary` | str | 32 | 100.0% | 27 | Swedish source: byggnadsnamn2 |
| `building_name_tertiary` | object | 0 | 100.0% | 0 | Swedish source: byggnadsnamn3 |
| `house_number` | float64 | 201,541 | 0.0% | 548 | Swedish source: husnummer |
| `main_building_flag` | bool | 201,594 | 0.0% | 2 | Main building flag (converted to boolean) |
| `primary_purpose` | str | 201,594 | 0.0% | 32 | Swedish source: andamal1 |
| `secondary_purpose` | str | 3,183 | 98.4% | 22 | Swedish source: andamal2 |
| `tertiary_purpose` | str | 12 | 100.0% | 2 | Swedish source: andamal3 |
| `quaternary_purpose` | object | 0 | 100.0% | 0 | Swedish source: andamal4 |
| `quinary_purpose` | object | 0 | 100.0% | 0 | Swedish source: andamal5 |
| `geometry` | geometry | 201,594 | 0.0% | 201594 |  |
| `object_type_en` | str | 201,594 | 0.0% | 7 | English label for object_type |
| `object_type_category` | str | 201,594 | 0.0% | 7 | High-level category for object_type |
| `primary_purpose_en` | str | 201,594 | 0.0% | 32 | English label for primary_purpose |
| `primary_purpose_category` | str | 201,594 | 0.0% | 1 | High-level category for primary_purpose |
| `collection_level_en` | str | 201,594 | 0.0% | 4 | English label for collection_level |

## Numeric Statistics

| Column | Min | Max | Mean | Std |
|--------|-----|-----|------|-----|
| `position_uncertainty_plan_m` | 0.02 | 50.00 | 0.16 | 0.89 |
| `position_uncertainty_height_m` | 0.03 | 2.50 | 2.46 | 0.29 |
| `house_number` | 1.00 | 1001.00 | 7.77 | 30.59 |

## Categorical Distributions

### object_id

- `00006008-42e3-44b2-b828-046c79421aee`: 1
- `0000710d-12fe-4f19-a030-1b344ce68878`: 1
- `0000dc54-c0ce-49a3-9826-b438c9313a60`: 1
- `000121d5-7764-4f4a-8cb3-9fbb262a6617`: 1
- `00012773-30d5-4900-b090-d0e2852ed2fe`: 1
- ... and 5 more

### original_organisation

- `Lantmäteriet`: 130449
- `Göteborg`: 41449
- `Mölndal`: 12165
- `Kommunsamverkan`: 5091
- `Partille`: 3008
- ... and 5 more

### object_type

- `Komplementbyggnad`: 96733
- `Bostad`: 90956
- `Samhällsfunktion`: 4964
- `Industri`: 3727
- `Verksamhet`: 3148
- ... and 2 more

### collection_level

- `Takkant`: 172539
- `Fasad`: 27773
- `Ospecificerad`: 1018
- `Illustrativt läge`: 264

### building_name_primary

- `Chalmers tekniska högskola`: 34
- `Sahlgrenska universitetssjukhuset`: 17
- `Högsbo sjukhus`: 14
- `Göteborgs universitet`: 12
- `Campus Linné`: 12
- ... and 5 more

### building_name_secondary

- `Fagereds yrkesskola`: 5
- `Påvelundsskolan`: 2
- `Sankt Markus kapell`: 1
- `Partille teknikcentrum`: 1
- `Idrottshall Simhall`: 1
- ... and 5 more

### building_name_tertiary


### primary_purpose

- `Komplementbyggnad;`: 96733
- `Bostad;Småhus friliggande`: 55577
- `Bostad;Småhus radhus`: 16606
- `Bostad;Flerfamiljshus`: 9610
- `Bostad;Småhus kedjehus`: 7710
- ... and 5 more

### secondary_purpose

- `Verksamhet;`: 2853
- `Bostad;Flerfamiljshus`: 155
- `Samhällsfunktion;Skola`: 76
- `Komplementbyggnad;`: 45
- `Bostad;Småhus friliggande`: 15
- ... and 5 more

### tertiary_purpose

- `Verksamhet;`: 11
- `Samhällsfunktion;Skola`: 1

### quaternary_purpose


### quinary_purpose


### object_type_en

- `Ancillary building`: 96733
- `Residence`: 90956
- `Public facility`: 4964
- `Industrial`: 3727
- `Business`: 3148
- ... and 2 more

### object_type_category

- `Small building attached to dwelling (garage, shed, etc., >15 kvm)`: 96733
- `Building used for residential purposes (single/multi-family, >15 kvm)`: 90956
- `Building for public community services (>15 kvm)`: 4964
- `Building containing manufacturing or processing of products (>15 kvm)`: 3727
- `Building used primarily for business (>50% non-residential, >15 kvm)`: 3148
- ... and 2 more

### primary_purpose_en

- `Komplementbyggnad;`: 96733
- `Bostad;Småhus friliggande`: 55577
- `Bostad;Småhus radhus`: 16606
- `Bostad;Flerfamiljshus`: 9610
- `Bostad;Småhus kedjehus`: 7710
- ... and 5 more

### primary_purpose_category

- `Other`: 201594

### collection_level_en

- `Takkant`: 172539
- `Fasad`: 27773
- `Ospecificerad`: 1018
- `Illustrativt läge`: 264

## Sample Rows

```
                                   object_id        version_valid_from  position_uncertainty_plan_m  position_uncertainty_height_m original_organisation  object_version  object_type_number        object_type collection_level building_name_primary building_name_secondary building_name_tertiary  house_number  main_building_flag                 primary_purpose secondary_purpose tertiary_purpose quaternary_purpose quinary_purpose                                                                                                                                   geometry      object_type_en                                               object_type_category              primary_purpose_en primary_purpose_category collection_level_en
79113   00006008-42e3-44b2-b828-046c79421aee 2011-03-23 08:54:49+00:00                         0.06                            2.5          Lantmäteriet               1                2066  Komplementbyggnad          Takkant                   NaN                     NaN                   None           3.0               False              Komplementbyggnad;               NaN              NaN               None            None     MULTIPOLYGON (((313557.031 6396532.82, 313559.148 6396527.205, 313561.25 6396528.002, 313559.132 6396533.611, 313557.031 6396532.82)))  Ancillary building  Small building attached to dwelling (garage, shed, etc., >15 kvm)              Komplementbyggnad;                    Other             Takkant
128666  0000710d-12fe-4f19-a030-1b344ce68878 2014-09-11 10:27:57+00:00                         0.06                            NaN              Göteborg               2                2066  Komplementbyggnad          Takkant                   NaN                     NaN                   None           2.0               False              Komplementbyggnad;               NaN              NaN               None            None  MULTIPOLYGON (((313838.039 6394181.542, 313834.914 6394181.989, 313834.124 6394176.468, 313837.249 6394176.021, 313838.039 6394181.542)))  Ancillary building  Small building attached to dwelling (garage, shed, etc., >15 kvm)              Komplementbyggnad;                    Other             Takkant
71101   0000dc54-c0ce-49a3-9826-b438c9313a60 2022-09-09 14:36:13+00:00                         0.10                            NaN              Göteborg               2                2063   Samhällsfunktion          Takkant                   NaN                     NaN                   None         322.0               False  Samhällsfunktion;Ospecificerad               NaN              NaN               None            None  MULTIPOLYGON (((306831.263 6404912.336, 306835.625 6404911.406, 306836.691 6404916.065, 306831.872 6404916.766, 306831.263 6404912.336)))     Public facility                   Building for public community services (>15 kvm)  Samhällsfunktion;Ospecificerad                    Other             Takkant
```
