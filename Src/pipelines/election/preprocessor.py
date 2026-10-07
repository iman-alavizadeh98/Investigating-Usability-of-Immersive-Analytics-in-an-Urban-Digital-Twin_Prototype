"""
Preprocessing of the election data.

Deliberately empty for now: the processing rules (long vs. wide form, how to
treat the collection district's votes, party grouping, moving results from
voting districts to another unit) have not been decided yet. Until they are,
the frames pass through unchanged (apart from the municipality filter done on
load), so the views show the data as delivered.

When a step is added here: keep the raw Swedish columns, add English or derived
columns next to them, and record what was done in the returned report (written
to election_preprocess_report.json).
"""

from __future__ import annotations

from .loader import ElectionSource


def preprocess_election(src: ElectionSource) -> dict:
    """Apply the preprocessing steps in place; returns what was done."""
    return {
        "steps": [],
        "note": "No preprocessing rules defined yet; frames are unchanged apart from the municipality filter on load.",
        "frames": {name: {"rows": int(len(df))} for name, df in src.sheets.items()} | {"districts": {"rows": int(len(src.districts))}},
    }
