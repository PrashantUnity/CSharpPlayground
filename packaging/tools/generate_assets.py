#!/usr/bin/env python3
"""Unified asset generation script for FrySharp (C# Code Studio).

Generates:
1. Master PNG (1024x1024)
2. Windows multi-res ICO (app-logo.ico)
3. macOS iconset and compiled AppIcon.icns
4. Inno Setup wizard artwork (wizard-large, wizard-small)
5. Windows MSIX package assets (Square150, Square44, Wide310, StoreLogo)
6. macOS DMG retina background (background.png, background@2x.png)
"""

from __future__ import annotations

import os
import pathlib
import shutil
import subprocess
import sys
from PIL import Image, ImageDraw, ImageFilter

import branding as b
import icoformat

# Paths
RUNNER_ASSETS = b.RUNNER_DIR / "Assets"
MACOS_DIR = b.PACKAGING_DIR / "macos"
MACOS_DMG_DIR = MACOS_DIR / "dmg"
WINDOWS_DIR = b.PACKAGING_DIR / "windows"
WINDOWS_BRANDING_DIR = WINDOWS_DIR / "branding"
WINDOWS_MSIX_ASSETS = WINDOWS_DIR / "msix" / "Assets"

# Inno Setup scales
WIZARD_LARGE_SCALES = (1.0, 2.0, 2.5)
WIZARD_LARGE_BASE = (164, 314)
WIZARD_SMALL_SIZES = (55, 110, 165)

# DMG Background dimensions
DMG_WIDTH, DMG_HEIGHT = 660, 440
DMG_HEADER_H = 84
DMG_RULE_H = 3
DMG_APP_XY = (170, 196)
DMG_APPLICATIONS_XY = (490, 196)
DMG_SURFACE_TOP = (238, 242, 247)
DMG_SURFACE_BOTTOM = (220, 228, 238)
DMG_PEDESTAL = (51, 65, 85)
DMG_CAPTION = (13, 43, 69)


def _vertical_gradient(size, top, bottom) -> Image.Image:
    width, height = size
    strip = Image.new("RGB", (1, height))
    pixels = strip.load()
    for y in range(height):
        t = y / max(1, height - 1)
        pixels[0, y] = tuple(round(top[i] + (bottom[i] - top[i]) * t) for i in range(3))
    return strip.resize((width, height), Image.BILINEAR)


