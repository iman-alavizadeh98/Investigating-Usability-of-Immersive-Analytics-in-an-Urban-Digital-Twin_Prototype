#!/usr/bin/env python3
"""
Pattern study (2D vs 3D), 3D side: write the Unity view presets and scenario file
for P1-P3 (+ practice T0) from configs/study/pattern_scenarios.json.

    python Src/Scripts/Unity/make_pattern_study_views.py

Writes configs/visualizations/pattern/ (catalog.json, scenarios.json, t0_practice.json,
p1.json, p2.json, p3.json). Questions are copied word for word (tests check it).
Encoding, as close to the 2D maps (study_2d_maps.pdf) as the renderer allows:
height = variable A (rank scale; the 2D map uses 5 blue classes), colour = variable B
in 5 equal-count (quantile) classes with the five oranges of the 2D maps
(palette study_oranges5), grey = no data, bold area outlines.
Then copy them into the package: build_unity_package.py --only visualizations.
"""
import os
os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
import json
from pathlib import Path

root = Path("configs/visualizations/pattern")
study = json.loads(Path("configs/study/pattern_scenarios.json").read_text(encoding="utf-8"))

OUTLINE = {"enabled": True, "width": 0.025, "color": {"r": 0.1, "g": 0.1, "b": 0.1, "a": 1.0}}
NO_DATA = {"r": 0.62, "g": 0.62, "b": 0.62, "a": 1.0}
SPATIAL = {"deso": "DeSO areas (SCB)", "valdistrikt": "voting districts", "ruta": "250 m grid cells (SCB)"}

def b_ref(b):
    if b["source"] == "employment_rate_deso":   # now in the package (deso_employment)
        return "deso_employment", "employment_rate_20_64"
    return b["dataLayerId"], b["variableId"]

def spec(pid, name, layer, a, b, unit_filter=None):
    out = {
        "id": pid, "displayName": name,
        "layers": [{
            "id": f"{pid}_columns", "enabled": True, "outline": OUTLINE, "mark": 1,
            "target": {"kind": 0, "layerId": layer, "mapping": {"mode": 0, "associationId": ""}},
            "encodings": [
                {"channel": 1, "role": 0,
                 "data": {"mode": 0, "variables": [{"dataLayerId": a[0], "variableId": a[1]}]},
                 "scale": {"type": 4},
                 "height": {"maximumVisualHeight": 0.6, "minimumVisualHeight": 0.01}},
                {"channel": 0, "role": 0,
                 "data": {"mode": 0, "variables": [{"dataLayerId": b[0], "variableId": b[1]}]},
                 "scale": {"type": 3, "quantileClasses": 5},
                 "color": {"paletteId": "study_oranges5", "noDataColor": NO_DATA}},
            ],
            "heightSurface": {"method": 1, "insetFactor": 1.0},
        }],
    }
    if unit_filter:   # e.g. 250 m cells with >= 50 residents; others are no data (Unity: UnitFilterSettings)
        out["layers"][0]["unitFilter"] = unit_filter
    return out

catalog, scenarios = [], []

# Practice view (T0): same encoding as the study views, other variables.
t0 = spec("t0_practice", "T0 Practice: residents (height) and share 65+ (colour)", "deso",
          ("deso_population", "population"), ("deso_population", "share_65_plus"))
(root / "t0_practice.json").write_text(json.dumps(t0, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
catalog.append({"id": "t0_practice", "displayName": "T0 Practice", "file": "t0_practice.json"})
scenarios.append({
    "id": "T0", "title": "Practice", "presetId": "t0_practice",
    "question": "Practice (about 1 minute). Look around the table. Point at a few areas and read their values. Say aloud what the height and the colour show.",
    "data": [
        "Height · Residents · persons · DeSO areas (SCB) · 2024",
        "Colour · Share of residents aged 65+ · % · DeSO areas (SCB) · 2024 · 5 classes, about the same number of areas each",
    ],
    "parts": [], "training": True,
})

for s in study["scenarios"]:
    pid = s["id"].lower()
    a = (s["a"]["dataLayerId"], s["a"]["variableId"])
    b = b_ref(s["b"])
    preset = spec(pid, f"{s['id']} {s['title']}: {s['a']['label']} (height) and {s['b']['label']} (colour)", s["unitLayer"], a, b,
                  s.get("unitFilter"))
    (root / f"{pid}.json").write_text(json.dumps(preset, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    catalog.append({"id": pid, "displayName": f"{s['id']} {s['title']}", "file": f"{pid}.json"})
    unit = s.get("unitLabel") if s.get("unitFilter") else SPATIAL[s["unitLayer"]]
    scenarios.append({
        "id": s["id"], "title": s["title"], "presetId": pid,
        "question": s["question"],                     # word for word from configs/study/pattern_scenarios.json
        "data": [
            f"Height (A) · {s['a']['label']} · {s['a']['unit']} · {unit} · {s['a']['year']}",
            f"Colour (B) · {s['b']['label']} · {s['b']['unit']} · {unit} · {s['b']['year']} · 5 classes, about the same number of areas each; grey = no data",
        ],
        "parts": [], "training": False,
    })

# Optional free exploration after the study (3D only, not scored): its view is
# copied from the evaluation set, so both studies show the same 4-variable profile.
show = study.get("showcase")
if show:
    evaluation = Path("configs/visualizations/evaluation")
    entry = next(e for e in json.loads((evaluation / "catalog.json").read_text(encoding="utf-8"))["visualizations"]
                 if e["id"] == show["presetId"])
    (root / entry["file"]).write_text((evaluation / entry["file"]).read_text(encoding="utf-8"), encoding="utf-8")
    catalog.append({"id": entry["id"], "displayName": "S " + show["title"], "file": entry["file"]})
    scenarios.append({
        "id": show["id"], "title": show["title"], "presetId": show["presetId"],
        "question": show["question"], "data": show["data"],
        "parts": [], "training": False, "optional": True,
    })

(root / "catalog.json").write_text(json.dumps({"visualizations": catalog}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
(root / "scenarios.json").write_text(json.dumps({
    "studyId": study["studyId"],
    "source": "configs/study/pattern_scenarios.json (questions copied word for word; checked by tests)",
    "scenarios": scenarios}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("written", [c["id"] for c in catalog])
