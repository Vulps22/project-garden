# Yggdrasil's rune atlas: eight glowing Elder Futhark glyphs, 4×2, white on clear, for the sapling's rune fountain.
# Run: python3 yggdrasil_runes.py <out.png>   (the seed's five glyphs, from Models/Yggdrasil/Yggdrasil.blender.py, plus three)
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

GLYPHS = [
    [((0, -1), (0, 1)), ((0, 0.2), (-0.6, 1)), ((0, 0.2), (0.6, 1))],                                   # algiz
    [((-0.3, -1), (-0.3, 1)), ((-0.3, 0.1), (0.4, 0.6)), ((-0.3, 0.6), (0.4, 1.0))],                     # fehu
    [((-0.3, -1), (-0.3, 1)), ((-0.3, 1.0), (0.4, 0.4)), ((-0.3, 0.4), (0.4, -0.2))],                    # ansuz
    [((0, -1), (0, 1)), ((0, 1), (-0.55, 0.4)), ((0, 1), (0.55, 0.4))],                                  # tiwaz
    [((-0.6, -1), (-0.6, 1)), ((0.6, -1), (0.6, 1)), ((-0.6, 1), (0.6, -1)), ((-0.6, -1), (0.6, 1))],    # dagaz
    [((-0.3, -1), (-0.3, 1)), ((-0.3, 1), (0.4, 0.5)), ((0.4, 0.5), (-0.3, 0)), ((-0.3, 0), (0.4, -1))], # raido
    [((-0.55, 1), (0.55, -1)), ((0.55, 1), (-0.55, -1))],                                                # gebo
    [((-0.3, -1), (-0.3, 1)), ((0.4, -1), (0.4, 1)), ((-0.3, 0.6), (0.4, 0.1))],                         # hagalaz
]

CELL, COLS, ROWS, SS = 128, 4, 2, 4   # SS: drawn 4× large and shrunk, for smooth strokes


def main(out):
    big = CELL * SS
    sheet = Image.new("L", (CELL * COLS, CELL * ROWS), 0)
    for i, strokes in enumerate(GLYPHS):
        cell = Image.new("L", (big, big), 0)
        d = ImageDraw.Draw(cell)
        half, scale = big / 2, big * 0.36
        for (x0, y0), (x1, y1) in strokes:
            a = (half + x0 * scale, half - y0 * scale)
            b = (half + x1 * scale, half - y1 * scale)
            w = int(big * 0.07)
            d.line([a, b], fill=255, width=w)
            for p in (a, b):
                d.ellipse([p[0] - w / 2, p[1] - w / 2, p[0] + w / 2, p[1] + w / 2], fill=255)
        cell = cell.resize((CELL, CELL), Image.LANCZOS)
        # A soft halo under the crisp glyph, so it glows when blended additively.
        glow = cell.filter(ImageFilter.GaussianBlur(7)).point(lambda v: min(255, int(v * 1.6)))
        cell = Image.fromarray(np.maximum(np.asarray(glow) * 0.55, np.asarray(cell)).astype("uint8"))
        sheet.paste(cell, ((i % COLS) * CELL, (i // COLS) * CELL))
    rgba = Image.merge("RGBA", [Image.new("L", sheet.size, 255)] * 3 + [sheet])
    rgba.save(out)


if __name__ == "__main__":
    main(sys.argv[1])
