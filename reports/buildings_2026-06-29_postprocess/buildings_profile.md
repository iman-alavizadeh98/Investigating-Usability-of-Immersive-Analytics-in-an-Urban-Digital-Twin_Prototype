# Buildings (Byggnad) - Postprocess Snapshot

**Type**: GeoDataFrame

## Summary

| Metric | Value |
|--------|-------|
| Rows | 2,762 |
| Columns | 25 |
| Memory | 2.8 MB |
| CRS | EPSG:3006 |
| Duplicate rows | 0 |
| Duplicate object_id | 0 |

## Missing Values

| Column | Missing | Missing % |
|--------|---------|-----------|
| `position_uncertainty_height_m` | 218 | 7.9% |
| `building_name_primary` | 2,760 | 99.9% |
| `building_name_secondary` | 2,762 | 100.0% |
| `building_name_tertiary` | 2,762 | 100.0% |
| `secondary_purpose` | 2,761 | 100.0% |
| `tertiary_purpose` | 2,762 | 100.0% |
| `quaternary_purpose` | 2,762 | 100.0% |
| `quinary_purpose` | 2,762 | 100.0% |

## Columns

| Name | Type | Non-Null | Null % | Unique | Meaning |
|------|------|----------|--------|--------|---------|
| `object_id` | str | 2,762 | 0.0% | 2762 | Swedish source: objektidentitet |
| `version_valid_from` | datetime64[ms, UTC] | 2,762 | 0.0% | 742 | Swedish source: versiongiltigfran |
| `position_uncertainty_plan_m` | float64 | 2,762 | 0.0% | 16 | Swedish source: lagesosakerhetplan |
| `position_uncertainty_height_m` | float64 | 2,544 | 7.9% | 7 | Swedish source: lagesosakerhethojd |
| `original_organisation` | str | 2,762 | 0.0% | 4 | Swedish source: ursprunglig_organisation |
| `object_version` | int32 | 2,762 | 0.0% | 7 | Swedish source: objektversion |
| `object_type_number` | int32 | 2,762 | 0.0% | 7 | Swedish source: objekttypnr |
| `object_type` | str | 2,762 | 0.0% | 7 | Swedish source: objekttyp |
| `collection_level` | str | 2,762 | 0.0% | 3 | Swedish source: insamlingslage |
| `building_name_primary` | str | 2 | 99.9% | 2 | Swedish source: byggnadsnamn1 |
| `building_name_secondary` | object | 0 | 100.0% | 0 | Swedish source: byggnadsnamn2 |
| `building_name_tertiary` | object | 0 | 100.0% | 0 | Swedish source: byggnadsnamn3 |
| `house_number` | int32 | 2,762 | 0.0% | 85 | Swedish source: husnummer |
| `main_building_flag` | bool | 2,762 | 0.0% | 2 | Main building flag (converted to boolean) |
| `primary_purpose` | str | 2,762 | 0.0% | 14 | Swedish source: andamal1 |
| `secondary_purpose` | str | 1 | 100.0% | 1 | Swedish source: andamal2 |
| `tertiary_purpose` | object | 0 | 100.0% | 0 | Swedish source: andamal3 |
| `quaternary_purpose` | object | 0 | 100.0% | 0 | Swedish source: andamal4 |
| `quinary_purpose` | object | 0 | 100.0% | 0 | Swedish source: andamal5 |
| `geometry` | geometry | 2,762 | 0.0% | 2762 |  |
| `object_type_en` | str | 2,762 | 0.0% | 7 | English label for object_type |
| `object_type_category` | str | 2,762 | 0.0% | 7 | High-level category for object_type |
| `primary_purpose_en` | str | 2,762 | 0.0% | 14 | English label for primary_purpose |
| `primary_purpose_category` | str | 2,762 | 0.0% | 1 | High-level category for primary_purpose |
| `collection_level_en` | str | 2,762 | 0.0% | 3 | English label for collection_level |

## Numeric Statistics

| Column | Min | Max | Mean | Std |
|--------|-----|-----|------|-----|
| `position_uncertainty_plan_m` | 0.03 | 50.00 | 0.19 | 1.38 |
| `position_uncertainty_height_m` | 0.03 | 10.00 | 3.13 | 2.15 |

## Categorical Distributions

### object_id

- `001876a1-9969-4aba-89c6-cf86713a3226`: 1
- `0023c9e0-a36d-412b-a59f-20052d0472fa`: 1
- `003700a3-fe6a-418c-89c2-4dac4bdfcb1a`: 1
- `0053e9c3-18ea-4178-8ba7-515ca9fd23c0`: 1
- `00732301-9c86-4d6a-a945-a9ef98c48d02`: 1
- ... and 5 more

