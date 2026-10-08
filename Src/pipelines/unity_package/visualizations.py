"""
Visualization presets for the Unity package.

Presets are VisualizationSpec JSON files authored in the repository
(configs/visualizations/<set>/, with a catalog.json listing them). They hold no
data, only references to layer and variable ids, so they are tracked in git and
copied into the package (visualizations/) at build time.

Before copying, every reference is checked against what the package contains:
spatial layers, data layers and their variables, associations and the buildings
context. A preset that names something missing fails the build, instead of
failing in front of a participant.
"""

from __future__ import annotations

import json
import logging
import shutil
from pathlib import Path
from typing import Dict, List, Set, Tuple

logger = logging.getLogger(__name__)

OUTPUT_SUBDIR = "visualizations"


def package_inventory(package_dir: Path) -> Dict:
    def ids(pattern: str) -> Set[str]:
        return {json.loads(p.read_text(encoding="utf-8")).get("id") or p.parent.name for p in package_dir.glob(pattern)}

    variables: Dict[str, Set[str]] = {}
    targets: Dict[str, str] = {}
    for p in package_dir.glob("data_layers/*/layer.json"):
        d = json.loads(p.read_text(encoding="utf-8"))
        variables[d["id"]] = {v["id"] for v in d.get("variables", [])}
        targets[d["id"]] = d.get("targetSpatialLayerId")
    return {
        "spatial": ids("spatial_layers/*/layer.json"),
        "context": ids("urban_context/*/layer.json"),
        "associations": ids("associations/*/layer.json"),
        "variables": variables,
        "targets": targets,
    }


def check_spec(spec: Dict, inv: Dict) -> List[str]:
    problems = []
    layer_ids = {layer.get("id") for layer in spec.get("layers", [])}
    for layer in spec.get("layers", []):
        lid = layer.get("id")
        target = layer.get("target", {})
        kind, tlayer = target.get("kind", 0), target.get("layerId")
        if kind == 1:
            if tlayer not in inv["context"]:
                problems.append(f"{lid}: urban-context layer '{tlayer}' not in package")
            assoc = target.get("mapping", {}).get("associationId")
            if target.get("mapping", {}).get("mode") == 1 and assoc not in inv["associations"]:
                problems.append(f"{lid}: association '{assoc}' not in package")
        elif tlayer not in inv["spatial"]:
            problems.append(f"{lid}: spatial layer '{tlayer}' not in package")
        for enc in layer.get("encodings", []):
            for var in enc.get("data", {}).get("variables", []):
                dl, vid = var.get("dataLayerId"), var.get("variableId")
                if dl not in inv["variables"]:
                    problems.append(f"{lid}: data layer '{dl}' not in package")
                elif vid not in inv["variables"][dl]:
                    problems.append(f"{lid}: variable '{dl}.{vid}' not in package")
                elif kind != 1 and inv["targets"].get(dl) != tlayer:
                    problems.append(f"{lid}: '{dl}' targets '{inv['targets'].get(dl)}', not '{tlayer}'")
        follow = layer.get("urbanContextPlacement", {})
        if follow.get("mode") == 1 and follow.get("sourceVisualizationLayerId") not in layer_ids:
            problems.append(f"{lid}: follows unknown layer '{follow.get('sourceVisualizationLayerId')}'")
    return problems


def export_visualizations(package_dir: Path, source_dir: Path) -> Dict:
    catalog_path = source_dir / "catalog.json"
    if not catalog_path.exists():
        raise FileNotFoundError(f"No catalog.json in {source_dir}")
    catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
    inv = package_inventory(package_dir)

    problems: Dict[str, List[str]] = {}
    files: List[Tuple[Path, str]] = []
    for entry in catalog.get("visualizations", []):
        src = source_dir / entry["file"]
        spec = json.loads(src.read_text(encoding="utf-8"))
        issues = check_spec(spec, inv)
        if issues:
            problems[entry["file"]] = issues
        files.append((src, entry["file"]))
    # Optional study scenarios: every scenario must name a preset of this catalog.
    scenarios_path = source_dir / "scenarios.json"
    scenario_count = 0
    if scenarios_path.exists():
        preset_ids = {e["id"] for e in catalog.get("visualizations", [])}
        scenarios = json.loads(scenarios_path.read_text(encoding="utf-8")).get("scenarios", [])
        scenario_count = len(scenarios)
        for s in scenarios:
            if s.get("presetId") not in preset_ids:
                problems.setdefault("scenarios.json", []).append(f"{s.get('id')}: preset '{s.get('presetId')}' not in catalog")
            if s.get("options") and s.get("expected") not in s["options"]:
                problems.setdefault("scenarios.json", []).append(f"{s.get('id')}: expected answer is not one of the options")

    if problems:
        raise ValueError("Visualization presets reference things missing from the package:\n" +
                         "\n".join(f"  {f}: {p}" for f, ps in problems.items() for p in ps))

    out = package_dir / OUTPUT_SUBDIR
    if out.exists():
        shutil.rmtree(out)  # the package copy is generated; the repo copy is the source
    out.mkdir(parents=True)
    shutil.copy2(catalog_path, out / "catalog.json")
    for src, name in files:
        shutil.copy2(src, out / name)
    if scenarios_path.exists():
        shutil.copy2(scenarios_path, out / "scenarios.json")
    logger.info(f"Visualizations: {len(files)} presets, {scenario_count} scenarios from {source_dir} → {out}")
    return {"presets": len(files), "scenarios": scenario_count, "source": str(source_dir)}