# -------------------------------------------------------------
# 1. Windows .ico and Inno Setup Graphics
# -------------------------------------------------------------
def generate_windows_branding(master: Image.Image) -> None:
    # 1. Write app-logo.ico
    ico_out = RUNNER_ASSETS / "app-logo.ico"
    icoformat.write_ico(master, ico_out)
    print(f"  [Windows ICO] {ico_out} ({ico_out.stat().st_size:,} bytes)")

    # 2. Inno Setup Large Wizard panel
    WINDOWS_BRANDING_DIR.mkdir(parents=True, exist_ok=True)
    for scale in WIZARD_LARGE_SCALES:
        width, height = (round(v * scale) for v in WIZARD_LARGE_BASE)
        s = lambda v: max(1, round(v * scale))

        canvas = _vertical_gradient((width, height), b.NAVY_DEEP, b.NAVY).convert("RGBA")
        draw = ImageDraw.Draw(canvas)

        logo = b.fit(master, s(112))
        logo_center_y = s(112)
        canvas.alpha_composite(
            logo, ((width - logo.width) // 2, logo_center_y - logo.height // 2)
        )

        def centered(text, y, size, weight, fill):
            f = b.font(s(size), weight)
            w = draw.textlength(text, font=f)
            draw.text(((width - w) / 2, y), text, font=f, fill=fill)

        centered("FrySharp", s(176), 21, "Semibold", b.WHITE)
        draw.rectangle(
            [(width - s(36)) / 2, s(206), (width + s(36)) / 2, s(206) + s(3)],
            fill=b.ACCENT_CYAN,
        )
        centered("Interactive C# Studio", s(218), 9, "Regular", b.MUTED)
        centered(b.URL, height - s(24), 8, "Regular", b.MUTED)

        out_large = WINDOWS_BRANDING_DIR / f"wizard-large-{width}x{height}.png"
        canvas.convert("RGB").save(out_large, optimize=True)
        print(f"  [Inno Wizard Large] {out_large}")

    # 3. Inno Setup Small Header panel
    for size in WIZARD_SMALL_SIZES:
        supersample = 4
        big = size * supersample

        tile = Image.new("RGB", (big, big), b.WHITE)
        mask = Image.new("L", (big, big), 0)
        ImageDraw.Draw(mask).rounded_rectangle(
            (0, 0, big - 1, big - 1), radius=round(big * 0.22), fill=255
        )
        tile.paste(Image.new("RGB", (big, big), b.NAVY), mask=mask)

        logo = b.fit(master, round(big * 0.64))
        tile_rgba = tile.convert("RGBA")
        tile_rgba.alpha_composite(
            logo, ((big - logo.width) // 2, (big - logo.height) // 2)
        )
        tile_rgba.putalpha(mask)

        out_small = WINDOWS_BRANDING_DIR / f"wizard-small-{size}x{size}.png"
        tile_rgba.resize((size, size), Image.LANCZOS).save(out_small, optimize=True)
        print(f"  [Inno Wizard Small] {out_small}")


# -------------------------------------------------------------
# 2. Windows MSIX Tile Assets
# -------------------------------------------------------------
def generate_msix_assets(master: Image.Image) -> None:
    WINDOWS_MSIX_ASSETS.mkdir(parents=True, exist_ok=True)

    msix_specs = [
        ("Square150x150Logo.png", 150, 150, 0.72),
        ("Square150x150Logo.scale-200.png", 300, 300, 0.72),
        ("Square44x44Logo.png", 44, 44, 0.75),
        ("Square44x44Logo.scale-200.png", 88, 88, 0.75),
        ("Square44x44Logo.targetsize-24_altform-unplated.png", 24, 24, 0.85),
        ("Wide310x150Logo.png", 310, 150, 0.65),
        ("Wide310x150Logo.scale-200.png", 620, 300, 0.65),
        ("StoreLogo.png", 50, 50, 0.8),
        ("StoreLogo.scale-200.png", 100, 100, 0.8),
    ]

    for filename, width, height, scale_ratio in msix_specs:
        canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        max_box = min(width, height)
        logo = b.fit(master, round(max_box * scale_ratio))
        canvas.alpha_composite(
            logo, ((width - logo.width) // 2, (height - logo.height) // 2)
        )
        out_path = WINDOWS_MSIX_ASSETS / filename
        canvas.save(out_path, optimize=True)
        print(f"  [MSIX Asset] {out_path}")


# -------------------------------------------------------------
# 3. macOS .iconset and AppIcon.icns
# -------------------------------------------------------------
def generate_macos_icons(master: Image.Image) -> None:
    iconset_dir = MACOS_DIR / "AppIcon.iconset"
    iconset_dir.mkdir(parents=True, exist_ok=True)

    iconset_specs = [
        ("icon_16x16.png", 16),
        ("icon_16x16@2x.png", 32),
        ("icon_32x32.png", 32),
        ("icon_32x32@2x.png", 64),
        ("icon_128x128.png", 128),
        ("icon_128x128@2x.png", 256),
        ("icon_256x256.png", 256),
        ("icon_256x256@2x.png", 512),
        ("icon_512x512.png", 512),
        ("icon_512x512@2x.png", 1024),
    ]

    for name, size in iconset_specs:
        resized = master.resize((size, size), Image.LANCZOS)
        out_path = iconset_dir / name
        resized.save(out_path, "PNG", optimize=True)

    # Compile with macOS iconutil
    icns_out = MACOS_DIR / "AppIcon.icns"
    if shutil.which("iconutil"):
        res = subprocess.run(
            ["iconutil", "-c", "icns", str(iconset_dir), "-o", str(icns_out)],
            capture_output=True,
            text=True,
        )
        if res.returncode == 0:
            print(f"  [macOS ICNS] {icns_out} ({icns_out.stat().st_size:,} bytes)")
        else:
            print(f"  [macOS ICNS warning] iconutil failed: {res.stderr}")
    else:
        print("  [macOS ICNS warning] iconutil not found on PATH; skipping ICNS compile")


# -------------------------------------------------------------
# 4. macOS DMG Retina Background
# -------------------------------------------------------------
def _pedestals(scale: float) -> Image.Image:
    s = lambda v: round(v * scale)
    layer = Image.new(
        "RGBA", (round(DMG_WIDTH * scale), round(DMG_HEIGHT * scale)), (0, 0, 0, 0)
    )
    draw = ImageDraw.Draw(layer)
    for cx, _ in (DMG_APP_XY, DMG_APPLICATIONS_XY):
        draw.ellipse(
            [s(cx) - s(88), s(273) - s(15), s(cx) + s(88), s(273) + s(15)],
            fill=DMG_PEDESTAL + (88,),
        )
    return layer.filter(ImageFilter.GaussianBlur(s(11)))


def _arrow(draw: ImageDraw.ImageDraw, scale: float) -> None:
    s = lambda v: round(v * scale)
    cx, cy = s(330), s(190)
    half_w, shaft_h = s(46), s(13)
    head_w, head_h = s(30), s(36)

    draw.rounded_rectangle(
        [cx - half_w, cy - shaft_h // 2, cx + half_w - head_w, cy + shaft_h // 2],
        radius=shaft_h // 2,
        fill=b.ACCENT_CYAN,
    )
    draw.polygon(
        [
            (cx + half_w, cy),
            (cx + half_w - head_w, cy - head_h // 2),
            (cx + half_w - head_w, cy + head_h // 2),
        ],
        fill=b.ACCENT_CYAN,
    )


def render_dmg_background(master: Image.Image, scale: float) -> Image.Image:
    s = lambda v: round(v * scale)
    width, height = round(DMG_WIDTH * scale), round(DMG_HEIGHT * scale)

    canvas = Image.new("RGB", (width, height), DMG_SURFACE_TOP)
    canvas.paste(
        _vertical_gradient(
            (width, height - s(DMG_HEADER_H)), DMG_SURFACE_TOP, DMG_SURFACE_BOTTOM
        ),
        (0, s(DMG_HEADER_H)),
    )
    canvas.paste(Image.new("RGB", (width, s(DMG_HEADER_H)), b.NAVY), (0, 0))
    canvas = canvas.convert("RGBA")

    draw = ImageDraw.Draw(canvas)
    draw.rectangle(
        [0, s(DMG_HEADER_H) - s(DMG_RULE_H), width, s(DMG_HEADER_H)], fill=b.ACCENT_CYAN
    )

    badge = b.fit(master, s(56))
    canvas.alpha_composite(badge, (s(28), s(14)))

    wordmark = b.font(s(26), "Semibold")
    draw.text((s(100), s(20)), "FrySharp", font=wordmark, fill=b.WHITE)
    tagline = b.font(s(12), "Regular")
    draw.text((s(101), s(54)), "Interactive C# Code Studio", font=tagline, fill=b.MUTED)

    canvas.alpha_composite(_pedestals(scale))
    _arrow(ImageDraw.Draw(canvas), scale)

    caption = b.font(s(15), "Semibold")
    text = "Drag FrySharp to Applications"
    draw = ImageDraw.Draw(canvas)
    draw.text(
        (s(330) - draw.textlength(text, font=caption) / 2, s(316)),
        text,
        font=caption,
        fill=DMG_CAPTION,
    )

    return canvas.convert("RGB")


def generate_dmg_backgrounds(master: Image.Image) -> None:
    MACOS_DMG_DIR.mkdir(parents=True, exist_ok=True)
    for scale, name in ((1.0, "background.png"), (2.0, "background@2x.png")):
        path = MACOS_DMG_DIR / name
        image = render_dmg_background(master, scale)
        image.save(path, optimize=True)
        print(f"  [DMG Background] {path} ({image.width}x{image.height})")


def main() -> int:
    print("=== Generating FrySharp Branding & Packaging Assets ===")
    master = b.load_master()
    print(f"Loaded master logo: {b.MASTER_PNG} ({master.width}x{master.height})")

    generate_windows_branding(master)
    generate_msix_assets(master)
    generate_macos_icons(master)
    generate_dmg_backgrounds(master)

    print("\n✨ All FrySharp packaging artwork generated successfully!")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
