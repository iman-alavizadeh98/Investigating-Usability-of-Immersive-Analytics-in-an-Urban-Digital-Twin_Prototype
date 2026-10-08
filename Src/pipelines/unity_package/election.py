"""
Election results per voting district (valdistrikt) for the Unity package.

Spatial layer
  valdistrikt   voting-district polygons of the municipality (whole municipality;
                it extends beyond the area covered by buildings and SCB data).
                Unit id "valdistrikt:<Valdistriktskod>".

Data layer
  valdistrikt_election   eligible voters, votes cast, turnout, vote share per
                party, the winning party, and deviations from the municipal
                result (percentage points) for diverging maps.

The collection district (Uppsamlingsdistrikt) has no area, so it is not a unit;
its votes are part of the municipal totals used for the deviations.

Parties: the eight Riksdag parties get short ids (s, m, sd, ...). Other parties
with at least `min_party_share_pct` of the municipal vote get a slug id; the
rest are summed into `share_other`.
"""

from __future__ import annotations

import logging
import re
import unicodedata
from pathlib import Path
from typing import Dict, Tuple

import geopandas as gpd
import pandas as pd

from pipelines.election.config import (
    DISTRICT_CODE_COLUMN,
    DISTRICT_NAME_COLUMN,
    INVALID_PREFIX,
    NATIONAL_PARTIES,
    SUMMARY_CATEGORIES,
)
from pipelines.election.loader import ElectionSource, load_election
from pipelines.election.validator import is_collection, long_by_district

from .layer_files import SpatialLayerSpec, Variable, write_data_layer, write_spatial_layer

logger = logging.getLogger(__name__)

DISTRICT_LAYER = "valdistrikt"


def party_id(name: str) -> str:
    if name in NATIONAL_PARTIES:
        return NATIONAL_PARTIES[name][0].lower()
    ascii_name = unicodedata.normalize("NFKD", name).encode("ascii", "ignore").decode()
    return re.sub(r"[^a-z0-9]+", "_", ascii_name.lower()).strip("_")


def party_label(name: str) -> str:
    if name in NATIONAL_PARTIES:
        abbr, en = NATIONAL_PARTIES[name]
        return f"{abbr} - {en}"
    return name


def export_election(package_dir: Path, folder: Path, municipality_code: str, min_party_share_pct: float = 0.5) -> Tuple[gpd.GeoDataFrame, Dict]:
    src: ElectionSource = load_election(folder, municipality_code)
    long = src.sheets["roster_KF"]
    d = long_by_district(long)

    # Party votes per district (wide), from the long sheet's party rows.
    category = long["Parti/kategori"].astype("string")
    is_party = ~(category.isin(list(SUMMARY_CATEGORIES)) | category.str.startswith(INVALID_PREFIX).fillna(False))
    votes = long[is_party].pivot_table(index=DISTRICT_CODE_COLUMN, columns="Parti/kategori", values="Röster", aggfunc="sum", fill_value=0)

    # Municipal totals include the collection district.
    muni_valid = float(d["valid_row"].sum())
    muni_cast = float(d["cast_row"].sum())
    muni_eligible = float(d["eligible_row"].sum())
    muni_turnout = 100.0 * muni_cast / muni_eligible
    muni_share = 100.0 * votes.sum() / muni_valid
    named = [p for p in muni_share.sort_values(ascending=False).index
             if p in NATIONAL_PARTIES or muni_share[p] >= min_party_share_pct]
    others = [p for p in votes.columns if p not in named]

    # Units: districts with an area.
    g = src.districts.copy()
    g["unit_id"] = f"{DISTRICT_LAYER}:" + g[DISTRICT_CODE_COLUMN].astype(str)
    name_col = "Valdistriktsnamn" if "Valdistriktsnamn" in g.columns else DISTRICT_NAME_COLUMN
    g["display_name"] = g[name_col].astype(str)
    g = g.sort_values("unit_id").reset_index(drop=True)
    write_spatial_layer(package_dir, SpatialLayerSpec(
        DISTRICT_LAYER, f"Voting districts {src.election_year or ''}".strip(),
        f"Voting districts (valdistrikt) of municipality {municipality_code}; whole municipality.",
        visible_by_default=False, provenance={"source": str(src.boundaries_path)},
    ), g)

    codes = g[DISTRICT_CODE_COLUMN].astype(str)
    idx = pd.Index(codes)
    valid = d["valid_row"].reindex(idx)
    v = votes.reindex(idx)
    turnout = 100.0 * d["cast_row"].reindex(idx) / d["eligible_row"].reindex(idx).where(lambda s: s > 0)

    y = src.election_year
    variables = [
        Variable("eligible_voters", "Eligible voters", d["eligible_row"].reindex(idx), "Integer", "persons", "Röstberättigade"),
        Variable("votes_cast", "Votes cast", d["cast_row"].reindex(idx), "Integer", "votes", "Valdeltagande (votes cast)"),
        Variable("valid_votes", "Valid votes", valid, "Integer", "votes", "Summa giltiga röster"),
        Variable("turnout", f"Turnout {y}", turnout, "Float", "%", "votes cast / eligible voters", "derived"),
        Variable("turnout_diff", "Turnout vs municipality", turnout - muni_turnout, "Float", "pp",
                 f"district turnout - municipal turnout ({muni_turnout:.1f} %, incl. collection district)", "derived"),
    ]
    shares = {}
    for p in named:
        pid = party_id(p)
        s = 100.0 * v[p] / valid.where(valid > 0)
        shares[pid] = s
        variables.append(Variable(f"share_{pid}", f"{party_label(p)} {y}", s, "Float", "%", f"votes for {p} / valid votes", "derived"))
        variables.append(Variable(f"share_{pid}_diff", f"{party_label(p)} vs municipality", s - muni_share[p], "Float", "pp",
                                  f"district share - municipal share ({muni_share[p]:.1f} %)", "derived"))
    if others:
        variables.append(Variable("share_other", "Other parties", 100.0 * v[others].sum(axis=1) / valid.where(valid > 0),
                                  "Float", "%", f"{len(others)} parties below {min_party_share_pct} % of the municipal vote", "derived"))
    share_frame = pd.DataFrame(shares)
    winner = share_frame.idxmax(axis=1)
    variables.append(Variable("winner", "Largest party", winner.map(lambda pid: pid.upper() if isinstance(pid, str) else None),
                              "String", "", "party with the highest share in the district", "derived"))
    variables.append(Variable("winner_share", "Largest party's share", share_frame.max(axis=1), "Float", "%", "", "derived"))

    report = {
        "election": f"{src.election_type} {y}",
        "municipality": municipality_code,
        "districts_with_area": int(len(g)),
        "collection_districts": sorted(long.loc[is_collection(long), DISTRICT_CODE_COLUMN].unique().tolist()),
        "municipal_turnout_pct": round(muni_turnout, 2),
        "named_parties": [party_id(p) for p in named],
        "other_parties": len(others),
        "results_without_area": sorted(set(d.index) - set(idx) - set(long.loc[is_collection(long), DISTRICT_CODE_COLUMN])),
    }
    unit_ids = list(g["unit_id"])
    report["valdistrikt_election"] = write_data_layer(
        package_dir, "valdistrikt_election", f"Election {y} (voting districts)",
        f"{src.election_type} {y}: turnout and vote shares per voting district; deviations from the municipal result.",
        DISTRICT_LAYER, unit_ids, variables,
        {"results": str(src.results_path), "boundaries": str(src.boundaries_path), "municipality": municipality_code},
    )
    return g, report
