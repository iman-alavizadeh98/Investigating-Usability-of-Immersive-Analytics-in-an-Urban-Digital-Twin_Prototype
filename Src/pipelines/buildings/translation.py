"""
Value translation for the buildings (Byggnad) dataset.

Swedish source values are never overwritten: every function here returns new
English values, and every value that is not in the lookup tables is collected
so the pipeline can log and report it instead of guessing a translation.
"""

from collections import Counter
from typing import Dict, Optional, Tuple

import pandas as pd

from .config import BUILDING_TYPES, COLLECTION_LEVELS, PURPOSES, PURPOSE_SEPARATOR


def split_purpose(value: str) -> Tuple[str, str]:
    """Split "Samhällsfunktion;Sjukhus" into ("Samhällsfunktion", "Sjukhus").

    A value without the separator is treated as an object type with an
    unspecified purpose. Whitespace around both parts is ignored.
    """
    object_type, _, subtype = str(value).partition(PURPOSE_SEPARATOR)
    return object_type.strip(), subtype.strip()


def lookup_purpose(value) -> Tuple[Optional[str], Optional[str], bool]:
    """Translate one andamal value.

    Returns (english_label, category, matched). Missing values give
    (None, None, True). Unknown values give (original value, category of the
    object-type part if known, False) so the original stays visible and the
    caller can report it.
    """
    if value is None or pd.isna(value) or str(value).strip() == "":
        return None, None, True

    object_type, subtype = split_purpose(value)
    type_info = BUILDING_TYPES.get(object_type)
    category = type_info["category"] if type_info else None
    entry = PURPOSES.get(object_type, {}).get(subtype)
    if entry is None:
        return str(value), category, False
    return entry["en"], category, True


def lookup_casefold(table: Dict[str, dict], value, field: str = "en"):
    """Case-insensitive lookup of `value` in `table`; returns (result, matched)."""
    if value is None or pd.isna(value):
        return None, True
    key = str(value).strip().casefold()
    for table_key, info in table.items():
        if table_key.casefold() == key:
            return info.get(field), True
    return str(value), False


def translate_purpose_column(series: pd.Series) -> Tuple[pd.Series, pd.Series, Counter]:
    """Translate a purpose column. Returns (english, category, unmatched counts)."""
    unmatched: Counter = Counter()
    cache = {}
    english, categories = [], []
    for value in series:
        key = None if value is None or pd.isna(value) else str(value)
        if key not in cache:
            cache[key] = lookup_purpose(value)
        label, category, matched = cache[key]
        if not matched:
            unmatched[key] += 1
        english.append(label)
        categories.append(category)
    return (
        pd.Series(english, index=series.index, dtype="object"),
        pd.Series(categories, index=series.index, dtype="object"),
        unmatched,
    )


def translate_object_type_column(series: pd.Series) -> Tuple[pd.Series, pd.Series, Counter]:
    """Translate objekttyp values. Returns (english, category, unmatched counts)."""
    unmatched: Counter = Counter()
    english, categories = [], []
    for value in series:
        info = BUILDING_TYPES.get(value) if isinstance(value, str) else None
        if info is None:
            if value is not None and not pd.isna(value):
                unmatched[str(value)] += 1
            english.append(None if value is None or pd.isna(value) else str(value))
            categories.append(None)
        else:
            english.append(info["en"])
            categories.append(info["category"])
    return (
        pd.Series(english, index=series.index, dtype="object"),
        pd.Series(categories, index=series.index, dtype="object"),
        unmatched,
    )


def translate_collection_level_column(series: pd.Series) -> Tuple[pd.Series, Counter]:
    """Translate insamlingslage values case-insensitively."""
    unmatched: Counter = Counter()
    english = []
    for value in series:
        label, matched = lookup_casefold(COLLECTION_LEVELS, value)
        if not matched:
            unmatched[str(value)] += 1
        english.append(label)
    return pd.Series(english, index=series.index, dtype="object"), unmatched
