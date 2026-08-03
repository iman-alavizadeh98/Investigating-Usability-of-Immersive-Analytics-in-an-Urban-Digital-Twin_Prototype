"""PDAL pipeline generation and execution for LiDAR preprocessing."""

import hashlib
import json
import logging
import shutil
from pathlib import Path
from typing import Any, Dict, Optional
import tempfile

import pdal

from .config import PDALConfig

logger = logging.getLogger(__name__)


class PDALCacheManager:
    """Manage caching of PDAL preprocessing outputs to speed up re-runs."""
    
    def __init__(self, cache_dir: Path, config: PDALConfig):
        """
        Initialize cache manager.
        
        Args:
            cache_dir: Directory to store cached preprocessed LAZ files
            config: PDALConfig with preprocessing parameters (used for cache invalidation)
        """
        self.cache_dir = cache_dir
        self.config = config
        self.cache_dir.mkdir(parents=True, exist_ok=True)
        self.hits = 0
        self.misses = 0
    
    def _compute_file_hash(self, filepath: Path) -> str:
        """
        Compute MD5 hash of input LAZ file.
        
        Args:
            filepath: Path to LAZ file
            
        Returns:
            Hex string MD5 hash of file contents
        """
        md5 = hashlib.md5()
        with open(filepath, 'rb') as f:
            for chunk in iter(lambda: f.read(8192), b''):
                md5.update(chunk)
        return md5.hexdigest()
    
    def _get_config_fingerprint(self) -> str:
        """
        Create hash of PDAL config parameters for cache invalidation.
        
        If config changes, cache should be invalidated. This ensures that
        changing parameters (outlier multiplier, SMRF settings, etc.) will
        not use stale cached outputs.
        
        Returns:
            Hex string MD5 hash of config parameters
        """
        config_str = (
            f"outlier:{self.config.outlier_method}_{self.config.outlier_multiplier}|"
            f"smrf:{self.config.smrf_slope}_{self.config.smrf_window}_{self.config.smrf_threshold}|"
            f"hag:{self.config.hag_method}|"
            f"compression:{self.config.compression}"
        )
        return hashlib.md5(config_str.encode()).hexdigest()
    
    def _get_cache_path(self, input_hash: str) -> Path:
        """
        Compute cache file path given input hash.
        
        Cache key format: {input_hash}_{config_hash}.laz
        This ensures different configs use different cache entries.
        
        Args:
            input_hash: MD5 hash of input LAZ file
            
        Returns:
            Path to cache file
        """
        config_hash = self._get_config_fingerprint()
        cache_filename = f"{input_hash}_{config_hash}.laz"
        return self.cache_dir / cache_filename
    
    def get_cached_or_process(
        self,
        input_laz: Path,
        pipeline_generator: 'PDALPipelineGenerator',
        temp_dir: Path
    ) -> Path:
        """
        Return cached preprocessed LAZ if available, else process with PDAL and cache.
        
        Args:
            input_laz: Path to raw input LAZ file
            pipeline_generator: PDALPipelineGenerator instance to execute pipeline if needed
            temp_dir: Temporary directory for PDAL output
            
        Returns:
            Path to preprocessed (classified) LAZ file - either from cache or newly generated
        """
        input_hash = self._compute_file_hash(input_laz)
        cache_path = self._get_cache_path(input_hash)
        
        # Check if cached version exists
        if cache_path.exists():
            logger.info(f"  Cache HIT: {input_laz.name} → {cache_path.name}")
            self.hits += 1
            return cache_path
        
        # Cache miss: run PDAL pipeline
        logger.info(f"  Cache MISS: {input_laz.name} → processing with PDAL")
        self.misses += 1
        
        # Create temporary output for PDAL
        output_name = f"{input_laz.stem}_classified.laz"
        temp_output = temp_dir / output_name
        
        # Generate and execute pipeline
        pipeline_dict = pipeline_generator.generate_laz_to_classified_laz(
            input_laz,
            temp_output
        )
        pipeline_generator.execute_pipeline(pipeline_dict, input_laz, temp_output, temp_dir)
        
        # Copy result to cache
        shutil.copy2(temp_output, cache_path)
        logger.info(f"  Cached: {cache_path.name}")
        
        return cache_path
    
    def get_stats(self) -> Dict[str, Any]:
        """
        Get cache statistics.
        
        Returns:
            Dict with hits, misses, and hit rate
        """
        total = self.hits + self.misses
        hit_rate = (self.hits / total * 100) if total > 0 else 0
        return {
            "cache_hits": self.hits,
            "cache_misses": self.misses,
            "cache_total": total,
            "cache_hit_rate": f"{hit_rate:.1f}%"
        }


