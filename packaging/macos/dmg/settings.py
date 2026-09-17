"""dmgbuild settings for the FrySharp disk image.

Invoked by packaging/macos/make-dmg.sh:

    dmgbuild -s packaging/macos/dmg/settings.py \
             -D app=/path/to/FrySharp.app -D assets=packaging/macos/dmg \
             "FrySharp" FrySharp-1.0.1-arm64.dmg

dmgbuild writes the Finder layout straight into the .DS_Store via ds_store /
mac_alias without driving Finder over AppleScript.
"""

import os.path

application = defines["app"]  # noqa: F821 - injected by dmgbuild
here = defines["assets"]  # noqa: F821
macos_dir = os.path.dirname(os.path.abspath(here))
app_name = os.path.basename(application)

format = "UDZO"
compression_level = 9
filesystem = "HFS+"

files = [application]
symlinks = {"Applications": "/Applications"}

icon = os.path.join(macos_dir, "AppIcon.icns")
background = os.path.join(here, "background.png")

window_rect = ((200, 120), (660, 428))
default_view = "icon-view"
show_icon_preview = False
include_icon_view_settings = True
include_list_view_settings = False

arrange_by = None
grid_offset = (0, 0)
label_pos = "bottom"
text_size = 13
icon_size = 128
icon_locations = {
    app_name: (170, 196),
    "Applications": (490, 196),
}

show_status_bar = False
show_tab_view = False
show_toolbar = False
show_pathbar = False
show_sidebar = False
