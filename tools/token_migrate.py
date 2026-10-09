#!/usr/bin/env python3
"""Turn hard-coded layout values in the studio's .axaml files into layout design tokens.

A value becomes a token *key* on the element or in the style setter, which Tokens (Services/Extensibility/Theming/
Layout/Tokens.cs) turns into the value for the current layout:

    FontSize="11"                                   ->  lt:Tokens.FontSize="DsFontSize200"
    <Setter Property="CornerRadius" Value="6" />    ->  <Setter Property="lt:Tokens.CornerRadius" Value="DsRadiusMD" />

(not {DynamicResource}: in shared style setters that cost ~140 MB, see Tokens.cs). Deterministic and idempotent: a
value that is already a key, a resource or a binding is left alone, so running it twice changes nothing the second
time. The token names and their default values come from Services/Extensibility/Theming/Layout/LayoutTokenMapper.cs
(and Styles/Tokens/StudioLayoutTokens.axaml). The `lt` namespace is added to each file's root element.

    python3 tools/token_migrate.py --dry-run                 # report what would change, per file and rule
    python3 tools/token_migrate.py Views/CSharpSettingsView.axaml Controls/Settings   # only these files/folders
    python3 tools/token_migrate.py                           # every .axaml of the studio
    python3 tools/token_migrate.py --to-keys                 # {DynamicResource <layout token>} -> keys
    python3 tools/token_migrate.py --reverse [--only DsFontSize,...]   # keys back to default literals (A/B runs)

Rules (attributes and style setters alike):
  FontSize        -> DsFontSize050..800 (the type ramp; near-duplicate sizes merged into the nearest step)
  FontWeight      -> DsWeightBody / Label / Emphasis / Strong
  LetterSpacing   -> DsTracking050..120
  CornerRadius    -> DsRadiusXS..4XL / Full, with Top/Bottom/Left/Right for one-sided corners; 0 stays
  BorderThickness -> DsBorderThin / Medium / Top / Bottom / Left / Right / NoBottom / AccentLeft; 0 stays
  FontFamily      -> DsCodeFontFamily for monospace font lists
  Height          -> DsControlHeight100..500 on text controls (buttons, boxes) of 26-36 px
  Spacing         -> DsSpace10..32 on StackPanels spaced 10 px or more (gaps next to icons stay)
  Cards           -> a Border with a surface background, the border brush and a radius of 8-16 gets DsRadiusCard (10),
                     DsCardPadding (18,16), DsCardBorder and DsCardShadow
"""
import argparse
import collections
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
DEFAULT_FOLDERS = ["Views", "Controls", "Styles", "Runner", "Charting/Controls", "Charting3D/Controls"]
SKIP = {"Styles/Tokens/StudioPaletteTokens.axaml", "Styles/Tokens/StudioLayoutTokens.axaml"}

FONT_SIZE = {
    "7": "050", "7.5": "050", "8": "050", "8.5": "050", "9": "075", "9.5": "075", "10": "100", "10.5": "150",
    "11": "200", "11.5": "250", "12": "300", "12.5": "350", "13": "400", "13.5": "400", "14": "500", "14.5": "500",
    "15": "600", "16": "600", "17": "600", "18": "700", "19": "700", "20": "700", "22": "700", "23": "800",
    "24": "800", "26": "800", "28": "800",
}
FONT_WEIGHT = {"Normal": "Body", "Regular": "Body", "Medium": "Label", "SemiBold": "Emphasis", "DemiBold": "Emphasis",
               "Bold": "Strong", "ExtraBold": "Strong", "UltraBold": "Strong", "Black": "Strong", "Heavy": "Strong"}
