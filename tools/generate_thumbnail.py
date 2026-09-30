#!/usr/bin/env python3
"""FrySharp Thumbnail Generator (Pure Scalable Vector SVG & Image Export)
Generates the high-resolution, vector-based FrySharp YouTube & README thumbnail.

Features:
- 100% pure scalable SVG with modern dark studio aesthetic, glassmorphism, and radial glows
- Authentic vector dragon brand logo inside glowing circular badge
- Polyglot notebook cell deck with crisp syntax highlighting, badges, and line numbers
- Interactive FrySharp Data View results table
- Symmetrical, balanced, tiled multi-language dock (honeycomb 5 + 4 with uniform tiles)
- Extensible: effortlessly add new languages, modify code cells, or change resolutions
"""

import argparse
import html
import os
import subprocess
import sys

# Official 128x128 vector symbols for languages (padded to avoid clipping)
SVG_SYMBOLS = {
    "csharp": """<symbol id="icon-csharp" viewBox="0 0 128 128">
  <g transform="translate(12.8, 12.8) scale(0.80)">
    <path fill="#9B4F96" d="M115.4 30.7L67.1 2.9c-.8-.5-1.9-.7-3.1-.7-1.2 0-2.3.3-3.1.7l-48 27.9c-1.7 1-2.9 3.5-2.9 5.4v55.7c0 1.1.2 2.4 1 3.5l106.8-62c-.6-1.2-1.5-2.1-2.4-2.7z"/>
    <path fill="#68217A" d="M10.7 95.3c.5.8 1.2 1.5 1.9 1.9l48.2 27.9c.8.5 1.9.7 3.1.7 1.2 0 2.3-.3 3.1-.7l48-27.9c1.7-1 2.9-3.5 2.9-5.4V36.1c0-.9-.1-1.9-.6-2.8l-106.6 62z"/>
    <path fill="#fff" d="M85.3 76.1C81.1 83.5 73.1 88.5 64 88.5c-13.5 0-24.5-11-24.5-24.5s11-24.5 24.5-24.5c9.1 0 17.1 5 21.3 12.5l13-7.5c-6.8-11.9-19.6-20-34.3-20-21.8 0-39.5 17.7-39.5 39.5s17.7 39.5 39.5 39.5c14.6 0 27.4-8 34.2-19.8l-12.9-7.6zM97 66.2l.9-4.3h-4.2v-4.7h5.1L100 51h4.9l-1.2 6.1h3.8l1.2-6.1h4.8l-1.2 6.1h2.4v4.7h-3.3l-.9 4.3h4.2v4.7h-5.1l-1.2 6h-4.9l1.2-6h-3.8l-1.2 6h-4.8l1.2-6h-2.4v-4.7H97zm4.8 0h3.8l.9-4.3h-3.8l-.9 4.3z"/>
  </g>
</symbol>""",

    "python": """<symbol id="icon-python" viewBox="0 0 128 128">
  <g transform="translate(14, 13) scale(0.80)">
    <path fill="url(#python-blue)" d="M63.2 2.1c-16.7 0-24.7 1.7-27.2 2.8-5.7 2.4-7 6.4-7 15.3v11.2h34.6v3.4H24.3c-9.1 0-16.8 5.5-19.3 15.9-2.9 12-2.8 19.5 0 31.8 2.2 9.4 7.6 15.9 16.7 15.9h9.8v-14c0-10.4 8.9-19.6 19.6-19.6h34.4c8.7 0 15.7-7.2 15.7-15.9V19.7c0-8.5-7.2-14.8-15.7-16.2-7.5-1.2-14.6-1.4-22.3-1.4zm-14.8 9.5c3.3 0 6 2.7 6 6s-2.7 6-6 6-6-2.7-6-6 2.7-6 6-6z"/>
    <path fill="url(#python-yellow)" d="M96.7 34.7H86.9v14c0 10.4-9.1 19.6-19.6 19.6H32.9c-8.7 0-15.7 7.2-15.7 15.9v28.5c0 8.5 7.4 13.6 15.7 16.2 10.4 3.2 20.3 3.6 34.4 0 7.8-2 15.7-6.8 15.7-16.2v-11.2H48.4V102h39.3c9.1 0 12.5-6.3 15.7-15.9 3.2-9.7 3.2-19 0-31.8-2.2-9.3-6.6-15.6-15.7-15.6zm-17.5 72.8c3.3 0 6 2.7 6 6s-2.7 6-6 6-6-2.7-6-6 2.7-6 6-6z"/>
  </g>
</symbol>""",

    "rust": """<symbol id="icon-rust" viewBox="0 0 128 128">
  <g transform="translate(12.8, 12.8) scale(0.80)">
    <path fill="#C5C7CA" d="M125.7 66.8c-.2-1.9-.6-3.8-1.2-5.6l-5.6 1.4c.5 1.5.8 3 .9 4.6l5.9-.4zm-3-16.3c-.8-1.7-1.8-3.4-2.9-4.9l-4.7 3.5c.9 1.3 1.7 2.7 2.4 4.1l5.2-2.7zm-7.7-14.7c-1.4-1.3-2.9-2.5-4.5-3.5l-3.3 4.9c1.3.8 2.5 1.8 3.7 2.9l4.1-4.3zm-11.6-11.7c-1.8-.8-3.6-1.5-5.5-2l-1.6 5.6c1.6.4 3.1 1 4.5 1.7l2.6-5.3zm-15.2-7c-1.9-.3-3.9-.4-5.9-.2l.1 5.8c1.6-.2 3.3-.1 4.9.2l.9-5.8zm-16.1.1c-1.9.4-3.8 1-5.6 1.8l2 5.5c1.5-.6 3.1-1.1 4.7-1.5L67.1 7.2zM36.3 13c-1.8 1-3.4 2.1-4.9 3.3l3.7 4.5c1.2-1 2.5-1.9 4-2.7L36.3 13zM22.5 23.3c-1.4 1.4-2.6 3-3.6 4.6l4.9 3.1c.8-1.3 1.8-2.6 3-3.7l-4.3-4zm-9.8 14.8c-.8 1.8-1.4 3.6-1.8 5.5l5.7 1.2c.4-1.6.8-3.1 1.5-4.5l-5.4-2.2zM6.9 56.6c-.3 1.9-.4 3.9-.2 5.9l5.8-.3c-.1-1.6 0-3.3.3-4.9L6.9 56.6zM9.8 74.4c.5 1.8 1.2 3.6 2 5.3l5.2-2.5c-.7-1.4-1.2-2.9-1.6-4.4L9.8 74.4zm7.6 14.7c1.1 1.6 2.3 3.1 3.7 4.4l4.1-4.1c-1.1-1.1-2.1-2.3-3-3.6l-4.8 3.3zm12.3 11.2c1.6 1.1 3.3 2 5.1 2.7l2.5-5.3c-1.4-.6-2.8-1.4-4.1-2.3l-3.5 4.9zm15.7 6.4c1.9.5 3.9.8 5.8.8l.5-5.8c-1.6 0-3.2-.2-4.8-.7l-1.5 5.7zm16.1-.7c1.9-.2 3.8-.7 5.7-1.4l-1.8-5.5c-1.5.5-3.1.9-4.7 1.1l.8 5.8zm15.5-6.5c1.7-.9 3.4-1.9 4.9-3.1l-3.5-4.7c-1.2.9-2.6 1.8-4 2.5l2.6 5.3zm13.1-11.4c1.3-1.4 2.5-3 3.4-4.7l-4.9-3.1c-.8 1.4-1.7 2.6-2.8 3.8l4.3 4zm8.6-15.1c.7-1.8 1.2-3.7 1.5-5.6l-5.7-1.2c-.3 1.6-.7 3.1-1.3 4.6l5.5 2.2z"/>
    <circle fill="#1e242f" cx="64" cy="64" r="50"/>
    <path fill="#fff" d="M64 22c-23.2 0-42 18.8-42 42s18.8 42 42 42 42-18.8 42-42-18.8-42-42-42zm-8.8 63.8H43.9V42.2h19.5c8.7 0 15 5.5 15 13.6 0 6.1-3.6 11.1-9.3 12.9l10.9 17.1H66.6L57.2 69h-2v16.8zm0-25.2h6.8c3.9 0 6.6-2.4 6.6-5.8s-2.7-5.8-6.6-5.8h-6.8v11.6z"/>
  </g>
</symbol>""",

    "go": """<symbol id="icon-go" viewBox="0 0 128 128">
  <g transform="translate(8, 4) scale(0.85)">
    <line x1="8" y1="52" x2="38" y2="52" stroke="#00ACD7" stroke-width="8" stroke-linecap="round"/>
    <line x1="16" y1="64" x2="44" y2="64" stroke="#00ACD7" stroke-width="8" stroke-linecap="round"/>
    <line x1="4" y1="76" x2="32" y2="76" stroke="#00ACD7" stroke-width="8" stroke-linecap="round"/>
    <text x="44" y="86" fill="#00ACD7" font-family="-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif" font-size="64" font-weight="900" font-style="italic" letter-spacing="-2">GO</text>
  </g>
</symbol>""",

    "java": """<symbol id="icon-java" viewBox="0 0 128 128">
  <g transform="translate(14, 10) scale(0.80)">
    <path fill="#EA2D2E" d="M69.8 56.3c6-7-1.6-13.2-1.6-13.2s15.3 7.9 8.3 17.8c-6.6 9.2-11.6 13.8 15.6 29.6 0 0-42.7-10.7-22.3-34.2z"/>
    <path fill="#EA2D2E" d="M76.5 1.6S89.5 14.6 64.2 34.5c-20.3 16-4.6 25.1 0 35.6-11.8-10.7-20.5-20.1-14.7-28.8C58 28.4 81.7 22.2 76.5 1.6z"/>
    <path fill="#0074BD" d="M47.6 93.1s-4.8 2.8 3.4 3.7c9.9 1.1 14.9 1 25.8-1.1 0 0 2.9 1.8 6.9 3.4-24.4 10.5-55.3-.6-36.1-6zm-3-13.7s-5.3 4 2.8 4.8c10.6 1.1 18.9 1.2 33.4-1.6 0 0 2 2 5.1 3.1-29.5 8.6-62.4.7-41.3-6.3z"/>
    <path fill="#0074BD" d="M102.1 103.2s3.5 2.9-3.9 5.2c-14.1 4.3-58.7 5.6-71.1.2-4.5-1.9 3.9-4.6 6.5-5.2 2.7-.6 4.3-.5 4.3-.5-5-3.5-32 6.9-13.7 9.8 49.8 8.1 90.8-3.6 77.9-9.5z"/>
    <text x="64" y="122" text-anchor="middle" fill="#EA2D2E" font-family="-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif" font-size="24" font-weight="900" letter-spacing="1">Java</text>
  </g>
</symbol>""",

    "cpp": """<symbol id="icon-cpp" viewBox="0 0 128 128">
  <g transform="translate(12.8, 12.8) scale(0.80)">
    <path fill="#00599C" d="M115.4 30.7L67.1 2.9c-.8-.5-1.9-.7-3.1-.7-1.2 0-2.3.3-3.1.7l-48 27.9c-1.7 1-2.9 3.5-2.9 5.4v55.7c0 1.1.2 2.4 1 3.5l106.8-62c-.6-1.2-1.5-2.1-2.4-2.7z"/>
    <path fill="#004482" d="M10.7 95.3c.5.8 1.2 1.5 1.9 1.9l48.2 27.9c.8.5 1.9.7 3.1.7 1.2 0 2.3-.3 3.1-.7l48-27.9c1.7-1 2.9-3.5 2.9-5.4V36.1c0-.9-.1-1.9-.6-2.8l-106.6 62z"/>
    <path fill="#fff" d="M78 76.1C73.8 83.5 65.8 88.5 56.7 88.5c-13.5 0-24.5-11-24.5-24.5s11-24.5 24.5-24.5c9.1 0 17.1 5 21.3 12.5l13-7.5c-6.8-11.9-19.6-20-34.3-20-21.8 0-39.5 17.7-39.5 39.5s17.7 39.5 39.5 39.5c14.6 0 27.4-8 34.2-19.8l-12.9-7.6z"/>
    <path fill="#659AD2" d="M85 61h4v-4h4v4h4v4h-4v4h-4v-4h-4zm18 0h4v-4h4v4h4v4h-4v4h-4v-4h-4z"/>
  </g>
</symbol>""",

    "javascript": """<symbol id="icon-javascript" viewBox="0 0 128 128">
  <g transform="translate(12.8, 12.8) scale(0.80)">
    <rect width="128" height="128" rx="16" fill="#F7DF1E"/>
    <path fill="#000" d="M67.3 100.3c3.8 6.3 8.7 11.1 17.7 11.1 7.5 0 12.3-3.7 12.3-8.8 0-6.1-4.9-8.3-13.1-11.8l-4.5-1.9c-12.9-5.5-21.5-12.5-21.5-27.3 0-13.6 10.4-23.9 26.8-23.9 11.6 0 19.9 4.1 25.8 14.5l-12.5 8c-3.1-5.5-6.5-7.7-13.3-7.7-6 0-9.8 3.8-9.8 8.1 0 5.5 3.8 7.7 11.3 10.9l4.5 1.9c15.2 6.5 23.6 13.3 23.6 28.5 0 16.3-12.8 25-28.5 25-16.1 0-25.7-7.8-30.8-17.8l12-8.7zm-40.4.9c2.3 4.1 4.5 7.5 9.7 7.5 5 0 8.2-2 8.2-9.7V40.2h15.9v58.6c0 16.3-9.5 23.5-23.6 23.5-12.9 0-20.3-6.6-24.3-15.5l14.1-5.6z"/>
  </g>
</symbol>""",

    "fsharp": """<symbol id="icon-fsharp" viewBox="0 0 128 128">
  <g transform="translate(11.5, 11.5) scale(0.82)">
    <path fill="#378BBA" d="M12 64.5L62 14v28L36 64.5l26 22.5v28L12 64.5z"/>
    <path fill="#30B9DB" d="M116 64.5L66 14v28l26 22.5-26 22.5v28L116 64.5z"/>
    <text x="44" y="78" fill="#fff" font-family="-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif" font-size="44" font-weight="900" letter-spacing="-1">F#</text>
  </g>
</symbol>""",

    "sql": """<symbol id="icon-sql" viewBox="0 0 128 128">
  <g transform="translate(11.5, 11.5) scale(0.82)">
    <ellipse cx="64" cy="28" rx="46" ry="16" fill="#4ade80"/>
    <path fill="#0284c7" d="M18 28v72c0 9 20.5 16 46 16s46-7 46-16V28c0 9-20.5 16-46 16S18 37 18 28z"/>
    <ellipse cx="64" cy="28" rx="44" ry="14" fill="#22c55e"/>
    <path fill="#0369a1" d="M18 58c0 9 20.5 16 46 16s46-7 46-16c0-9-20.5-16-46-16S18 49 18 58z" opacity="0.25"/>
    <text x="64" y="92" text-anchor="middle" fill="#ffffff" font-family="-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif" font-size="34" font-weight="900" letter-spacing="1">SQL</text>
  </g>
</symbol>""",

    "c": """<symbol id="icon-c" viewBox="0 0 128 128">
  <g transform="translate(12.8, 12.8) scale(0.80)">
    <path fill="#A8B9CC" d="M115.4 30.7L67.1 2.9c-.8-.5-1.9-.7-3.1-.7-1.2 0-2.3.3-3.1.7l-48 27.9c-1.7 1-2.9 3.5-2.9 5.4v55.7c0 1.1.2 2.4 1 3.5l106.8-62c-.6-1.2-1.5-2.1-2.4-2.7z"/>
    <path fill="#283593" d="M10.7 95.3c.5.8 1.2 1.5 1.9 1.9l48.2 27.9c.8.5 1.9.7 3.1.7 1.2 0 2.3-.3 3.1-.7l48-27.9c1.7-1 2.9-3.5 2.9-5.4V36.1c0-.9-.1-1.9-.6-2.8l-106.6 62z"/>
    <path fill="#fff" d="M82 76.1C77.8 83.5 69.8 88.5 60.7 88.5c-13.5 0-24.5-11-24.5-24.5s11-24.5 24.5-24.5c9.1 0 17.1 5 21.3 12.5l13-7.5c-6.8-11.9-19.6-20-34.3-20-21.8 0-39.5 17.7-39.5 39.5s17.7 39.5 39.5 39.5c14.6 0 27.4-8 34.2-19.8l-12.9-7.6z"/>
  </g>
</symbol>""",
}

