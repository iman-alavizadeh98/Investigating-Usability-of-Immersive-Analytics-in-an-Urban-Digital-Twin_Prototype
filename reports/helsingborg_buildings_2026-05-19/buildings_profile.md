# Buildings (Byggnad) - Processed

**Type**: GeoDataFrame

## Summary

| Metric | Value |
|--------|-------|
| Rows | 45,891 |
| Columns | 25 |
| Memory | 47.0 MB |
| CRS | EPSG:3006 |
| Duplicate rows | 0 |
| Duplicate object_id | 1,910 |

## Missing Values

| Column | Missing | Missing % |
|--------|---------|-----------|
| `position_uncertainty_height_m` | 8,371 | 18.2% |
| `building_name_primary` | 45,653 | 99.5% |
| `building_name_secondary` | 45,877 | 100.0% |
| `building_name_tertiary` | 45,891 | 100.0% |
| `secondary_purpose` | 45,144 | 98.4% |
| `tertiary_purpose` | 45,890 | 100.0% |
| `quaternary_purpose` | 45,891 | 100.0% |
| `quinary_purpose` | 45,891 | 100.0% |

## Columns

| Name | Type | Non-Null | Null % | Unique | Meaning |
|------|------|----------|--------|--------|---------|
| `object_id` | str | 45,891 | 0.0% | 43981 | Swedish source: objektidentitet |
| `version_valid_from` | datetime64[ms, UTC] | 45,891 | 0.0% | 5521 | Swedish source: versiongiltigfran |
| `position_uncertainty_plan_m` | float64 | 45,891 | 0.0% | 15 | Swedish source: lagesosakerhetplan |
| `position_uncertainty_height_m` | float64 | 37,520 | 18.2% | 13 | Swedish source: lagesosakerhethojd |
| `original_organisation` | str | 45,891 | 0.0% | 3 | Swedish source: ursprunglig_organisation |
| `object_version` | int32 | 45,891 | 0.0% | 10 | Swedish source: objektversion |
| `object_type_number` | int32 | 45,891 | 0.0% | 7 | Swedish source: objekttypnr |
| `object_type` | str | 45,891 | 0.0% | 7 | Swedish source: objekttyp |
| `collection_level` | str | 45,891 | 0.0% | 3 | Swedish source: insamlingslage |
| `building_name_primary` | str | 238 | 99.5% | 131 | Swedish source: byggnadsnamn1 |
| `building_name_secondary` | str | 14 | 100.0% | 12 | Swedish source: byggnadsnamn2 |
| `building_name_tertiary` | object | 0 | 100.0% | 0 | Swedish source: byggnadsnamn3 |
| `house_number` | int32 | 45,891 | 0.0% | 642 | Swedish source: husnummer |
| `main_building_flag` | bool | 45,891 | 0.0% | 2 | Main building flag (converted to boolean) |
| `primary_purpose` | str | 45,891 | 0.0% | 27 | Swedish source: andamal1 |
| `secondary_purpose` | str | 747 | 98.4% | 7 | Swedish source: andamal2 |
| `tertiary_purpose` | str | 1 | 100.0% | 1 | Swedish source: andamal3 |
| `quaternary_purpose` | object | 0 | 100.0% | 0 | Swedish source: andamal4 |
| `quinary_purpose` | object | 0 | 100.0% | 0 | Swedish source: andamal5 |
| `geometry` | geometry | 45,891 | 0.0% | 45891 |  |
| `object_type_en` | str | 45,891 | 0.0% | 7 | English label for object_type |
| `object_type_category` | str | 45,891 | 0.0% | 7 | High-level category for object_type |
| `primary_purpose_en` | str | 45,891 | 0.0% | 27 | English label for primary_purpose |
| `primary_purpose_category` | str | 45,891 | 0.0% | 1 | High-level category for primary_purpose |
| `collection_level_en` | str | 45,891 | 0.0% | 3 | English label for collection_level |

## Numeric Statistics

| Column | Min | Max | Mean | Std |
|--------|-----|-----|------|-----|
| `position_uncertainty_plan_m` | 0.03 | 50.00 | 0.23 | 0.35 |
| `position_uncertainty_height_m` | 0.03 | 10.00 | 3.59 | 2.94 |

## Categorical Distributions

### object_id

- `fe9feffb-995b-41c4-9a83-3e311071d3b6`: 11
- `9ed19160-ac81-4e56-8949-f5617a4c08a5`: 11
- `2c58055a-b0f0-4f4e-bd0e-06d129fbe585`: 8
- `e7e6dda6-451b-49bd-86b9-01a68b86a38a`: 7
- `c45d00de-7065-462d-9635-62e1337f197f`: 6
- ... and 5 more

### original_organisation

- `Lantmäteriet`: 26377
- `Helsingborg`: 19430
- `Kommunsamverkan`: 84

### object_type

- `Komplementbyggnad`: 27246
- `Bostad`: 12803
- `Övrig byggnad`: 2619
- `Industri`: 1330
- `Samhällsfunktion`: 1122
- ... and 2 more

### collection_level

- `Fasad`: 25582
- `Ospecificerad`: 17021
- `Takkant`: 3288

### building_name_primary

- `Rönnowska skolan`: 13
- `Högastensskolan`: 11
- `Fredriksdals friluftsmuseum`: 8
- `Nanny Palmkvistskolan`: 7
- `Ringstorpsskolan`: 7
- ... and 5 more

### building_name_secondary