TRACKING = {"0.5": "050", "0.6": "060", "0.7": "060", "0.8": "080", "1": "100", "1.0": "100", "1.2": "120", "1.5": "120"}
RADIUS = {"2": "XS", "3": "XS", "3.5": "XS", "4": "SM", "5": "SM", "6": "MD", "7": "MD", "8": "LG", "9": "LG",
          "10": "XL", "11": "XL", "12": "2XL", "14": "2XL", "16": "3XL", "17": "3XL", "20": "3XL", "24": "4XL",
          "999": "Full", "9999": "Full", "99999": "Full"}
BORDER = {"1": "Thin", "1,1,1,1": "Thin", "1.5": "Medium", "0,1,0,0": "Top", "0,0,0,1": "Bottom", "1,0,0,0": "Left",
          "0,0,1,0": "Right", "1,1,1,0": "NoBottom", "2,0,0,0": "AccentLeft", "3,0,0,0": "AccentLeft"}
CONTROL_HEIGHT = {"26": "100", "28": "150", "30": "200", "32": "300", "34": "400", "36": "500"}
TEXT_CONTROLS = {"Button", "ToggleButton", "TextBox", "ComboBox", "AutoCompleteBox", "NumericUpDown", "SplitButton",
                 "DropDownButton", "RepeatButton", "CalendarDatePicker"}
SPACE = {"10", "12", "14", "16", "18", "20", "24", "32"}
MONO = re.compile(r"mono|consolas|menlo|cascadia|courier|fira code|jetbrains", re.I)

LT_NS = 'xmlns:lt="clr-namespace:PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout;assembly=CSharpEditorPlugin"'
ALWAYS_KEYED_IN_STYLES = {"FontSize", "FontWeight", "FontFamily", "LetterSpacing", "CornerRadius", "BorderThickness", "BoxShadow"}
KEYED = {"FontSize", "FontWeight", "FontFamily", "LetterSpacing", "CornerRadius", "BorderThickness", "Padding",
         "Height", "MinHeight", "Spacing", "BoxShadow"}
LAYOUT_KEYS = set(re.findall(r'x:Key="([^"]+)"', (ROOT / "Styles/Tokens/StudioLayoutTokens.axaml").read_text(encoding="utf-8")))

TAG = re.compile(r'<([A-Za-z][\w:.]*)((?:[^<>"]|"[^"]*")*?)(/?)>', re.S)
ATTR = re.compile(r'(\s)((?:[A-Za-z]\w*\.)?)([A-Za-z]\w*)="([^"]*)"')


def radius_token(value):
    v = value.replace(" ", "")
    if v in RADIUS:
        return "DsRadius" + RADIUS[v]
    parts = v.split(",")
    if len(parts) == 4:
        a, b, c, d = parts
        if a == b and c == d == "0" and a in RADIUS:
            return "DsRadius%sTop" % RADIUS[a]
        if a == b == "0" and c == d and c in RADIUS:
            return "DsRadius%sBottom" % RADIUS[c]
        if a == d and b == c == "0" and a in RADIUS:
            return "DsRadius%sLeft" % RADIUS[a]
        if a == d == "0" and b == c and b in RADIUS:
            return "DsRadius%sRight" % RADIUS[b]
        if a == b == c == d and a in RADIUS:
            return "DsRadius" + RADIUS[a]
    return None


