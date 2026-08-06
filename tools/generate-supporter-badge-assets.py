#!/usr/bin/env python3
"""Generate the standalone LYRIC HOVER Pro supporter badge asset pack.

The resulting GLB is a collectible/editable marketing asset. The desktop app
does not load or package it; the app renders the matching badge with WPF.
"""

from __future__ import annotations

import io
import json
import math
import struct
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "artifacts" / "pro-supporter-badge"
FONT_REGULAR = Path(r"C:\Windows\Fonts\msyh.ttc")
FONT_BOLD = Path(r"C:\Windows\Fonts\msyhbd.ttc")
SEGMENTS = 128
GOLD = (201, 154, 50, 255)
GOLD_LIGHT = (247, 215, 120, 255)
GOLD_DARK = (112, 80, 26, 255)
NAVY = (7, 24, 52, 255)


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    path = FONT_BOLD if bold and FONT_BOLD.exists() else FONT_REGULAR
    return ImageFont.truetype(str(path), size)


def centered_text(
    draw: ImageDraw.ImageDraw,
    xy: tuple[float, float],
    text: str,
    text_font: ImageFont.FreeTypeFont,
    fill,
    stroke_width: int = 0,
    stroke_fill=None,
) -> None:
    bounds = draw.textbbox((0, 0), text, font=text_font, stroke_width=stroke_width)
    width = bounds[2] - bounds[0]
    height = bounds[3] - bounds[1]
    draw.text(
        (xy[0] - width / 2, xy[1] - height / 2 - bounds[1]),
        text,
        font=text_font,
        fill=fill,
        stroke_width=stroke_width,
        stroke_fill=stroke_fill,
    )


def gold_surface(size: int) -> Image.Image:
    y, x = np.mgrid[0:size, 0:size]
    nx = x / max(1, size - 1)
    ny = y / max(1, size - 1)
    sweep = np.clip(1.08 - np.abs(nx * 0.68 + ny * 0.32 - 0.48) * 1.45, 0, 1)
    radial = np.clip(1.0 - np.hypot(nx - 0.43, ny - 0.34) * 0.72, 0, 1)
    grain = np.sin((x + y * 0.37) * 0.09) * 0.012
    light = np.clip(0.28 + sweep * 0.54 + radial * 0.24 + grain, 0, 1)
    dark = np.array([44, 33, 14], dtype=np.float32)
    bright = np.array([247, 215, 120], dtype=np.float32)
    rgb = dark + (bright - dark) * light[..., None]
    alpha = np.full((size, size, 1), 255, dtype=np.float32)
    return Image.fromarray(np.uint8(np.concatenate([rgb, alpha], axis=2)), "RGBA")


def circular_badge_base(size: int) -> Image.Image:
    surface = gold_surface(size)
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).ellipse((42, 42, size - 42, size - 42), fill=255)
    surface.putalpha(mask)
    y, x = np.mgrid[0:size, 0:size]
    nx = x / max(1, size - 1)
    ny = y / max(1, size - 1)
    glow = np.clip(1.0 - np.hypot(nx - 0.34, ny - 0.22) * 1.15, 0, 1)
    navy_dark = np.array([3, 12, 30], dtype=np.float32)
    navy_light = np.array([28, 62, 110], dtype=np.float32)
    navy_rgb = navy_dark + (navy_light - navy_dark) * glow[..., None]
    navy_image = Image.fromarray(
        np.uint8(np.concatenate([navy_rgb, np.full((size, size, 1), 255)], axis=2)),
        "RGBA",
    )
    inner_mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(inner_mask).ellipse((174, 174, size - 174, size - 174), fill=255)
    surface.paste(navy_image, (0, 0), inner_mask)
    draw = ImageDraw.Draw(surface)
    draw.ellipse((52, 52, size - 52, size - 52), outline=GOLD_LIGHT, width=18)
    draw.ellipse((142, 142, size - 142, size - 142), outline=GOLD_DARK, width=16)
    draw.ellipse((162, 162, size - 162, size - 162), outline=GOLD_LIGHT, width=8)
    return surface


