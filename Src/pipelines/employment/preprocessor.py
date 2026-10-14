"""
Preprocessing of the employment tables.

Deliberately empty for now: the processing rules (target unit, whether to use
the residence-based Tab7 or the workplace-based Tab8, how to merge the sector
spelling variants Näringsli/Naringsliv and Ovrigt/Övrigt, derived values such as
the employment rate Sysselsatt / Totalt) have not been decided yet. Until they
are, the tables pass through unchanged, so the profiles and HTML views show the
data exactly as delivered.

When a step is added here: keep the raw Swedish columns, add English or derived
columns next to them, never mix residence-based and workplace-based counts, and
record what was done in the returned report (written to
employment_preprocess_report.json).
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
