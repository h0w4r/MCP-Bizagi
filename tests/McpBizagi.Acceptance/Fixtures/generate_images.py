"""Rebuild our tiny authored raster corpus with Pillow 11.0.0; never uses vendor/user images."""
import hashlib
import json
from pathlib import Path

from PIL import Image, PngImagePlugin

root = Path(__file__).parent / "images"
root.mkdir(exist_ok=True)
first = Image.new("RGBA", (64, 48))
second = Image.new("RGBA", (64, 48))
for y in range(48):
    for x in range(64):
        # Distinct quadrants and partial alpha catch orientation, alpha, scaling and frame-selection mistakes.
        first.putpixel((x, y), ((220, 30, 40, 255) if x < 32 else (20, 170, 90, 128)) if y < 24 else (20, 60, 210, 0))
        second.putpixel((x, y), (230, 180, 10, 255) if x < 32 else (90, 20, 170, 255))
metadata = PngImagePlugin.PngInfo()
metadata.add_text("Author", "h0w4r")
metadata.add_text("Purpose", "MCP-Bizagi independently authored native image acceptance")
first.save(root / "alpha.png", pnginfo=metadata)
first.convert("RGB").save(root / "opaque.bmp")
second.convert("RGB").save(root / "opaque.jpg", quality=95, subsampling=0)
second.convert("P").save(root / "palette.gif")
first.save(root / "multipage.tiff", save_all=True, append_images=[second], compression="raw")
first.convert("RGB").save(root / "animated.gif", save_all=True, append_images=[second.convert("RGB")], duration=[100, 150], loop=0)
manifest = []
for file in sorted(root.iterdir()):
    if file.suffix == ".json":
        continue
    with Image.open(file) as image:
        frames = []
        for index in range(getattr(image, "n_frames", 1)):
            image.seek(index)
            rgba = image.convert("RGBA")
            frames.append({"index": index, "width": rgba.width, "height": rgba.height,
                "pixelSha256": hashlib.sha256(rgba.tobytes("raw", "BGRA")).hexdigest(),
                "hasTransparency": rgba.getextrema()[3][0] < 255})
        manifest.append({"fileName": file.name, "sha256": hashlib.sha256(file.read_bytes()).hexdigest(), "frames": frames})
(root / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
