from __future__ import annotations

import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[2]
QA_ROOT = ROOT / "tmp" / "pdfs"


def make_sheets(kind: str, columns: int = 4, rows: int = 3) -> None:
    source = QA_ROOT / kind
    pages = sorted(source.glob("page-*.png"), key=lambda p: int(p.stem.split("-")[-1]))
    if not pages:
        raise RuntimeError(f"No rendered pages in {source}")

    thumb_width = 260
    label_height = 24
    padding = 12
    with Image.open(pages[0]) as first:
        ratio = first.height / first.width
    thumb_height = int(thumb_width * ratio)
    sheet_width = padding + columns * (thumb_width + padding)
    sheet_height = padding + rows * (thumb_height + label_height + padding)
    per_sheet = columns * rows
    font = ImageFont.load_default()

    for sheet_index in range(math.ceil(len(pages) / per_sheet)):
        canvas = Image.new("RGB", (sheet_width, sheet_height), "#D9DEE7")
        draw = ImageDraw.Draw(canvas)
        subset = pages[sheet_index * per_sheet : (sheet_index + 1) * per_sheet]
        for slot, page in enumerate(subset):
            row, col = divmod(slot, columns)
            x = padding + col * (thumb_width + padding)
            y = padding + row * (thumb_height + label_height + padding)
            with Image.open(page) as image:
                thumb = image.convert("RGB")
                thumb.thumbnail((thumb_width, thumb_height), Image.Resampling.LANCZOS)
                canvas.paste(thumb, (x, y))
            page_number = int(page.stem.split("-")[-1])
            draw.text((x, y + thumb_height + 5), f"{kind} page {page_number}", fill="#111827", font=font)
        output = QA_ROOT / f"{kind}-contact-{sheet_index + 1}.png"
        canvas.save(output, quality=92)


def main() -> None:
    make_sheets("source")
    make_sheets("manual-final2")


if __name__ == "__main__":
    main()
