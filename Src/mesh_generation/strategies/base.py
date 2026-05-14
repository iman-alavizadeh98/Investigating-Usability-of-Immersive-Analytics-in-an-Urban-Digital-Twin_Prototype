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
import logging

logger = logging.getLogger(__name__)


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
            "coverage_percentage": 0.0
        }
        
        if not self.groups:
            report["validation_errors"].append("No groups created")
            return report
        
        # Check for duplicates FIRST - each building can only appear once
        assignment_count = {}
        for group in self.groups:
            for idx in group.building_indices:
                assignment_count[idx] = assignment_count.get(idx, 0) + 1
                report["buildings_per_group"].append(len(group.building_indices))
        
        # Find duplicate assignments
        duplicates = [idx for idx, count in assignment_count.items() if count > 1]
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
        
        missing_buildings = set(range(len(self.buildings_gdf))) - all_assigned
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
