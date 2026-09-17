"""Shared brand constants and visual helpers for FrySharp (C# Code Studio).

Used by asset generators, Inno Setup wizard branding, and DMG packaging.
"""

from __future__ import annotations

import os
import pathlib
import subprocess
import sys
from PIL import Image, ImageFont

SCRIPT_DIR = pathlib.Path(__file__).resolve().parent
PACKAGING_DIR = SCRIPT_DIR.parent
PLUGIN_ROOT = PACKAGING_DIR.parent
RUNNER_DIR = PLUGIN_ROOT / "Runner"
MASTER_SVG = RUNNER_DIR / "Assets" / "Appiconlogo.svg"
MASTER_PNG = RUNNER_DIR / "Assets" / "app-logo.png"

# Identity
APP_NAME = "FrySharp"
APP_TITLE = "FryPDF C# Code Studio"
TAGLINE = "Interactive C# Code Studio"
SUBTITLE = "High-Performance Roslyn Script Studio"
COMPANY = "Code Fry Dev"
URL = "codefrydev.in"
COPYRIGHT = "Copyright (C) 2026 Code Fry Dev"

# Brand palette
NAVY = (13, 43, 69)          # #0D2B45
NAVY_DEEP = (8, 28, 46)      # #081C2E
BLUE = (26, 61, 92)          # #1A3D5C
ACCENT_CYAN = (78, 175, 203) # #4EAFCB
WHITE = (255, 255, 255)
MUTED = (159, 179, 200)

_SFNS = "/System/Library/Fonts/SFNS.ttf"
_HELVETICA_NEUE = "/System/Library/Fonts/HelveticaNeue.ttc"
_HELVETICA_NEUE_INDEX = {"Regular": 0, "Medium": 0, "Semibold": 1, "Bold": 1}


def font(size: int, weight: str = "Regular") -> ImageFont.FreeTypeFont:
    """Load a system font at `size` px, preferring SF Pro at the requested weight."""
    try:
        f = ImageFont.truetype(_SFNS, size)
        f.set_variation_by_name(weight)
        return f
    except Exception:
        pass
    try:
        return ImageFont.truetype(
            _HELVETICA_NEUE, size, index=_HELVETICA_NEUE_INDEX.get(weight, 0)
        )
    except Exception:
        return ImageFont.load_default(size)


def ensure_master_png() -> pathlib.Path:
    """Ensure the 1024x1024 master PNG exists, generating from SVG via qlmanage if needed."""
    if MASTER_PNG.exists() and MASTER_PNG.stat().st_size > 1000:
        return MASTER_PNG

    MASTER_PNG.parent.mkdir(parents=True, exist_ok=True)
    if MASTER_SVG.exists():
        tmp_dir = pathlib.Path("/tmp/frysharp-render")
        tmp_dir.mkdir(parents=True, exist_ok=True)
        # Use macOS qlmanage for native high-res SVG rasterization
        res = subprocess.run(
            ["qlmanage", "-t", "-s", "1024", "-o", str(tmp_dir), str(MASTER_SVG)],
            capture_output=True,
            text=True,
        )
        rendered = tmp_dir / f"{MASTER_SVG.name}.png"
        if rendered.exists():
            img = Image.open(rendered).convert("RGBA")
            img.save(MASTER_PNG, "PNG", optimize=True)
            return MASTER_PNG

    # Fallback if qlmanage unavailable: generate clean circular badge with C# logo
    fallback = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    from PIL import ImageDraw
    draw = ImageDraw.Draw(fallback)
    draw.ellipse((32, 32, 992, 992), fill=NAVY)
    f = font(420, "Bold")
    draw.text((512, 512), "C#", font=f, fill=ACCENT_CYAN, anchor="mm")
    fallback.save(MASTER_PNG, "PNG")
    return MASTER_PNG


def load_master() -> Image.Image:
    """Load the 1024x1024 RGBA master logo."""
    png_path = ensure_master_png()
    return Image.open(png_path).convert("RGBA")


def fit(image: Image.Image, box: int) -> Image.Image:
    """Scale `image` so its longest side is `box` px."""
    scale = box / max(image.size)
    return image.resize(
        (max(1, round(image.width * scale)), max(1, round(image.height * scale))),
        Image.LANCZOS,
    )
