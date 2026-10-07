"""
Preprocessing of the population tables.

Deliberately empty for now: the processing rules (which tables, which units,
how to handle the mixed 250/1,000 m grid, English aliases, derived variables)
have not been decided yet. Until they are, the tables pass through unchanged,
so the profiles and HTML views show the data exactly as delivered.

When a step is added here: keep the raw Swedish columns, add English or derived
columns next to them, and record what was done in the returned report (it is
written to population_preprocess_report.json).
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
