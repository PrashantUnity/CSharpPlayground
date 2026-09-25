#!/usr/bin/env python3
"""Look closely at UiSnapshots PNGs: zoom into a region, overlay a coordinate grid, diff two snapshots, tile several.

Needs Pillow (`pip install pillow`). Every command prints the path of the image it wrote, so you can open it next.

  python3 tools/UiSnapshots/images.py grid    out/blind75.png [--step 100]
  python3 tools/UiSnapshots/images.py zoom    out/blind75.png X Y WIDTH HEIGHT [--scale 2]
  python3 tools/UiSnapshots/images.py compare before.png after.png
  python3 tools/UiSnapshots/images.py sheet   out/p1_v1_s*.png [--columns 2] [--width 700]
  python3 tools/UiSnapshots/images.py info    out/blind75.png

Coordinates are image pixels from the top-left; they equal window pixels, so a point read off `grid` can go straight
into `--hover x,y`. Output files land next to the first input with a suffix (_grid, _zoom, _diff, sheet.png) unless
-o/--output says otherwise.
"""
import argparse
import glob
import os
import sys

try:
    from PIL import Image, ImageChops, ImageDraw
except ImportError:
    sys.exit("images.py needs Pillow: pip install pillow")

YELLOW = (255, 214, 0)
RED = (255, 64, 64)


def _output(path, suffix, explicit):
    if explicit:
        return explicit
    root, _ = os.path.splitext(path)
    return f"{root}_{suffix}.png"


def _label(draw, xy, text, fill=YELLOW):
    x, y = xy
    draw.rectangle([x, y, x + 7 * len(text) + 4, y + 12], fill=(0, 0, 0))
    draw.text((x + 2, y), text, fill=fill)


def grid(args):
    """Faint lines every --step pixels, labelled with their coordinate: read off where things are."""
    image = Image.open(args.image).convert("RGB")
    draw = ImageDraw.Draw(image)
    for x in range(0, image.width, args.step):
        draw.line([(x, 0), (x, image.height)], fill=(255, 0, 255), width=1)
        _label(draw, (x + 2, 2), str(x))
    for y in range(0, image.height, args.step):
        draw.line([(0, y), (image.width, y)], fill=(0, 255, 255), width=1)
        _label(draw, (2, y + 2), str(y))
    path = _output(args.image, "grid", args.output)
    image.save(path)
    print(path)


def zoom(args):
    """Crop a region and enlarge it, to read small text or check alignment to the pixel."""
    image = Image.open(args.image).convert("RGB")
    box = (args.x, args.y, min(image.width, args.x + args.width), min(image.height, args.y + args.height))
    region = image.crop(box)
    region = region.resize((region.width * args.scale, region.height * args.scale), Image.Resampling.NEAREST if args.pixelated else Image.Resampling.LANCZOS)
    path = _output(args.image, f"zoom_{args.x}_{args.y}", args.output)
    region.save(path)
    print(path)


def compare(args):
    """Before and after side by side, with every changed area boxed in red; prints where and how much changed."""
    before = Image.open(args.before).convert("RGB")
    after = Image.open(args.after).convert("RGB")
    width, height = max(before.width, after.width), max(before.height, after.height)
    padded = []
    for image in (before, after):
        canvas = Image.new("RGB", (width, height), (0, 0, 0))
        canvas.paste(image, (0, 0))
        padded.append(canvas)

    mask = ImageChops.difference(padded[0], padded[1]).convert("L").point(lambda v: 255 if v > args.threshold else 0)
    changed = mask.histogram()[255]  # the mask is only 0 or 255
    total = width * height
    boxes = _changed_boxes(mask, args.cell)

    # Wide screenshots go one above the other, so the pair stays readable when a viewer shrinks it to fit.
    stacked = args.layout == "stack" or (args.layout == "auto" and width > height)
    step = (0, height + 28) if stacked else (width + 10, 0)
    sheet = Image.new("RGB", (width + step[0], height + step[1] + 18), (40, 40, 40))
    draw = ImageDraw.Draw(sheet)
    for index, (image, label) in enumerate(((padded[0], f"before: {os.path.basename(args.before)}"),
                                            (padded[1], f"after: {os.path.basename(args.after)}"))):
        dx, dy = step[0] * index, step[1] * index
        sheet.paste(image, (dx, dy + 18))
        draw.text((dx + 4, dy + 3), label, fill=YELLOW)
        for left, top, right, bottom in boxes:
            draw.rectangle([left + dx, top + dy + 18, right + dx, bottom + dy + 18], outline=RED, width=2)

    path = _output(args.after, "diff", args.output)
    sheet.save(path)
    print(path)
    if changed == 0:
        print("identical")
    else:
        print(f"{changed} of {total} pixels changed ({100 * changed / total:.2f}%) in {len(boxes)} area(s), largest first:")
        boxes.sort(key=lambda b: (b[2] - b[0]) * (b[3] - b[1]), reverse=True)
        for left, top, right, bottom in boxes[:12]:
            print(f"  x {left}-{right}, y {top}-{bottom}")
        if len(boxes) > 12:
            print(f"  ... and {len(boxes) - 12} smaller ones (all boxed in the image)")