def token_for(element, prop, value, report):
    """The token key for a literal `value` of property `prop` on `element`, or None to leave it."""
    v = value.strip()
    if not v or v.startswith("{"):
        return None
    if prop == "FontSize":
        if v in FONT_SIZE:
            return "DsFontSize" + FONT_SIZE[v]
        report["unmapped FontSize " + v] += 1
    elif prop == "FontWeight":
        if v in FONT_WEIGHT:
            return "DsWeight" + FONT_WEIGHT[v]
        report["unmapped FontWeight " + v] += 1
    elif prop == "LetterSpacing":
        if v in TRACKING:
            return "DsTracking" + TRACKING[v]
        if v not in ("0",):
            report["unmapped LetterSpacing " + v] += 1
    elif prop == "CornerRadius":
        if v.replace(",", "").replace("0", "").strip() == "":
            return None
        key = radius_token(v)
        if key:
            return key
        report["unmapped CornerRadius " + v] += 1
    elif prop == "BorderThickness":
        if v.replace(",", "").replace("0", "").strip() == "":
            return None
        if v.replace(" ", "") in BORDER:
            return "DsBorder" + BORDER[v.replace(" ", "")]
        report["unmapped BorderThickness " + v] += 1
    elif prop == "FontFamily":
        if MONO.search(v):
            return "DsCodeFontFamily"
    elif prop in ("Height", "MinHeight") and element in TEXT_CONTROLS:
        if v in CONTROL_HEIGHT:
            return "DsControlHeight" + CONTROL_HEIGHT[v]
    elif prop == "Spacing" and element == "StackPanel":
        if v in SPACE:
            return "DsSpace" + v
    return None


def is_card(element, attrs):
    if element != "Border":
        return False
    background = attrs.get("Background", "")
    brush = attrs.get("BorderBrush", "")
    radius = attrs.get("CornerRadius", "")
    if not ("DsSurface" in background and "DsBorderBrush" in brush and radius in ("8", "10", "12", "14", "16")):
        return False
    # A fixed-height box (a search field, a pill) or a slim padding is a control, not a card.
    if "Height" in attrs or "MaxHeight" in attrs:
        return False
    padding = attrs.get("Padding", "")
    if padding and not padding.startswith("{"):
        try:
            if min(float(x) for x in padding.split(",")) < 8:
                return False
        except ValueError:
            return False
    return True


def migrate_tag(match, report):
    element_full, body, closing = match.group(1), match.group(2), match.group(3)
    attrs = {m.group(3): m.group(4) for m in ATTR.finditer(body) if not m.group(2)}

    # A style setter: <Setter Property="FontSize" Value="11" />. In styles, these properties always go through a key
    # (a token, or "=value" when there is none): which style wins is then decided on the key (see Tokens.cs).
    if element_full == "Setter" and "Property" in attrs and "Value" in attrs:
        if attrs["Property"].startswith("lt:"):
            return match.group(0)
        prop = attrs["Property"].split(".")[-1]
        key = token_for("Setter", prop, attrs["Value"], report)
        if key is None and prop in ALWAYS_KEYED_IN_STYLES and not attrs["Property"].startswith("lt:"):
            value = attrs["Value"].strip()
            if value.startswith("{"):
                report["setter %s left as binding %s" % (prop, value.split()[0])] += 1
            else:
                key = "=" + value
        if key:
            report["setter " + prop] += 1
            body = re.sub(r'(\sProperty=)"[^"]*"', lambda m: '%s"lt:Tokens.%s"' % (m.group(1), prop), body, count=1)
            body = re.sub(r'(\sValue=)"[^"]*"', lambda m: '%s"%s"' % (m.group(1), key), body, count=1)
        return "<%s%s%s>" % (element_full, body, closing)

    card = is_card(element_full, attrs)

    def replace(m):
        space, owner, prop, value = m.group(1), m.group(2), m.group(3), m.group(4)
        key = None
        if card and not owner:
            if prop == "CornerRadius" and value == "10":
                key = "DsRadiusCard"
            elif prop == "Padding" and value.replace(" ", "") == "18,16":
                key = "DsCardPadding"
            elif prop == "BorderThickness" and value.strip() == "1":
                key = "DsCardBorder"
        if key is None:
            key = token_for(element_full, prop, value, report)
        if key is None:
            return m.group(0)
        report[prop + (" (card)" if key in ("DsRadiusCard", "DsCardPadding", "DsCardBorder") else "")] += 1
        return '%slt:Tokens.%s="%s"' % (space, prop, key)

    body = ATTR.sub(replace, body)
    if card and "BoxShadow=" not in body:
        report["BoxShadow (card)"] += 1
        trailing = body[len(body.rstrip()):]
        body = body.rstrip() + ' lt:Tokens.BoxShadow="DsCardShadow"' + trailing
    return "<%s%s%s>" % (element_full, body, closing)


