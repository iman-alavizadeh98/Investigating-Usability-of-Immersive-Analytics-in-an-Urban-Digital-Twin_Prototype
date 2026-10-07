"""
Income (SCB *Inkomster*) preprocess pipeline.

Load, check, profile and view the income tables (shared SCB core in
`pipelines.scb`); processing rules go in `preprocessor.py` once decided.
"""

from __future__ import annotations

from typing import Dict

from pipelines.scb.loader import SourceTable
from pipelines.scb.pipeline import ScbTablesPipeline

from .config import INCOME
from .preprocessor import preprocess_tables


class IncomePipeline(ScbTablesPipeline):
    dataset = INCOME

    def preprocess_tables(self, tables: Dict[str, SourceTable]) -> dict:
        return preprocess_tables(tables)