### original_organisation

- `Lantmäteriet`: 2208
- `Helsingborg`: 537
- `Landskrona`: 16
- `Kommunsamverkan`: 1

### object_type

- `Komplementbyggnad`: 1763
- `Bostad`: 953
- `Samhällsfunktion`: 31
- `Övrig byggnad`: 9
- `Verksamhet`: 3
- ... and 2 more

### collection_level

- `Fasad`: 2108
- `Ospecificerad`: 618
- `Takkant`: 36

### building_name_primary

- `Rydebäcks kyrka`: 1
- `Karmeliterklostret`: 1

### building_name_secondary


### building_name_tertiary


### primary_purpose

- `Komplementbyggnad;`: 1763
- `Bostad;Småhus friliggande`: 493
- `Bostad;Småhus kedjehus`: 228
- `Bostad;Småhus radhus`: 160
- `Bostad;Småhus med flera lägenheter`: 53
- ... and 5 more

### secondary_purpose

- `Verksamhet;`: 1

### tertiary_purpose


### quaternary_purpose


### quinary_purpose


### object_type_en

- `Ancillary building`: 1763
- `Residence`: 953
- `Public facility`: 31
- `Other building`: 9
- `Business`: 3
- ... and 2 more

### object_type_category

- `Small building attached to dwelling (garage, shed, etc., >15 kvm)`: 1763
- `Building used for residential purposes (single/multi-family, >15 kvm)`: 953
- `Building for public community services (>15 kvm)`: 31
- `Building with other purpose (colonist hut, shelter, tower, etc., >15 kvm)`: 9
- `Building used primarily for business (>50% non-residential, >15 kvm)`: 3
- ... and 2 more

### primary_purpose_en

- `Komplementbyggnad;`: 1763
- `Bostad;Småhus friliggande`: 493
- `Bostad;Småhus kedjehus`: 228
- `Bostad;Småhus radhus`: 160
- `Bostad;Småhus med flera lägenheter`: 53
- ... and 5 more

### primary_purpose_category

- `Other`: 2762

### collection_level_en

- `Fasad`: 2108
- `Ospecificerad`: 618
- `Takkant`: 36

## Sample Rows

```
                                 object_id               version_valid_from  position_uncertainty_plan_m  position_uncertainty_height_m original_organisation  object_version  object_type_number        object_type collection_level building_name_primary building_name_secondary building_name_tertiary  house_number  main_building_flag            primary_purpose secondary_purpose tertiary_purpose quaternary_purpose quinary_purpose                                                                                                                                                                                  geometry      object_type_en                                                   object_type_category         primary_purpose_en primary_purpose_category collection_level_en
2357  001876a1-9969-4aba-89c6-cf86713a3226 2017-07-24 13:51:41.718000+00:00                         0.15                          10.00           Helsingborg               2                2061             Bostad            Fasad                   NaN                    None                   None             1               False  Bostad;Småhus friliggande               NaN             None               None            None                                                 MULTIPOLYGON (((360428.946 6203951.526, 360428.971 6203958.364, 360415.034 6203958.413, 360415.009 6203951.575, 360428.946 6203951.526)))           Residence  Building used for residential purposes (single/multi-family, >15 kvm)  Bostad;Småhus friliggande                    Other               Fasad
1249  0023c9e0-a36d-412b-a59f-20052d0472fa 2011-03-21 14:55:57.753000+00:00                         0.03                           2.50          Lantmäteriet               1                2066  Komplementbyggnad            Fasad                   NaN                    None                   None             2               False         Komplementbyggnad;               NaN             None               None            None  MULTIPOLYGON (((360375.177 6203764.226, 360381.311 6203766.66, 360381.146 6203767.077, 360379.829 6203770.398, 360372.578 6203767.528, 360374.062 6203763.784, 360375.177 6203764.226)))  Ancillary building      Small building attached to dwelling (garage, shed, etc., >15 kvm)         Komplementbyggnad;                    Other               Fasad
406   003700a3-fe6a-418c-89c2-4dac4bdfcb1a 2021-11-08 11:01:14.642000+00:00                         0.04                           0.05           Helsingborg               1                2066  Komplementbyggnad            Fasad                   NaN                    None                   None             3               False         Komplementbyggnad;               NaN             None               None            None                                                  MULTIPOLYGON (((361496.218 6204129.786, 361496.016 6204138.645, 361491.056 6204138.523, 361491.262 6204129.66, 361496.218 6204129.786)))  Ancillary building      Small building attached to dwelling (garage, shed, etc., >15 kvm)         Komplementbyggnad;                    Other               Fasad
```
