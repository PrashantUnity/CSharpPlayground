"""A hand-rolled Windows .ico writer/reader for FrySharp.

Pillow's own ICO encoder PNG-compresses every frame and writes wPlanes=0.
Windows Vista+ tolerates that, but it is not the canonical layout: plenty of
third-party consumers only understand DIB frames below 256px, and some render
PNG-based frames with a black backplate. So the frames below 256 are written as
32-bit BGRA DIBs with a real AND mask, and only the 256px frame is PNG.
"""

from __future__ import annotations

import io
import struct
from PIL import Image

# Sizes Windows 11 asks for across the context menu, title bar, tray, taskbar
# and Start scale factors. See "Construct your Windows app's icon" on MS Learn.
DIB_SIZES = (16, 20, 24, 32, 40, 48, 64, 128)
PNG_SIZES = (256,)
ALL_SIZES = DIB_SIZES + PNG_SIZES

_PNG_MAGIC = b"\x89PNG\r\n\x1a\n"


def _and_mask_stride(width: int) -> int:
    """AND mask rows are padded to a 4-byte boundary."""
    return ((width + 31) // 32) * 4


def _encode_dib(image: Image.Image) -> bytes:
    """BITMAPINFOHEADER + bottom-up BGRA XOR bitmap + bottom-up 1bpp AND mask."""
    width, height = image.size
    pixels = image.load()

    header = struct.pack(
        "<IiiHHIIiiII",
        40,          # biSize
        width,       # biWidth
        height * 2,  # biHeight - XOR and AND stacked
        1,           # biPlanes
        32,          # biBitCount
        0,           # biCompression (BI_RGB)
        0,           # biSizeImage (0 is fine for uncompressed)
        0,           # biXPelsPerMeter
        0,           # biYPelsPerMeter
        0,           # biClrUsed
        0,           # biClrImportant
    )

    # Bottom-up BGRA.
    xor_rows = []
    for y in reversed(range(height)):
        row = bytearray()
        for x in range(width):
            r, g, b, a = pixels[x, y]
            row.extend((b, g, r, a))
        xor_rows.append(bytes(row))
    xor_bytes = b"".join(xor_rows)

    # Bottom-up 1bpp AND mask: 1 for fully-transparent pixels, 0 elsewhere.
    stride = _and_mask_stride(width)
    and_rows = []
    for y in reversed(range(height)):
        row = bytearray(stride)
        for x in range(width):
            if pixels[x, y][3] == 0:
                row[x // 8] |= 0x80 >> (x % 8)
        and_rows.append(bytes(row))
    and_bytes = b"".join(and_rows)

    return header + xor_bytes + and_bytes


def _encode_png(image: Image.Image) -> bytes:
    buf = io.BytesIO()
    image.save(buf, format="PNG", optimize=True)
    return buf.getvalue()


def write_ico(master: Image.Image, out_path) -> None:
    """Write an .ico containing every standard Windows size downsampled from `master`."""
    entries = []
    blobs = []

    for size in ALL_SIZES:
        frame = master.resize((size, size), Image.LANCZOS).convert("RGBA")
        blob = _encode_png(frame) if size in PNG_SIZES else _encode_dib(frame)

        # ICONDIRENTRY: width/height byte is 0 for 256.
        b_width = 0 if size == 256 else size
        b_height = 0 if size == 256 else size
        entries.append((b_width, b_height, len(blob)))
        blobs.append(blob)

    # Header is 6 bytes; each of N entries is 16 bytes. Blobs follow immediately.
    header_size = 6 + len(entries) * 16
    offset = header_size

    out = bytearray()
    out.extend(struct.pack("<HHH", 0, 1, len(entries)))  # idReserved, idType=1, idCount

    for (w, h, length) in entries:
        out.extend(
            struct.pack(
                "<BBBBHHII",
                w,
                h,
                0,       # bColorCount (0 for 32bpp)
                0,       # bReserved
                1,       # wPlanes
                32,      # wBitCount
                length,  # dwBytesInRes
                offset,  # dwImageOffset
            )
        )
        offset += length

    for blob in blobs:
        out.extend(blob)

    with open(out_path, "wb") as f:
        f.write(out)
