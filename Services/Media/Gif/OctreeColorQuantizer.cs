using System;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Media.Gif;

/// <summary>
/// Result of color quantization on an image frame.
/// </summary>
public sealed record QuantizedFrame(
    byte[] IndexedPixels,
    byte[] PaletteRgb,
    int? TransparentIndex);

/// <summary>
/// High-performance color quantizer for reducing 32-bit RGBA/BGRA images to 256-color GIF palettes.
/// Uses fast unique-color indexing for diagram graphics and median-cut box reduction for complex gradients.
/// </summary>
public static class OctreeColorQuantizer
{
    private const int MaxColors = 256;

    private sealed class ColorBox
    {
        public List<int> Colors = new();
        public int MinR = 255, MaxR = 0;
        public int MinG = 255, MaxG = 0;
        public int MinB = 255, MaxB = 0;
        public long TotalCount;

        public int RangeR => MaxR - MinR;
        public int RangeG => MaxG - MinG;
        public int RangeB => MaxB - MinB;
        public int MaxRange => Math.Max(RangeR, Math.Max(RangeG, RangeB));

        public void UpdateBounds(IReadOnlyDictionary<int, int> counts)
        {
            MinR = MinG = MinB = 255;
            MaxR = MaxG = MaxB = 0;
            TotalCount = 0;

            foreach (int c in Colors)
            {
                int r = (c >> 16) & 0xFF;
                int g = (c >> 8) & 0xFF;
                int b = c & 0xFF;
                int count = counts[c];
                TotalCount += count;

                if (r < MinR) MinR = r;
                if (r > MaxR) MaxR = r;
                if (g < MinG) MinG = g;
                if (g > MaxG) MaxG = g;
                if (b < MinB) MinB = b;
                if (b > MaxB) MaxB = b;
            }
        }
    }

