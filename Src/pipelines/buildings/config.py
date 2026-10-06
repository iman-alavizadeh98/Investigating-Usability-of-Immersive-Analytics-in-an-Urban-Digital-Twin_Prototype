"""
Buildings dataset configuration and translations.

Contains:
- Field name translations (Swedish → English)
- Building type classifications
- Purpose/usage category mappings
- Dataset metadata
"""

# === FIELD NAME TRANSLATIONS (Swedish → English) ===
# Based on Lantmäteriet PRODUKTBESKRIVNING: Byggnad Nedladdning, vektor (v1.6)

FIELD_TRANSLATIONS = {
    # Core identifiers
    "objektidentitet": "object_id",
    "versiongiltigfran": "version_valid_from",
    "objektversion": "object_version",
    "objekttypnr": "object_type_number",
    "ursprunglig_organisation": "original_organisation",
    
    # Position and accuracy
    "lagesosakerhetplan": "position_uncertainty_plan_m",
    "lagesosakerhethojd": "position_uncertainty_height_m",
    "insamlingslage": "collection_level",
    
    # Classification
    "objekttyp": "object_type",
    "huvudbyggnad": "main_building_flag",
    "husnummer": "house_number",
    
    # Names
    "byggnadsnamn1": "building_name_primary",
    "byggnadsnamn2": "building_name_secondary",
    "byggnadsnamn3": "building_name_tertiary",
    
    # Usage/Purpose (andamål)
    "andamal1": "primary_purpose",
    "andamal2": "secondary_purpose",
    "andamal3": "tertiary_purpose",
    "andamal4": "quaternary_purpose",
    "andamal5": "quinary_purpose",
}

# === BUILDING OBJECT TYPES ===
# Values for objekttyp field (Table 4 in PDF).
# "category" is a short English class for grouping/colouring in the runtime;
# "description" is the product-description definition (kept for metadata only).

BUILDING_TYPES = {
    "Bostad": {
        "en": "Residence",
        "category": "Residential",
        "description": "Building predominantly used for permanent or holiday housing",
        "object_type_nr": 2061
    },
    "Industri": {
        "en": "Industrial",
        "category": "Industrial",
        "description": "Building predominantly used for manufacturing products and processing raw materials",
        "object_type_nr": 2062
    },
    "Samhällsfunktion": {
        "en": "Public facility",
        "category": "Public",
        "description": "Building predominantly housing activities used by citizens in community life",
        "object_type_nr": 2063
    },
    "Verksamhet": {
        "en": "Business",
        "category": "Business",
        "description": "Building predominantly used for business: >50% non-residential, e.g. hotel, office, retail, restaurant, car park",
        "object_type_nr": 2064
    },
    "Ekonomibyggnad": {
        "en": "Farm building",
        "category": "Agricultural",
        "description": "Building predominantly for agriculture, forestry or comparable trades",
        "object_type_nr": 2065
    },
    "Komplementbyggnad": {
        "en": "Ancillary building",
        "category": "Ancillary",
        "description": "Building belonging to a small house, e.g. outbuilding, garage, carport, cistern, storage, boathouse; includes structures without walls",
        "object_type_nr": 2066
    },
    "Övrig byggnad": {
        "en": "Other building",
        "category": "Other",
        "description": "Building of none of the other types, e.g. allotment cottage, wind shelter, Sami hut (kåta), tower, windmill, bell tower, lighthouse, permanent free-standing canopy",
        "object_type_nr": 2067
    },
}

# === PURPOSE (ANDAMÅL1-5) VALUES ===
# Table 6 in PDF. The delivered values have the form "<objekttyp>;<ändamål>",
# e.g. "Samhällsfunktion;Sjukhus", so purposes are keyed by object type first and
# then by the part after ";". The same subtype ("Ospecificerad") exists under
# several object types, so a flat subtype key is ambiguous.
# An empty subtype ("Verksamhet;") is the PDF's "-": purpose not specified.
# Unknown values are logged and reported by the pipeline, never guessed.

PURPOSE_SEPARATOR = ";"