def _changed_boxes(mask, cell):
    """Groups changed pixels into rectangles: mark grid cells that changed, then merge touching cells."""
    columns, rows = (mask.width + cell - 1) // cell, (mask.height + cell - 1) // cell
    hot = set()
    for row in range(rows):
        for column in range(columns):
            box = (column * cell, row * cell, min(mask.width, (column + 1) * cell), min(mask.height, (row + 1) * cell))
            if mask.crop(box).getbbox():
                hot.add((column, row))
    boxes, seen = [], set()
    for start in sorted(hot):
        if start in seen:
            continue
        stack, members = [start], []
        seen.add(start)
        while stack:
            column, row = stack.pop()
            members.append((column, row))
            for neighbour in ((column + 1, row), (column - 1, row), (column, row + 1), (column, row - 1)):
                if neighbour in hot and neighbour not in seen:
                    seen.add(neighbour)
                    stack.append(neighbour)
        left = min(c for c, _ in members) * cell
        top = min(r for _, r in members) * cell
        right = min(mask.width, (max(c for c, _ in members) + 1) * cell) - 1
        bottom = min(mask.height, (max(r for _, r in members) + 1) * cell) - 1
        # Shrink each group's box to the pixels that actually changed.
        tight = mask.crop((left, top, right + 1, bottom + 1)).getbbox()
        if tight:
            boxes.append((left + tight[0], top + tight[1], left + tight[2] - 1, top + tight[3] - 1))
    return boxes


def sheet(args):
    """Tile images (e.g. visualizer steps) into one, each labelled with its file name."""
    paths = []
    for pattern in args.images:
        paths.extend(sorted(glob.glob(pattern)) or [pattern])
    paths = [p for p in paths if os.path.isfile(p)]
    if not paths:
        sys.exit("no images matched")

    tiles = []
    for path in paths:
        image = Image.open(path).convert("RGB")
        image.thumbnail((args.width, args.width * 4))
        tiles.append((os.path.basename(path), image))
    columns = max(1, min(args.columns, len(tiles)))
    rows = (len(tiles) + columns - 1) // columns
    tile_height = max(image.height for _, image in tiles) + 16
    canvas = Image.new("RGB", (columns * (args.width + 8), rows * tile_height), (24, 24, 24))
    draw = ImageDraw.Draw(canvas)
    for index, (name, image) in enumerate(tiles):
        x, y = (index % columns) * (args.width + 8), (index // columns) * tile_height
        canvas.paste(image, (x, y + 16))
        draw.text((x + 2, y + 2), name, fill=YELLOW)
    path = args.output or os.path.join(os.path.dirname(paths[0]), "sheet.png")
    canvas.save(path)
    print(path)


def info(args):
    for path in args.images:
        with Image.open(path) as image:
            print(f"{path}: {image.width}x{image.height}")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    commands = parser.add_subparsers(dest="command", required=True)

    p = commands.add_parser("grid", help="overlay a labelled coordinate grid")
    p.add_argument("image")
    p.add_argument("--step", type=int, default=100)
    p.add_argument("-o", "--output")
    p.set_defaults(run=grid)

    p = commands.add_parser("zoom", help="crop a region and enlarge it")
    p.add_argument("image")
    for name in ("x", "y", "width", "height"):
        p.add_argument(name, type=int)
    p.add_argument("--scale", type=int, default=2)
    p.add_argument("--pixelated", action="store_true", help="enlarge without smoothing, to judge exact pixels")
    p.add_argument("-o", "--output")
    p.set_defaults(run=zoom)

    p = commands.add_parser("compare", help="before/after side by side with changes boxed")
    p.add_argument("before")
    p.add_argument("after")
    p.add_argument("--threshold", type=int, default=24, help="per-channel difference that counts as a change (0-255)")
    p.add_argument("--cell", type=int, default=48, help="changes closer than this many pixels are one area")
    p.add_argument("--layout", choices=("auto", "side", "stack"), default="auto",
                   help="side by side, one above the other, or auto: stacked when the images are wider than tall")
    p.add_argument("-o", "--output")
    p.set_defaults(run=compare)

    p = commands.add_parser("sheet", help="tile several images into one")
    p.add_argument("images", nargs="+", help="files or glob patterns")
    p.add_argument("--columns", type=int, default=2)
    p.add_argument("--width", type=int, default=700, help="width of each tile")
    p.add_argument("-o", "--output")
    p.set_defaults(run=sheet)

    p = commands.add_parser("info", help="print image sizes")
    p.add_argument("images", nargs="+")
    p.set_defaults(run=info)

    args = parser.parse_args()
    try:
        args.run(args)
    except FileNotFoundError as error:
        sys.exit(f"no such file: {error.filename}")


if __name__ == "__main__":
    main()
