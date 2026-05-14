"""Configuration dataclasses for the LiDAR height estimation pipeline."""

from dataclasses import dataclass, field
from enum import Enum
from pathlib import Path
from typing import Dict, Optional
import uuid
from datetime import datetime


class QualityLevel(str, Enum):
    """Height estimation quality classification."""
    HIGH = "high"
    MEDIUM = "medium"
    LOW = "low"


class HeightSource(str, Enum):
    """Source of height estimate."""
    LIDAR_HAG_P95 = "lidar_hag_p95"
    FALLBACK_DEFAULT = "fallback_default"


@dataclass
class TileIndexConfig:
    """Configuration for LiDAR tile indexing and spatial mapping."""
    
    tile_directory: Path
    """Path to directory containing LAZ tiles."""
    
    tile_extension: str = "laz"
    """File extension for tiles (default: 'laz')."""
    
    crs: str = "EPSG:3006"
    """Coordinate reference system for all spatial operations."""
    
    buffer_meters: float = 10.0
    """Buffer around building footprints when selecting point cloud samples."""


@dataclass
class PDALConfig:
    """Configuration for PDAL preprocessing pipeline."""
    
    # Outlier removal (statistical filter)
    outlier_method: str = "statistical"
    """Outlier detection method (e.g., 'statistical')."""
    
    outlier_multiplier: float = 2.0
    """Multiplier for statistical outlier threshold (higher = less aggressive)."""
    
    # Ground classification (SMRF - Simple Morphological Filter)
    smrf_slope: float = 0.15
    """SMRF slope parameter for ground classification."""
    
    smrf_window: float = 16.0
    """SMRF window size in meters."""
    
    smrf_threshold: float = 0.5
    """SMRF height threshold in meters."""
    
    # Height-above-ground computation
    hag_method: str = "delaunay"
    """HAG computation method (e.g., 'delaunay' or 'nearest')."""
    
    # Output format
    compression: str = "lazrs"
    """LAZ compression method (e.g., 'lazrs' for LZRS compression)."""
    
    output_classification: bool = True
    """Whether to output classification codes in processed LAZ."""


@dataclass
class HeightExtractionConfig:
    """Configuration for per-building height extraction and quality assessment."""
    
    min_points: int = 5
    """Minimum number of points required for reliable height estimation."""
    
    percentile: float = 95.0
    """Percentile of non-ground points to use for height (e.g., 95th)."""
    
    min_height_m: float = 2.0
    """Minimum acceptable height in meters (prevents negative or zero values)."""
    
    fallback_height_m: float = 10.0
    """Default height when insufficient LiDAR coverage (matches mesh generator default)."""
    
    # Quality thresholds
    high_quality_min_points: int = 1000
    """Minimum point count for 'high' quality classification."""
    
    high_quality_min_coverage: float = 0.8
    """Minimum coverage ratio (0.0-1.0) for 'high' quality."""
    
    medium_quality_min_points: int = 100
    """Minimum point count for 'medium' quality classification."""
    
    medium_quality_min_coverage: float = 0.5
    """Minimum coverage ratio for 'medium' quality."""
    
    # Variance thresholds for quality assessment
    high_quality_max_variance: float = 50.0
    """Maximum z-variance (m²) for 'high' quality."""


@dataclass
class LiDARHeightPipelineConfig:
    """Master configuration for the LiDAR height estimation pipeline."""
    
    # Input/output paths
    input_buildings_path: Path
    """Path to processed buildings GeoPackage from buildings pipeline."""
    
    output_directory: Path
    """Directory for outputs (GeoPackage, Parquet, QC CSV)."""
    
    lidar_directory: Path
    """Path to directory containing LiDAR LAZ tiles."""
    
    temp_directory: Optional[Path] = None
    """Temporary directory for PDAL processing (default: output_directory/temp)."""
    
    # Sub-configs
    tile_index_config: TileIndexConfig = field(default_factory=TileIndexConfig)
    pdal_config: PDALConfig = field(default_factory=PDALConfig)
    height_extraction_config: HeightExtractionConfig = field(default_factory=HeightExtractionConfig)
    
    # CRS and spatial reference
    crs: str = "EPSG:3006"
    """Authoritative projected CRS (must match buildings)."""
    
    # Reproducibility
    height_run_id: str = field(default_factory=lambda: str(uuid.uuid4()))
    """UUID for this pipeline run; links all heights generated in this batch."""
    
    run_timestamp: str = field(default_factory=lambda: datetime.utcnow().isoformat())
    """ISO 8601 timestamp of pipeline start."""
    
    # Processing options
    batch_size: Optional[int] = None
    """Maximum buildings per batch (None = process all)."""
    
    verbose: bool = True
    """Enable verbose logging."""
    
    def __post_init__(self):
        """Validate and set defaults."""
        self.input_buildings_path = Path(self.input_buildings_path)
        self.output_directory = Path(self.output_directory)
        self.lidar_directory = Path(self.lidar_directory)
        
        if self.temp_directory is None:
            self.temp_directory = self.output_directory / "temp" / self.height_run_id
        else:
            self.temp_directory = Path(self.temp_directory)
        
        # Propagate CRS to sub-configs
        self.tile_index_config.crs = self.crs
    
    @staticmethod
    def from_dict(config_dict: Dict) -> "LiDARHeightPipelineConfig":
        """Create config from dictionary (useful for CLI argument parsing)."""
        # Build nested configs first
        tile_config = TileIndexConfig(**config_dict.pop("tile_index_config", {}))
        pdal_config = PDALConfig(**config_dict.pop("pdal_config", {}))
        height_config = HeightExtractionConfig(**config_dict.pop("height_extraction_config", {}))
        
        return LiDARHeightPipelineConfig(
            tile_index_config=tile_config,
            pdal_config=pdal_config,
            height_extraction_config=height_config,
            **config_dict
        )