def spaced_text(draw, center, text, text_font, spacing, fill, shadow=True):
    widths = [draw.textlength(character, font=text_font) for character in text]
    total = sum(widths) + spacing * max(0, len(text) - 1)
    x = center[0] - total / 2
    for character, width in zip(text, widths):
        bounds = draw.textbbox((0, 0), character, font=text_font)
        y = center[1] - (bounds[3] - bounds[1]) / 2 - bounds[1]
        if shadow:
            draw.text((x + 9, y + 12), character, font=text_font, fill=(0, 0, 0, 190))
        draw.text((x, y), character, font=text_font, fill=fill)
        x += width + spacing


def draw_star(draw, center, outer, inner, fill):
    x, y = center
    points = [(x, y - outer), (x + inner, y - inner), (x + outer, y),
              (x + inner, y + inner), (x, y + outer), (x - inner, y + inner),
              (x - outer, y), (x - inner, y - inner)]
    shadow = [(px + 10, py + 12) for px, py in points]
    draw.polygon(shadow, fill=(0, 0, 0, 180))
    draw.polygon(points, fill=fill)


def draw_front_relief(image: Image.Image) -> None:
    draw = ImageDraw.Draw(image)
    shadow = (0, 0, 0, 190)
    capsule = (684, 276, 1364, 416)
    draw.rounded_rectangle((700, 294, 1380, 434), radius=70, fill=shadow)
    draw.rounded_rectangle(capsule, radius=70, fill=GOLD)
    draw.rounded_rectangle((706, 298, 1342, 394), radius=48, fill=(2, 10, 24, 255))
    draw.line((750, 302, 1298, 302), fill=(255, 242, 185, 190), width=5)

    heights = [132, 208, 296, 428, 552, 452, 344, 264, 376, 488,
               592, 448, 332, 236, 372, 496, 432, 316, 232]
    for index, height in enumerate(heights):
        x = 312 + index * 78
        rect = (x, 1040 - height, x + 34, 1040)
        draw.rounded_rectangle((x + 12, 1056 - height, x + 46, 1056), radius=17, fill=shadow)
        draw.rounded_rectangle(rect, radius=17, fill=GOLD)
        draw.line((x + 5, 1050 - height, x + 5, 1028), fill=GOLD_LIGHT, width=5)

    for baseline, amplitude, width in [(975, 132, 32), (1142, 96, 24),
                                       (1298, 84, 22), (1442, 68, 20)]:
        points = []
        for px in range(188, 1861, 10):
            t = (px - 188) / (1861 - 188)
            py = baseline + math.sin(t * math.tau * 2.15 + baseline * 0.01) * amplitude * (0.72 + 0.28 * t)
            points.append((px, py))
        shadow_points = [(px + 12, py + 16) for px, py in points]
        draw.line(shadow_points, fill=shadow, width=width + 8, joint="curve")
        draw.line(points, fill=GOLD, width=width, joint="curve")

    # Raised eighth note.
    draw.line((850, 842, 1240, 748), fill=shadow, width=72)
    draw.line((872, 850, 872, 1270), fill=shadow, width=70)
    draw.line((1202, 770, 1202, 1184), fill=shadow, width=70)
    draw.ellipse((756, 1176, 950, 1340), fill=shadow)
    draw.ellipse((1086, 1090, 1280, 1254), fill=shadow)
    draw.line((830, 820, 1220, 726), fill=GOLD, width=66)
    draw.line((850, 824, 850, 1248), fill=GOLD, width=64)
    draw.line((1180, 744, 1180, 1162), fill=GOLD, width=64)
    draw.ellipse((734, 1154, 928, 1318), fill=GOLD)
    draw.ellipse((1064, 1068, 1258, 1232), fill=GOLD)

    draw_star(draw, (1524, 428), 84, 34, GOLD_LIGHT)
    draw_star(draw, (1510, 1316), 34, 14, GOLD)
    draw_star(draw, (472, 1336), 28, 12, GOLD)
    for px, py, radius in [(420, 1208, 12), (528, 1112, 8), (1408, 1114, 10),
                           (1624, 1180, 8), (674, 1472, 9), (1360, 1458, 9)]:
        draw.ellipse((px - radius, py - radius, px + radius, py + radius), fill=GOLD)

    spaced_text(draw, (1024, 1580), "LYRIC HOVER", font(96, True), 24, GOLD_LIGHT)
    pro = (720, 1684, 1328, 1852)
    draw.rounded_rectangle((734, 1700, 1342, 1868), radius=84, fill=shadow)
    draw.rounded_rectangle(pro, radius=84, fill=(3, 14, 33, 255), outline=GOLD_LIGHT, width=14)
    centered_text(draw, (1024, 1768), "PRO", font(95, True), GOLD_LIGHT, 2, GOLD_DARK)
    for px in (644, 1404):
        draw.ellipse((px - 16, 1752, px + 16, 1784), fill=GOLD_LIGHT)


