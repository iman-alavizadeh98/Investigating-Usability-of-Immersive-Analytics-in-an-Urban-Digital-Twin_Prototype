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
from typing import Dict, List, Optional, Set, Tuple

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
        unit_filter = layer.get("unitFilter") or {}
        if unit_filter.get("variableId"):
            dl, vid = unit_filter.get("dataLayerId"), unit_filter["variableId"]
            if dl not in inv["variables"] or vid not in inv["variables"][dl]:
                problems.append(f"{lid}: unit filter variable '{dl}.{vid}' not in package")
        follow = layer.get("urbanContextPlacement", {})
        if follow.get("mode") == 1 and follow.get("sourceVisualizationLayerId") not in layer_ids:
            problems.append(f"{lid}: follows unknown layer '{follow.get('sourceVisualizationLayerId')}'")
    return problems


PART_KINDS = {"compare", "compareCombined", "relativeParty", "pattern", "slotCheck", "open", "choice"}


def check_scenario(s: Dict, preset_ids: Set[str], inv: Dict) -> List[str]:
    """
    A scenario names presets of the catalog and (pair rule, sub-questions)
    variables of the package. Expected answers and comparison areas are NOT
    in the file: the Unity runtime derives them from the data
    (StudyAnswerKey), so they cannot drift from the package.
    """
    problems = []
    for key in ("presetId", "alternatePresetId"):
        if s.get(key) and s[key] not in preset_ids:
            problems.append(f"{key} '{s[key]}' not in catalog")
    if s.get("options") and s.get("expected") not in s["options"]:
        problems.append("expected answer is not one of the options")

    def var(layer, variable, where):
        if not variable:
            return
        if layer not in inv["variables"]:
            problems.append(f"{where}: data layer '{layer}' not in package")
        elif variable not in inv["variables"][layer]:
            problems.append(f"{where}: variable '{layer}.{variable}' not in package")

    pair = s.get("pair") or {}
    if pair:
        if pair.get("spatialLayerId") not in inv["spatial"]:
            problems.append(f"pair: spatial layer '{pair.get('spatialLayerId')}' not in package")
        for prefix in ("", "2"):
            layer = pair.get(f"dataLayerId{prefix}")
            var(layer, pair.get(f"variableId{prefix}"), "pair")
            if layer and inv["targets"].get(layer) != pair.get("spatialLayerId"):
                problems.append(f"pair: '{layer}' targets '{inv['targets'].get(layer)}', not '{pair.get('spatialLayerId')}'")
        var(pair.get("requireDataLayerId"), pair.get("requireVariableId"), "pair require")
        var(pair.get("filterDataLayerId"), pair.get("filterVariableId"), "pair filter")

    for part in s.get("parts") or []:
        where = f"part {part.get('id')}"
        if part.get("kind") not in PART_KINDS:
            problems.append(f"{where}: unknown kind '{part.get('kind')}'")
        var(part.get("dataLayerId"), part.get("variableId"), where)
        var(part.get("dataLayerId2") or part.get("dataLayerId"), part.get("variableId2"), where)
        if part.get("kind") == "relativeParty":
            for v in (part.get("variableId"), part.get("variableId2")):
                var(part.get("dataLayerId"), f"{v}_diff", where)
        if part.get("kind") in ("compare", "compareCombined", "relativeParty", "pattern", "slotCheck") and not pair:
            problems.append(f"{where}: needs a pair rule (comparison areas)")
    return problems


def export_visualization_sets(package_dir: Path, main_dir: Optional[Path], sets: Dict[str, Path]) -> Dict:
    """
    The main preset folder to <package>/visualizations/ and each further set
    (e.g. a second study with its own catalog and scenarios) to
    <package>/visualizations/<name>/. The main folder is written first because
    it replaces the whole visualizations/ folder.
    """
    report = {}
    if main_dir is not None:
        report["main"] = export_visualizations(package_dir, main_dir)
    for name, folder in sets.items():
        report[name] = export_visualizations(package_dir, folder, subdir=name)
    return report


def export_visualizations(package_dir: Path, source_dir: Path, subdir: Optional[str] = None) -> Dict:
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
            for issue in check_scenario(s, preset_ids, inv):
                problems.setdefault("scenarios.json", []).append(f"{s.get('id')}: {issue}")

    if problems:
        raise ValueError("Visualization presets reference things missing from the package:\n" +
                         "\n".join(f"  {f}: {p}" for f, ps in problems.items() for p in ps))

    out = package_dir / OUTPUT_SUBDIR / subdir if subdir else package_dir / OUTPUT_SUBDIR
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
