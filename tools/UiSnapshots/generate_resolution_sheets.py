#!/usr/bin/env python3
"""Generates multi-resolution contact sheets for all 5 C# Code Studio views across 7 screen resolutions:
1280x720 (720p HD)
1366x768 (Budget laptop)
1440x900 (MacBook Air 16:10)
1536x864 (1080p @ 125% scaling)
1920x1080 (1080p FHD baseline)
2560x1440 (1440p QHD / 2K)
3840x2160 (4K UHD)
"""
import os
import subprocess
import sys
from PIL import Image, ImageDraw, ImageFont

RESOLUTIONS = [
    ("720p_HD", 1280, 720),
    ("768p_Laptop", 1366, 768),
    ("900p_MacBook", 1440, 900),
    ("864p_Scaled", 1536, 864),
    ("1080p_FHD", 1920, 1080),
    ("1440p_QHD", 2560, 1440),
    ("2160p_4K", 3840, 2160)
]

VIEW_CONFIGS = [
    ("hub_templates", ["hub", "--templates", "--name", "hub_templates"]),
    ("blind75_details", ["blind75", "--details", "1", "--name", "blind75_details"]),
    ("docs", ["docs", "--name", "docs"]),
    ("notebook", ["notebook", "1", "--name", "notebook"]),
    ("studio_results", ["studio", "1", "--run", "--panel", "results", "--name", "studio_results"]),
    ("studio_right_dock", ["studio", "1", "--run", "--panel", "results", "--dock-right", "--name", "studio_right_dock"])
]

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_DIR = os.path.dirname(os.path.dirname(BASE_DIR))
OUT_DIR = os.path.join(BASE_DIR, "out", "resolutions")
BIN_DLL = os.path.join(BASE_DIR, "bin", "Debug", "net10.0", "UiSnapshots.dll")

def capture_snapshots():
    os.makedirs(OUT_DIR, exist_ok=True)
    for res_name, w, h in RESOLUTIONS:
        res_dir = os.path.join(OUT_DIR, res_name)
        os.makedirs(res_dir, exist_ok=True)
        for view_key, args in VIEW_CONFIGS:
            cmd = ["dotnet", BIN_DLL] + args + ["--width", str(w), "--height", str(h), "--out", res_dir]
            print(f"Capturing {view_key} at {w}x{h} ({res_name})...")
            try:
                subprocess.run(cmd, cwd=REPO_DIR, check=True)
            except Exception as e:
                print(f"Error capturing {view_key} at {res_name}: {e}")

def create_contact_sheet(view_name, output_path):
    tiles = []
    for res_name, w, h in RESOLUTIONS:
        img_path = os.path.join(OUT_DIR, res_name, f"{view_name}.png")
        if not os.path.exists(img_path):
            alt = os.path.join(OUT_DIR, res_name, f"{view_name}_1.png")
            if os.path.exists(alt):
                img_path = alt
            else:
                continue
        im = Image.open(img_path).convert("RGB")
        target_width = 800
        target_height = int(im.height * (target_width / im.width))
        im_thumb = im.resize((target_width, target_height), Image.Resampling.LANCZOS)
        label = f"{res_name}: {w}x{h}"
        tiles.append((label, im_thumb))

    if not tiles:
        print(f"No tiles found for {view_name}")
        return

    tile_w = 800
    tile_h = max(im.height for _, im in tiles) + 36
    columns = 2
    rows = (len(tiles) + columns - 1) // columns

    sheet_w = columns * (tile_w + 20) + 20
    sheet_h = rows * tile_h + 48
    sheet = Image.new("RGB", (sheet_w, sheet_h), (20, 20, 24))
    draw = ImageDraw.Draw(sheet)

    draw.text((24, 14), f"C# Code Studio - {view_name.upper()} Across All 7 Resolutions (720p HD to 4K UHD)", fill=(255, 214, 0))

    for idx, (label, im) in enumerate(tiles):
        col = idx % columns
        row = idx // columns
        x = 20 + col * (tile_w + 20)
        y = 48 + row * tile_h

        draw.text((x + 4, y + 4), label, fill=(180, 200, 255))
        sheet.paste(im, (x, y + 26))
        draw.rectangle([x, y + 26, x + im.width, y + 26 + im.height], outline=(80, 80, 90), width=1)

    sheet.save(output_path)
    print(f"Created contact sheet: {output_path}")

if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "capture":
        capture_snapshots()
    for view_key, _ in VIEW_CONFIGS:
        sheet_out = os.path.join(OUT_DIR, f"{view_key}_responsive_sheet.png")
        create_contact_sheet(view_key, sheet_out)