- `Söderskolan`: 2
- `Filbornaskolan särskola`: 2
- `Filbornakyrkan`: 1
- `Maria gymnasiesärskola`: 1
- `Fyrklöverns montessori`: 1
- ... and 5 more

### building_name_tertiary


### primary_purpose

- `Komplementbyggnad;`: 27246
- `Bostad;Småhus friliggande`: 6632
- `Övrig byggnad;`: 2619
- `Bostad;Flerfamiljshus`: 2463
- `Bostad;Småhus radhus`: 1843
- ... and 5 more

### secondary_purpose

- `Verksamhet;`: 682
- `Bostad;Flerfamiljshus`: 60
- `Bostad;Småhus friliggande`: 1
- `Samhällsfunktion;Sporthall`: 1
- `Samhällsfunktion;Busstation`: 1
- ... and 2 more

### tertiary_purpose

- `Verksamhet;`: 1

### quaternary_purpose


### quinary_purpose


### object_type_en

- `Ancillary building`: 27246
- `Residence`: 12803
- `Other building`: 2619
- `Industrial`: 1330
- `Public facility`: 1122
- ... and 2 more

### object_type_category

- `Small building attached to dwelling (garage, shed, etc., >15 kvm)`: 27246
- `Building used for residential purposes (single/multi-family, >15 kvm)`: 12803
- `Building with other purpose (colonist hut, shelter, tower, etc., >15 kvm)`: 2619
- `Building containing manufacturing or processing of products (>15 kvm)`: 1330
- `Building for public community services (>15 kvm)`: 1122
- ... and 2 more

### primary_purpose_en

- `Komplementbyggnad;`: 27246
- `Bostad;Småhus friliggande`: 6632
- `Övrig byggnad;`: 2619
- `Bostad;Flerfamiljshus`: 2463
- `Bostad;Småhus radhus`: 1843
- ... and 5 more

### primary_purpose_category

- `Other`: 45891

### collection_level_en

- `Fasad`: 25582
- `Ospecificerad`: 17021
- `Takkant`: 3288

## Sample Rows

```
                              object_id        version_valid_from  position_uncertainty_plan_m  position_uncertainty_height_m original_organisation  object_version  object_type_number    object_type collection_level building_name_primary building_name_secondary building_name_tertiary  house_number  main_building_flag                     primary_purpose secondary_purpose tertiary_purpose quaternary_purpose quinary_purpose                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 geometry  object_type_en                                                       object_type_category                  primary_purpose_en primary_purpose_category collection_level_en
0  15fc7757-1cf2-4d88-a12d-8d12ccc0c872 2017-10-18 15:46:07+00:00                         0.15                          10.00           Helsingborg               4                2061         Bostad            Fasad                   NaN                     NaN                   None             3               False  Bostad;Småhus med flera lägenheter               NaN              NaN               None            None  MULTIPOLYGON (((359811.201 6211983.499, 359805.569 6211983.228, 359805.741 6211979.633, 359804.493 6211979.573, 359804.598 6211977.376, 359802.039 6211977.253, 359802.388 6211969.973, 359804.947 6211970.095, 359805.062 6211967.689, 359804.064 6211967.641, 359804.198 6211964.844, 359801.639 6211964.722, 359801.988 6211957.441, 359804.547 6211957.564, 359804.681 6211954.767, 359806.329 6211954.847, 359806.456 6211952.179, 359811.29 6211952.411, 359811.162 6211955.079, 359812.81 6211955.158, 359812.212 6211967.641, 359813.211 6211967.689, 359812.622 6211979.962, 359811.374 6211979.903, 359811.201 6211983.499)))       Residence      Building used for residential purposes (single/multi-family, >15 kvm)  Bostad;Småhus med flera lägenheter                    Other               Fasad
1  68307637-8623-45e4-8e2c-28d9b4cb5977 2017-05-03 09:42:44+00:00                         0.08                           0.25           Helsingborg               1                2067  Övrig byggnad          Takkant                   NaN                     NaN                   None           342               False                      Övrig byggnad;               NaN              NaN               None            None                                                                                                                                                                                                                                                   MULTIPOLYGON (((358432.397 6214654.677, 358432.215 6214654.323, 358432.046 6214654.41, 358430.382 6214651.159, 358433.484 6214649.571, 358432.952 6214648.531, 358435.999 6214646.972, 358438.402 6214651.666, 358438.21 6214651.764, 358438.406 6214652.148, 358436.253 6214653.251, 358435.834 6214652.433, 358433.817 6214653.466, 358434.013 6214653.85, 358432.397 6214654.677)))  Other building  Building with other purpose (colonist hut, shelter, tower, etc., >15 kvm)                      Övrig byggnad;                    Other             Takkant
2  68307637-8623-45e4-8e2c-28d9b4cb5977 2017-05-03 09:42:44+00:00                         0.08                           0.25           Helsingborg               1                2067  Övrig byggnad          Takkant                   NaN                     NaN                   None           342               False                      Övrig byggnad;               NaN              NaN               None            None                                                                                                                                                                                                                                                                                                                                                                                                                                                 MULTIPOLYGON (((358433.617 6214648.191, 358432.952 6214648.531, 358433.484 6214649.571, 358430.588 6214651.054, 358429.47 6214648.869, 358433.031 6214647.045, 358433.617 6214648.191)))  Other building  Building with other purpose (colonist hut, shelter, tower, etc., >15 kvm)                      Övrig byggnad;                    Other             Takkant
```