# Code snippets for left-side notebook cells
CELL_PRESETS = [
    {
        "lang": "C#",
        "badge_color": "#93c5fd",
        "lines": [
            ("2", [("int", "#60a5fa"), (" maxScore = 0;", "#e2e8f0")]),
            ("3", [("for", "#f472b6"), (" (", "#e2e8f0"), ("int", "#60a5fa"), (" s : scores) {", "#e2e8f0")]),
            ("4", [("    ", "#e2e8f0"), ("if", "#f472b6"), (" (s > maxScore) maxScore = s;", "#e2e8f0")]),
            ("5", [("}", "#e2e8f0")]),
            ("6", [("std::cout << ", "#e2e8f0"), ('"C++ calculated maximum score: "', "#fdba74"), (" << maxScore << std::endl;", "#e2e8f0")]),
            ("7", [("fry::display::table(scores, ", "#e2e8f0"), ('"C++ Processed Scores"', "#fdba74"), (");", "#e2e8f0")]),
        ]
    },
    {
        "lang": "Python",
        "badge_color": "#fef08a",
        "lines": [
            ("2", [("def", "#f472b6"), (" main():", "#e2e8f0")]),
            ("3", [("    ", "#e2e8f0"), ("for", "#f472b6"), (" s ", "#e2e8f0"), ("in", "#f472b6"), (" scores.average(", "#e2e8f0"), ("'score'", "#fdba74"), ("):", "#e2e8f0")]),
            ("4", [("        print(", "#e2e8f0"), ("f'Score: {s}'", "#fdba74"), (")", "#e2e8f0")]),
            ("5", [("    ", "#e2e8f0"), ("return", "#f472b6"), (" scores", "#e2e8f0")]),
        ]
    },
    {
        "lang": "Rust",
        "badge_color": "#fdba74",
        "lines": [
            ("2", [("fn", "#f472b6"), (" main() {", "#e2e8f0")]),
            ("3", [("    ", "#e2e8f0"), ("for", "#f472b6"), (" arg ", "#e2e8f0"), ("in", "#f472b6"), (" args {", "#e2e8f0")]),
            ("4", [("        println!(", "#e2e8f0"), ('"Hello world!"', "#fdba74"), (");", "#e2e8f0")]),
            ("5", [("    }", "#e2e8f0")]),
            ("6", [("}", "#e2e8f0")]),
        ]
    },
    {
        "lang": "Go",
        "badge_color": "#67e8f9",
        "lines": [
            ("2", [("func", "#f472b6"), (" main() {", "#e2e8f0")]),
            ("3", [("    ", "#e2e8f0"), ("var", "#60a5fa"), (" a = ", "#e2e8f0"), ("19", "#93c5fd")]),
            ("4", [("    fmt.Println(", "#e2e8f0"), ('"Hello Go!"', "#fdba74"), (")", "#e2e8f0")]),
            ("5", [("}", "#e2e8f0")]),
        ]
    }
]