def make_front() -> Image.Image:
    image = circular_badge_base(2048)
    draw_front_relief(image)
    mask = Image.new("L", image.size, 0)
    ImageDraw.Draw(mask).ellipse((42, 42, 2006, 2006), fill=255)
    image.putalpha(mask)
    return image


def engraved_text(
    draw: ImageDraw.ImageDraw,
    xy: tuple[int, int],
    text: str,
    text_font: ImageFont.FreeTypeFont,
) -> None:
    centered_text(draw, (xy[0] + 4, xy[1] + 4), text, text_font, (255, 235, 178, 105))
    centered_text(draw, xy, text, text_font, (74, 69, 63, 255))


def make_back() -> Image.Image:
    image = gold_surface(2048)
    mask = Image.new("L", image.size, 0)
    ImageDraw.Draw(mask).ellipse((42, 42, 2006, 2006), fill=255)
    image.putalpha(mask)
    draw = ImageDraw.Draw(image)
    for line in range(120, 1928, 9):
        draw.line((140, line, 1908, line + 24), fill=(255, 244, 195, 30), width=2)
    draw.ellipse((56, 56, 1992, 1992), outline=GOLD_LIGHT, width=18)
    draw.ellipse((226, 226, 1822, 1822), outline=(111, 67, 13, 180), width=20)
    draw.rounded_rectangle((810, 386, 1238, 530), radius=58, outline=(255, 238, 172, 170), width=10)
    draw.rounded_rectangle((880, 442, 1168, 476), radius=17, fill=(91, 62, 24, 80))
    engraved_text(draw, (1024, 720), "歌词岛 LYRIC HOVER", font(105, True))
    engraved_text(draw, (1024, 944), "Pro 支持者徽章", font(98, True))
    engraved_text(draw, (1024, 1192), "LYRIC HOVER 支持者", font(88, True))
    engraved_text(draw, (1024, 1424), "2026.08.01  20:00", font(73))
    image.putalpha(mask)
    return image


