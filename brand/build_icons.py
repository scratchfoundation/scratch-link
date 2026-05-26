"""Rebuild Windows .ico + MSIX .png assets from the Alux brand SVG.

Source of truth:
    brand/labs-l.svg

Outputs (overwrites in place; all paths relative to repo root):
    scratch-link-win/aluxlabs-link.ico        app icon, 16..256 sizes
    scratch-link-win/aluxlabs-link-tray.ico   tray icon, 16/24/32
    scratch-link-win-msix/Images/*.png       MSIX tile/splash/store/lock assets

How to run (from repo root):
    pip install Pillow                   # one-time
    python brand/build_icons.py

When to re-run:
    Whenever brand/labs-l.svg changes, or when a new MSIX asset slot needs
    to be filled. Generated files are committed alongside the source so
    that builds work without running this script.

How it works:
    labs-l.svg is a thin SVG wrapper around a single base64-encoded PNG
    (non-square). We extract that PNG once, then for each target
    slot fit it preserving aspect ratio onto a transparent canvas of the
    required size. No external SVG renderer (cairosvg, rsvg, etc.) is
    needed - Pillow is the only dependency.

Editing the targets:
    To add or change output sizes, edit ICO_TARGETS / PNG_TARGETS below.
    ICO sizes must match the slots the existing aluxlabs-link*.ico files
    advertise; MSIX PNG dimensions are dictated by Windows
    (Square44x44Logo.scale-200 must be 88x88, etc.).
"""

from __future__ import annotations

import base64
import re
from io import BytesIO
from pathlib import Path

from PIL import Image

REPO = Path(__file__).resolve().parent.parent
SVG = REPO / "brand" / "labs-l.svg"

WIN = REPO / "scratch-link-win"
MSIX = REPO / "scratch-link-win-msix" / "Images"

ICO_TARGETS = {
    WIN / "aluxlabs-link.ico": [16, 24, 32, 48, 64, 96, 128, 256],
    WIN / "aluxlabs-link-tray.ico": [16, 24, 32],
}

PNG_TARGETS = {
    MSIX / "LockScreenLogo.scale-200.png": (48, 48),
    MSIX / "SplashScreen.scale-200.png": (1240, 600),
    MSIX / "Square150x150Logo.scale-200.png": (300, 300),
    MSIX / "Square44x44Logo.scale-200.png": (88, 88),
    MSIX / "Square44x44Logo.targetsize-24_altform-unplated.png": (24, 24),
    MSIX / "StoreLogo.png": (50, 50),
    MSIX / "Wide310x150Logo.scale-200.png": (620, 300),
}


def extract_source() -> Image.Image:
    svg_text = SVG.read_text(encoding="utf-8")
    match = re.search(r'xlink:href="data:image/png;base64,([^"]+)"', svg_text)
    if not match:
        raise SystemExit("Could not find embedded base64 PNG in SVG.")
    png_bytes = base64.b64decode(match.group(1))
    img = Image.open(BytesIO(png_bytes)).convert("RGBA")
    print(f"source image: {img.size}, mode={img.mode}")
    return img


def fit_padded(src: Image.Image, target: tuple[int, int]) -> Image.Image:
    """Scale src to fit inside `target` preserving aspect, center on transparent."""
    tw, th = target
    sw, sh = src.size
    scale = min(tw / sw, th / sh)
    nw, nh = max(1, round(sw * scale)), max(1, round(sh * scale))
    resized = src.resize((nw, nh), Image.LANCZOS)
    canvas = Image.new("RGBA", target, (0, 0, 0, 0))
    canvas.paste(resized, ((tw - nw) // 2, (th - nh) // 2), resized)
    return canvas


def main() -> None:
    src = extract_source()

    for path, sizes in ICO_TARGETS.items():
        biggest = max(sizes)
        base = fit_padded(src, (biggest, biggest))
        # Pillow's ICO writer accepts a list of (w,h) sizes; it down-samples
        # `base` for each entry. Providing pre-rendered frames isn't supported
        # directly, but Lanczos downscaling from the 256x256 master is fine.
        base.save(path, format="ICO", sizes=[(s, s) for s in sizes])
        print(f"wrote {path.relative_to(REPO)} with sizes {sorted(sizes)}")

    for path, target in PNG_TARGETS.items():
        out = fit_padded(src, target)
        out.save(path, format="PNG", optimize=True)
        print(f"wrote {path.relative_to(REPO)} {target}")


if __name__ == "__main__":
    main()
