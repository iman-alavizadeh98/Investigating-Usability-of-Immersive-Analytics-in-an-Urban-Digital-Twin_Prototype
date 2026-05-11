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
        
        # Check coverage
        all_building_indices = set()
        for group in self.groups:
            all_building_indices.update(group.building_indices)
            report["buildings_per_group"].append(len(group.building_indices))
        
        total_assigned = len(all_building_indices)
        report["coverage_percentage"] = (
            total_assigned / len(self.buildings_gdf) * 100
            if self.buildings_gdf is not None else 0
        )
        
        missing_buildings = set(range(len(self.buildings_gdf))) - all_building_indices
        if missing_buildings:
            report["validation_errors"].append(
                f"Missing {len(missing_buildings)} buildings "
                f"({len(missing_buildings)/len(self.buildings_gdf)*100:.1f}%)"
            )
        
        # Check for duplicates (building in multiple groups)
        total_assigned_count = sum(len(g.building_indices) for g in self.groups)
        if total_assigned_count != total_assigned:
            report["validation_errors"].append(
                f"Buildings assigned multiple times "
                f"({total_assigned_count - total_assigned} duplicates)"
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