def make_three_quarter(front: Image.Image) -> Image.Image:
    canvas = Image.new("RGBA", (2048, 2048), (0, 0, 0, 0))
    shadow = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    shadow_draw = ImageDraw.Draw(shadow)
    shadow_draw.ellipse((300, 390, 1790, 1810), fill=(0, 0, 0, 120))
    shadow = shadow.filter(ImageFilter.GaussianBlur(55))
    canvas.alpha_composite(shadow)

    side = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    side_draw = ImageDraw.Draw(side)
    for offset in range(118, -1, -3):
        tone = int(88 + (118 - offset) * 0.72)
        side_draw.ellipse(
            (280 + offset, 244 + offset // 2, 1760 + offset, 1836 + offset // 2),
            fill=(min(255, tone + 40), tone, 15, 255),
        )
    canvas.alpha_composite(side)

    face = front.resize((1480, 1592), Image.Resampling.LANCZOS).rotate(
        -8,
        resample=Image.Resampling.BICUBIC,
        expand=True,
    )
    canvas.alpha_composite(face, (205, 160))
    glint = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    glint_draw = ImageDraw.Draw(glint)
    glint_draw.ellipse((570, 290, 1110, 1540), fill=(255, 249, 208, 63))
    glint = glint.filter(ImageFilter.GaussianBlur(78))
    canvas.alpha_composite(glint)
    return canvas


class GlbBuilder:
    def __init__(self) -> None:
        self.binary = bytearray()
        self.buffer_views: list[dict] = []
        self.accessors: list[dict] = []
        self.images: list[dict] = []
        self.textures: list[dict] = []
        self.materials: list[dict] = []
        self.meshes: list[dict] = []
        self.nodes: list[dict] = []

    def add_blob(self, data: bytes, target: int | None = None) -> int:
        while len(self.binary) % 4:
            self.binary.append(0)
        offset = len(self.binary)
        self.binary.extend(data)
        view = {"buffer": 0, "byteOffset": offset, "byteLength": len(data)}
        if target is not None:
            view["target"] = target
        self.buffer_views.append(view)
        return len(self.buffer_views) - 1

    def add_accessor(self, array: np.ndarray, kind: str, component: int, target: int) -> int:
        contiguous = np.ascontiguousarray(array)
        view = self.add_blob(contiguous.tobytes(), target)
        count = int(contiguous.shape[0])
        accessor = {
            "bufferView": view,
            "componentType": component,
            "count": count,
            "type": kind,
        }
        if kind in {"VEC2", "VEC3"}:
            accessor["min"] = contiguous.min(axis=0).astype(float).tolist()
            accessor["max"] = contiguous.max(axis=0).astype(float).tolist()
        elif kind == "SCALAR":
            accessor["min"] = [int(contiguous.min())]
            accessor["max"] = [int(contiguous.max())]
        self.accessors.append(accessor)
        return len(self.accessors) - 1

    def add_image(self, image: Image.Image, name: str) -> int:
        stream = io.BytesIO()
        image.save(stream, format="PNG", optimize=True)
        view = self.add_blob(stream.getvalue())
        self.images.append({"name": name, "bufferView": view, "mimeType": "image/png"})
        self.textures.append({"source": len(self.images) - 1})
        return len(self.textures) - 1

    def add_material(
        self,
        name: str,
        base_color: list[float],
        metallic: float,
        roughness: float,
        texture: int | None = None,
        alpha: bool = False,
    ) -> int:
        pbr = {
            "baseColorFactor": base_color,
            "metallicFactor": metallic,
            "roughnessFactor": roughness,
        }
        if texture is not None:
            pbr["baseColorTexture"] = {"index": texture}
        material = {
            "name": name,
            "pbrMetallicRoughness": pbr,
            "doubleSided": True,
        }
        if alpha:
            material["alphaMode"] = "BLEND"
        self.materials.append(material)
        return len(self.materials) - 1

    def add_mesh(
        self,
        name: str,
        positions: np.ndarray,
        normals: np.ndarray,
        uvs: np.ndarray,
        indices: np.ndarray,
        material: int,
    ) -> int:
        primitive = {
            "attributes": {
                "POSITION": self.add_accessor(positions.astype("<f4"), "VEC3", 5126, 34962),
                "NORMAL": self.add_accessor(normals.astype("<f4"), "VEC3", 5126, 34962),
                "TEXCOORD_0": self.add_accessor(uvs.astype("<f4"), "VEC2", 5126, 34962),
            },
            "indices": self.add_accessor(indices.astype("<u2"), "SCALAR", 5123, 34963),
            "material": material,
        }
        self.meshes.append({"name": name, "primitives": [primitive]})
        self.nodes.append({"name": name, "mesh": len(self.meshes) - 1})
        return len(self.nodes) - 1

    def export(self, path: Path) -> None:
        while len(self.binary) % 4:
            self.binary.append(0)
        document = {
            "asset": {"version": "2.0", "generator": "LYRIC HOVER badge asset generator"},
            "scene": 0,
            "scenes": [{"nodes": list(range(len(self.nodes)))}],
            "nodes": self.nodes,
            "meshes": self.meshes,
            "materials": self.materials,
            "textures": self.textures,
            "images": self.images,
            "samplers": [{"magFilter": 9729, "minFilter": 9987, "wrapS": 33071, "wrapT": 33071}],
            "accessors": self.accessors,
            "bufferViews": self.buffer_views,
            "buffers": [{"byteLength": len(self.binary)}],
        }
        json_bytes = json.dumps(document, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
        while len(json_bytes) % 4:
            json_bytes += b" "
        total = 12 + 8 + len(json_bytes) + 8 + len(self.binary)
        with path.open("wb") as stream:
            stream.write(struct.pack("<4sII", b"glTF", 2, total))
            stream.write(struct.pack("<I4s", len(json_bytes), b"JSON"))
            stream.write(json_bytes)
            stream.write(struct.pack("<I4s", len(self.binary), b"BIN\x00"))
            stream.write(self.binary)


def cylinder_side(radius: float, z0: float, z1: float):
    positions, normals, uvs, indices = [], [], [], []
    for segment in range(SEGMENTS + 1):
        angle = segment * math.tau / SEGMENTS
        c, s = math.cos(angle), math.sin(angle)
        positions += [(radius * c, radius * s, z0), (radius * c, radius * s, z1)]
        normals += [(c, s, 0), (c, s, 0)]
        uvs += [(segment / SEGMENTS, 1), (segment / SEGMENTS, 0)]
    for segment in range(SEGMENTS):
        index = segment * 2
        indices += [index, index + 1, index + 2, index + 2, index + 1, index + 3]
    return np.array(positions), np.array(normals), np.array(uvs), np.array(indices)


def disc(radius: float, z: float, front: bool):
    positions = [(0, 0, z)]
    normals = [(0, 0, 1 if front else -1)]
    uvs = [(0.5, 0.5)]
    indices = []
    for segment in range(SEGMENTS + 1):
        angle = segment * math.tau / SEGMENTS
        x, y = radius * math.cos(angle), radius * math.sin(angle)
        positions.append((x, y, z))
        normals.append((0, 0, 1 if front else -1))
        uvs.append((0.5 + (x if front else -x) / (2 * radius), 0.5 - y / (2 * radius)))
    for segment in range(SEGMENTS):
        if front:
            indices += [0, segment + 1, segment + 2]
        else:
            indices += [0, segment + 2, segment + 1]
    return np.array(positions), np.array(normals), np.array(uvs), np.array(indices)


def ring(outer: float, inner: float, z: float, front: bool):
    positions, normals, uvs, indices = [], [], [], []
    for segment in range(SEGMENTS + 1):
        angle = segment * math.tau / SEGMENTS
        c, s = math.cos(angle), math.sin(angle)
        positions += [(outer * c, outer * s, z), (inner * c, inner * s, z)]
        normals += [(0, 0, 1 if front else -1)] * 2
        uvs += [(segment / SEGMENTS, 0), (segment / SEGMENTS, 1)]
    for segment in range(SEGMENTS):
        i = segment * 2
        if front:
            indices += [i, i + 2, i + 1, i + 1, i + 2, i + 3]
        else:
            indices += [i, i + 1, i + 2, i + 1, i + 3, i + 2]
    return np.array(positions), np.array(normals), np.array(uvs), np.array(indices)


def quad(width: float, height: float, y: float, z: float, front: bool):
    if front:
        positions = [(-width / 2, y - height / 2, z), (width / 2, y - height / 2, z),
                     (width / 2, y + height / 2, z), (-width / 2, y + height / 2, z)]
        normal = (0, 0, 1)
    else:
        positions = [(width / 2, y - height / 2, z), (-width / 2, y - height / 2, z),
                     (-width / 2, y + height / 2, z), (width / 2, y + height / 2, z)]
        normal = (0, 0, -1)
    return (
        np.array(positions),
        np.array([normal] * 4),
        np.array([(0, 1), (1, 1), (1, 0), (0, 0)]),
        np.array([0, 1, 2, 0, 2, 3]),
    )


def transparent_text_texture(
    text: str,
    size: tuple[int, int],
    font_size: int,
    bold=True,
    fill=(69, 59, 48, 255),
) -> Image.Image:
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    centered_text(draw, (size[0] / 2, size[1] / 2), text, font(font_size, bold), fill)
    return image


def logo_texture() -> Image.Image:
    image = Image.new("RGBA", (2048, 2048), (0, 0, 0, 0))
    draw_front_relief(image)
    ImageDraw.Draw(image).rectangle((0, 1480, 2048, 2048), fill=(0, 0, 0, 0))
    return image


def build_glb(path: Path) -> None:
    builder = GlbBuilder()
    gold = builder.add_material("Warm_Gold_Metal", [0.92, 0.66, 0.18, 1], 0.92, 0.22)
    deep_gold = builder.add_material("Deep_Gold_Side", [0.42, 0.22, 0.025, 1], 0.88, 0.29)
    navy = builder.add_material("Deep_Navy_Enamel", [0.025, 0.085, 0.19, 1], 0.55, 0.18)
    front_logo_texture = builder.add_image(logo_texture(), "Front_Logo_Texture")
    front_title_texture = builder.add_image(
        transparent_text_texture("LYRIC HOVER", (1024, 256), 112, fill=GOLD_LIGHT),
        "Front_Title_Texture",
    )
    front_pro_texture = builder.add_image(
        transparent_text_texture("PRO", (1024, 256), 132, fill=GOLD_LIGHT),
        "Front_Pro_Texture",
    )
    back_textures = [
        builder.add_image(transparent_text_texture(text, (1024, 192), size), f"Back_Text_{index + 1}_Texture")
        for index, (text, size) in enumerate(
            [
                ("歌词岛 LYRIC HOVER", 84),
                ("Pro 支持者徽章", 86),
                ("LYRIC HOVER 支持者", 76),
                ("2026.08.01  20:00", 66),
            ]
        )
    ]
    logo_material = builder.add_material("Gold_Raised_Spectrum_Waves_Note", [1, 1, 1, 1], 0.72, 0.24, front_logo_texture, True)
    title_material = builder.add_material("Front_Text_Raised_Gold", [1, 1, 1, 1], 0.72, 0.25, front_title_texture, True)
    pro_material = builder.add_material("Front_PRO_Raised", [1, 1, 1, 1], 0.62, 0.29, front_pro_texture, True)
    back_materials = [
        builder.add_material(f"Back_Text_{index + 1}_Engraved", [1, 1, 1, 1], 0.25, 0.48, texture, True)
        for index, texture in enumerate(back_textures)
    ]

    builder.add_mesh("Badge_Base", *cylinder_side(20.0, -2.0, 2.0), deep_gold)
    builder.add_mesh("Front_Rim", *ring(19.7, 17.4, 2.08, True), gold)
    builder.add_mesh("Front_Plate", *disc(17.35, 2.10, True), navy)
    builder.add_mesh("Front_Logo_Spectrum_Waves_Note", *disc(17.15, 2.20, True), logo_material)
    builder.add_mesh("Front_Text_LYRIC_HOVER", *quad(20.5, 3.8, -9.4, 2.25, True), title_material)
    builder.add_mesh("Front_Text_PRO", *quad(12.4, 3.2, -13.2, 2.27, True), pro_material)
    builder.add_mesh("Back_Plate", *disc(19.6, -2.02, False), gold)
    back_y = [7.2, 2.2, -3.2, -8.0]
    back_h = [3.2, 3.1, 2.9, 2.6]
    for index, (y, height, material) in enumerate(zip(back_y, back_h, back_materials)):
        builder.add_mesh(
            f"Back_Text_{index + 1}",
            *quad(21.5, height, y, -2.23, False),
            material,
        )
    builder.export(path)


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    front = make_front()
    back = make_back()
    three_quarter = make_three_quarter(front)
    front.save(OUTPUT / "lyric-island-pro-badge-front.png", optimize=True)
    back.save(OUTPUT / "lyric-island-pro-badge-back.png", optimize=True)
    three_quarter.save(OUTPUT / "lyric-island-pro-badge-three-quarter.png", optimize=True)
    build_glb(OUTPUT / "lyric-island-pro-supporter-badge.glb")
    (OUTPUT / "README.md").write_text(
        """# LYRIC HOVER Pro 支持者徽章素材包

- `lyric-island-pro-supporter-badge.glb`：40 mm 直径、4 mm 厚，嵌入材质与文字纹理。
- `lyric-island-pro-badge-front.png`：2048×2048 正面透明 PNG。
- `lyric-island-pro-badge-back.png`：2048×2048 背面透明 PNG。
- `lyric-island-pro-badge-three-quarter.png`：2048×2048 三分之四视角透明 PNG。

GLB 节点保留 `Badge_Base`、`Front_Rim`、`Front_Logo_Spectrum_Waves_Note`、
`Front_Text_LYRIC_HOVER`、`Front_Text_PRO`、`Back_Plate` 与
`Back_Text_1` 至 `Back_Text_4`。应用运行时不加载此 GLB，而使用原生
WPF `Viewport3D` 程序化生成相同造型。正面为深海军蓝珐琅与金色浮雕，
背面为拉丝金属并保留四行动态信息节点。
""",
        encoding="utf-8",
    )
    print(OUTPUT)


if __name__ == "__main__":
    main()
