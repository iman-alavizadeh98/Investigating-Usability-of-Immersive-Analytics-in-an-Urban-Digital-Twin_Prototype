# Buildings (Byggnad) - Processed

**Type**: GeoDataFrame

## Summary

| Metric | Value |
|--------|-------|
| Rows | 220,239 |
| Columns | 25 |
| Memory | 210.7 MB |
| CRS | EPSG:3006 |
| Duplicate rows | 17 |
| Duplicate object_id | 18,645 |

## Missing Values

| Column | Missing | Missing % |
|--------|---------|-----------|
| `position_uncertainty_plan_m` | 264 | 0.1% |
| `position_uncertainty_height_m` | 76,674 | 34.8% |
| `building_name_primary` | 218,053 | 99.0% |
| `building_name_secondary` | 220,154 | 100.0% |
| `building_name_tertiary` | 220,239 | 100.0% |
| `house_number` | 55 | 0.0% |
| `secondary_purpose` | 216,566 | 98.3% |
| `tertiary_purpose` | 220,226 | 100.0% |
| `quaternary_purpose` | 220,239 | 100.0% |
| `quinary_purpose` | 220,239 | 100.0% |

## Columns

| Name | Type | Non-Null | Null % | Unique | Meaning |
|------|------|----------|--------|--------|---------|
| `object_id` | str | 220,239 | 0.0% | 201594 | Swedish source: objektidentitet |
| `version_valid_from` | datetime64[ms, UTC] | 220,239 | 0.0% | 44253 | Swedish source: versiongiltigfran |
| `position_uncertainty_plan_m` | float64 | 219,975 | 0.1% | 37 | Swedish source: lagesosakerhetplan |
| `position_uncertainty_height_m` | float64 | 143,565 | 34.8% | 13 | Swedish source: lagesosakerhethojd |
| `original_organisation` | str | 220,239 | 0.0% | 20 | Swedish source: ursprunglig_organisation |
| `object_version` | int32 | 220,239 | 0.0% | 16 | Swedish source: objektversion |
| `object_type_number` | int32 | 220,239 | 0.0% | 7 | Swedish source: objekttypnr |
| `object_type` | str | 220,239 | 0.0% | 7 | Swedish source: objekttyp |
| `collection_level` | str | 220,239 | 0.0% | 4 | Swedish source: insamlingslage |
| `building_name_primary` | str | 2,186 | 99.0% | 650 | Swedish source: byggnadsnamn1 |
| `building_name_secondary` | str | 85 | 100.0% | 27 | Swedish source: byggnadsnamn2 |
| `building_name_tertiary` | object | 0 | 100.0% | 0 | Swedish source: byggnadsnamn3 |
| `house_number` | float64 | 220,184 | 0.0% | 548 | Swedish source: husnummer |
| `main_building_flag` | bool | 220,239 | 0.0% | 2 | Main building flag (converted to boolean) |
| `primary_purpose` | str | 220,239 | 0.0% | 32 | Swedish source: andamal1 |
| `secondary_purpose` | str | 3,673 | 98.3% | 22 | Swedish source: andamal2 |
| `tertiary_purpose` | str | 13 | 100.0% | 2 | Swedish source: andamal3 |
| `quaternary_purpose` | object | 0 | 100.0% | 0 | Swedish source: andamal4 |
| `quinary_purpose` | object | 0 | 100.0% | 0 | Swedish source: andamal5 |
| `geometry` | geometry | 220,239 | 0.0% | 220239 |  |
| `object_type_en` | str | 220,239 | 0.0% | 7 | English label for object_type |
| `object_type_category` | str | 220,239 | 0.0% | 7 | High-level category for object_type |
| `primary_purpose_en` | str | 220,239 | 0.0% | 32 | English label for primary_purpose |
| `primary_purpose_category` | str | 220,239 | 0.0% | 1 | High-level category for primary_purpose |
| `collection_level_en` | str | 220,239 | 0.0% | 4 | English label for collection_level |

## Numeric Statistics

| Column | Min | Max | Mean | Std |
|--------|-----|-----|------|-----|
| `position_uncertainty_plan_m` | 0.02 | 50.00 | 0.15 | 0.87 |
| `position_uncertainty_height_m` | 0.03 | 2.50 | 2.46 | 0.28 |
| `house_number` | 1.00 | 1001.00 | 7.61 | 29.92 |

## Categorical Distributions

### object_id

- `144d4f05-add8-4f4a-b7c7-b065106e81ce`: 39
- `666565d3-179a-4ef9-8957-911e4a940c5a`: 38
- `913299b4-ed65-4d84-848a-86ed6fb6475f`: 28
- `9e463a55-b1ed-46fe-a6a5-ecf516557e3f`: 28
- `6fd87879-441c-4545-abd1-f99127e227fc`: 25
- ... and 5 more

### original_organisation

- `Lantmäteriet`: 137601
- `Göteborg`: 46180
- `Mölndal`: 12247
- `Kommunsamverkan`: 10343
- `göteborg`: 3208
- ... and 5 more

### object_type

- `Komplementbyggnad`: 100951
- `Bostad`: 99104
- `Samhällsfunktion`: 6797
- `Industri`: 6738
- `Verksamhet`: 4456
- ... and 2 more

### collection_level

