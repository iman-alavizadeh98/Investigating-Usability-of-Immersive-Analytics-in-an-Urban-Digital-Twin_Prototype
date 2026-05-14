"""PDAL pipeline generation and execution for LiDAR preprocessing."""

import json
import logging
from pathlib import Path
from typing import Any, Dict, Optional
import tempfile

import pdal

from .config import PDALConfig

logger = logging.getLogger(__name__)


class PDALPipelineGenerator:
    """Generate and execute PDAL processing pipelines for LiDAR preprocessing."""
    
    def __init__(self, config: PDALConfig):
        """
        Initialize pipeline generator.
        
        Args:
            config: PDALConfig with preprocessing parameters
        """
        self.config = config
    
    def generate_laz_to_classified_laz(
        self,
        input_laz: Path,
        output_laz: Path
    ) -> Dict[str, Any]:
        """
        Generate PDAL JSON pipeline for LAZ preprocessing.
        
        Pipeline steps:
        1. readers.las - Read LAZ file
        2. filters.outlier - Remove statistical outliers
        3. filters.smrf - Ground classification (Simple Morphological Filter)
        4. filters.hag_delaunay - Compute height-above-ground
        5. writers.las - Write classified LAZ
        
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
        
        Args:
            input_laz: Path to raw LAZ input
            temp_dir: Directory for temporary/output files
            
        Returns:
            Path to classified LAZ with ground classification and HAG
            
        Raises:
            RuntimeError: If preprocessing fails
        """
        # Create output path in temp directory
        output_name = f"{input_laz.stem}_classified.laz"
        output_laz = temp_dir / output_name
        
        # Generate pipeline
        pipeline_dict = self.generate_laz_to_classified_laz(input_laz, output_laz)
        
        # Execute
        result = self.execute_pipeline(pipeline_dict, input_laz, output_laz, temp_dir)
        
        return Path(result)
