"""Build the Unity runtime package for one city from a city config.

Nothing here is tied to a city: everything city-specific (name, origin, input
files) comes from a JSON config in configs/cities/. See
Src/Scripts/Unity/build_unity_package.py for the command and
config.py for the config format.

Package layout (inside StreamingAssets/cities/<packageDirectory>/; the cities/
folder is git-ignored because packages are generated from licensed data):
    project_manifest.json              written from what was exported
    urban_context/buildings/
        layer.json                     definition, field list, provenance
        geometry.bin                   GBLD v3 footprints + heights (binary)
        attributes.json                per-building values, column-wise
"""