def migrate_text(text, report):
    out, pos = [], 0
    for m in TAG.finditer(text):
        # Leave comments, CDATA and processing instructions alone.
        if m.group(1).startswith(("!", "?")):
            continue
        out.append(text[pos:m.start()])
        out.append(migrate_tag(m, report))
        pos = m.end()
    out.append(text[pos:])
    return with_namespace("".join(out))


def with_namespace(text):
    """Adds xmlns:lt to the root element when the file uses lt: and doesn't declare it."""
    if "lt:Tokens." not in text or LT_NS in text:
        return text
    root = re.search(r'<(?!\?|!)([A-Za-z][\w:.]*)\b', text)
    insert_at = root.end()
    return text[:insert_at] + " " + LT_NS + text[insert_at:]


def to_keys_text(text, report):
    """{DynamicResource <layout token>} on keyed properties (attributes and setters) -> keys."""
    def attr(m):
        space, owner, prop, key = m.group(1), m.group(2), m.group(3), m.group(4)
        if prop not in KEYED or key not in LAYOUT_KEYS:
            return m.group(0)
        report["attribute " + prop] += 1
        return '%slt:Tokens.%s="%s"' % (space, prop, key)

    def setter(m):
        prop, key = m.group(2).split(".")[-1], m.group(4)
        if prop not in KEYED or key not in LAYOUT_KEYS:
            return m.group(0)
        report["setter " + prop] += 1
        return '%s"lt:Tokens.%s"%s"%s"' % (m.group(1), prop, m.group(3), key)

    text = re.sub(r'(<Setter\s+Property=)"([\w.]+)"(\s+Value=)"\{DynamicResource (\w+)\}"', setter, text)
    text = re.sub(r'(\s)((?:[A-Za-z]\w*\.)?)([A-Za-z]\w*)="\{DynamicResource (\w+)\}"', attr, text)
    return with_namespace(text)


def reverse_text(text, only=None):
    """Every layout token back to its default literal (for A/B measurements); font families stay tokens."""
    default = {}
    for k, v in FONT_SIZE.items():
        default.setdefault("DsFontSize" + v, k)
    default.update({"DsFontSize050": "8.5", "DsFontSize075": "9.5", "DsFontSize400": "13", "DsFontSize500": "14",
                    "DsFontSize600": "16", "DsFontSize700": "20", "DsFontSize800": "26"})
    default.update({"DsWeightBody": "Normal", "DsWeightLabel": "Medium", "DsWeightEmphasis": "SemiBold", "DsWeightStrong": "Bold"})
    default.update({"DsTracking050": "0.5", "DsTracking060": "0.6", "DsTracking080": "0.8", "DsTracking100": "1", "DsTracking120": "1.2"})
    radii = {"XS": "3", "SM": "4", "MD": "6", "LG": "8", "XL": "10", "2XL": "12", "3XL": "16", "4XL": "24", "Full": "9999"}
    for step, r in radii.items():
        default["DsRadius" + step] = r
        default["DsRadius%sTop" % step] = "%s,%s,0,0" % (r, r)
        default["DsRadius%sBottom" % step] = "0,0,%s,%s" % (r, r)
        default["DsRadius%sLeft" % step] = "%s,0,0,%s" % (r, r)
        default["DsRadius%sRight" % step] = "0,%s,%s,0" % (r, r)
    default.update({"DsRadiusCard": "10", "DsCardPadding": "18,16", "DsCardBorder": "1"})
    default.update({"DsBorder" + v: k for k, v in BORDER.items() if k not in ("1,1,1,1", "3,0,0,0")})
    default.update({"DsControlHeight" + v: k for k, v in CONTROL_HEIGHT.items()})
    default.update({"DsSpace" + v: v for v in SPACE})
    def wanted(key):
        return key in default and (not only or any(key.startswith(prefix) for prefix in only))

    if not only or any("DsCardShadow".startswith(p) for p in only):
        text = re.sub(r' lt:Tokens\.BoxShadow="DsCardShadow"', "", text)
    text = re.sub(r'(<Setter\s+Property=)"lt:Tokens\.(\w+)"(\s+Value=)"(\w+)"',
                  lambda m: '%s"%s"%s"%s"' % (m.group(1), m.group(2), m.group(3), default[m.group(4)]) if wanted(m.group(4)) else m.group(0), text)
    return re.sub(r'(\s)lt:Tokens\.(\w+)="(\w+)"',
                  lambda m: '%s%s="%s"' % (m.group(1), m.group(2), default[m.group(3)]) if wanted(m.group(3)) else m.group(0), text)


