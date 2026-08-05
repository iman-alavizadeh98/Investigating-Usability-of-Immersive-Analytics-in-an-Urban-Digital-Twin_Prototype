"""
Base class for mesh generation strategies.

Defines the interface that all grouping strategies must implement.
Each strategy defines how to partition buildings into groups,
where each group becomes one merged mesh file.
"""

from abc import ABC, abstractmethod
from pathlib import Path
from dataclasses import dataclass, field
from typing import List, Dict, Tuple, Optional, Any
import pandas as pd
import logging

logger = logging.getLogger(__name__)


class StrategyValidationError(Exception):
    """
    Raised when a partition violates the strict ownership policy.

    Previously these violations were logged as warnings and generation continued,
    which is how a 5.0% building duplication (9,690 buildings in 2-4 grid cells)
    reached a shipped run unnoticed. Ownership errors now stop the run by default;
    override with GeneratorConfig.strict_ownership=False.
    """


@dataclass
class MeshGroup:
    """Represents a group of buildings to be meshed together."""
    group_id: str
    group_name: str
    building_indices: List[int]  # Row indices in GeoDataFrame
    bounds: Tuple[float, float, float, float]  # (minx, miny, maxx, maxy)
    building_count: int
    estimated_triangles: int = 0
    metadata: Dict[str, Any] = field(default_factory=dict)


@dataclass
class StrategyConfig:
    """Base configuration for any strategy."""
    pass


class MeshStrategy(ABC):
    """
    Abstract base class for mesh grouping strategies.
    
    Each strategy defines how to partition buildings into groups,
    where each group becomes one merged mesh file.
    """
    
    def __init__(self, buildings_gdf, config: StrategyConfig = None):
        """
        Initialize strategy.
        
        Args:
            buildings_gdf: GeoDataFrame with building geometries
            config: Strategy-specific configuration
        """
        # MeshGroup.building_indices are POSITIONAL row numbers: strategies build
        # them with numpy positions and downstream code reads them with .iloc.
        # A non-default index would silently mix labels and positions, so require
        # a clean 0..n-1 RangeIndex rather than discovering the mismatch as
        # corrupted meshes.
        if buildings_gdf is not None:
            index = getattr(buildings_gdf, "index", None)
            is_default_range = (
                isinstance(index, pd.RangeIndex)
                and index.start == 0
                and index.step == 1
            )
            if not is_default_range:
                raise ValueError(
                    "buildings_gdf must have a default RangeIndex (0..n-1); got "
                    f"{type(index).__name__}. Call .reset_index(drop=True) first — "
                    "MeshGroup.building_indices are positional and are read with .iloc."
                )

        self.buildings_gdf = buildings_gdf
        self.config = config
        self.groups: List[MeshGroup] = []
        self.logger = logging.getLogger(self.__class__.__name__)
    
    @abstractmethod
    def partition(self) -> List[MeshGroup]:
        """
        Partition buildings into groups.
        
        Returns:
            List of MeshGroup objects, each representing buildings
            to be meshed together.
        """
        pass
    
    @abstractmethod
    def get_strategy_name(self) -> str:
        """Return human-readable name of this strategy."""
        pass
    
    @abstractmethod
    def get_strategy_description(self) -> str:
        """Return description of this strategy's approach."""
        pass
    
    def validate(self) -> Dict[str, Any]:
        """
        Validate strategy configuration and data.
        
        STRICT DETERMINISTIC OWNERSHIP POLICY:
        - Each building MUST be assigned to exactly one group
        - No duplicates, no missing buildings
        - Deterministic per-run (same input → same assignment)
        
        Returns:
            dict with validation report
        """
        report = {
            "strategy": self.get_strategy_name(),
            "total_buildings": len(self.buildings_gdf),
            "group_count": len(self.groups),
            "buildings_per_group": [],
            "validation_errors": [],
            "coverage_percentage": 0.0,
            # Machine-checkable ownership facts, so callers and tests can assert
            # numbers instead of parsing error strings.
            "max_assignments_per_building": 0,
            "duplicate_building_count": 0,
            "unassigned_building_count": 0,
        }
        
        if not self.groups:
            report["validation_errors"].append("No groups created")
            return report
        
        # Check for duplicates FIRST - each building can only appear once
        assignment_count = {}
        for group in self.groups:
            report["buildings_per_group"].append(len(group.building_indices))
            for idx in group.building_indices:
                assignment_count[idx] = assignment_count.get(idx, 0) + 1
        
        report["max_assignments_per_building"] = (
            max(assignment_count.values()) if assignment_count else 0
        )

        # Find duplicate assignments
        duplicates = [idx for idx, count in assignment_count.items() if count > 1]
        report["duplicate_building_count"] = len(duplicates)
        if duplicates:
            report["validation_errors"].append(
                f"STRICT: {len(duplicates)} building(s) assigned to multiple groups: {duplicates[:10]}"
            )
        
        # Check coverage - all buildings must be assigned exactly once
        all_assigned = set(assignment_count.keys())
        total_assigned = len(all_assigned)
        report["coverage_percentage"] = (
            total_assigned / len(self.buildings_gdf) * 100
            if self.buildings_gdf is not None else 0
        )
        
        # NOTE: assumes positional 0..n-1 indices, consistent with the RangeIndex
        # contract asserted in MeshStrategy.__init__.
        missing_buildings = set(range(len(self.buildings_gdf))) - all_assigned
        report["unassigned_building_count"] = len(missing_buildings)
        if missing_buildings:
            report["validation_errors"].append(
                f"STRICT: {len(missing_buildings)} building(s) not assigned: {list(missing_buildings)[:10]}"
            )
        
        # Cross-check: total assignments must equal total buildings
        total_assigned_count = sum(len(g.building_indices) for g in self.groups)
        if total_assigned_count != len(self.buildings_gdf):
            report["validation_errors"].append(
                f"STRICT: Assignment count mismatch - "
                f"assigned {total_assigned_count}, total buildings {len(self.buildings_gdf)}"
            )
        
        return report
    
    def get_statistics(self) -> Dict[str, Any]:
        """Get statistics about partitioning."""
        if not self.groups:
            return {"error": "No groups created"}
        
        building_counts = [len(g.building_indices) for g in self.groups]
        
        return {
            "group_count": len(self.groups),
            "min_buildings_per_group": min(building_counts),
            "max_buildings_per_group": max(building_counts),
            "avg_buildings_per_group": sum(building_counts) / len(building_counts),
            "total_buildings_assigned": sum(building_counts)
        }
