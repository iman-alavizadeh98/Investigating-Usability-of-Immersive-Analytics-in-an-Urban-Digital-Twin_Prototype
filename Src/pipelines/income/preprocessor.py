"""
Preprocessing of the income tables.

Deliberately empty for now: the processing rules (target unit, derived values
such as the mean economic standard Tot_CDISP0 / Totalt, how to treat units with
0 households) have not been decided yet. Until they are, the tables pass through
unchanged, so the profiles and HTML views show the data exactly as delivered.

When a step is added here: keep the raw Swedish columns, add English or derived
columns next to them, never sum or average `MedianInk` across units, and record
what was done in the returned report (written to income_preprocess_report.json).
"""

from __future__ import annotations

from typing import Dict

from pipelines.scb.loader import SourceTable


def preprocess_tables(tables: Dict[str, SourceTable]) -> dict:
    """Apply the preprocessing steps in place; returns what was done per table."""
    return {
        "steps": [],
        "note": "No preprocessing rules defined yet; tables are unchanged from the source files.",
        "tables": {key: {"rows": int(len(t.data))} for key, t in tables.items()},
    }