    public static QuantizedFrame Quantize(GifFrame frame, bool dither = false)
    {
        byte[] pixels = frame.RgbaPixels;
        int width = frame.Width;
        int height = frame.Height;
        int totalPixels = width * height;
        bool isBgra = frame.IsBgra;
        bool hasTrans = frame.HasTransparency;

        // 1. First pass: Collect unique colors and histogram
        var colorCounts = new Dictionary<int, int>();
        bool foundTransparency = false;

        for (int i = 0; i < totalPixels; i++)
        {
            int offset = i * 4;
            byte a = pixels[offset + 3];
            if (hasTrans && a < 128)
            {
                foundTransparency = true;
                continue;
            }

            int r = isBgra ? pixels[offset + 2] : pixels[offset + 0];
            int g = pixels[offset + 1];
            int b = isBgra ? pixels[offset + 0] : pixels[offset + 2];
            int key = (r << 16) | (g << 8) | b;

            colorCounts[key] = colorCounts.GetValueOrDefault(key) + 1;
        }

        byte[] palette = new byte[768];
        int? transIndex = null;
        int paletteOffset = 0;

        if (foundTransparency)
        {
            transIndex = 0;
            palette[0] = 0;
            palette[1] = 0;
            palette[2] = 0;
            paletteOffset = 1;
        }

        int maxOpaqueColors = MaxColors - paletteOffset;
        var colorToPaletteIndex = new Dictionary<int, byte>();

        // Fast Path: Unique colors <= available palette slots (common in UI diagrams and visualizers)
        if (colorCounts.Count <= maxOpaqueColors)
        {
            foreach (var kvp in colorCounts)
            {
                int c = kvp.Key;
                byte r = (byte)((c >> 16) & 0xFF);
                byte g = (byte)((c >> 8) & 0xFF);
                byte b = (byte)(c & 0xFF);

                palette[paletteOffset * 3 + 0] = r;
                palette[paletteOffset * 3 + 1] = g;
                palette[paletteOffset * 3 + 2] = b;
                colorToPaletteIndex[c] = (byte)paletteOffset;
                paletteOffset++;
            }
        }
        else
        {
            // Median-cut color quantization for gradients
            var initialBox = new ColorBox { Colors = new List<int>(colorCounts.Keys) };
            initialBox.UpdateBounds(colorCounts);

            var boxes = new List<ColorBox> { initialBox };
            while (boxes.Count < maxOpaqueColors)
            {
                // Find box with greatest color range
                ColorBox? boxToSplit = null;
                int maxRange = -1;
                foreach (var b in boxes)
                {
                    if (b.Colors.Count > 1 && b.MaxRange > maxRange)
                    {
                        maxRange = b.MaxRange;
                        boxToSplit = b;
                    }
                }

                if (boxToSplit == null) break;

                // Sort along widest color channel
                int rangeR = boxToSplit.RangeR;
                int rangeG = boxToSplit.RangeG;
                int rangeB = boxToSplit.RangeB;

                if (rangeR >= rangeG && rangeR >= rangeB)
                {
                    boxToSplit.Colors.Sort((c1, c2) => ((c1 >> 16) & 0xFF).CompareTo((c2 >> 16) & 0xFF));
                }
                else if (rangeG >= rangeR && rangeG >= rangeB)
                {
                    boxToSplit.Colors.Sort((c1, c2) => ((c1 >> 8) & 0xFF).CompareTo((c2 >> 8) & 0xFF));
                }
                else
                {
                    boxToSplit.Colors.Sort((c1, c2) => (c1 & 0xFF).CompareTo(c2 & 0xFF));
                }

                int half = boxToSplit.Colors.Count / 2;
                var newBox = new ColorBox { Colors = boxToSplit.Colors.GetRange(half, boxToSplit.Colors.Count - half) };
                boxToSplit.Colors.RemoveRange(half, boxToSplit.Colors.Count - half);

                boxToSplit.UpdateBounds(colorCounts);
                newBox.UpdateBounds(colorCounts);
                boxes.Add(newBox);
            }

            // Assign representative colors for each box
            foreach (var box in boxes)
            {
                long sumR = 0, sumG = 0, sumB = 0, total = 0;
                foreach (int c in box.Colors)
                {
                    int count = colorCounts[c];
                    sumR += ((c >> 16) & 0xFF) * (long)count;
                    sumG += ((c >> 8) & 0xFF) * (long)count;
                    sumB += (c & 0xFF) * (long)count;
                    total += count;
                }

                byte avgR = (byte)(total > 0 ? sumR / total : 0);
                byte avgG = (byte)(total > 0 ? sumG / total : 0);
                byte avgB = (byte)(total > 0 ? sumB / total : 0);

                palette[paletteOffset * 3 + 0] = avgR;
                palette[paletteOffset * 3 + 1] = avgG;
                palette[paletteOffset * 3 + 2] = avgB;

                byte palIdx = (byte)paletteOffset;
                foreach (int c in box.Colors)
                {
                    colorToPaletteIndex[c] = palIdx;
                }
                paletteOffset++;
            }
        }

        // 2. Map pixel buffer to palette indices
        byte[] indexedPixels = new byte[totalPixels];
        for (int i = 0; i < totalPixels; i++)
        {
            int offset = i * 4;
            byte a = pixels[offset + 3];
            if (hasTrans && a < 128 && transIndex.HasValue)
            {
                indexedPixels[i] = (byte)transIndex.Value;
                continue;
            }

            int r = isBgra ? pixels[offset + 2] : pixels[offset + 0];
            int g = pixels[offset + 1];
            int b = isBgra ? pixels[offset + 0] : pixels[offset + 2];
            int key = (r << 16) | (g << 8) | b;

            if (colorToPaletteIndex.TryGetValue(key, out byte idx))
            {
                indexedPixels[i] = idx;
            }
            else
            {
                // Fallback nearest color in palette
                indexedPixels[i] = FindNearestPaletteIndex(r, g, b, palette, paletteOffset);
            }
        }

        return new QuantizedFrame(indexedPixels, palette, transIndex);
    }

    private static byte FindNearestPaletteIndex(int r, int g, int b, byte[] palette, int count)
    {
        int bestIdx = 0;
        int bestDist = int.MaxValue;

        for (int i = 0; i < count; i++)
        {
            int pr = palette[i * 3 + 0];
            int pg = palette[i * 3 + 1];
            int pb = palette[i * 3 + 2];

            int dr = r - pr;
            int dg = g - pg;
            int db = b - pb;
            int dist = dr * dr + dg * dg + db * db;

            if (dist < bestDist)
            {
                bestDist = dist;
                bestIdx = i;
                if (dist == 0) break;
            }
        }

        return (byte)bestIdx;
    }
}