- `Takkant`: 189201
- `Fasad`: 29654
- `Ospecificerad`: 1120
- `Illustrativt läge`: 264

### building_name_primary

- `Chalmers tekniska högskola`: 136
- `Sahlgrenska universitetssjukhuset`: 70
- `Medicinsk högskola`: 51
- `Sahlgrenska universitetssjukhuset Östra`: 28
- `Lindholmens kunskapscentrum`: 26
- ... and 5 more

### building_name_secondary

- `Blå stället`: 23
- `Påvelundsskolan`: 9
- `Barnsjukhuset`: 6
- `Idrottshall Simhall`: 5
- `Kulturskolan`: 5
- ... and 5 more

### building_name_tertiary


### primary_purpose

- `Komplementbyggnad;`: 100951
- `Bostad;Småhus friliggande`: 61320
- `Bostad;Småhus radhus`: 17429
- `Bostad;Flerfamiljshus`: 10578
- `Bostad;Småhus kedjehus`: 8238
- ... and 5 more

### secondary_purpose

- `Verksamhet;`: 3155
- `Bostad;Flerfamiljshus`: 209
- `Samhällsfunktion;Skola`: 102
- `Komplementbyggnad;`: 96
- `Samhällsfunktion;Kulturbyggnad`: 45
- ... and 5 more

### tertiary_purpose

- `Verksamhet;`: 12
- `Samhällsfunktion;Skola`: 1

### quaternary_purpose


### quinary_purpose


### object_type_en

- `Ancillary building`: 100951
- `Residence`: 99104
- `Public facility`: 6797
- `Industrial`: 6738
- `Business`: 4456
- ... and 2 more

### object_type_category

- `Small building attached to dwelling (garage, shed, etc., >15 kvm)`: 100951
- `Building used for residential purposes (single/multi-family, >15 kvm)`: 99104
- `Building for public community services (>15 kvm)`: 6797
- `Building containing manufacturing or processing of products (>15 kvm)`: 6738
- `Building used primarily for business (>50% non-residential, >15 kvm)`: 4456
- ... and 2 more

### primary_purpose_en

- `Komplementbyggnad;`: 100951
- `Bostad;Småhus friliggande`: 61320
- `Bostad;Småhus radhus`: 17429
- `Bostad;Flerfamiljshus`: 10578
- `Bostad;Småhus kedjehus`: 8238
- ... and 5 more

### primary_purpose_category

- `Other`: 220239

### collection_level_en

- `Takkant`: 189201
- `Fasad`: 29654
- `Ospecificerad`: 1120
- `Illustrativt läge`: 264

## Sample Rows

```
                              object_id        version_valid_from  position_uncertainty_plan_m  position_uncertainty_height_m original_organisation  object_version  object_type_number        object_type collection_level building_name_primary building_name_secondary building_name_tertiary  house_number  main_building_flag            primary_purpose secondary_purpose tertiary_purpose quaternary_purpose quinary_purpose                                                                                                                                                                                                                                                                                geometry      object_type_en                                                   object_type_category         primary_purpose_en primary_purpose_category collection_level_en
0  1d79e67a-ae99-4629-909b-fed38c548862 2019-09-10 08:49:41+00:00                        0.025                            0.2            Kungsbacka               2                2061             Bostad            Fasad                   NaN                     NaN                   None           1.0               False  Bostad;Småhus friliggande               NaN              NaN               None            None                                               MULTIPOLYGON (((316758.268 6383772.355, 316751.273 6383772.639, 316751.091 6383768.142, 316743.698 6383768.443, 316743.441 6383762.149, 316750.835 6383761.848, 316750.653 6383757.353, 316757.647 6383757.068, 316758.268 6383772.355)))           Residence  Building used for residential purposes (single/multi-family, >15 kvm)  Bostad;Småhus friliggande                    Other               Fasad
1  34e2d09c-9fac-46a5-863c-d82b77bdfc89 2019-09-16 13:00:44+00:00                        0.100                            0.2            Kungsbacka               2                2061             Bostad            Fasad                   NaN                     NaN                   None           1.0               False  Bostad;Småhus friliggande               NaN              NaN               None            None  MULTIPOLYGON (((317213.951 6383718.834, 317206.992 6383719.682, 317204.259 6383697.231, 317211.216 6383696.37, 317211.385 6383697.76, 317215.217 6383697.293, 317215.072 6383696.102, 317215.498 6383696.05, 317216.564 6383704.805, 317212.306 6383705.324, 317213.951 6383718.834)))           Residence  Building used for residential purposes (single/multi-family, >15 kvm)  Bostad;Småhus friliggande                    Other               Fasad
2  e2f45889-a2c4-4212-af42-ec3ee674e86b 2017-11-09 16:52:58+00:00                        0.200                            0.2            kungsbacka               1                2066  Komplementbyggnad          Takkant                   NaN                     NaN                   None           2.0               False         Komplementbyggnad;               NaN              NaN               None            None                                                                                                                                               MULTIPOLYGON (((316544.422 6383993.634, 316544.221 6383990.118, 316547.814 6383989.914, 316547.974 6383993.394, 316544.422 6383993.634)))  Ancillary building      Small building attached to dwelling (garage, shed, etc., >15 kvm)         Komplementbyggnad;                    Other             Takkant
```
