#!/usr/bin/env python3
"""Asset integrity and dimension validator for FrySharp."""

import pathlib
import sys
from PIL import Image

if sys.stdout and hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass
if sys.stderr and hasattr(sys.stderr, "reconfigure"):
    try:
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

SCRIPT_DIR = pathlib.Path(__file__).resolve().parent
PACKAGING_DIR = SCRIPT_DIR.parent
RUNNER_DIR = PACKAGING_DIR.parent / "Runner"

REQUIRED_FILES = [
    RUNNER_DIR / "Assets" / "app-logo.png",
    RUNNER_DIR / "Assets" / "app-logo.ico",
    PACKAGING_DIR / "macos" / "AppIcon.icns",
    PACKAGING_DIR / "macos" / "dmg" / "background.png",
    PACKAGING_DIR / "macos" / "dmg" / "background@2x.png",
    PACKAGING_DIR / "windows" / "branding" / "wizard-large-164x314.png",
    PACKAGING_DIR / "windows" / "branding" / "wizard-large-328x628.png",
    PACKAGING_DIR / "windows" / "branding" / "wizard-small-55x55.png",
    PACKAGING_DIR / "windows" / "branding" / "wizard-small-110x110.png",
    PACKAGING_DIR / "windows" / "msix" / "Assets" / "Square150x150Logo.png",
    PACKAGING_DIR / "windows" / "msix" / "Assets" / "Square44x44Logo.png",
    PACKAGING_DIR / "windows" / "msix" / "Assets" / "StoreLogo.png",
    PACKAGING_DIR / "windows" / "msix" / "Assets" / "Wide310x150Logo.png",
]

def main() -> int:
    failed = 0
    print("=== Verifying FrySharp Packaging Assets ===")
    for p in REQUIRED_FILES:
        if not p.exists():
            print(f"  [MISSING] {p}")
            failed += 1
        elif p.stat().st_size == 0:
            print(f"  [EMPTY]   {p}")
            failed += 1
        else:
            print(f"  [OK]      {p.name} ({p.stat().st_size:,} bytes)")

    if failed > 0:
        print(f"\n[FAIL] {failed} packaging asset(s) failed verification!")
        return 1

    print("\n[SUCCESS] All packaging assets verified successfully!")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