class PDALPipelineGenerator:
    """Generate and execute PDAL processing pipelines for LiDAR preprocessing."""
    
    def __init__(self, config: PDALConfig, cache_manager: Optional[PDALCacheManager] = None):
        """
        Initialize pipeline generator.
        
        Args:
            config: PDALConfig with preprocessing parameters
            cache_manager: Optional PDALCacheManager for caching preprocessed LAZ files
        """
        self.config = config
        self.cache_manager = cache_manager
    
    def generate_laz_to_classified_laz(
        self,
        input_laz: Path,
        output_laz: Path
    ) -> Dict[str, Any]:
        """
        Generate PDAL JSON pipeline for LAZ preprocessing.
        
        Pipeline steps:
        1. readers.las - Read LAZ file
        2. filters.outlier - Flag statistical outliers (classification 7)
        3. filters.range - Drop the flagged outliers before classification
        4. filters.smrf - Ground classification (Simple Morphological Filter)
        5. filters.hag_delaunay - Compute height-above-ground
        6. writers.las - Write classified LAZ
        
        Args:
            input_laz: Path to input LAZ file
            output_laz: Path for output LAZ file
            
        Returns:
            Dict representing PDAL JSON pipeline
        """
        pipeline = [
            {
                "type": "readers.las",
                "filename": str(input_laz)
            },
            {
                "type": "filters.outlier",
                "method": self.config.outlier_method,
                "multiplier": self.config.outlier_multiplier
            },
            {
                # filters.outlier only *flags* outliers as classification 7; it
                # does not remove them. Drop them here so they do not bias SMRF
                # ground classification or HAG computation.
                "type": "filters.range",
                "limits": "Classification![7:7]"
            },
            {
                "type": "filters.smrf",
                "slope": self.config.smrf_slope,
                "window": self.config.smrf_window,
                "threshold": self.config.smrf_threshold
            },
            {
                "type": f"filters.hag_{self.config.hag_method}",
            },
            {
                "type": "writers.las",
                "filename": str(output_laz),
                "compression": self.config.compression,
                "forward": ["all"]
            }
        ]
        
        return {"pipeline": pipeline}
    
    def execute_pipeline(
        self,
        pipeline_dict: Dict[str, Any],
        input_laz: Path,
        output_laz: Path,
        temp_dir: Optional[Path] = None
    ) -> str:
        """
        Execute PDAL pipeline and return output path.
        
        Args:
            pipeline_dict: PDAL pipeline dictionary
            input_laz: Input LAZ file (for error reporting)
            output_laz: Expected output LAZ file
            temp_dir: Optional temporary directory for intermediate files
            
        Returns:
            Path to output LAZ file
            
        Raises:
            RuntimeError: If pipeline execution fails
        """
        try:
            logger.info(f"Executing PDAL pipeline for {input_laz.name}...")
            
            # Create PDAL pipeline
            pipeline_json = json.dumps(pipeline_dict)
            pipeline = pdal.Pipeline(pipeline_json)
            
            # Execute
            pipeline.execute()
            
            # Verify output exists
            if not Path(output_laz).exists():
                raise RuntimeError(f"Pipeline did not create output: {output_laz}")
            
            logger.info(f"  Pipeline complete: {output_laz.name}")
            return str(output_laz)
        
        except Exception as e:
            logger.error(f"PDAL pipeline failed for {input_laz.name}: {e}")
            raise RuntimeError(
                f"PDAL pipeline execution failed for {input_laz.name}: {str(e)}"
            ) from e
    
    def preprocess_tile(
        self,
        input_laz: Path,
        temp_dir: Path
    ) -> Path:
        """
        Preprocess a single LiDAR tile from input to classified output.
        
        Checks cache first; if available, returns cached preprocessed LAZ.
        If not cached, runs PDAL pipeline and caches the result.
        
        Args:
            input_laz: Path to raw LAZ input
            temp_dir: Directory for temporary/output files and cache
            
        Returns:
            Path to classified LAZ with ground classification and HAG
            
        Raises:
            RuntimeError: If preprocessing fails
        """
        # Check cache first if available
        if self.cache_manager is not None:
            return self.cache_manager.get_cached_or_process(
                input_laz,
                self,
                temp_dir
            )
        
        # No cache: run PDAL directly
        output_name = f"{input_laz.stem}_classified.laz"
        output_laz = temp_dir / output_name
        
        # Generate pipeline
        pipeline_dict = self.generate_laz_to_classified_laz(input_laz, output_laz)
        
        # Execute
        result = self.execute_pipeline(pipeline_dict, input_laz, output_laz, temp_dir)
        
        return Path(result)