TABLE_PRESET = {
    "title": "FrySharp Data View",
    "columns": ["score", "score", "letter", "average", "avg", "JavaScript"],
    "rows": [
        ["0", "69", "85", "72", "'E'", "Passed", "89:17"],
        ["1", "78", "85", "72", "'C'", "Passed", "90:11"],
        ["2", "71", "99", "32", "'C'", "Passed", "75:56"],
        ["3", "72", "95", "80", "'C'", "Passed", "90:50"],
        ["4", "76", "93", "85", "'B'", "Passed", "89:17"],
        ["5", "70", "97", "80", "'C'", "Passed", "80:17"],
    ]
}


def load_svg_icons(svg_dir="tools/svg_icons"):
    icons = {}
    if os.path.exists(svg_dir):
        for f in os.listdir(svg_dir):
            if f.endswith(".svg"):
                name = os.path.splitext(f)[0]
                with open(os.path.join(svg_dir, f), "r", encoding="utf-8") as fp:
                    content = fp.read()
                start = content.find(">") + 1
                end = content.rfind("</svg>")
                if start > 0 and end > start:
                    inner = content[start:end].strip()
                    if name == "rust":
                        # Ensure rust gear has crisp white fill on dark cards
                        if "<path " in inner and "fill=" not in inner:
                            inner = inner.replace("<path ", '<path fill="#ffffff" ', 1)
                    icons[name] = inner
    return icons


