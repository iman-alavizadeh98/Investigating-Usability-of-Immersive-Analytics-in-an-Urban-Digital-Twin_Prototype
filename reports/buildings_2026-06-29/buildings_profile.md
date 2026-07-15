# Buildings (Byggnad) - Processed

**Type**: GeoDataFrame

## Summary

| Metric | Value |
|--------|-------|
| Rows | 2,861 |
| Columns | 25 |
| Memory | 2.9 MB |
| CRS | EPSG:3006 |
| Duplicate rows | 0 |
| Duplicate object_id | 99 |

## Missing Values

| Column | Missing | Missing % |
|--------|---------|-----------|
| `position_uncertainty_height_m` | 222 | 7.8% |
| `building_name_primary` | 2,859 | 99.9% |
| `building_name_secondary` | 2,861 | 100.0% |
| `building_name_tertiary` | 2,861 | 100.0% |
| `secondary_purpose` | 2,860 | 100.0% |
| `tertiary_purpose` | 2,861 | 100.0% |
| `quaternary_purpose` | 2,861 | 100.0% |
| `quinary_purpose` | 2,861 | 100.0% |

## Columns

| Name | Type | Non-Null | Null % | Unique | Meaning |
|------|------|----------|--------|--------|---------|
| `object_id` | str | 2,861 | 0.0% | 2762 | Swedish source: objektidentitet |
| `version_valid_from` | datetime64[ms, UTC] | 2,861 | 0.0% | 742 | Swedish source: versiongiltigfran |
| `position_uncertainty_plan_m` | float64 | 2,861 | 0.0% | 16 | Swedish source: lagesosakerhetplan |
| `position_uncertainty_height_m` | float64 | 2,639 | 7.8% | 7 | Swedish source: lagesosakerhethojd |
| `original_organisation` | str | 2,861 | 0.0% | 4 | Swedish source: ursprunglig_organisation |
| `object_version` | int32 | 2,861 | 0.0% | 7 | Swedish source: objektversion |
| `object_type_number` | int32 | 2,861 | 0.0% | 7 | Swedish source: objekttypnr |
| `object_type` | str | 2,861 | 0.0% | 7 | Swedish source: objekttyp |
| `collection_level` | str | 2,861 | 0.0% | 3 | Swedish source: insamlingslage |
| `building_name_primary` | str | 2 | 99.9% | 2 | Swedish source: byggnadsnamn1 |
| `building_name_secondary` | object | 0 | 100.0% | 0 | Swedish source: byggnadsnamn2 |
| `building_name_tertiary` | object | 0 | 100.0% | 0 | Swedish source: byggnadsnamn3 |
| `house_number` | int32 | 2,861 | 0.0% | 85 | Swedish source: husnummer |
| `main_building_flag` | bool | 2,861 | 0.0% | 2 | Main building flag (converted to boolean) |
| `primary_purpose` | str | 2,861 | 0.0% | 14 | Swedish source: andamal1 |
| `secondary_purpose` | str | 1 | 100.0% | 1 | Swedish source: andamal2 |
| `tertiary_purpose` | object | 0 | 100.0% | 0 | Swedish source: andamal3 |
| `quaternary_purpose` | object | 0 | 100.0% | 0 | Swedish source: andamal4 |
| `quinary_purpose` | object | 0 | 100.0% | 0 | Swedish source: andamal5 |
| `geometry` | geometry | 2,861 | 0.0% | 2861 |  |
| `object_type_en` | str | 2,861 | 0.0% | 7 | English label for object_type |
| `object_type_category` | str | 2,861 | 0.0% | 7 | High-level category for object_type |
| `primary_purpose_en` | str | 2,861 | 0.0% | 14 | English label for primary_purpose |
| `primary_purpose_category` | str | 2,861 | 0.0% | 1 | High-level category for primary_purpose |
| `collection_level_en` | str | 2,861 | 0.0% | 3 | English label for collection_level |

## Numeric Statistics

| Column | Min | Max | Mean | Std |
|--------|-----|-----|------|-----|
| `position_uncertainty_plan_m` | 0.03 | 50.00 | 0.19 | 1.35 |
| `position_uncertainty_height_m` | 0.03 | 10.00 | 3.14 | 2.16 |

## Categorical Distributions

### object_id

- `554d2d5f-0d6a-48e2-9e79-2e6b1353792e`: 3
- `f8fca013-f116-40ba-a576-f237ae8d8ec9`: 3
- `4e2f9b6a-6189-4e9a-ae69-fd4a1a13b98e`: 3
- `0cd324c1-13e0-4432-b3be-2ab21cbd71a1`: 2
- `9b542944-d9bc-434e-bb66-a1f113a30e4a`: 2
- ... and 5 more

