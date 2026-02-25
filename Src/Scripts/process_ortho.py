#!/usr/bin/env python3
"""
process_ortho.py
Clip + resample orthophoto tiles to the study bbox, export a Unity-ready texture.

Requires:
  pip install rasterio numpy imageio shapely
"""

import argparse
import json
import os
from glob import glob

import numpy as np
import rasterio
from rasterio.merge import merge
from rasterio.transform import from_bounds
from rasterio.warp import reproject, Resampling
from rasterio.windows import from_bounds as window_from_bounds
import imageio.v3 as iio
from shapely.geometry import box as shapely_box


def find_overlapping_tiles(tif_paths: list[str], bbox) -> list[str]:
    """Return only those GeoTIFF paths whose extent overlaps the bbox."""
    xmin, ymin, xmax, ymax = bbox
    hits = []
    for p in tif_paths:
        with rasterio.open(p) as src:
            b = src.bounds
            # Check overlap
            if b.right > xmin and b.left < xmax and b.top > ymin and b.bottom < ymax:
                hits.append(p)
                print(f"  [overlap] {os.path.basename(p)}  bounds={b}")
            else:
                print(f"  [skip]    {os.path.basename(p)}")
    return hits


def mosaic_clip_resample(tif_paths: list[str], bbox, out_png: str, out_size: int = 2048) -> dict:
    """
    Open overlapping tiles, merge them in memory, clip to bbox,
    and resample to out_size x out_size. Returns metadata dict.
    """
    xmin, ymin, xmax, ymax = bbox

    # Open all datasets
    datasets = [rasterio.open(p) for p in tif_paths]

    try:
        # Merge overlapping tiles into a single mosaic (in memory)
        mosaic, mosaic_transform = merge(datasets, bounds=(xmin, ymin, xmax, ymax))
        crs = datasets[0].crs
    finally:
        for ds in datasets:
            ds.close()

    # mosaic shape: (bands, H, W)
    bands, h, w = mosaic.shape
    print(f"  Mosaic clipped: {bands} bands, {w}x{h} px")
    if bands < 3:
        raise ValueError(f"Expected RGB (>=3 bands), got {bands} band(s).")

    # Prepare destination array
    dst = np.zeros((3, out_size, out_size), dtype=np.float32)
    dst_transform = from_bounds(xmin, ymin, xmax, ymax, out_size, out_size)

    # Resample each RGB band
    for b in range(3):
        reproject(
            source=mosaic[b].astype(np.float32),
            destination=dst[b],
            src_transform=mosaic_transform,
            src_crs=crs,
            dst_transform=dst_transform,
            dst_crs=crs,
            resampling=Resampling.bilinear,
        )

    # Convert to uint8 RGB image (H, W, 3)
    rgb = np.stack([dst[0], dst[1], dst[2]], axis=-1)
    rgb = np.clip(rgb, 0, 255).astype(np.uint8)

    # Save PNG
    os.makedirs(os.path.dirname(out_png) or ".", exist_ok=True)
    iio.imwrite(out_png, rgb)

    return {
        "texture_px": [out_size, out_size],
        "bands": 3,
        "dtype": "uint8",
        "source_tiles": [os.path.basename(p) for p in tif_paths],
        "mosaic_native_px": [w, h],
    }


def main():
    ap = argparse.ArgumentParser(description="Clip & resample orthophoto tiles for Unity.")
    ap.add_argument("--ortho_dir", required=True, help="Folder with orthophoto GeoTIFF tiles")
    ap.add_argument("--metadata", required=True, help="Path to metadata_1km.json")
    ap.add_argument("--out_dir", required=True, help="Output folder")
    ap.add_argument("--out_size", type=int, default=2048, help="Output texture size (power of two)")
    ap.add_argument("--update_metadata", action="store_true", help="Write texture info back into metadata JSON")
    args = ap.parse_args()

    # Load bbox from metadata
    with open(args.metadata, "r", encoding="utf-8") as f:
        meta = json.load(f)

    bbox_d = meta["bbox"]
    bbox = (bbox_d["xmin"], bbox_d["ymin"], bbox_d["xmax"], bbox_d["ymax"])
    print(f"Study area bbox: {bbox}")

    # Find candidate GeoTIFFs
    tif_paths = sorted(glob(os.path.join(args.ortho_dir, "**/*.tif"), recursive=True))
    tif_paths += sorted(glob(os.path.join(args.ortho_dir, "**/*.tiff"), recursive=True))
    print(f"Found {len(tif_paths)} .tif/.tiff files total")
    if not tif_paths:
        raise FileNotFoundError(f"No .tif/.tiff found under: {args.ortho_dir}")

    # Filter to only tiles that overlap
    overlap_paths = find_overlapping_tiles(tif_paths, bbox)
    if not overlap_paths:
        raise RuntimeError("No orthophoto tiles overlap the study area bbox!")
    print(f"\n{len(overlap_paths)} tile(s) overlap the study area.")

    # Mosaic + clip + resample
    os.makedirs(args.out_dir, exist_ok=True)
    out_png = os.path.join(args.out_dir, "ortho_texture_1km.png")
    print(f"\nMosaicing, clipping, and resampling to {args.out_size}x{args.out_size} ...")
    ortho_info = mosaic_clip_resample(overlap_paths, bbox, out_png, out_size=args.out_size)
    print(f"Saved: {out_png}")

    # Optionally update metadata
    if args.update_metadata:
        meta.setdefault("ortho", {})
        meta["ortho"].update(ortho_info)
        meta.setdefault("outputs", {})
        meta["outputs"]["ortho_png"] = "ortho_texture_1km.png"
        out_meta = os.path.join(args.out_dir, os.path.basename(args.metadata))
        with open(out_meta, "w", encoding="utf-8") as f:
            json.dump(meta, f, indent=2)
        print("Updated metadata:", out_meta)


if __name__ == "__main__":
    main()