def files_from(paths):
    for p in paths:
        path = (ROOT / p).resolve()
        if path.is_dir():
            yield from sorted(path.rglob("*.axaml"))
        elif path.suffix == ".axaml":
            yield path


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("paths", nargs="*", help="files or folders (default: every studio folder)")
    parser.add_argument("--dry-run", action="store_true", help="report only, write nothing")
    parser.add_argument("--reverse", action="store_true", help="tokens back to their default literals (A/B measurements)")
    parser.add_argument("--only", default="", help="with --reverse: only tokens starting with these prefixes (comma-separated)")
    parser.add_argument("--to-keys", action="store_true", help="{DynamicResource <layout token>} -> lt:Tokens keys")
    args = parser.parse_args()

    if args.to_keys:
        total = collections.Counter()
        n = 0
        for path in files_from(args.paths or DEFAULT_FOLDERS):
            rel = path.relative_to(ROOT).as_posix()
            if rel in SKIP:
                continue
            text = path.read_text(encoding="utf-8")
            new = to_keys_text(text, total)
            if new != text:
                n += 1
                if not args.dry_run:
                    path.write_text(new, encoding="utf-8")
        print("%d files %s" % (n, "would change" if args.dry_run else "changed to keys"))
        for k, v in sorted(total.items()):
            print("  %-28s %5d" % (k, v))
        return 0

    if args.reverse:
        n = 0
        for path in files_from(args.paths or DEFAULT_FOLDERS):
            rel = path.relative_to(ROOT).as_posix()
            if rel in SKIP:
                continue
            text = path.read_text(encoding="utf-8")
            new = reverse_text(text, [p for p in args.only.split(",") if p])
            if new != text:
                n += 1
                if not args.dry_run:
                    path.write_text(new, encoding="utf-8")
        print("%d files %s" % (n, "would change" if args.dry_run else "changed back to literals"))
        return 0

    total = collections.Counter()
    unmapped = collections.Counter()
    changed_files = 0
    for path in files_from(args.paths or DEFAULT_FOLDERS):
        rel = path.relative_to(ROOT).as_posix()
        if rel in SKIP or "/bin/" in rel or "/obj/" in rel:
            continue
        text = path.read_text(encoding="utf-8")
        report = collections.Counter()
        new = migrate_text(text, report)
        for k, v in report.items():
            (unmapped if k.startswith("unmapped") else total)[k] += v
        if new != text:
            changed_files += 1
            edits = sum(v for k, v in report.items() if not k.startswith("unmapped"))
            print("%-80s %5d" % (rel, edits))
            if not args.dry_run:
                path.write_text(new, encoding="utf-8")

    print("\n%d files %s" % (changed_files, "would change" if args.dry_run else "changed"))
    for k, v in sorted(total.items()):
        print("  %-28s %5d" % (k, v))
    if unmapped:
        print("\nleft as they are (no token):")
        for k, v in unmapped.most_common():
            print("  %-36s %5d" % (k, v))
    return 0


if __name__ == "__main__":
    sys.exit(main())