### original_organisation

- `Lantmäteriet`: 2279
- `Helsingborg`: 565
- `Landskrona`: 16
- `Kommunsamverkan`: 1

### object_type

- `Komplementbyggnad`: 1842
- `Bostad`: 972
- `Samhällsfunktion`: 31
- `Övrig byggnad`: 9
- `Verksamhet`: 3
- ... and 2 more

### collection_level

- `Fasad`: 2180
- `Ospecificerad`: 645
- `Takkant`: 36

### building_name_primary

- `Karmeliterklostret`: 1
- `Rydebäcks kyrka`: 1

### building_name_secondary


### building_name_tertiary


### primary_purpose

- `Komplementbyggnad;`: 1842
- `Bostad;Småhus friliggande`: 499
- `Bostad;Småhus kedjehus`: 237
- `Bostad;Småhus radhus`: 163
- `Bostad;Småhus med flera lägenheter`: 53
- ... and 5 more

### secondary_purpose

- `Verksamhet;`: 1

### tertiary_purpose


### quaternary_purpose


### quinary_purpose


### object_type_en

- `Ancillary building`: 1842
- `Residence`: 972
- `Public facility`: 31
- `Other building`: 9
- `Business`: 3
- ... and 2 more

### object_type_category

- `Small building attached to dwelling (garage, shed, etc., >15 kvm)`: 1842
- `Building used for residential purposes (single/multi-family, >15 kvm)`: 972
- `Building for public community services (>15 kvm)`: 31
- `Building with other purpose (colonist hut, shelter, tower, etc., >15 kvm)`: 9
- `Building used primarily for business (>50% non-residential, >15 kvm)`: 3
- ... and 2 more

### primary_purpose_en

- `Komplementbyggnad;`: 1842
- `Bostad;Småhus friliggande`: 499
- `Bostad;Småhus kedjehus`: 237
- `Bostad;Småhus radhus`: 163
- `Bostad;Småhus med flera lägenheter`: 53
- ... and 5 more

### primary_purpose_category

- `Other`: 2861

### collection_level_en

- `Fasad`: 2180
- `Ospecificerad`: 645
- `Takkant`: 36

## Sample Rows

```
                              object_id               version_valid_from  position_uncertainty_plan_m  position_uncertainty_height_m original_organisation  object_version  object_type_number object_type collection_level building_name_primary building_name_secondary building_name_tertiary  house_number  main_building_flag            primary_purpose secondary_purpose tertiary_purpose quaternary_purpose quinary_purpose                                                                                                                                                                                                                                 geometry object_type_en                                                   object_type_category         primary_purpose_en primary_purpose_category collection_level_en
0  6aa3e8d2-6af9-4725-bff0-0056194bdfde 2011-03-24 10:45:38.830000+00:00                        0.800                            2.5          Lantmäteriet               1                2061      Bostad    Ospecificerad                   NaN                    None                   None             1               False  Bostad;Småhus friliggande               NaN             None               None            None                                                 MULTIPOLYGON (((360080.263 6203559.017, 360088.806 6203559.882, 360088.147 6203569.022, 360080.429 6203568.454, 360080.694 6203564.426, 360079.73 6203564.301, 360080.263 6203559.017)))      Residence  Building used for residential purposes (single/multi-family, >15 kvm)  Bostad;Småhus friliggande                    Other       Ospecificerad
1  71777d4b-1c56-460d-84cf-1996e7aa9529 2013-06-13 12:32:00.131000+00:00                        0.025                            2.5          Lantmäteriet               3                2064  Verksamhet            Fasad                   NaN                    None                   None             1               False                Verksamhet;               NaN             None               None            None  MULTIPOLYGON (((360526.791 6203641.901, 360526.792 6203636.361, 360546.633 6203636.361, 360546.644 6203641.919, 360541.229 6203641.916, 360541.276 6203644.265, 360535.72 6203644.278, 360535.74 6203641.893, 360526.791 6203641.901)))       Business   Building used primarily for business (>50% non-residential, >15 kvm)                Verksamhet;                    Other               Fasad
2  d71e058f-2ff3-46bd-a964-266843223396 2026-02-27 08:40:33.131000+00:00                        0.150                            NaN            Landskrona               5                2061      Bostad          Takkant                   NaN                    None                   None             1               False  Bostad;Småhus friliggande               NaN             None               None            None                                                                                                                   POLYGON ((360090.789 6203500, 360090.566 6203504.535, 360098.115 6203504.964, 360098.372 6203500, 360090.789 6203500))      Residence  Building used for residential purposes (single/multi-family, >15 kvm)  Bostad;Småhus friliggande                    Other             Takkant
```
