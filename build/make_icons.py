"""Draws the Otium icon and writes every format the packages need.

    python build/make_icons.py

Outputs (committed, so builds don't need Python):
    src/Otium.App/Assets/otium.ico   Windows exe, window and tray icon
    src/Otium.App/Assets/otium.png   in-app logo (256 px)
    build/icons/otium.icns                macOS app bundle
    build/icons/otium.png                 Linux AppImage / desktop entry (512 px)
    build/icons/otium-1024.png            store listings, website
"""
import math
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
SIZE = 1024
SS = 4  # supersampling factor for smooth edges

TOP = (52, 199, 132)     # light green
BOTTOM = (18, 110, 70)   # deep green
WHITE = (255, 255, 255, 255)


def draw_master() -> Image.Image:
    s = SIZE * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))

    # Rounded-square background with a vertical gradient.
    gradient = Image.new("RGBA", (s, s))
    gd = ImageDraw.Draw(gradient)
    for y in range(s):
        t = y / (s - 1)
        colour = tuple(round(TOP[i] + (BOTTOM[i] - TOP[i]) * t) for i in range(3)) + (255,)
        gd.line([(0, y), (s, y)], fill=colour)
    mask = Image.new("L", (s, s), 0)
    margin = round(s * 0.04)
    ImageDraw.Draw(mask).rounded_rectangle([margin, margin, s - margin, s - margin], radius=round(s * 0.22), fill=255)
    img.paste(gradient, (0, 0), mask)

    d = ImageDraw.Draw(img)
    cx = cy = s / 2

    # A cycle arrow: thick arc with an arrowhead, suggesting reused ("spare") cycles.
    r = s * 0.30
    width = round(s * 0.085)
    start, end = 120, 400  # degrees, clockwise from 3 o'clock
    d.arc([cx - r, cy - r, cx + r, cy + r], start=start, end=end, fill=WHITE, width=width)

    # Arrowhead at the end of the arc, pointing along the direction of travel.
    # PIL draws the arc's stroke inward from the bounding radius, so its centre line sits at r - width/2.
    a = math.radians(end)
    centre_r = r - width / 2
    radial = (math.cos(a), math.sin(a))
    tangent = (-math.sin(a), math.cos(a))  # clockwise direction of travel
    base = (cx + centre_r * radial[0], cy + centre_r * radial[1])
    half = width * 1.15
    head_len = width * 1.5
    inner = (base[0] - radial[0] * half, base[1] - radial[1] * half)
    outer = (base[0] + radial[0] * half, base[1] + radial[1] * half)
    tip = (base[0] + tangent[0] * head_len, base[1] + tangent[1] * head_len)
    d.polygon([inner, outer, tip], fill=WHITE)

    # Lightning bolt in the middle: power being put to work.
    u = s * 0.0105
    bolt = [(4, -17), (-8, 2), (-1, 2), (-4, 17), (8, -3), (1, -3), (4, -17)]
    d.polygon([(cx + x * u, cy + y * u) for x, y in bolt], fill=WHITE)

    return img.resize((SIZE, SIZE), Image.LANCZOS)


def main() -> None:
    master = draw_master()
    icons = ROOT / "build" / "icons"
    icons.mkdir(parents=True, exist_ok=True)

    master.save(icons / "otium-1024.png")
    master.resize((512, 512), Image.LANCZOS).save(icons / "otium.png")
    master.save(icons / "otium.icns")
    master.resize((256, 256), Image.LANCZOS).save(ROOT / "src" / "Otium.App" / "Assets" / "otium.png")
    master.save(
        ROOT / "src" / "Otium.App" / "Assets" / "otium.ico",
        sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)],
    )
    print("icons written to", icons)


if __name__ == "__main__":
    main()