def build_svg(
    width=1376,
    height=768,
    title="FrySharp",
    subtitle="Polyglot Notebook & IDE",
    languages=None,
    show_play_button=True,
    dragon_logo_svg_path=None,
    svg_icons_dir="tools/svg_icons"
):
    if languages is None:
        languages = ["csharp", "python", "rust", "go", "java", "cpp", "javascript", "fsharp", "sql"]

    # Load authentic official SVGs
    icons_data = load_svg_icons(svg_icons_dir)

    # Base coordinates scaled to width/height (canonical baseline: 1376x768)
    sx = width / 1376.0
    sy = height / 768.0

    # Read dragon SVG paths from Assets/Appiconlogo.svg if available
    dragon_paths = ""
    if dragon_logo_svg_path and os.path.exists(dragon_logo_svg_path):
        with open(dragon_logo_svg_path, "r", encoding="utf-8") as f:
            content = f.read()
            start = content.find("<path")
            end = content.rfind("</svg>")
            if start != -1 and end != -1:
                dragon_paths = content[start:end]

    if not dragon_paths:
        dragon_paths = """
  <path d="M2474 751C2532.5 786.5 2609.5 835.5 2682.5 859.5C2768.74 897.828 2854.61 905.811 2884 925.5C2935.5 960 2964.5 1038.74 3038 1085C3111.5 1131.26 3226 1156 3267 1156.5C3308 1157 3322.5 1124 3356 1128C3389.5 1132 3422.5 1199.5 3431 1251C3439.5 1302.5 3449.5 1442 3417.5 1450.5C3385.5 1459 3252 1403 3164 1391.5C3076 1380 2823 1391.5 2823 1391.5C2823 1391.5 2973 1587.5 3068.5 1626C3164 1664.5 3244.5 1651.5 3257.5 1664.5C3270.5 1677.5 3226.5 1772 3164 1813.5C3101.5 1855 3011 1874.5 2995.5 1864.5C2980 1854.5 2903 1640.5 2742 1630.5C2581 1620.5 2531 1626 2481.5 1607C2432 1588 2370.5 1526 2345.5 1479.5C2320.5 1433 2325 1310 2287.5 1311C2250 1312 1948 1522.5 1927.5 1743C1907 1963.5 1943 2117.5 1968 2220.5C1993 2323.5 2133 2584 2133 2584C2133 2584 1945.5 2439.5 1887 2353C1828.5 2266.5 1747.5 2037 1747.5 2037L1645 2220.5C1645 2220.5 1619 2037 1619 1916C1619 1795 1667 1626 1667 1626C1667 1626 1633 1639 1589.5 1662C1546 1685 1493 1736.5 1493 1736.5C1493 1736.5 1547.5 1549 1615 1457C1682.5 1365 1855.5 1222.5 1855.5 1222.5C1855.5 1222.5 1751.5 1217 1717.5 1215C1683.5 1213 1575 1222.5 1575 1222.5C1575 1222.5 1727.5 1086 1832.5 1045C1937.5 1004 2133 973 2133 973C2133 973 1961 878 1832.5 753C1704 628 1667 353 1667 353C1667 353 1824.5 563.337 1958.5 664C2092.5 764.663 2290.5 822.5 2290.5 822.5C2290.5 822.5 2245.5 757 2203 650C2160.5 543 2158.5 411.5 2158.5 411.5C2158.5 411.5 2415.5 715.5 2474 751Z" fill="#0D2B45"/>
  <path d="M2291.5 1633C2291.5 1633 2268.5 1913.5 2645.5 2143C3022.5 2372.5 3176.5 2469.5 3337 2716C3497.5 2962.5 3276.5 3592.5 3276.5 3592.5C3276.5 3592.5 3305 3376 3276.5 3111C3248 2846 3028 2641.5 2916 2545C2804 2448.5 2527 2287.5 2437.5 2205.5C2348 2123.5 2273.5 2027.5 2263.5 1917.5C2253.5 1807.5 2291.5 1633 2291.5 1633Z" fill="#1A3D5C"/>
  <path d="M4213 2989C4213 3215.14 4168.46 3439.06 4081.92 3647.98C3995.38 3856.9 3868.54 4046.74 3708.64 4206.64C3548.74 4366.54 3358.9 4493.38 3149.98 4579.92C2941.06 4666.46 2717.14 4711 2491 4711C2264.86 4711 2040.94 4666.46 1832.02 4579.92C1623.1 4493.38 1433.26 4366.54 1273.36 4206.64C1113.46 4046.74 986.618 3856.9 900.079 3647.98C813.541 3439.06 769 3215.14 769 2989H1299C1299 3142.65 1329.25 3294.79 1388.02 3436.74C1446.8 3578.69 1532.94 3707.67 1641.54 3816.31C1750.14 3924.96 1879.06 4011.14 2020.95 4069.94C2162.84 4128.74 2314.92 4159 2468.5 4159C2622.08 4159 2774.16 4128.74 2916.05 4069.94C3057.94 4011.14 3186.86 3924.96 3295.46 3816.32C3404.06 3707.67 3490.2 3578.69 3548.98 3436.74C3607.75 3294.79 3638 3142.65 3638 2989H4213Z" fill="#0D2B45"/>
  <path d="M2654.5 1023.5C2654.5 1023.5 2753.5 1021 2810 1033C2866.5 1045 2920 1126 2920 1126C2920 1126 2872 1139 2847 1140C2822 1141 2757.5 1132.5 2718.5 1111.5C2679.5 1090.5 2654.5 1023.5 2654.5 1023.5Z" fill="#7CA2C2"/>
  <text style="fill: rgb(51, 51, 51); font-family: sans-serif; font-size: 150px; font-weight: bold; white-space: pre;" transform="matrix(11.983413, 0, 0, 7.54, -1323.201782, -8995.59082)" x="382.306" y="1473.57">#</text>
"""

    svg = []
    svg.append(f'<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 {width} {height}" width="{width}" height="{height}">')
    svg.append('<defs>')
    
    # Background gradient
    svg.append("""  <linearGradient id="bg-grad" x1="0%" y1="0%" x2="100%" y2="100%">
    <stop offset="0%" stop-color="#0c111a"/>
    <stop offset="50%" stop-color="#121824"/>
    <stop offset="100%" stop-color="#171f2e"/>
  </linearGradient>""")

    # Ambient radial glows
    svg.append("""  <radialGradient id="ambient-cyan" cx="25%" cy="35%" r="65%">
    <stop offset="0%" stop-color="#38bdf8" stop-opacity="0.08"/>
    <stop offset="100%" stop-color="#38bdf8" stop-opacity="0"/>
  </radialGradient>""")
    svg.append("""  <radialGradient id="ambient-purple" cx="75%" cy="30%" r="55%">
    <stop offset="0%" stop-color="#818cf8" stop-opacity="0.09"/>
    <stop offset="100%" stop-color="#818cf8" stop-opacity="0"/>
  </radialGradient>""")
    svg.append("""  <radialGradient id="logo-glow" cx="50%" cy="50%" r="50%">
    <stop offset="0%" stop-color="#ffffff" stop-opacity="0.25"/>
    <stop offset="100%" stop-color="#38bdf8" stop-opacity="0"/>
  </radialGradient>""")

    # Python gradients
    svg.append("""  <linearGradient id="python-blue" x1="0%" y1="0%" x2="100%" y2="100%">
    <stop offset="0%" stop-color="#387EB8"/>
    <stop offset="100%" stop-color="#366994"/>
  </linearGradient>""")
    svg.append("""  <linearGradient id="python-yellow" x1="0%" y1="0%" x2="100%" y2="100%">
    <stop offset="0%" stop-color="#FFE873"/>
    <stop offset="100%" stop-color="#FFD43B"/>
  </linearGradient>""")

    # Tile container gradient
    svg.append("""  <linearGradient id="tile-grad" x1="0%" y1="0%" x2="100%" y2="100%">
    <stop offset="0%" stop-color="#222f44" stop-opacity="0.9"/>
    <stop offset="100%" stop-color="#131b28" stop-opacity="0.95"/>
  </linearGradient>""")

    # Drop shadows
    svg.append("""  <filter id="card-shadow" x="-5%" y="-10%" width="115%" height="130%">
    <feDropShadow dx="0" dy="8" stdDeviation="12" flood-color="#000000" flood-opacity="0.45"/>
  </filter>""")
    svg.append("""  <filter id="tile-shadow" x="-10%" y="-10%" width="120%" height="120%">
    <feDropShadow dx="0" dy="4" stdDeviation="5" flood-color="#000000" flood-opacity="0.30"/>
  </filter>""")
    svg.append("""  <filter id="logo-shadow" x="-20%" y="-20%" width="140%" height="140%">
    <feDropShadow dx="0" dy="10" stdDeviation="20" flood-color="#000000" flood-opacity="0.4"/>
  </filter>""")

    # Language symbols
    for lang_key, sym in SVG_SYMBOLS.items():
        svg.append(f"  {sym}")

    svg.append('</defs>')

    # 1. Background Layers
    svg.append(f'  <rect width="{width}" height="{height}" fill="url(#bg-grad)"/>')
    svg.append(f'  <rect width="{width}" height="{height}" fill="url(#ambient-cyan)"/>')
    svg.append(f'  <rect width="{width}" height="{height}" fill="url(#ambient-purple)"/>')

    # 2. Left Side: Polyglot Notebook Cells
    card_x = int(75 * sx)
    card_w = int(585 * sx)
    gutter_x = int(38 * sx)

    cell_configs = [
        {"y": 50, "h": 140, "preset": CELL_PRESETS[0]},
        {"y": 204, "h": 115, "preset": CELL_PRESETS[1]},
        {"y": 332, "h": 115, "preset": CELL_PRESETS[2]},
        {"y": 460, "h": 108, "preset": CELL_PRESETS[3]},
    ]

    for item in cell_configs:
        cy = int(item["y"] * sy)
        ch = int(item["h"] * sy)
        preset = item["preset"]

        # Run indicator outside card: ▶ [ ]
        play_y = cy + int(24 * sy)
        svg.append(f'  <!-- Gutter ▶ [ ] for {preset["lang"]} -->')
        svg.append(f'  <polygon points="{gutter_x},{play_y - 6} {gutter_x + 9},{play_y} {gutter_x},{play_y + 6}" fill="#34d399"/>')
        svg.append(f'  <text x="{gutter_x + 14}" y="{play_y + 4}" fill="#526077" font-family="monospace" font-size="{int(13*sy)}" font-weight="bold">[ ]</text>')

        # Card container
        svg.append(f'  <rect x="{card_x}" y="{cy}" width="{card_w}" height="{ch}" rx="10" fill="#141a26" stroke="#253046" stroke-width="1.2" filter="url(#card-shadow)"/>')

        # Language Badge Pill
        badge_w = int(len(preset["lang"]) * 10 + 24)
        badge_h = int(22 * sy)
        badge_x = card_x + int(42 * sx)
        badge_y = cy + int(10 * sy)
        svg.append(f'  <rect x="{badge_x}" y="{badge_y}" width="{badge_w}" height="{badge_h}" rx="5" fill="#222c3e" stroke="#364560" stroke-width="0.8"/>')
        svg.append(f'  <text x="{badge_x + badge_w // 2}" y="{badge_y + int(15 * sy)}" text-anchor="middle" fill="{preset["badge_color"]}" font-family="-apple-system, BlinkMacSystemFont, Roboto, sans-serif" font-size="{int(12 * sy)}" font-weight="700">{preset["lang"]}</text>')

        # Line numbers and code lines
        line_num_x = card_x + int(26 * sx)
        code_x = card_x + int(42 * sx)
        start_code_y = cy + int(46 * sy)
        line_step = int(18 * sy)

        for idx, (ln, tokens) in enumerate(preset["lines"]):
            curr_y = start_code_y + idx * line_step
            svg.append(f'  <text x="{line_num_x}" y="{curr_y}" text-anchor="end" fill="#4a5568" font-family="SFMono-Regular, Menlo, Monaco, Consolas, monospace" font-size="{int(11 * sy)}">{ln}</text>')
            
            tspan_parts = []
            for text, color in tokens:
                escaped = html.escape(text)
                escaped = escaped.replace("    ", "&#160;&#160;&#160;&#160;").replace("  ", "&#160;&#160;")
                tspan_parts.append(f'<tspan fill="{color}">{escaped}</tspan>')
            svg.append(f'  <text x="{code_x}" y="{curr_y}" fill="#e2e8f0" font-family="SFMono-Regular, Menlo, Monaco, Consolas, monospace" font-size="{int(11.5 * sy)}">{"".join(tspan_parts)}</text>')

    # Bottom Table: FrySharp Data View
    table_y = int(582 * sy)
    table_h = int(172 * sy)
    t_play_y = table_y + int(24 * sy)

    svg.append('  <!-- Gutter ▶ [ ] for Table -->')
    svg.append(f'  <polygon points="{gutter_x},{t_play_y - 6} {gutter_x + 9},{t_play_y} {gutter_x},{t_play_y + 6}" fill="#34d399"/>')
    svg.append(f'  <text x="{gutter_x + 14}" y="{t_play_y + 4}" fill="#526077" font-family="monospace" font-size="{int(13*sy)}" font-weight="bold">[ ]</text>')

    # Table Card Container
    svg.append(f'  <rect x="{card_x}" y="{table_y}" width="{card_w}" height="{table_h}" rx="10" fill="#131925" stroke="#253046" stroke-width="1.2" filter="url(#card-shadow)"/>')

    # Table Header Bar
    t_header_h = int(28 * sy)
    svg.append(f'  <text x="{card_x + int(16 * sx)}" y="{table_y + int(19 * sy)}" fill="#e2e8f0" font-family="-apple-system, BlinkMacSystemFont, Roboto, sans-serif" font-size="{int(12.5 * sy)}" font-weight="700">{html.escape(TABLE_PRESET["title"])}</text>')
    svg.append(f'  <text x="{card_x + card_w - int(20 * sx)}" y="{table_y + int(19 * sy)}" fill="#64748b" font-family="sans-serif" font-size="{int(13 * sy)}">&#x2715;</text>')
    svg.append(f'  <line x1="{card_x}" y1="{table_y + t_header_h}" x2="{card_x + card_w}" y2="{table_y + t_header_h}" stroke="#232d40" stroke-width="1"/>')

    # Table Columns
    col_y = table_y + t_header_h
    col_h = int(22 * sy)
    svg.append(f'  <rect x="{card_x}" y="{col_y}" width="{card_w}" height="{col_h}" fill="#192233"/>')

    col_widths = [int(45 * sx), int(80 * sx), int(80 * sx), int(80 * sx), int(90 * sx), int(85 * sx), int(100 * sx)]
    col_x_offsets = [card_x]
    for w in col_widths:
        col_x_offsets.append(col_x_offsets[-1] + w)

    # Column titles
    for i, col_title in enumerate([""] + TABLE_PRESET["columns"]):
        if col_title:
            tx = col_x_offsets[i] + int(10 * sx)
            svg.append(f'  <text x="{tx}" y="{col_y + int(15 * sy)}" fill="#94a3b8" font-family="-apple-system, BlinkMacSystemFont, Roboto, sans-serif" font-size="{int(11 * sy)}" font-weight="600">{html.escape(col_title)}</text>')
        svg.append(f'  <line x1="{col_x_offsets[i]}" y1="{col_y}" x2="{col_x_offsets[i]}" y2="{table_y + table_h}" stroke="#1f2a3d" stroke-width="0.8"/>')

    # Data Rows
    row_step = int(19 * sy)
    for r_idx, row in enumerate(TABLE_PRESET["rows"]):
        ry = col_y + col_h + r_idx * row_step
        svg.append(f'  <line x1="{card_x}" y1="{ry}" x2="{card_x + card_w}" y2="{ry}" stroke="#1c2536" stroke-width="0.8"/>')
        
        for c_idx, val in enumerate(row):
            cx = col_x_offsets[c_idx] + int(10 * sx)
            color = "#a7f3d0" if val == "Passed" else ("#fcd34d" if "'" in val else "#cbd5e1")
            if c_idx == 0:
                color = "#64748b"
            svg.append(f'  <text x="{cx}" y="{ry + int(14 * sy)}" fill="{color}" font-family="SFMono-Regular, Menlo, Monaco, Consolas, monospace" font-size="{int(11 * sy)}">{html.escape(val)}</text>')

    # Table scrollbar
    sb_x = card_x + card_w - int(8 * sx)
    svg.append(f'  <rect x="{sb_x}" y="{table_y + t_header_h + 4}" width="{int(4 * sx)}" height="{table_h - t_header_h - 8}" rx="2" fill="#1e293b"/>')
    svg.append(f'  <rect x="{sb_x}" y="{table_y + t_header_h + 12}" width="{int(4 * sx)}" height="{int(45 * sy)}" rx="2" fill="#475569"/>')

    # 3. Right Side: Hero Branding & Language Ecosystem
    hero_center_x = int(1040 * sx)

    # Dragon Logo Badge (White Circle)
    logo_radius = int(112 * sy)
    logo_cy = int(180 * sy)
    svg.append(f'  <!-- Hero Dragon Logo Badge -->')
    svg.append(f'  <circle cx="{hero_center_x}" cy="{logo_cy}" r="{logo_radius + 15}" fill="url(#logo-glow)"/>')
    svg.append(f'  <circle cx="{hero_center_x}" cy="{logo_cy}" r="{logo_radius}" fill="#ffffff" filter="url(#logo-shadow)"/>')

    # Vector Dragon inside circle
    scale_factor = (logo_radius * 2.0 * 0.90) / 5064.0
    trans_x = hero_center_x - (logo_radius * 0.90)
    trans_y = logo_cy - (logo_radius * 0.90)
    svg.append(f'  <g transform="translate({trans_x}, {trans_y}) scale({scale_factor})">')
    svg.append(dragon_paths)
    svg.append('  </g>')

    # Title: FrySharp
    title_y = int(350 * sy)
    svg.append(f'  <text x="{hero_center_x}" y="{title_y}" text-anchor="middle" fill="#ffffff" font-family="-apple-system, BlinkMacSystemFont, \'Segoe UI\', Roboto, Helvetica, Arial, sans-serif" font-size="{int(74 * sy)}" font-weight="900" letter-spacing="-1.5">{html.escape(title)}</text>')

    # Subtitle: Polyglot Notebook & IDE
    sub_y = int(402 * sy)
    svg.append(f'  <text x="{hero_center_x}" y="{sub_y}" text-anchor="middle" fill="#f8fafc" font-family="-apple-system, BlinkMacSystemFont, \'Segoe UI\', Roboto, Helvetica, Arial, sans-serif" font-size="{int(29 * sy)}" font-weight="700">{html.escape(subtitle)}</text>')

    # Symmetrical 2-Line Language Dock with Uniform Tiles
    tile_w = int(58 * sx)
    tile_h = int(58 * sy)
    gap_x = int(16 * sx)
    gap_y = int(14 * sy)

    # 5 icons on row 1, 4 icons on row 2
    mid = (len(languages) + 1) // 2
    row1 = languages[:mid]
    row2 = languages[mid:]

    r1_n = len(row1)
    r2_n = len(row2)

    r1_total_w = r1_n * tile_w + (r1_n - 1) * gap_x
    r2_total_w = r2_n * tile_w + (r2_n - 1) * gap_x
    max_row_w = max(r1_total_w, r2_total_w)

    dock_pad_x = int(28 * sx)
    dock_pad_y = int(18 * sy)
    dock_w = max_row_w + 2 * dock_pad_x
    dock_h = 2 * tile_h + gap_y + 2 * dock_pad_y
    dock_x = hero_center_x - dock_w // 2
    dock_y = int(445 * sy)

    svg.append('  <!-- Symmetrical Tiled Language Dock -->')
    svg.append(f'  <rect x="{dock_x}" y="{dock_y}" width="{dock_w}" height="{dock_h}" rx="24" fill="#101725" fill-opacity="0.88" stroke="#26354a" stroke-width="1.5" filter="url(#card-shadow)"/>')

    # Row 1 start
    r1_start_x = dock_x + (dock_w - r1_total_w) // 2
    r1_y = dock_y + dock_pad_y

    # Row 2 start (staggered & centered under Row 1)
    r2_start_x = dock_x + (dock_w - r2_total_w) // 2
    r2_y = r1_y + tile_h + gap_y

    icon_size = int(40 * sy)
    icon_pad = (tile_w - icon_size) // 2

    # Render Row 1
    for i, lang in enumerate(row1):
        tx = r1_start_x + i * (tile_w + gap_x)
        ty = r1_y
        svg.append(f'  <rect x="{tx}" y="{ty}" width="{tile_w}" height="{tile_h}" rx="14" fill="url(#tile-grad)" stroke="#334560" stroke-width="1" filter="url(#tile-shadow)"/>')
        ix = tx + icon_pad
        iy = ty + icon_pad
        if lang in icons_data:
            svg.append(f'  <svg x="{ix}" y="{iy}" width="{icon_size}" height="{icon_size}" viewBox="0 0 128 128">{icons_data[lang]}</svg>')
        elif lang in SVG_SYMBOLS:
            svg.append(f'  <use href="#icon-{lang}" x="{ix}" y="{iy}" width="{icon_size}" height="{icon_size}"/>')
        else:
            svg.append(f'  <text x="{tx + tile_w // 2}" y="{ty + tile_h // 2 + 5}" text-anchor="middle" fill="#38bdf8" font-family="sans-serif" font-size="12" font-weight="bold">{lang[:3].upper()}</text>')

    # Render Row 2
    for i, lang in enumerate(row2):
        tx = r2_start_x + i * (tile_w + gap_x)
        ty = r2_y
        svg.append(f'  <rect x="{tx}" y="{ty}" width="{tile_w}" height="{tile_h}" rx="14" fill="url(#tile-grad)" stroke="#334560" stroke-width="1" filter="url(#tile-shadow)"/>')
        ix = tx + icon_pad
        iy = ty + icon_pad
        if lang in icons_data:
            svg.append(f'  <svg x="{ix}" y="{iy}" width="{icon_size}" height="{icon_size}" viewBox="0 0 128 128">{icons_data[lang]}</svg>')
        elif lang in SVG_SYMBOLS:
            svg.append(f'  <use href="#icon-{lang}" x="{ix}" y="{iy}" width="{icon_size}" height="{icon_size}"/>')
        else:
            svg.append(f'  <text x="{tx + tile_w // 2}" y="{ty + tile_h // 2 + 5}" text-anchor="middle" fill="#38bdf8" font-family="sans-serif" font-size="12" font-weight="bold">{lang[:3].upper()}</text>')

    # 4. Center Play Button Overlay (YouTube video style)
    if show_play_button:
        play_cx = int(width * 0.495)
        play_cy = int(height * 0.49)
        play_radius = int(86 * sy)
        tri_size = int(48 * sy)
        svg.append('  <!-- Center Frosted Play Button -->')
        svg.append(f'  <circle cx="{play_cx}" cy="{play_cy}" r="{play_radius}" fill="#0f172a" fill-opacity="0.45" stroke="#ffffff" stroke-opacity="0.25" stroke-width="2.5"/>')
        x1 = play_cx - int(tri_size * 0.35)
        y1 = play_cy - int(tri_size * 0.55)
        x2 = play_cx + int(tri_size * 0.65)
        y2 = play_cy
        x3 = play_cx - int(tri_size * 0.35)
        y3 = play_cy + int(tri_size * 0.55)
        svg.append(f'  <polygon points="{x1},{y1} {x2},{y2} {x3},{y3}" fill="#cbd5e1" fill-opacity="0.75"/>')

    svg.append('</svg>\n')
    return "\n".join(svg)


