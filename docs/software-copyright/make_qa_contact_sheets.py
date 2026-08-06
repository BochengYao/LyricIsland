from __future__ import annotations

import re
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[2]
QA_ROOT = ROOT / "tmp" / "pdfs"
OUT = QA_ROOT / "v2036-contact-sheets"
JOBS = ("v2036-source", "v2036-manual", "v2036-brief", "v2036-version")


def page_number(path: Path) -> int:
    match = re.search(r"(\d+)$", path.stem)
    if not match:
        raise ValueError(path)
    return int(match.group(1))


def make_sheets(job: str) -> None:
    pages = sorted((QA_ROOT / job).glob("page-*.png"), key=page_number)
    if not pages:
        raise RuntimeError(f"No pages found for {job}")
    columns = 2
    rows = 2
    per_sheet = columns * rows
    thumb_width = 650
    label_height = 32
    padding = 18
    with Image.open(pages[0]) as first:
        ratio = first.height / first.width
    thumb_height = int(thumb_width * ratio)
    sheet_width = padding + columns * (thumb_width + padding)
    sheet_height = padding + rows * (thumb_height + label_height + padding)
    font = ImageFont.truetype(r"C:\Windows\Fonts\arial.ttf", 20)
    OUT.mkdir(parents=True, exist_ok=True)

    for sheet_index in range((len(pages) + per_sheet - 1) // per_sheet):
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
            draw.text(
                (x, y + thumb_height + 4),
                f"{job} page {page_number(page)}",
                fill="#111827",
                font=font,
            )
        canvas.save(OUT / f"{job}-sheet-{sheet_index + 1:02d}.png")


def main() -> None:
    for job in JOBS:
        make_sheets(job)
    print(OUT)


if __name__ == "__main__":
    main()
