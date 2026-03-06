import os
import numpy as np
import imageio.v3 as iio

png_path = "heightmap_1km.png"
raw_path = "heightmap_1km_513.r16"

img = iio.imread(png_path)
if img.ndim == 3 and img.shape[2] == 1:
    img = img[:, :, 0]
img = np.asarray(img, dtype=np.uint16)

h, w = img.shape
print("PNG shape:", (h, w), "dtype:", img.dtype)

target = 513
ys = np.linspace(0, h - 1, target)
xs = np.linspace(0, w - 1, target)

x0 = np.floor(xs).astype(int)
x1 = np.clip(x0 + 1, 0, w - 1)
y0 = np.floor(ys).astype(int)
y1 = np.clip(y0 + 1, 0, h - 1)

wx = (xs - x0).astype(np.float32)
wy = (ys - y0).astype(np.float32)

out = np.empty((target, target), dtype=np.float32)
for i, (yy0, yy1, wyy) in enumerate(zip(y0, y1, wy)):
    row0 = (1 - wx) * img[yy0, x0] + wx * img[yy0, x1]
    row1 = (1 - wx) * img[yy1, x0] + wx * img[yy1, x1]
    out[i, :] = (1 - wyy) * row0 + wyy * row1

out16 = np.clip(np.round(out), 0, 65535).astype(np.uint16)

# Write raw (little-endian uint16) — correct for Windows
out16.tofile(raw_path)

print("Saved:", raw_path)
print("RAW bytes:", os.path.getsize(raw_path))
print("Expected bytes:", target * target * 2)