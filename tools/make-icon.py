"""Generates the MetaRestoreGeek app icon at every size the range uses.

Same house style as every other Geek app: a rounded square with a vertical blue
gradient and a single flat white glyph, no lettering. Edit the glyph function
below, never the PNGs directly, then re-run this to regenerate everything.

    python tools/make-icon.py
"""

from PIL import Image, ImageDraw
import os

SIZES = [1024, 512, 256, 128]
TOP = (123, 169, 246)
BOTTOM = (47, 109, 237)
WHITE = (255, 255, 255, 255)
OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "icons")


def rounded_square(size: int) -> Image.Image:
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    for y in range(size):
        t = y / (size - 1)
        r = round(TOP[0] + (BOTTOM[0] - TOP[0]) * t)
        g = round(TOP[1] + (BOTTOM[1] - TOP[1]) * t)
        b = round(TOP[2] + (BOTTOM[2] - TOP[2]) * t)
        for x in range(size):
            img.putpixel((x, y), (r, g, b, 255))

    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        [0, 0, size - 1, size - 1], radius=round(size * 0.191), fill=255
    )
    img.putalpha(mask)
    return img


def draw_glyph(draw: ImageDraw.ImageDraw, size: int) -> None:
    """A photo frame with a location pin overlapping its bottom-right corner: 'a photo, with
    its place restored'. One idea, no lettering, reads at 16px as well as at 1024px."""

    cx, cy = size / 2, size / 2

    # The photo frame: a rounded rect outline, slightly up and left of centre, with a small
    # mountain/peak glyph inside it standing in for "a picture" without drawing a literal photo.
    frame_w, frame_h = size * 0.46, size * 0.36
    fx = cx - size * 0.07
    fy = cy - size * 0.05
    stroke = max(2, round(size * 0.045))
    frame_box = [fx - frame_w / 2, fy - frame_h / 2, fx + frame_w / 2, fy + frame_h / 2]
    draw.rounded_rectangle(frame_box, radius=round(size * 0.035), outline=WHITE, width=stroke)

    # A small sun/dot and a mountain fold inside the frame, drawn solid so it reads at small sizes.
    sun_r = size * 0.035
    sun_cx, sun_cy = fx - frame_w * 0.22, fy - frame_h * 0.20
    draw.ellipse([sun_cx - sun_r, sun_cy - sun_r, sun_cx + sun_r, sun_cy + sun_r], fill=WHITE)

    peak_bottom = fy + frame_h / 2 - stroke * 0.9
    peak1 = [(fx - frame_w * 0.32, peak_bottom), (fx - frame_w * 0.05, fy + frame_h * 0.02), (fx + frame_w * 0.18, peak_bottom)]
    peak2 = [(fx + frame_w * 0.02, peak_bottom), (fx + frame_w * 0.24, fy - frame_h * 0.06), (fx + frame_w * 0.34, peak_bottom)]
    draw.polygon(peak1, fill=WHITE)
    draw.polygon(peak2, fill=WHITE)

    # The location pin, overlapping the frame's bottom-right corner, filled solid so it reads as
    # its own distinct shape rather than blending into the frame outline.
    pin_r = size * 0.15
    pin_cx = fx + frame_w / 2 + size * 0.02
    pin_cy = fy + frame_h / 2 + size * 0.02
    draw.ellipse([pin_cx - pin_r, pin_cy - pin_r, pin_cx + pin_r, pin_cy + pin_r], fill=WHITE)
    tip = (pin_cx, pin_cy + pin_r * 1.85)
    tri = [(pin_cx - pin_r * 0.78, pin_cy + pin_r * 0.55), (pin_cx + pin_r * 0.78, pin_cy + pin_r * 0.55), tip]
    draw.polygon(tri, fill=WHITE)
    hole_r = pin_r * 0.4
    draw.ellipse([pin_cx - hole_r, pin_cy - hole_r, pin_cx + hole_r, pin_cy + hole_r], fill=(0, 0, 0, 0))
    # PIL can't punch a transparent hole into an opaque glyph without compositing; draw the hole
    # in the background gradient colour sampled at the pin centre instead, close enough at every
    # size this ships at since the gradient barely shifts across the pin's own height.
    bg_t = pin_cy / (size - 1)
    bg = tuple(round(TOP[i] + (BOTTOM[i] - TOP[i]) * bg_t) for i in range(3))
    draw.ellipse([pin_cx - hole_r, pin_cy - hole_r, pin_cx + hole_r, pin_cy + hole_r], fill=bg)


def build(size: int) -> Image.Image:
    img = rounded_square(size)
    draw = ImageDraw.Draw(img)
    draw_glyph(draw, size)
    return img


def main() -> None:
    os.makedirs(OUT_DIR, exist_ok=True)
    images = {s: build(s) for s in SIZES}

    for s, img in images.items():
        img.save(os.path.join(OUT_DIR, f"metarestoregeek-{s}.png"))

    ico_sizes = [16, 24, 32, 48, 64, 128, 256]
    master = images[1024]
    frames = [master.resize((s, s), Image.LANCZOS) for s in ico_sizes]
    frames[0].save(
        os.path.join(OUT_DIR, "metarestoregeek.ico"),
        format="ICO",
        sizes=[(s, s) for s in ico_sizes],
        append_images=frames[1:],
    )
    print(f"Wrote {len(images)} PNGs and one .ico to {OUT_DIR}")


if __name__ == "__main__":
    main()