PURPOSES = {
    "Bostad": {
        "Småhus friliggande": {"en": "Detached house"},
        "Småhus kedjehus": {"en": "Linked house"},
        "Småhus radhus": {"en": "Terraced house"},
        "Småhus med flera lägenheter": {"en": "Small house with several flats"},
        "Flerfamiljshus": {"en": "Apartment building"},
        "Ospecificerad": {"en": "Residence (unspecified)"},
    },
    "Industri": {
        "Annan tillverkningsindustri": {"en": "Other manufacturing industry"},
        "Industrihotell": {"en": "Multi-tenant industrial building"},
        "Metall- eller maskinindustri": {"en": "Metal or machinery industry"},
        "Textilindustri": {"en": "Textile industry"},
        "Trävaruindustri": {"en": "Wood products industry"},
        "Övrig industribyggnad": {"en": "Other industrial building (non-manufacturing)"},
        "Ospecificerad": {"en": "Industrial (unspecified)"},
        # Not in Table 6 of v1.6, but present in the Gothenburg, Helsingborg and
        # Fortuna deliveries (2025). Translated literally; flagged in metadata.
        "Tillverkning": {"en": "Manufacturing", "not_in_spec": True},
    },
    "Samhällsfunktion": {
        "Badhus": {"en": "Public bath"},
        "Brandstation": {"en": "Fire station"},
        "Busstation": {"en": "Bus station"},
        "Djursjukhus": {"en": "Animal hospital"},
        "Högskola": {"en": "University college"},
        "Ishall": {"en": "Ice rink"},
        "Järnvägsstation": {"en": "Railway station"},
        "Kommunhus": {"en": "Town hall"},
        "Kriminalvårdsanstalt": {"en": "Prison"},
        "Kulturbyggnad": {"en": "Cultural building"},
        "Multiarena": {"en": "Multi-purpose arena"},
        "Polisstation": {"en": "Police station"},
        "Ridhus": {"en": "Riding hall"},
        "Samfund": {"en": "Religious building"},
        "Sjukhus": {"en": "Hospital"},
        "Skola": {"en": "School"},
        "Sporthall": {"en": "Sports hall"},
        "Universitet": {"en": "University"},
        "Vårdcentral": {"en": "Health centre"},
        "Ospecificerad": {"en": "Public facility (unspecified)"},
    },
    "Verksamhet": {"": {"en": "Business (unspecified)"}},
    "Ekonomibyggnad": {"": {"en": "Farm building (unspecified)"}},
    "Komplementbyggnad": {"": {"en": "Ancillary building (unspecified)"}},
    "Övrig byggnad": {"": {"en": "Other building (unspecified)"}},
}

# Purpose columns after field renaming, in andamal1..5 order.
PURPOSE_FIELDS = [
    "primary_purpose",
    "secondary_purpose",
    "tertiary_purpose",
    "quaternary_purpose",
    "quinary_purpose",
]

# === MAIN BUILDING FLAG (HUVUDBYGGNAD) ===
MAIN_BUILDING_VALUES = {"Ja": True, "Nej": False}

# === COLLECTION LEVEL (INSAMLINGSLAGE) ===
# Table 7 in PDF - how building location was determined.
# Keys are lowercase as in the PDF; the delivered data is capitalised
# ("Fasad", "Takkant"), so lookups are case-insensitive.

COLLECTION_LEVELS = {
    "fasad": {
        "en": "Facade",
        "description": "Building perimeter measured from facade within roof edge"
    },
    "takkant": {
        "en": "Roof edge",
        "description": "Building boundary measured at roof edge line"
    },
    "illustrativt läge": {
        "en": "Schematic/illustrative",
        "description": "Building shown schematically, not surveyed (may be under road/structure)"
    },
    "ospecificerad": {
        "en": "Unspecified",
        "description": "Collection level not specified"
    },
}

# === DATASET METADATA ===

DATASET_METADATA = {
    "name_sv": "Byggnad Nedladdning, vektor",
    "name_en": "Buildings download, vector",
    "authority": "Lantmäteriet (Swedish Land Survey)",
    "product_description_version": "1.6",
    "product_description_date": "2023-02-01",  # date of the PDF, not of the data
    "layer": "byggnad",
    "coordinate_system_plan": "SWEREF 99 TM",
    "coordinate_system_height": "RH 2000",
    "geographic_coverage": "Sweden (nationwide)",
    # Buildings larger than 15 m² must be included; smaller ones MAY be
    # (Table 4). It is not a minimum: Helsingborg has 12,657 buildings < 15 m².
    "mandatory_above_m2": 15.0,
    "description": "Vector dataset of building footprints with semantic attributes including type, usage, and positional accuracy",
    "update_frequency": "Municipal responsibility areas: delivered to Lantmäteriet at least twice a year; "
                        "outside them: periodic, following the aerial imagery programme. "
                        "Register attributes are updated continuously by municipalities.",
    "data_quality": {
        "completeness": "Spot checks in municipal areas: ~4% deviation (missing or surplus buildings) nationally",
        "logical_consistency": "Very high - geometry and value sets checked on storage",
        "thematic_accuracy": "Deviations mainly in the classes Övrig byggnad, Ekonomibyggnad and "
                             "Komplementbyggnad; no field checks by Lantmäteriet",
        "positional_accuracy_plan_m": (0.02, 50.0),  # Table 3 requirement range for Byggnad (yta)
    }
}