def export_raster(svg_path, out_path, width=1376, height=768):
    """Optionally renders SVG to PNG/JPEG using Google Chrome headless."""
    chrome_bin = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
    if os.path.exists(chrome_bin):
        abs_svg = os.path.abspath(svg_path)
        cmd = [
            chrome_bin,
            "--headless",
            f"--screenshot={os.path.abspath(out_path)}",
            f"--window-size={width},{height}",
            f"file://{abs_svg}"
        ]
        subprocess.run(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=True)
        return True
    return False


def main():
    parser = argparse.ArgumentParser(description="Generate FrySharp SVG & Image Thumbnails")
    parser.add_argument("--out", "-o", default="Assets/frysharp-thumbnail.svg", help="Output path (default: Assets/frysharp-thumbnail.svg)")
    parser.add_argument("--width", "-W", type=int, default=1376, help="Width in pixels (default: 1376)")
    parser.add_argument("--height", "-H", type=int, default=768, help="Height in pixels (default: 768)")
    parser.add_argument("--title", default="FrySharp", help="Title text (default: FrySharp)")
    parser.add_argument("--subtitle", default="Polyglot Notebook & IDE", help="Subtitle text")
    parser.add_argument("--languages", nargs="+", default=["csharp", "python", "rust", "go", "java", "cpp", "javascript", "fsharp", "sql"], help="List of languages for bottom dock")
    parser.add_argument("--no-play-button", action="store_true", help="Omit the center play button overlay")
    parser.add_argument("--logo", default="Assets/Appiconlogo.svg", help="Path to dragon logo SVG")
    parser.add_argument("--render-raster", action="store_true", help="Also render PNG and JPG versions")

    args = parser.parse_args()

    svg_content = build_svg(
        width=args.width,
        height=args.height,
        title=args.title,
        subtitle=args.subtitle,
        languages=args.languages,
        show_play_button=not args.no_play_button,
        dragon_logo_svg_path=args.logo
    )

    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as f:
        f.write(svg_content)
    print(f"Successfully generated vector thumbnail: {args.out} ({args.width}x{args.height})")

    if args.render_raster:
        base = os.path.splitext(args.out)[0]
        png_path = base + ".png"
        jpg_path = base + ".jpg"
        if export_raster(args.out, png_path, args.width, args.height):
            print(f"Rendered raster PNG: {png_path}")
            from PIL import Image
            img = Image.open(png_path).convert("RGB")
            img.save(jpg_path, quality=95)
            print(f"Rendered raster JPG: {jpg_path}")


if __name__ == "__main__":
    main()
