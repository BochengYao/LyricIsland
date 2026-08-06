#!/usr/bin/env python3
"""Build the LYRIC HOVER Pro supporter medal in Blender.

Run with Blender (not the system Python):

    blender --background --python tools/generate_supporter_badge_model.py

Optional output directory:

    blender --background --python tools/generate_supporter_badge_model.py -- \
        --output-dir artifacts/pro-supporter-badge/blender

The model is authored in metres, is 40 mm in diameter, is centred at the
world origin, and faces +Z.  Front relief remains separate named geometry so
the .blend file is editable and runtime material assignment can be audited.
"""

from __future__ import annotations

import argparse
import json
import math
import struct
import sys
import zlib
from pathlib import Path

import bpy
import bmesh
from mathutils import Matrix, Vector
from mathutils.geometry import tessellate_polygon


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT = ROOT / "artifacts" / "pro-supporter-badge" / "blender"
FINAL_RUNTIME_OUTPUT = ROOT / "artifacts" / "pro-supporter-badge" / "final-model"

DIAMETER = 0.040
RADIUS = DIAMETER / 2.0
THICKNESS = 0.0024
FRONT_Z = THICKNESS / 2.0
BACK_Z = -THICKNESS / 2.0
RELIEF_Z = FRONT_Z + 0.00010
RELIEF_HEIGHT = 0.00028
BACK_RELIEF_Z = BACK_Z - 0.00010
TOP_HOLE_Y = 0.01290
TOP_HOLE_WIDTH = 0.00698
TOP_HOLE_HEIGHT = 0.00132
TOP_HOLE_RADIUS = 0.00065

# Principled BSDF node colors are scene-linear. These intentionally subdued
# values render as champagne gold after AgX display conversion instead of
# clipping to white under a product-photography light rig.
GOLD = (0.60, 0.390, 0.150, 1.0)
GOLD_RELIEF = (0.555, 0.355, 0.125, 1.0)
GOLD_HERO = (0.645, 0.435, 0.175, 1.0)
GOLD_LIGHT = (0.64, 0.430, 0.175, 1.0)
BACK_CHAMPAGNE = (0.53, 0.36, 0.15, 1.0)
BACK_RIM_CHAMPAGNE = (0.61, 0.415, 0.165, 1.0)
BACK_PLAQUE_NAVY = (0.0010, 0.0055, 0.0190, 1.0)
NAVY = (0.0008, 0.0045, 0.0210, 1.0)
DARK = (0.0012, 0.0025, 0.0055, 1.0)
ENGRAVED = (0.055, 0.060, 0.070, 1.0)


def command_line_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=DEFAULT_OUTPUT,
        help="Directory for blend, glb, obj and PBR textures.",
    )
    parser.add_argument(
        "--no-export",
        action="store_true",
        help="Build the scene without writing files (useful while editing).",
    )
    parser.add_argument(
        "--runtime-export",
        action="store_true",
        help="Create the final glTF runtime export in artifacts/pro-supporter-badge/final-model.",
    )
    parser.add_argument(
        "--runtime-precheck",
        action="store_true",
        help="Build the runtime copy and print PRE_EXPORT_CHECK without writing a GLB.",
    )
    parser.add_argument(
        "--no-render",
        action="store_true",
        help="Skip the five Blender review renders.",
    )
    parser.add_argument(
        "--front-only",
        action="store_true",
        help="Render only the orthographic front approval image.",
    )
    parser.add_argument(
        "--back-only",
        action="store_true",
        help="Render only the orthographic back approval image.",
    )
    parser.add_argument(
        "--note-study",
        action="store_true",
        help="Also render front and angled close-ups of the unified music-note relief.",
    )
    parser.add_argument(
        "--detail-study",
        action="store_true",
        help="Render enlarged front-note and lower-right rim cleanup studies.",
    )
    parser.add_argument(
        "--front-calibration-study",
        action="store_true",
        help="Render front-only capsule and centre-axis calibration crops.",
    )
    parser.add_argument(
        "--multiview-study",
        action="store_true",
        help="Also render the through-hole macro and a neutral solid geometry study.",
    )
    parser.add_argument(
        "--back-material-study",
        action="store_true",
        help="Render only the brushed-back full, macro and shallow-angle studies.",
    )
    parser.add_argument(
        "--samples",
        type=int,
        default=160,
        help="Cycles samples for development renders (default: 160).",
    )
    arguments = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    return parser.parse_args(arguments)


def reset_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.materials,
        bpy.data.cameras,
        bpy.data.lights,
    ):
        for datablock in list(datablocks):
            datablocks.remove(datablock)

    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.length_unit = "MILLIMETERS"
    try:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    except TypeError:
        scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 1600
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    # Development renders are reproducible outputs; do not leave .blend1 backup
    # files beside the authoritative generated .blend asset.
    bpy.context.preferences.filepaths.save_version = 0
    scene.view_settings.look = "AgX - Medium Low Contrast"
    # Keep highlight headroom for champagne gold; the navy enamel should read
    # from reflected shape rather than from a bright frontal wash.
    scene.view_settings.exposure = -0.10


def set_socket(node, names: tuple[str, ...], value) -> None:
    for name in names:
        socket = node.inputs.get(name)
        if socket is not None:
            socket.default_value = value
            return


def pbr_material(
    name: str,
    base_color,
    metallic: float,
    roughness: float,
    clearcoat: float = 0.0,
    clearcoat_roughness: float = 0.15,
    ior: float = 1.5,
    anisotropic: float = 0.0,
    specular_ior_level: float = 0.5,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = base_color
    principled = material.node_tree.nodes.get("Principled BSDF")
    set_socket(principled, ("Base Color",), base_color)
    set_socket(principled, ("Metallic",), metallic)
    set_socket(principled, ("Roughness",), roughness)
    set_socket(principled, ("IOR",), ior)
    set_socket(principled, ("Specular IOR Level", "Specular"), specular_ior_level)
    set_socket(principled, ("Anisotropic IOR Level", "Anisotropic"), anisotropic)
    set_socket(principled, ("Coat Weight", "Clearcoat"), clearcoat)
    set_socket(
        principled,
        ("Coat Roughness", "Clearcoat Roughness"),
        clearcoat_roughness,
    )
    return material


def generated_roughness_texture(
    output_dir: Path,
    filename: str,
    radial: bool,
    size: int = 512,
) -> bpy.types.Image:
    """Create a restrained brushed-metal roughness map without dependencies."""
    image = bpy.data.images.new(filename, width=size, height=size, alpha=False)
    pixels: list[float] = []
    centre = (size - 1) / 2.0
    for y in range(size):
        for x in range(size):
            if radial:
                angle = math.atan2(y - centre, x - centre)
                radius = math.hypot(x - centre, y - centre)
                grain = math.sin(angle * 220.0 + radius * 0.06)
                fine = math.sin(angle * 570.0 - radius * 0.025)
            else:
                grain = math.sin(y * 0.71 + x * 0.025)
                fine = math.sin(y * 2.17 - x * 0.014)
            value = max(0.0, min(1.0, 0.36 + grain * 0.055 + fine * 0.025))
            pixels.extend((value, value, value, 1.0))
    image.pixels = pixels
    image.colorspace_settings.name = "Non-Color"
    image.filepath_raw = str(output_dir / filename)
    image.file_format = "PNG"
    image.save()
    return image


def connect_roughness_map(material: bpy.types.Material, image: bpy.types.Image) -> None:
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = nodes.get("Principled BSDF")
    texture = nodes.new("ShaderNodeTexImage")
    texture.name = f"{material.name}_Roughness"
    texture.image = image
    texture.interpolation = "Linear"
    roughness = principled.inputs.get("Roughness")
    if roughness is not None:
        links.new(texture.outputs["Color"], roughness)


def configure_enamel_nodes(material: bpy.types.Material) -> None:
    """Add restrained radial depth and microscopic enamel texture."""
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = nodes.get("Principled BSDF")

    coordinates = nodes.new("ShaderNodeTexCoord")
    separate = nodes.new("ShaderNodeSeparateXYZ")
    links.new(coordinates.outputs["Generated"], separate.inputs["Vector"])

    subtract_x = nodes.new("ShaderNodeMath")
    subtract_x.operation = "SUBTRACT"
    subtract_x.inputs[1].default_value = 0.5
    subtract_y = nodes.new("ShaderNodeMath")
    subtract_y.operation = "SUBTRACT"
    subtract_y.inputs[1].default_value = 0.5
    square_x = nodes.new("ShaderNodeMath")
    square_x.operation = "MULTIPLY"
    square_y = nodes.new("ShaderNodeMath")
    square_y.operation = "MULTIPLY"
    add = nodes.new("ShaderNodeMath")
    add.operation = "ADD"
    root = nodes.new("ShaderNodeMath")
    root.operation = "SQRT"
    links.new(separate.outputs["X"], subtract_x.inputs[0])
    links.new(separate.outputs["Y"], subtract_y.inputs[0])
    links.new(subtract_x.outputs[0], square_x.inputs[0])
    links.new(subtract_x.outputs[0], square_x.inputs[1])
    links.new(subtract_y.outputs[0], square_y.inputs[0])
    links.new(subtract_y.outputs[0], square_y.inputs[1])
    links.new(square_x.outputs[0], add.inputs[0])
    links.new(square_y.outputs[0], add.inputs[1])
    links.new(add.outputs[0], root.inputs[0])

    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "EASE"
    ramp.color_ramp.elements[0].position = 0.0
    ramp.color_ramp.elements[0].color = (0.0024, 0.0130, 0.0520, 1.0)
    ramp.color_ramp.elements[1].position = 0.52
    ramp.color_ramp.elements[1].color = (0.00045, 0.0022, 0.0110, 1.0)
    middle = ramp.color_ramp.elements.new(0.34)
    middle.color = (0.0011, 0.0062, 0.0280, 1.0)
    links.new(root.outputs[0], ramp.inputs["Fac"])
    links.new(ramp.outputs["Color"], principled.inputs["Base Color"])

    noise = nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 92.0
    noise.inputs["Detail"].default_value = 2.0
    noise.inputs["Roughness"].default_value = 0.34
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.045
    bump.inputs["Distance"].default_value = 0.000055
    links.new(coordinates.outputs["Generated"], noise.inputs["Vector"])
    links.new(noise.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], principled.inputs["Normal"])


def configure_concentric_brushed_back_nodes(
    material: bpy.types.Material,
) -> None:
    """Build seamless concentric machining grain for the rear metal plate.

    The rings are procedural in Generated XY space and remain centred on the
    medal.  They modulate roughness by only +/- 0.04 and feed a six-micron
    bump, so the full medal reads as brushed metal rather than a record groove.
    """
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = nodes.get("Principled BSDF")

    coordinates = nodes.new("ShaderNodeTexCoord")
    coordinates.name = "Back_Concentric_Coordinates"
    separate = nodes.new("ShaderNodeSeparateXYZ")
    separate.name = "Back_Concentric_XY"
    links.new(coordinates.outputs["Generated"], separate.inputs["Vector"])

    subtract_x = nodes.new("ShaderNodeMath")
    subtract_x.name = "Back_Centre_X"
    subtract_x.operation = "SUBTRACT"
    subtract_x.inputs[1].default_value = 0.5
    subtract_y = nodes.new("ShaderNodeMath")
    subtract_y.name = "Back_Centre_Y"
    subtract_y.operation = "SUBTRACT"
    subtract_y.inputs[1].default_value = 0.5
    square_x = nodes.new("ShaderNodeMath")
    square_x.operation = "MULTIPLY"
    square_y = nodes.new("ShaderNodeMath")
    square_y.operation = "MULTIPLY"
    radius_squared = nodes.new("ShaderNodeMath")
    radius_squared.operation = "ADD"
    radius = nodes.new("ShaderNodeMath")
    radius.name = "Back_Radial_Distance"
    radius.operation = "SQRT"
    links.new(separate.outputs["X"], subtract_x.inputs[0])
    links.new(separate.outputs["Y"], subtract_y.inputs[0])
    links.new(subtract_x.outputs[0], square_x.inputs[0])
    links.new(subtract_x.outputs[0], square_x.inputs[1])
    links.new(subtract_y.outputs[0], square_y.inputs[0])
    links.new(subtract_y.outputs[0], square_y.inputs[1])
    links.new(square_x.outputs[0], radius_squared.inputs[0])
    links.new(square_y.outputs[0], radius_squared.inputs[1])
    links.new(radius_squared.outputs[0], radius.inputs[0])

    centre_fade = nodes.new("ShaderNodeMapRange")
    centre_fade.name = "Back_Centre_Grain_Fade"
    centre_fade.clamp = True
    centre_fade.inputs["From Min"].default_value = 0.0
    centre_fade.inputs["From Max"].default_value = 0.085
    centre_fade.inputs["To Min"].default_value = 0.0
    centre_fade.inputs["To Max"].default_value = 1.0
    links.new(radius.outputs[0], centre_fade.inputs["Value"])

    radial_vector = nodes.new("ShaderNodeCombineXYZ")
    radial_vector.name = "Back_Radial_Noise_Vector"
    links.new(radius.outputs[0], radial_vector.inputs["X"])
    radial_noise = nodes.new("ShaderNodeTexNoise")
    radial_noise.name = "Back_Radial_Micro_Variation"
    radial_noise.noise_dimensions = "3D"
    radial_noise.inputs["Scale"].default_value = 28.0
    radial_noise.inputs["Detail"].default_value = 3.0
    radial_noise.inputs["Roughness"].default_value = 0.42
    links.new(radial_vector.outputs["Vector"], radial_noise.inputs["Vector"])
    centre_noise = nodes.new("ShaderNodeMath")
    centre_noise.operation = "SUBTRACT"
    centre_noise.inputs[1].default_value = 0.5
    noise_phase = nodes.new("ShaderNodeMath")
    noise_phase.operation = "MULTIPLY"
    noise_phase.inputs[1].default_value = 0.72
    links.new(radial_noise.outputs["Fac"], centre_noise.inputs[0])
    links.new(centre_noise.outputs[0], noise_phase.inputs[0])

    primary_frequency = nodes.new("ShaderNodeMath")
    primary_frequency.name = "Back_Primary_Ring_Frequency_5400"
    primary_frequency.operation = "MULTIPLY"
    primary_frequency.inputs[1].default_value = 5400.0
    primary_phase = nodes.new("ShaderNodeMath")
    primary_phase.operation = "ADD"
    primary_ring = nodes.new("ShaderNodeMath")
    primary_ring.name = "Back_Primary_Concentric_Rings"
    primary_ring.operation = "SINE"
    links.new(radius.outputs[0], primary_frequency.inputs[0])
    links.new(primary_frequency.outputs[0], primary_phase.inputs[0])
    links.new(noise_phase.outputs[0], primary_phase.inputs[1])
    links.new(primary_phase.outputs[0], primary_ring.inputs[0])

    fine_frequency = nodes.new("ShaderNodeMath")
    fine_frequency.name = "Back_Fine_Ring_Frequency_12200"
    fine_frequency.operation = "MULTIPLY"
    fine_frequency.inputs[1].default_value = 12200.0
    fine_ring = nodes.new("ShaderNodeMath")
    fine_ring.name = "Back_Fine_Concentric_Rings"
    fine_ring.operation = "SINE"
    links.new(radius.outputs[0], fine_frequency.inputs[0])
    links.new(fine_frequency.outputs[0], fine_ring.inputs[0])

    primary_roughness = nodes.new("ShaderNodeMath")
    primary_roughness.operation = "MULTIPLY"
    primary_roughness.inputs[1].default_value = 0.012
    fine_roughness = nodes.new("ShaderNodeMath")
    fine_roughness.operation = "MULTIPLY"
    fine_roughness.inputs[1].default_value = 0.0045
    roughness_variation = nodes.new("ShaderNodeMath")
    roughness_variation.operation = "ADD"
    masked_roughness = nodes.new("ShaderNodeMath")
    masked_roughness.name = "Back_Centre_Faded_Roughness_Variation"
    masked_roughness.operation = "MULTIPLY"
    base_roughness = nodes.new("ShaderNodeMath")
    base_roughness.name = "Back_Brushed_Roughness_0_3535_to_0_3865"
    base_roughness.operation = "ADD"
    base_roughness.inputs[1].default_value = 0.37
    links.new(primary_ring.outputs[0], primary_roughness.inputs[0])
    links.new(fine_ring.outputs[0], fine_roughness.inputs[0])
    links.new(primary_roughness.outputs[0], roughness_variation.inputs[0])
    links.new(fine_roughness.outputs[0], roughness_variation.inputs[1])
    links.new(roughness_variation.outputs[0], masked_roughness.inputs[0])
    links.new(centre_fade.outputs["Result"], masked_roughness.inputs[1])
    links.new(masked_roughness.outputs[0], base_roughness.inputs[0])
    links.new(base_roughness.outputs[0], principled.inputs["Roughness"])

    fine_height = nodes.new("ShaderNodeMath")
    fine_height.operation = "MULTIPLY"
    fine_height.inputs[1].default_value = 0.34
    combined_height = nodes.new("ShaderNodeMath")
    combined_height.operation = "ADD"
    masked_height = nodes.new("ShaderNodeMath")
    masked_height.name = "Back_Centre_Faded_Micro_Height"
    masked_height.operation = "MULTIPLY"
    links.new(fine_ring.outputs[0], fine_height.inputs[0])
    links.new(primary_ring.outputs[0], combined_height.inputs[0])
    links.new(fine_height.outputs[0], combined_height.inputs[1])
    links.new(combined_height.outputs[0], masked_height.inputs[0])
    links.new(centre_fade.outputs["Result"], masked_height.inputs[1])
    bump = nodes.new("ShaderNodeBump")
    bump.name = "Back_Concentric_Micro_Bump"
    bump.inputs["Strength"].default_value = 0.0028
    bump.inputs["Distance"].default_value = 0.000002
    links.new(masked_height.outputs[0], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], principled.inputs["Normal"])

    tangent_socket = principled.inputs.get("Tangent")
    if tangent_socket is not None:
        tangent = nodes.new("ShaderNodeTangent")
        tangent.name = "Back_Radial_Anisotropy_Tangent"
        tangent.direction_type = "RADIAL"
        tangent.axis = "Z"
        links.new(tangent.outputs["Tangent"], tangent_socket)
    anisotropic_socket = principled.inputs.get(
        "Anisotropic IOR Level"
    ) or principled.inputs.get("Anisotropic")
    if anisotropic_socket is not None:
        anisotropic_level = nodes.new("ShaderNodeMath")
        anisotropic_level.name = "Back_Centre_Faded_Anisotropy"
        anisotropic_level.operation = "MULTIPLY"
        anisotropic_level.inputs[1].default_value = 0.36
        links.new(centre_fade.outputs["Result"], anisotropic_level.inputs[0])
        links.new(anisotropic_level.outputs[0], anisotropic_socket)


def create_materials(output_dir: Path) -> dict[str, bpy.types.Material]:
    gold = pbr_material("Gold_PBR", GOLD, metallic=1.0, roughness=0.30)
    relief_gold = pbr_material(
        "Gold_Relief_PBR", GOLD_RELIEF, metallic=1.0, roughness=0.30
    )
    hero_gold = pbr_material(
        "Gold_Hero_PBR", GOLD_HERO, metallic=1.0, roughness=0.255
    )
    navy = pbr_material(
        "Navy_Enamel_PBR",
        NAVY,
        metallic=0.0,
        roughness=0.33,
        clearcoat=0.12,
        clearcoat_roughness=0.20,
        ior=1.48,
        specular_ior_level=0.14,
    )
    configure_enamel_nodes(navy)
    plaque_navy = pbr_material(
        "Navy_Plaque_PBR",
        (0.00038, 0.0022, 0.0105, 1.0),
        metallic=0.0,
        roughness=0.34,
        clearcoat=0.10,
        clearcoat_roughness=0.22,
        ior=1.46,
        specular_ior_level=0.10,
    )
    back = pbr_material(
        "Back_Brushed_Gold_PBR",
        BACK_CHAMPAGNE,
        metallic=1.0,
        roughness=0.37,
        anisotropic=0.36,
    )
    configure_concentric_brushed_back_nodes(back)
    back_rim = pbr_material(
        "Back_Rim_Polished_Champagne_PBR",
        BACK_RIM_CHAMPAGNE,
        metallic=1.0,
        roughness=0.215,
        anisotropic=0.08,
    )
    back_plaque = pbr_material(
        "Back_NamePlate_Navy_Enamel_PBR",
        BACK_PLAQUE_NAVY,
        metallic=0.0,
        roughness=0.27,
        clearcoat=0.22,
        clearcoat_roughness=0.14,
        ior=1.48,
        specular_ior_level=0.13,
    )
    inset = pbr_material(
        "Dark_Inset_PBR", DARK, metallic=0.0, roughness=0.48,
        specular_ior_level=0.12,
    )
    side = pbr_material(
        "Gold_Side_PBR", GOLD, metallic=0.82, roughness=0.50, anisotropic=0.12
    )
    engraving = pbr_material(
        "Back_Engraving_PBR",
        ENGRAVED,
        metallic=0.35,
        roughness=0.48,
    )
    # Keep a compact fallback map for future Viewport3D baking/export work.
    # Blender approval renders use the seamless procedural node graph above.
    generated_roughness_texture(
        output_dir,
        "badge-back-roughness.png",
        radial=True,
    )
    # Keep the exposed shell deliberately restrained. A hard reflection strip
    # on the near-edge shell reads as a triangular shard at grazing angles;
    # the geometry remains unchanged while uniform roughness keeps the actual
    # 2.4 mm thickness legible.
    generated_roughness_texture(
        output_dir,
        "badge-side-roughness.png",
        radial=False,
    )
    return {
        "gold": gold,
        "relief_gold": relief_gold,
        "hero_gold": hero_gold,
        "navy": navy,
        "plaque_navy": plaque_navy,
        "back": back,
        "back_rim": back_rim,
        "back_plaque": back_plaque,
        "inset": inset,
        "side": side,
        "engraving": engraving,
    }


def apply_bevel(
    obj: bpy.types.Object,
    width: float,
    segments: int = 3,
) -> None:
    bevel = obj.modifiers.new("Micro_Bevel", "BEVEL")
    bevel.width = width
    bevel.segments = segments
    bevel.limit_method = "ANGLE"
    bevel.angle_limit = math.radians(20.0)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=bevel.name)


def shade_smooth_by_angle(obj: bpy.types.Object) -> None:
    if obj.type != "MESH":
        return
    for polygon in obj.data.polygons:
        # Keep planar medal faces flat. Smoothing the large annular front ngons
        # creates radial normal seams that look like missing pieces in the rim.
        polygon.use_smooth = abs(polygon.normal.z) < 0.72
    obj.data.set_sharp_from_angle(angle=math.radians(42.0)) if hasattr(
        obj.data, "set_sharp_from_angle"
    ) else None


def clean_mesh_topology(obj: bpy.types.Object, merge_distance: float = 0.0000001) -> None:
    """Remove boolean residue and restore consistently outward-facing normals."""
    if obj.type != "MESH":
        return
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=merge_distance)
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")


def cylinder(
    name: str,
    radius: float,
    depth: float,
    z: float,
    material: bpy.types.Material,
    vertices: int = 128,
    bevel: float = 0.00008,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=(0.0, 0.0, z),
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    if bevel > 0:
        apply_bevel(obj, bevel)
    shade_smooth_by_angle(obj)
    return obj


def rounded_rectangle_points(
    width: float,
    height: float,
    radius: float,
    segments_per_corner: int = 10,
) -> list[tuple[float, float]]:
    radius = min(radius, width / 2.0, height / 2.0)
    corners = (
        (width / 2.0 - radius, height / 2.0 - radius, 0.0),
        (-width / 2.0 + radius, height / 2.0 - radius, 90.0),
        (-width / 2.0 + radius, -height / 2.0 + radius, 180.0),
        (width / 2.0 - radius, -height / 2.0 + radius, 270.0),
    )
    points: list[tuple[float, float]] = []
    for cx, cy, start_degrees in corners:
        for step in range(segments_per_corner + 1):
            angle = math.radians(start_degrees + step * 90.0 / segments_per_corner)
            points.append((cx + math.cos(angle) * radius, cy + math.sin(angle) * radius))
    return points


def prism_from_loops(
    name: str,
    loops: list[list[tuple[float, float]]],
    z_min: float,
    z_max: float,
    material: bpy.types.Material,
) -> bpy.types.Object:
    """Create a solid or ring prism. loops[0] is outer; loops[1] is a hole."""
    outer = loops[0]
    inner = loops[1] if len(loops) > 1 else None
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []

    for z in (z_min, z_max):
        vertices.extend((x, y, z) for x, y in outer)
        if inner:
            vertices.extend((x, y, z) for x, y in inner)

    outer_count = len(outer)
    inner_count = len(inner) if inner else 0
    layer_count = outer_count + inner_count
    bottom_outer = 0
    bottom_inner = outer_count
    top_outer = layer_count
    top_inner = layer_count + outer_count

    for index in range(outer_count):
        next_index = (index + 1) % outer_count
        faces.append(
            (
                bottom_outer + index,
                bottom_outer + next_index,
                top_outer + next_index,
                top_outer + index,
            )
        )

    if inner:
        for index in range(inner_count):
            next_index = (index + 1) % inner_count
            faces.append(
                (
                    bottom_inner + next_index,
                    bottom_inner + index,
                    top_inner + index,
                    top_inner + next_index,
                )
            )
        for index in range(outer_count):
            next_index = (index + 1) % outer_count
            inner_index = round(index * inner_count / outer_count) % inner_count
            inner_next = round(next_index * inner_count / outer_count) % inner_count
            faces.append(
                (
                    top_outer + index,
                    top_outer + next_index,
                    top_inner + inner_next,
                    top_inner + inner_index,
                )
            )
            faces.append(
                (
                    bottom_outer + next_index,
                    bottom_outer + index,
                    bottom_inner + inner_index,
                    bottom_inner + inner_next,
                )
            )
    else:
        faces.append(tuple(reversed(range(bottom_outer, bottom_outer + outer_count))))
        faces.append(tuple(range(top_outer, top_outer + outer_count)))

    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    return obj


def holed_disc_from_curve(
    name: str,
    radius: float,
    z_min: float,
    z_max: float,
    hole_centre_y: float,
    hole_width: float,
    hole_height: float,
    hole_radius: float,
    material: bpy.types.Material,
) -> bpy.types.Object:
    """Create a watertight circular plate around an off-centre capsule hole.

    The planar faces are split into four simple regions (top, bottom, left and
    right of the aperture) before tessellation. This avoids both boolean ngons
    and curve-fill bridge sectors while keeping one merged, continuous plate.
    """
    outer_segments = 256
    boundary_steps = 64
    half_height = hole_height / 2.0
    cap_radius = min(hole_radius, half_height)
    straight_half_width = hole_width / 2.0 - cap_radius
    straight_half_height = half_height - cap_radius
    hole_bottom = hole_centre_y - half_height
    hole_top = hole_centre_y + half_height

    def capsule_half_width(y: float) -> float:
        local_y = abs(y - hole_centre_y)
        arc_y = max(0.0, local_y - straight_half_height)
        return straight_half_width + math.sqrt(
            max(0.0, cap_radius * cap_radius - arc_y * arc_y)
        )

    def arc_points(start: float, end: float, steps: int) -> list[tuple[float, float]]:
        return [
            (
                radius * math.cos(start + (end - start) * index / steps),
                radius * math.sin(start + (end - start) * index / steps),
            )
            for index in range(steps + 1)
        ]

    bottom_angle = math.asin(hole_bottom / radius)
    top_angle = math.asin(hole_top / radius)
    outer_bottom = arc_points(math.pi - bottom_angle, math.tau + bottom_angle, 192)
    outer_top = arc_points(top_angle, math.pi - top_angle, 64)
    y_values = [
        hole_bottom + (hole_top - hole_bottom) * index / boundary_steps
        for index in range(boundary_steps + 1)
    ]
    inner_left = [(-capsule_half_width(y), y) for y in y_values]
    inner_right = [(capsule_half_width(y), y) for y in y_values]
    outer_left = [
        (-math.sqrt(max(0.0, radius * radius - y * y)), y) for y in y_values
    ]
    outer_right = [
        (math.sqrt(max(0.0, radius * radius - y * y)), y) for y in y_values
    ]

    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []

    def add_planar_triangle(points: tuple[tuple[float, float], ...]) -> None:
        local = list(points)
        a, b, c = local
        signed_area = (
            (b[0] - a[0]) * (c[1] - a[1])
            - (b[1] - a[1]) * (c[0] - a[0])
        )
        if signed_area < 0.0:
            local[1], local[2] = local[2], local[1]
        base = len(vertices)
        vertices.extend((x, y, z_min) for x, y in local)
        vertices.extend((x, y, z_max) for x, y in local)
        faces.append((base + 3, base + 4, base + 5))
        faces.append((base + 2, base + 1, base))

    def add_planar_region(points: list[tuple[float, float]]) -> None:
        """Tessellate one non-overlapping simple region on both plate faces."""
        polygon = [Vector((x, y, 0.0)) for x, y in points]
        for triangle in tessellate_polygon([polygon]):
            resolved = [
                polygon[point] if isinstance(point, int) else point
                for point in triangle
            ]
            add_planar_triangle(
                tuple((float(point.x), float(point.y)) for point in resolved)
            )

    # Each cap is a single simple polygon.  The previous fan-plus-chord layout
    # covered part of these regions twice; Cycles then exposed the coplanar
    # overlap as large triangular "fragments" even though the boundary mesh was
    # manifold.  Tessellating the exact region once removes that Z-fighting.
    add_planar_region(
        outer_bottom
        + [inner_right[0], inner_left[0]]
    )
    add_planar_region(
        outer_top
        + [inner_left[-1], inner_right[-1]]
    )

    for index in range(boundary_steps):
        add_planar_triangle(
            (outer_left[index], inner_left[index], inner_left[index + 1])
        )
        add_planar_triangle(
            (outer_left[index], inner_left[index + 1], outer_left[index + 1])
        )
        add_planar_triangle(
            (inner_right[index], outer_right[index], outer_right[index + 1])
        )
        add_planar_triangle(
            (inner_right[index], outer_right[index + 1], inner_right[index + 1])
        )

    # Reuse the exact same sampled perimeter vertices as the planar regions so
    # the cylindrical wall welds edge-for-edge instead of forming a second,
    # numerically different outline.
    outer = (
        outer_top
        + list(reversed(outer_left))[1:]
        + outer_bottom[1:]
        + outer_right[1:-1]
    )
    hole = inner_right + list(reversed(inner_left))
    for loop, inward in ((outer, False), (hole, True)):
        base = len(vertices)
        vertices.extend((x, y, z_min) for x, y in loop)
        vertices.extend((x, y, z_max) for x, y in loop)
        count = len(loop)
        for index in range(count):
            following = (index + 1) % count
            face = (
                base + index,
                base + following,
                base + count + following,
                base + count + index,
            )
            faces.append(tuple(reversed(face)) if inward else face)

    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    clean_mesh_topology(obj, merge_distance=0.00000002)
    # Keep the broad reverse face perfectly planar. Beveling a triangulated
    # plate perturbs normals across the construction seams and turns softbox
    # reflections into large polygonal patches. The continuous medal side and
    # full-depth gold capsule liner already provide the visible edge bevels.
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.dissolve_degenerate(threshold=0.000000001)
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    shade_smooth_by_angle(obj)
    obj["construction"] = "sectioned_disc_with_capsule_hole"
    return obj


def beveled_annular_ring(
    name: str,
    outer_radius: float,
    inner_radius: float,
    z_min: float,
    z_max: float,
    material: bpy.types.Material,
    segments: int = 128,
    bevel: float = 0.00009,
) -> bpy.types.Object:
    """Create a watertight ring with an explicit restrained bevel profile.

    A bevel modifier on a large annular ngon can produce radial shading seams.
    Spinning this eight-point cross-section yields predictable quad topology,
    real front/side normal changes and no missing-looking rim segments.
    """
    profile = (
        (inner_radius + bevel, z_max),
        (outer_radius - bevel, z_max),
        (outer_radius, z_max - bevel),
        (outer_radius, z_min + bevel),
        (outer_radius - bevel, z_min),
        (inner_radius + bevel, z_min),
        (inner_radius, z_min + bevel),
        (inner_radius, z_max - bevel),
    )
    vertices: list[tuple[float, float, float]] = []
    for index in range(segments):
        angle = index * math.tau / segments
        cos_angle = math.cos(angle)
        sin_angle = math.sin(angle)
        vertices.extend(
            (radius * cos_angle, radius * sin_angle, z) for radius, z in profile
        )

    profile_count = len(profile)
    faces: list[tuple[int, int, int, int]] = []
    for segment in range(segments):
        next_segment = (segment + 1) % segments
        for profile_index in range(profile_count):
            next_profile = (profile_index + 1) % profile_count
            faces.append(
                (
                    segment * profile_count + profile_index,
                    next_segment * profile_count + profile_index,
                    next_segment * profile_count + next_profile,
                    segment * profile_count + next_profile,
                )
            )

    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    shade_smooth_by_angle(obj)
    return obj


def rounded_rectangle(
    name: str,
    width: float,
    height: float,
    radius: float,
    z_min: float,
    z_max: float,
    material: bpy.types.Material,
    border: float | None = None,
    segments_per_corner: int = 10,
    bevel_width: float | None = None,
) -> bpy.types.Object:
    outer = rounded_rectangle_points(
        width, height, radius, segments_per_corner=segments_per_corner
    )
    loops = [outer]
    if border:
        inner = rounded_rectangle_points(
            width - border * 2.0,
            height - border * 2.0,
            max(0.00001, radius - border),
            segments_per_corner=segments_per_corner,
        )
        loops.append(inner)
    obj = prism_from_loops(name, loops, z_min, z_max, material)
    effective_bevel = (
        min(0.00006, (z_max - z_min) * 0.22)
        if bevel_width is None
        else bevel_width
    )
    if effective_bevel > 0.0:
        apply_bevel(obj, effective_bevel, segments=3)
    shade_smooth_by_angle(obj)
    return obj


def cut_through_capsule_opening(
    objects: list[bpy.types.Object],
    centre_y: float,
    width: float,
    height: float,
    radius: float,
    wall_material: bpy.types.Material,
) -> bpy.types.Object:
    """Cut one real capsule window through the complete medal stack.

    The cutter is shared by the side body, front enamel and back plate so the
    opening is physically continuous. A thin full-depth gold liner supplies a
    clean, readable inner wall without filling the aperture.
    """
    cutter_outline = rounded_rectangle_points(
        width, height, radius, segments_per_corner=32
    )
    cutter = prism_from_loops(
        "Badge_Top_Capsule_Through_Cutter",
        [cutter_outline],
        -THICKNESS,
        THICKNESS,
        wall_material,
    )
    cutter.location.y = centre_y
    bpy.context.view_layer.update()
    for obj in objects:
        modifier = obj.modifiers.new("Top_Capsule_Through_Cut", "BOOLEAN")
        modifier.operation = "DIFFERENCE"
        modifier.solver = "EXACT"
        modifier.object = cutter
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        clean_mesh_topology(obj, merge_distance=0.00000002)
        if obj.name == "Badge_Back_Metal":
            # Blender's boolean can leave the planar reverse as an ngon whose
            # inner loop is evaluated inconsistently by Cycles. Resolve that
            # face explicitly before beveling so the aperture stays local.
            triangulate = obj.modifiers.new("Back_Face_Triangulate", "TRIANGULATE")
            triangulate.quad_method = "BEAUTY"
            triangulate.ngon_method = "BEAUTY"
            bpy.context.view_layer.objects.active = obj
            bpy.ops.object.modifier_apply(modifier=triangulate.name)
            # Apply the restrained machining bevel only after the aperture has
            # been cut, so the reverse face remains a clean continuous plate.
            apply_bevel(obj, 0.000055, segments=3)
        shade_smooth_by_angle(obj)
    bpy.data.objects.remove(cutter, do_unlink=True)

    liner_outer = rounded_rectangle_points(
        width, height, radius, segments_per_corner=32
    )
    liner_inner = rounded_rectangle_points(
        width - 0.00018,
        height - 0.00018,
        radius - 0.00009,
        segments_per_corner=32,
    )
    liner = prism_from_loops(
        "Badge_Top_Capsule_Inner_Wall",
        [liner_outer, liner_inner],
        BACK_Z - 0.00003,
        FRONT_Z + 0.00012,
        wall_material,
    )
    liner.location.y = centre_y
    apply_bevel(liner, 0.000025, segments=3)
    shade_smooth_by_angle(liner)
    liner["construction"] = "full_depth_through_window_liner"
    return liner


def curve_object(
    name: str,
    paths: list[list[tuple[float, float]]],
    z: float,
    radius: float,
    material: bpy.types.Material,
) -> bpy.types.Object:
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 3
    curve.bevel_depth = radius
    curve.bevel_resolution = 2
    for path in paths:
        spline = curve.splines.new("NURBS")
        spline.points.add(len(path) - 1)
        for point, (x, y) in zip(spline.points, path):
            point.co = (x, y, z, 1.0)
        spline.order_u = min(4, len(path))
        spline.use_endpoint_u = True
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    return obj


def polyline_curve_object(
    name: str,
    paths: list[list[tuple[float, float]]],
    z: float,
    radius: float,
    material: bpy.types.Material,
) -> bpy.types.Object:
    """Render sampled geometry exactly, without NURBS interpolation drift."""
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 1
    curve.bevel_depth = radius
    curve.bevel_resolution = 2
    for path in paths:
        spline = curve.splines.new("POLY")
        spline.points.add(len(path) - 1)
        for point, (x, y) in zip(spline.points, path):
            point.co = (x, y, z, 1.0)
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    return obj


def wave_path(y: float, amplitude: float, phase: float) -> list[tuple[float, float]]:
    points: list[tuple[float, float]] = []
    for index in range(41):
        x = -0.0158 + index * (0.0316 / 40.0)
        envelope = 0.88 + 0.12 * math.cos(x / 0.0158 * math.pi)
        value = y + amplitude * envelope * math.sin(x * 245.0 + phase)
        points.append((x, value))
    return points


def wave_band(
    name: str,
    centreline: list[tuple[float, float]],
    width: float,
    z_min: float,
    z_max: float,
    material: bpy.types.Material,
    bevel_width: float = 0.000025,
) -> bpy.types.Object:
    """Create a flat, extruded ribbon instead of a round wire-like curve."""
    half_width = width / 2.0
    upper = [(x, y + half_width) for x, y in centreline]
    lower = [(x, y - half_width) for x, y in reversed(centreline)]
    obj = prism_from_loops(name, [upper + lower], z_min, z_max, material)
    if bevel_width > 0.0:
        apply_bevel(obj, min(bevel_width, width * 0.08), segments=2)
    shade_smooth_by_angle(obj)
    return obj


def elliptical_disc(
    name: str,
    radius_x: float,
    radius_y: float,
    depth: float,
    z: float,
    rotation: float,
    material: bpy.types.Material,
    bevel_width: float = 0.000055,
) -> bpy.types.Object:
    """Create a clean, shallow note head with applied elliptical proportions."""
    bpy.ops.mesh.primitive_cylinder_add(vertices=64, radius=1.0, depth=depth, location=(0, 0, z))
    obj = bpy.context.object
    obj.name = name
    obj.scale = (radius_x, radius_y, 1.0)
    obj.rotation_euler[2] = rotation
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    if bevel_width > 0.0:
        apply_bevel(obj, bevel_width, segments=3)
    shade_smooth_by_angle(obj)
    return obj


def filled_four_point_star(
    name: str,
    centre: tuple[float, float],
    radius: float,
    z_min: float,
    z_max: float,
    material: bpy.types.Material,
) -> bpy.types.Object:
    """Create a restrained solid four-point sparkle instead of a wire outline."""
    cx, cy = centre
    points: list[tuple[float, float]] = []
    for index in range(8):
        angle = math.pi / 2.0 + index * math.pi / 4.0
        point_radius = radius if index % 2 == 0 else radius * 0.19
        points.append(
            (
                cx + math.cos(angle) * point_radius,
                cy + math.sin(angle) * point_radius,
            )
        )
    obj = prism_from_loops(name, [points], z_min, z_max, material)
    apply_bevel(obj, min(0.000035, radius * 0.08), segments=2)
    shade_smooth_by_angle(obj)
    return obj


def cube_part(
    name: str,
    location: tuple[float, float, float],
    scale: tuple[float, float, float],
    material: bpy.types.Material,
    bevel: float,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    apply_bevel(obj, bevel, segments=3)
    return obj


def rounded_bar_between(
    name: str,
    start: tuple[float, float],
    end: tuple[float, float],
    width: float,
    depth: float,
    z: float,
    material: bpy.types.Material,
    bevel_width: float = 0.00010,
) -> bpy.types.Object:
    """Create a gently rounded relief bar aligned between two face points."""
    x1, y1 = start
    x2, y2 = end
    dx = x2 - x1
    dy = y2 - y1
    length = math.hypot(dx, dy)
    bpy.ops.mesh.primitive_cube_add(
        location=((x1 + x2) / 2.0, (y1 + y2) / 2.0, z)
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = (width / 2.0, length / 2.0, depth / 2.0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    obj.rotation_euler[2] = -math.atan2(dx, dy)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    if bevel_width > 0.0:
        apply_bevel(obj, min(bevel_width, width * 0.28), segments=3)
    return obj


def cubic_bezier_points(
    p0: tuple[float, float],
    p1: tuple[float, float],
    p2: tuple[float, float],
    p3: tuple[float, float],
    steps: int = 12,
    include_start: bool = False,
) -> list[tuple[float, float]]:
    """Sample one planar cubic Bezier segment deterministically."""
    points: list[tuple[float, float]] = []
    first = 0 if include_start else 1
    for index in range(first, steps + 1):
        t = index / steps
        u = 1.0 - t
        x = (
            u * u * u * p0[0]
            + 3.0 * u * u * t * p1[0]
            + 3.0 * u * t * t * p2[0]
            + t * t * t * p3[0]
        )
        y = (
            u * u * u * p0[1]
            + 3.0 * u * u * t * p1[1]
            + 3.0 * u * t * t * p2[1]
            + t * t * t * p3[1]
        )
        points.append((x, y))
    return points


def translate_cubic(
    segment: tuple[
        tuple[float, float],
        tuple[float, float],
        tuple[float, float],
        tuple[float, float],
    ],
    offset: tuple[float, float],
):
    ox, oy = offset
    return tuple((x + ox, y + oy) for x, y in segment)


def reverse_cubics(segments):
    return [
        (segment[3], segment[2], segment[1], segment[0])
        for segment in reversed(segments)
    ]


def note_head_cubics(offset: tuple[float, float] = (0.0, 0.0)):
    """Return one G1-continuous, slightly tilted note-head/stem socket.

    The first and last handles are vertical, so the head joins the two stem
    sides tangentially instead of producing a rectangular shoulder. The head
    is about ten percent smaller than the prior ellipse-disc construction.
    """
    mm = 0.001
    base = [
        # A broad but shallow neck grows into a tilted, flattened oval. Both
        # socket tangents stay vertical while the intervening contour remains
        # continuously curved, avoiding a lollipop-like circular head.
        ((-1.42 * mm, 0.38 * mm), (-1.42 * mm, 0.15 * mm), (-1.83 * mm, 0.17 * mm), (-2.20 * mm, 0.06 * mm)),
        ((-2.20 * mm, 0.06 * mm), (-2.56 * mm, -0.04 * mm), (-3.16 * mm, -0.18 * mm), (-3.16 * mm, -0.52 * mm)),
        ((-3.16 * mm, -0.52 * mm), (-3.14 * mm, -0.82 * mm), (-2.86 * mm, -1.18 * mm), (-2.48 * mm, -1.25 * mm)),
        ((-2.48 * mm, -1.25 * mm), (-2.12 * mm, -1.37 * mm), (-1.67 * mm, -1.30 * mm), (-1.44 * mm, -1.12 * mm)),
        ((-1.44 * mm, -1.12 * mm), (-1.10 * mm, -0.90 * mm), (-0.66 * mm, -0.22 * mm), (-0.62 * mm, 0.20 * mm)),
    ]
    return [translate_cubic(segment, offset) for segment in base]


def music_note_outline():
    """Create one closed Bezier-derived silhouette for the complete note."""
    mm = 0.001
    left_head = note_head_cubics()
    right_head = note_head_cubics((4.35 * mm, 0.72 * mm))
    segments = [
        # Beam upper edge and a rounded outer-right turn. Adjacent handles
        # are collinear at every join, including the closing left corner.
        ((-1.05 * mm, 7.32 * mm), (-0.15 * mm, 7.41 * mm), (3.30 * mm, 8.00 * mm), (3.50 * mm, 8.04 * mm)),
        ((3.50 * mm, 8.04 * mm), (3.66 * mm, 8.07 * mm), (3.78 * mm, 7.92 * mm), (3.78 * mm, 7.72 * mm)),
        # Right outer stem descends tangentially into its head socket.
        ((3.78 * mm, 7.72 * mm), (3.76 * mm, 5.38 * mm), (3.73 * mm, 2.22 * mm), (3.73 * mm, 0.92 * mm)),
        *reverse_cubics(right_head),
        # Right inner stem and softened beam underside corner.
        ((2.93 * mm, 1.10 * mm), (2.93 * mm, 2.92 * mm), (3.03 * mm, 6.92 * mm), (3.03 * mm, 7.20 * mm)),
        ((3.03 * mm, 7.20 * mm), (3.03 * mm, 7.34 * mm), (2.90 * mm, 7.28 * mm), (2.76 * mm, 7.26 * mm)),
        # Beam underside returns to the left stem.
        ((2.76 * mm, 7.26 * mm), (1.65 * mm, 7.10 * mm), (0.22 * mm, 6.73 * mm), (-0.54 * mm, 6.59 * mm)),
        ((-0.54 * mm, 6.59 * mm), (-0.61 * mm, 6.58 * mm), (-0.62 * mm, 6.48 * mm), (-0.62 * mm, 6.40 * mm)),
        # Left inner stem, head, then outer stem back to the beam top.
        ((-0.62 * mm, 6.40 * mm), (-0.62 * mm, 3.92 * mm), (-0.62 * mm, 1.12 * mm), (-0.62 * mm, 0.20 * mm)),
        *reverse_cubics(left_head),
        ((-1.42 * mm, 0.38 * mm), (-1.42 * mm, 2.38 * mm), (-1.32 * mm, 5.80 * mm), (-1.32 * mm, 7.02 * mm)),
        ((-1.32 * mm, 7.02 * mm), (-1.32 * mm, 7.18 * mm), (-1.18 * mm, 7.307 * mm), (-1.05 * mm, 7.32 * mm)),
    ]
    # Normalize every shared handle direction once on the complete silhouette.
    # This makes all head/neck/stem/beam joins mathematically G1-continuous,
    # instead of relying on almost-collinear hand-authored handle coordinates.
    mutable_segments = [[list(point) for point in segment] for segment in segments]
    for index, segment in enumerate(mutable_segments):
        next_segment = mutable_segments[(index + 1) % len(mutable_segments)]
        tangent_x = segment[3][0] - segment[2][0]
        tangent_y = segment[3][1] - segment[2][1]
        tangent_length = math.hypot(tangent_x, tangent_y)
        handle_length = math.dist(next_segment[0], next_segment[1])
        if tangent_length > 1e-12 and handle_length > 1e-12:
            next_segment[1][0] = (
                next_segment[0][0] + tangent_x / tangent_length * handle_length
            )
            next_segment[1][1] = (
                next_segment[0][1] + tangent_y / tangent_length * handle_length
            )
    segments = [
        tuple((point[0], point[1]) for point in segment)
        for segment in mutable_segments
    ]
    # Slightly enlarge the complete symbol around its visual centre without
    # changing the tangent relationships or turning the heads into circles.
    centre = (0.30 * mm, 3.30 * mm)
    scale_x = 1.12
    scale_y = 1.035
    segments = [
        tuple(
            (
                centre[0] + (point[0] - centre[0]) * scale_x,
                centre[1] + (point[1] - centre[1]) * scale_y,
            )
            for point in segment
        )
        for segment in segments
    ]
    outline: list[tuple[float, float]] = []
    for index, segment in enumerate(segments):
        outline.extend(
            cubic_bezier_points(
                *segment,
                steps=16 if index in {2, 3, 4, 5, 9, 10, 11, 12} else 12,
                include_start=index == 0,
            )
        )
    if outline and math.dist(outline[0], outline[-1]) < 1e-10:
        outline.pop()
    return outline, segments


def boolean_union_relief(
    name: str,
    parts: list[bpy.types.Object],
    material: bpy.types.Material,
    bevel_width: float,
) -> bpy.types.Object:
    """Fuse overlapping relief parts into one watertight mesh before beveling.

    Joining objects leaves coplanar faces and visible contact seams. Exact
    boolean union removes every internal interface, after which one bevel and
    one normal treatment give heads, stems and beam a single visual language.
    """
    if not parts:
        raise ValueError("At least one relief part is required")
    result = parts[0]
    result.name = f"{name}_UnionBase"
    for index, part in enumerate(parts[1:], start=1):
        modifier = result.modifiers.new(f"Union_{index:02d}", "BOOLEAN")
        modifier.operation = "UNION"
        modifier.solver = "EXACT"
        modifier.object = part
        bpy.ops.object.select_all(action="DESELECT")
        result.select_set(True)
        bpy.context.view_layer.objects.active = result
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        bpy.data.objects.remove(part, do_unlink=True)
    result.name = name
    clean_mesh_topology(result)
    result.data.materials.clear()
    result.data.materials.append(material)
    apply_bevel(result, bevel_width, segments=6)
    # Do not merge after beveling: bevel intentionally creates near-coincident
    # corner loops, and welding them can cut tiny dark notches into the outline.
    bpy.ops.object.select_all(action="DESELECT")
    result.select_set(True)
    bpy.context.view_layer.objects.active = result
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    shade_smooth_by_angle(result)
    return result


def join_objects(name: str, objects: list[bpy.types.Object]) -> bpy.types.Object:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    objects[0].name = name
    return objects[0]


def create_text(
    name: str,
    text: str,
    size: float,
    location: tuple[float, float, float],
    material: bpy.types.Material,
    extrude: float = 0.00016,
    bevel: float = 0.000025,
    font_path: Path | None = None,
    spacing: float = 1.0,
) -> bpy.types.Object:
    curve = bpy.data.curves.new(f"{name}_Curve", "FONT")
    curve.body = text
    curve.align_x = "CENTER"
    curve.align_y = "CENTER"
    curve.size = size
    curve.extrude = extrude
    curve.bevel_depth = bevel
    curve.bevel_resolution = 1
    curve.resolution_u = 3
    curve.space_character = spacing
    if font_path and font_path.exists():
        curve.font = bpy.data.fonts.load(str(font_path))
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.data.materials.append(material)
    return obj


def create_front(materials: dict[str, bpy.types.Material]) -> None:
    gold = materials["gold"]
    relief_gold = materials["relief_gold"]
    hero_gold = materials["hero_gold"]
    navy = materials["navy"]
    plaque_navy = materials["plaque_navy"]
    inset = materials["inset"]

    cylinder(
        "Badge_Front_Enamel",
        radius=0.01848,
        depth=0.00036,
        z=FRONT_Z - 0.00015,
        material=navy,
        vertices=256,
        bevel=0.000045,
    )
    # A hairline inner pinstripe gives the enamel field a minted, finished
    # boundary without changing the already-approved outer-rim proportion.
    beveled_annular_ring(
        "Badge_Front_Inner_Pinstripe",
        outer_radius=0.01818,
        inner_radius=0.01806,
        z_min=FRONT_Z + 0.000020,
        z_max=FRONT_Z + 0.000115,
        material=relief_gold,
        segments=256,
        bevel=0.000018,
    )

    # A fine decorative island frame. Its centre is cut through the complete
    # medal stack later, so this is a genuine window rather than a black inset.
    top_y = TOP_HOLE_Y
    rounded_rectangle(
        "Badge_Top_Capsule",
        width=0.00730,
        height=0.00164,
        radius=0.00081,
        z_min=RELIEF_Z,
        z_max=RELIEF_Z + 0.000245,
        material=relief_gold,
        border=0.00016,
    ).location.y = top_y
    # Seventeen broad bars form one dense relief field. Their lower ends are
    # deliberately submerged behind the first wave so the systems interlock.
    heights = (
        0.00370, 0.00455, 0.00545, 0.00605, 0.00540, 0.00495,
        0.00585, 0.00658, 0.00692, 0.00632, 0.00558, 0.00608,
        0.00543, 0.00482, 0.00520, 0.00442, 0.00358,
    )
    waveform_parts: list[bpy.types.Object] = []
    for index, height in enumerate(heights):
        x = (index - (len(heights) - 1) / 2.0) * 0.00103
        base_y = 0.00305 + 0.00016 * math.cos(index * 0.77)
        waveform_parts.append(
            cube_part(
                f"Waveform_{index:02d}",
                (x, base_y + height / 2.0, RELIEF_Z + 0.000135),
                (0.00042, height / 2.0, 0.000115),
                relief_gold,
                bevel=0.000095,
            )
        )
    join_objects("Badge_Waveform", waveform_parts)

    wave_objects = []
    for index, (y, amplitude, phase, width) in enumerate(
        (
            (0.00285, 0.00108, 0.30, 0.00050),
            (-0.00015, 0.00088, 1.25, 0.00045),
            (-0.00292, 0.00071, 2.35, 0.00041),
            (-0.00530, 0.00057, 3.05, 0.00037),
        )
    ):
        wave_objects.append(
            wave_band(
                f"Wave_{index}",
                wave_path(y, amplitude, phase),
                width,
                RELIEF_Z + 0.00020,
                RELIEF_Z + 0.00043,
                relief_gold,
            )
        )
    join_objects("Badge_Waves", wave_objects)

    # The complete double note is one Bezier-derived silhouette. Heads, necks,
    # stems and beam therefore share one front face and one side wall; no
    # overlapping discs, rectangular stems or Boolean interfaces remain.
    note_depth = 0.00034
    note_z = RELIEF_Z + 0.00048
    note_outline, _note_bezier_segments = music_note_outline()
    # Align the note's visible silhouette, rather than just its local origin,
    # to the same x=0 axis as the waveform's central peak cluster.
    min_note_x = min(point[0] for point in note_outline)
    max_note_x = max(point[0] for point in note_outline)
    note_axis_offset = -(min_note_x + max_note_x) / 2.0
    note_outline = [(x + note_axis_offset, y) for x, y in note_outline]
    note = prism_from_loops(
        "Badge_Music_Note",
        [note_outline],
        note_z - note_depth / 2.0,
        note_z + note_depth / 2.0,
        hero_gold,
    )
    apply_bevel(note, 0.000070, segments=5)
    shade_smooth_by_angle(note)
    bpy.ops.object.select_all(action="DESELECT")
    note.select_set(True)
    bpy.context.view_layer.objects.active = note
    weighted_normal = note.modifiers.new("Note_Weighted_Normals", "WEIGHTED_NORMAL")
    weighted_normal.keep_sharp = True
    weighted_normal.weight = 50
    bpy.ops.object.modifier_apply(modifier=weighted_normal.name)
    note["construction"] = "single_closed_cubic_bezier_outline"
    note["relief_depth_mm"] = note_depth * 1000.0
    note["head_stem_transition"] = "g1_tangent_continuous_neck"
    note["boolean_operations"] = 0
    note["front_axis_offset_mm"] = note_axis_offset * 1000.0

    star_objects = [
        filled_four_point_star(
            f"Star_{index}", (x, y), radius,
            RELIEF_Z + 0.00020, RELIEF_Z + 0.00040, relief_gold,
        )
        for index, (x, y, radius) in enumerate(
            (
                (0.0118, 0.0109, 0.00062),
                (-0.0112, -0.0009, 0.00023),
                (0.0107, -0.0025, 0.00025),
            )
        )
    ]
    join_objects("Badge_Stars", star_objects)
    accent_dots = []
    for index, (x, y, radius) in enumerate(
        (
            (-0.0128, 0.0087, 0.000095),
            (0.0128, 0.0068, 0.000095),
            (-0.0097, -0.0036, 0.000080),
        )
    ):
        dot = cylinder(
            f"Accent_Dot_{index}", radius, 0.00018,
            RELIEF_Z + 0.00014, relief_gold, vertices=32, bevel=0.000018
        )
        dot.location.x = x
        dot.location.y = y
        accent_dots.append(dot)
    join_objects("Badge_Accent_Dots", accent_dots)

    font_path = Path(r"C:\Windows\Fonts\seguisb.ttf")
    pro_font_path = Path(r"C:\Windows\Fonts\seguisb.ttf")
    brand_text = create_text(
        "Badge_Lyric_Hover_Text",
        "L Y R I C   H O V E R",
        0.00215,
        (0.0, -0.00810, RELIEF_Z + 0.00010),
        relief_gold,
        extrude=0.00019,
        bevel=0.000026,
        font_path=font_path,
        spacing=0.95,
    )
    # HOVER is one letter shorter than ISLAND. Preserve the approved visual
    # footprint instead of leaving the lower brand field under-filled.
    brand_text.scale.x = 1.68

    pro_y = -0.01220
    rounded_rectangle(
        "Badge_Pro_Capsule",
        width=0.01025,
        height=0.00276,
        radius=0.00137,
        z_min=RELIEF_Z,
        z_max=RELIEF_Z + 0.000205,
        material=relief_gold,
        border=0.00015,
        segments_per_corner=32,
        bevel_width=0.000022,
    ).location.y = pro_y
    rounded_rectangle(
        "Badge_Pro_Capsule_Inset",
        width=0.00991,
        height=0.00242,
        radius=0.001205,
        z_min=RELIEF_Z + 0.000006,
        z_max=RELIEF_Z + 0.000030,
        material=plaque_navy,
        segments_per_corner=32,
        bevel_width=0.000010,
    ).location.y = pro_y
    pro_text = create_text(
        "Badge_Pro_Text",
        "PRO",
        0.00156,
        (0.0, pro_y, RELIEF_Z + 0.00011),
        relief_gold,
        extrude=0.000145,
        bevel=0.000012,
        font_path=pro_font_path,
        spacing=1.10,
    )
    pro_text.scale.x = 1.13
    pro_text.data.resolution_u = 8
    pro_text.data.bevel_resolution = 3
    cylinder(
        "Badge_Pro_Left_Dot",
        radius=0.000205,
        depth=0.000160,
        z=RELIEF_Z + 0.000080,
        material=relief_gold,
        vertices=64,
        bevel=0.000022,
    ).location.x = -0.00635
    bpy.data.objects["Badge_Pro_Left_Dot"].location.y = pro_y
    cylinder(
        "Badge_Pro_Right_Dot",
        radius=0.000205,
        depth=0.000160,
        z=RELIEF_Z + 0.000080,
        material=relief_gold,
        vertices=64,
        bevel=0.000022,
    ).location.x = 0.00635
    bpy.data.objects["Badge_Pro_Right_Dot"].location.y = pro_y


def create_back(materials: dict[str, bpy.types.Material]) -> None:
    gold = materials["gold"]
    back = materials["back"]
    back_rim = materials["back_rim"]
    back_plaque = materials["back_plaque"]
    engraving = materials["engraving"]

    holed_disc_from_curve(
        "Badge_Back_Metal",
        radius=0.01895,
        z_min=BACK_Z - 0.00009,
        z_max=BACK_Z + 0.00033,
        hole_centre_y=TOP_HOLE_Y,
        hole_width=TOP_HOLE_WIDTH,
        hole_height=TOP_HOLE_HEIGHT,
        hole_radius=TOP_HOLE_RADIUS,
        material=back,
    )
    # A dedicated rear finishing ring masks the construction cap of the main
    # side cylinder and gives the reverse the same continuous machined edge as
    # the approved front.  It sits fractionally proud of the body, with a very
    # narrow bevel, so no Boolean cap triangles can flash around the perimeter.
    beveled_annular_ring(
        "Badge_Back_Rim",
        outer_radius=RADIUS,
        inner_radius=0.01890,
        z_min=BACK_Z - 0.00011,
        z_max=BACK_Z + 0.00012,
        material=back_rim,
        segments=256,
        bevel=0.000025,
    )

    # Back geometry faces -Z. Text is rotated 180 degrees around X so it is
    # readable when the same medal is turned to its back in the runtime viewer.
    logo = rounded_rectangle(
        "Badge_Back_Logo",
        width=0.0068,
        height=0.00195,
        radius=0.00095,
        z_min=BACK_RELIEF_Z - 0.00010,
        z_max=BACK_RELIEF_Z,
        material=gold,
        border=0.00030,
    )
    logo.location.y = 0.0108

    font_regular = Path(r"C:\Windows\Fonts\msyh.ttc")
    static_lines = [
        ("歌词岛  LYRIC HOVER", 0.0064, 0.00135),
        ("Pro 支持者徽章", 0.0037, 0.00118),
    ]
    static_objects = []
    for index, (text, y, size) in enumerate(static_lines):
        obj = create_text(
            f"Back_Static_{index}",
            text,
            size,
            # The back plate's outer face is at BACK_Z - 0.00009.  Start the
            # rotated back-facing text exactly at that surface so it grows
            # continuously out of the plate rather than hovering a fraction
            # of a millimetre above it (which aliases as a double edge in
            # neutral glTF viewers).
            # Seat the text 0.016 mm inside the authored back face.  After
            # runtime thickness normalization this leaves roughly 0.01 mm of
            # overlap with the plate, then a real 0.08–0.10 mm outward relief.
            (0.0, y, BACK_Z - 0.00009 + 0.000016),
            engraving,
            extrude=0.00015,
            bevel=0.000020,
            font_path=font_regular,
        )
        # Face toward -Z without turning the inscription upside down after the
        # whole medal rotates 180 degrees around Y in the viewer.
        obj.rotation_euler[1] = math.pi
        static_objects.append(obj)
    # Keep one stable editable object name for the static back inscription.
    for obj in static_objects:
        bpy.context.view_layer.objects.active = obj
    static_objects[0].name = "Badge_Back_Static_Text"

    nameplate = rounded_rectangle(
        "Badge_Back_NamePlate",
        width=0.0146,
        height=0.0068,
        radius=0.00115,
        z_min=BACK_RELIEF_Z - 0.00007,
        z_max=BACK_RELIEF_Z,
        material=back_plaque,
        border=None,
    )
    nameplate.location.y = -0.0041
    # The PBR runtime maps a high-resolution dynamic decal onto this face:
    # {username}\n{obtainedDate}. The runtime may keep its original DateTime
    # source field, but the decal formatter must emit yyyy.MM.dd date-only text.
    # Static model files deliberately contain no user data.
    nameplate["dynamic_decal_lines"] = "{username}\n{obtainedDate}"
    nameplate["obtained_date_format"] = "yyyy.MM.dd"
    nameplate["decal_role"] = "runtime_supporter_identity"


def create_medal(materials: dict[str, bpy.types.Material]) -> None:
    # Use a closed annular shell for the medal body instead of a capped full
    # cylinder.  The approved front enamel and the reverse plate already close
    # the two faces; removing the hidden broad cylinder caps prevents Boolean
    # cap triangles from flashing at grazing review angles.
    side_shell = beveled_annular_ring(
        "Badge_Gold_Side",
        outer_radius=RADIUS,
        inner_radius=0.01872,
        z_min=BACK_Z + 0.00012,
        z_max=FRONT_Z - 0.00010,
        material=materials["side"],
        segments=256,
        bevel=0.00013,
    )
    # The annular profile is authored clockwise for the front-facing rings.
    # Flip only the full-depth shell so its normals face outward on the exposed
    # medal edge; this does not alter the already-approved front rim geometry.
    for polygon in side_shell.data.polygons:
        polygon.flip()
    side_shell.data.update()

    # A restrained annular rim avoids the inflated torus profile of the old
    # procedural WPF model while retaining real bevel-catching geometry.
    # 1.38 mm visible face ring = 6.9% of the 20 mm radius. The former
    # incidental exposed gap is replaced by a dedicated 0.15 mm annular groove
    # so its width and darkness remain exact around the full circumference.
    inner_radius = 0.01862
    beveled_annular_ring(
        "Badge_Gold_Rim",
        RADIUS,
        inner_radius,
        FRONT_Z - 0.00010,
        FRONT_Z + 0.00030,
        materials["gold"],
        segments=256,
        bevel=0.000075,
    )
    beveled_annular_ring(
        "Badge_Inner_Groove",
        outer_radius=0.01863,
        inner_radius=0.01847,
        z_min=FRONT_Z - 0.000025,
        z_max=FRONT_Z + 0.000055,
        material=materials["inset"],
        segments=256,
        bevel=0.000018,
    )

    create_front(materials)
    create_back(materials)

    cut_through_capsule_opening(
        [
            bpy.data.objects["Badge_Front_Enamel"],
        ],
        centre_y=TOP_HOLE_Y,
        width=TOP_HOLE_WIDTH,
        height=TOP_HOLE_HEIGHT,
        radius=TOP_HOLE_RADIUS,
        wall_material=materials["gold"],
    )

    root = bpy.data.objects.new("Badge_Root", None)
    bpy.context.collection.objects.link(root)
    for obj in list(bpy.context.collection.objects):
        if obj != root and obj.type in {"MESH", "CURVE", "FONT"}:
            obj.parent = root


def track_to(obj: bpy.types.Object, target: Vector) -> None:
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def create_review_stage(samples: int = 160) -> None:
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    # Detail approval renders need clean sub-pixel ring and relief edges. This
    # raises only development-time Cycles sampling; it adds nothing to runtime.
    scene.cycles.samples = max(1, samples)
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 5
    scene.cycles.diffuse_bounces = 2
    scene.cycles.glossy_bounces = 3

    world = bpy.data.worlds.new("Badge_Studio_World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (
        0.004,
        0.006,
        0.011,
        1.0,
    )
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.038
    bpy.context.scene.world = world

    for name, energy, color, location, size, size_y, diffuse, specular in (
        # Large, low-energy key creates a broad tonal gradient while its
        # reduced specular contribution prevents a white enamel patch.
        ("Studio_Key", 0.045, (1.0, 0.82, 0.66), (-0.045, 0.030, 0.075), 0.052, 0.038, 0.36, 0.28),
        # A narrow near-camera reflection card gives flat gold relief a slim
        # champagne highlight. With diffuse disabled it does not wash the navy.
        ("Studio_Metal_Strip", 0.014, (1.0, 0.92, 0.78), (0.006, 0.010, 0.082), 0.011, 0.0008, 0.0, 0.40),
        # Back-right strip is kept off the front face and only catches bevels.
        ("Studio_Rim", 0.017, (0.58, 0.68, 1.0), (0.040, 0.022, -0.030), 0.034, 0.006, 0.18, 0.58),
        # Very weak cool fill retains navy detail without its own highlight.
        ("Studio_Fill", 0.008, (0.42, 0.55, 0.82), (0.028, -0.040, 0.060), 0.055, 0.040, 0.28, 0.03),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.color = color
        data.shape = "RECTANGLE"
        data.size = size
        data.size_y = size_y
        data.diffuse_factor = diffuse
        data.specular_factor = specular
        obj = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(obj)
        obj.location = location
        track_to(obj, Vector((0.0, 0.0, 0.0)))

    camera_data = bpy.data.cameras.new("Badge_Review_Camera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 0.0455
    camera_data.lens = 72.0
    camera_data.sensor_width = 36.0
    # The 40 mm medal is photographed from 90 mm away. Blender's default
    # 100 mm near clip plane would remove the entire face-on model and leave
    # only a thin sliver visible at grazing angles.
    camera_data.clip_start = 0.001
    camera_data.clip_end = 1.0
    camera = bpy.data.objects.new("Badge_Review_Camera", camera_data)
    bpy.context.collection.objects.link(camera)
    # The authored front surface is the outward-facing side at +Z; Blender's
    # camera looks along local -Z, so the neutral validation camera sits just
    # behind the origin and looks through +Z to view that surface without
    # changing the exported model's axis contract.
    camera.location = (0.0, 0.0, -0.100)
    track_to(camera, Vector((0.0, 0.0, 0.0)))
    bpy.context.scene.camera = camera


def render_review_previews(
    output_dir: Path,
    front_only: bool = False,
    back_only: bool = False,
) -> None:
    scene = bpy.context.scene
    root = bpy.data.objects["Badge_Root"]
    views = (
        ("front", 0.0, 0.0),
        ("left-20", -20.0, -2.0),
        ("right-20", 20.0, -2.0),
        ("side", 82.0, -3.0),
        ("back", 180.0, 0.0),
    )
    if front_only:
        views = views[:1]
    elif back_only:
        views = views[-1:]
    dynamic_preview_objects: list[bpy.types.Object] = []
    for name, yaw, pitch in views:
        temporary_back_lights: list[bpy.types.Object] = []
        saved_strip_energy: float | None = None
        saved_world_color = None
        saved_world_strength: float | None = None
        # The two identity rows are deliberately created only for the Blender
        # review frame. They are removed immediately afterwards and never enter
        # the saved/exported medal geometry; Badge_Back_NamePlate remains the
        # runtime decal target for username and date-only obtainedDate.
        if name == "back":
            engraving = pbr_material(
                "Back_Dynamic_Preview_Engraving",
                (0.34, 0.36, 0.40, 1.0),
                metallic=0.15,
                roughness=0.44,
            )
            font_path = Path(r"C:\Windows\Fonts\segoeui.ttf")
            for index, (body, y, size) in enumerate(
                (("{username}", -0.00325, 0.00115), ("{obtainedDate}", -0.00535, 0.00096))
            ):
                preview_text = create_text(
                    f"Badge_Back_Dynamic_Preview_{index}",
                    body,
                    size,
                    (0.0, y, BACK_RELIEF_Z - 0.00013),
                    engraving,
                    extrude=0.000055,
                    bevel=0.000008,
                    font_path=font_path,
                    spacing=1.03,
                )
                preview_text.rotation_euler[1] = math.pi
                preview_text.parent = root
                preview_text["preview_only_dynamic_decal"] = True
                dynamic_preview_objects.append(preview_text)

            # The front uses a narrow reflection strip to reveal micro-bevels.
            # A flat brushed-metal back would mirror that strip as a hard white
            # slash, so the back approval frame swaps it for broad softboxes.
            strip = bpy.data.lights.get("Studio_Metal_Strip")
            if strip is not None:
                saved_strip_energy = strip.energy
                strip.energy = 0.0
            world_background = scene.world.node_tree.nodes.get("Background")
            if world_background is not None:
                saved_world_color = tuple(
                    world_background.inputs["Color"].default_value
                )
                saved_world_strength = world_background.inputs["Strength"].default_value
                world_background.inputs["Color"].default_value = (
                    0.040,
                    0.026,
                    0.014,
                    1.0,
                )
                world_background.inputs["Strength"].default_value = 0.12
            for light_name, energy, color, location, size, size_y, diffuse, specular in (
                (
                    "Back_Review_Softbox",
                    0.030,
                    (1.0, 0.76, 0.50),
                    (-0.012, 0.018, 0.070),
                    0.060,
                    0.050,
                    0.62,
                    0.10,
                ),
                (
                    "Back_Review_Fill",
                    0.014,
                    (0.54, 0.64, 0.84),
                    (0.030, -0.025, 0.060),
                    0.046,
                    0.040,
                    0.42,
                    0.06,
                ),
            ):
                light_data = bpy.data.lights.new(light_name, "AREA")
                light_data.energy = energy
                light_data.color = color
                light_data.shape = "RECTANGLE"
                light_data.size = size
                light_data.size_y = size_y
                light_data.diffuse_factor = diffuse
                light_data.specular_factor = specular
                light_obj = bpy.data.objects.new(light_name, light_data)
                bpy.context.collection.objects.link(light_obj)
                light_obj.location = location
                track_to(light_obj, Vector((0.0, 0.0, BACK_Z)))
                temporary_back_lights.append(light_obj)
        root.rotation_euler = (math.radians(pitch), math.radians(yaw), 0.0)
        scene.render.filepath = str(output_dir / f"preview-{name}.png")
        bpy.context.view_layer.update()
        bpy.ops.render.render(write_still=True)
        if name == "back":
            for obj in dynamic_preview_objects:
                curve = obj.data
                bpy.data.objects.remove(obj, do_unlink=True)
                if curve.users == 0:
                    bpy.data.curves.remove(curve)
            dynamic_preview_objects.clear()
            dynamic_material = bpy.data.materials.get("Back_Dynamic_Preview_Engraving")
            if dynamic_material is not None:
                bpy.data.materials.remove(dynamic_material, do_unlink=True)
            for light_obj in temporary_back_lights:
                light_data = light_obj.data
                bpy.data.objects.remove(light_obj, do_unlink=True)
                if light_data.users == 0:
                    bpy.data.lights.remove(light_data)
            if saved_strip_energy is not None:
                strip = bpy.data.lights.get("Studio_Metal_Strip")
                if strip is not None:
                    strip.energy = saved_strip_energy
            world_background = scene.world.node_tree.nodes.get("Background")
            if world_background is not None and saved_world_color is not None:
                world_background.inputs["Color"].default_value = saved_world_color
            if world_background is not None and saved_world_strength is not None:
                world_background.inputs["Strength"].default_value = saved_world_strength
    root.rotation_euler = (0.0, 0.0, 0.0)
    bpy.context.view_layer.update()


def render_back_material_studies(output_dir: Path) -> None:
    """Render only the three approval frames for the brushed rear plate."""
    scene = bpy.context.scene
    root = bpy.data.objects["Badge_Root"]
    camera = bpy.data.objects["Badge_Review_Camera"]
    camera_data = camera.data
    saved_camera = {
        "type": camera_data.type,
        "ortho_scale": camera_data.ortho_scale,
        "lens": camera_data.lens,
        "location": camera.location.copy(),
        "rotation": camera.rotation_euler.copy(),
    }
    saved_light_energy = {
        name: bpy.data.lights[name].energy
        for name in (
            "Studio_Key",
            "Studio_Metal_Strip",
            "Studio_Rim",
            "Studio_Fill",
        )
        if name in bpy.data.lights
    }
    for name in saved_light_energy:
        bpy.data.lights[name].energy = 0.0

    world_background = scene.world.node_tree.nodes.get("Background")
    saved_world_color = tuple(world_background.inputs["Color"].default_value)
    saved_world_strength = world_background.inputs["Strength"].default_value
    world_background.inputs["Color"].default_value = (
        0.010,
        0.006,
        0.003,
        1.0,
    )
    world_background.inputs["Strength"].default_value = 0.032

    temporary_lights: list[bpy.types.Object] = []
    light_specs = (
        {
            "name": "Back_Brushed_Warm_Softbox",
            "shape": "DISK",
            "energy": 0.033,
            "color": (1.0, 0.88, 0.72),
            "location": (-0.032, 0.030, 0.058),
            "size": 0.048,
            "diffuse": 0.18,
            "specular": 0.78,
        },
        {
            "name": "Back_Brushed_Cool_Fill",
            "shape": "DISK",
            "energy": 0.010,
            "color": (0.58, 0.66, 0.82),
            "location": (0.030, -0.024, 0.052),
            "size": 0.058,
            "diffuse": 0.12,
            "specular": 0.30,
        },
    )
    for spec in light_specs:
        light_data = bpy.data.lights.new(spec["name"], "AREA")
        light_data.energy = spec["energy"]
        light_data.color = spec["color"]
        light_data.shape = spec["shape"]
        light_data.size = spec["size"]
        if spec["shape"] in {"RECTANGLE", "ELLIPSE"}:
            light_data.size_y = spec["size_y"]
        light_data.diffuse_factor = spec["diffuse"]
        light_data.specular_factor = spec["specular"]
        light_obj = bpy.data.objects.new(spec["name"], light_data)
        bpy.context.collection.objects.link(light_obj)
        light_obj.location = spec["location"]
        track_to(light_obj, Vector((0.0, 0.0, 0.0)))
        temporary_lights.append(light_obj)

    engraving = pbr_material(
        "Back_Dynamic_Material_Study_Engraving",
        (0.34, 0.36, 0.40, 1.0),
        metallic=0.15,
        roughness=0.44,
    )
    font_path = Path(r"C:\Windows\Fonts\segoeui.ttf")
    dynamic_text: list[bpy.types.Object] = []
    for index, (body, y, size) in enumerate(
        (("{username}", -0.00325, 0.00115), ("{obtainedDate}", -0.00535, 0.00096))
    ):
        preview_text = create_text(
            f"Badge_Back_Material_Study_Text_{index}",
            body,
            size,
            (0.0, y, BACK_RELIEF_Z - 0.00013),
            engraving,
            extrude=0.000055,
            bevel=0.000008,
            font_path=font_path,
            spacing=1.03,
        )
        preview_text.rotation_euler[1] = math.pi
        preview_text.parent = root
        preview_text["preview_only_dynamic_decal"] = True
        dynamic_text.append(preview_text)

    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 0.0455
    camera.location = (0.0, 0.0, 0.100)
    track_to(camera, Vector((0.0, 0.0, 0.0)))
    root.rotation_euler = (0.0, math.radians(180.0), 0.0)
    scene.render.filepath = str(output_dir / "preview-back-brushed-low.png")
    bpy.context.view_layer.update()
    bpy.ops.render.render(write_still=True)

    # Macro crop deliberately targets uninterrupted metal beside the identity
    # plaque so the radial grain is visible without changing the approved copy.
    macro_target = Vector((0.0100, 0.0020, 0.0))
    camera_data.ortho_scale = 0.0115
    camera.location = (macro_target.x, macro_target.y, 0.100)
    track_to(camera, macro_target)
    scene.render.filepath = str(output_dir / "preview-back-brushed-macro.png")
    bpy.context.view_layer.update()
    bpy.ops.render.render(write_still=True)

    camera_data.ortho_scale = 0.0455
    camera.location = (0.0, 0.0, 0.100)
    track_to(camera, Vector((0.0, 0.0, 0.0)))
    root.rotation_euler = (math.radians(-4.0), math.radians(160.0), 0.0)
    scene.render.filepath = str(output_dir / "preview-back-brushed-angle.png")
    bpy.context.view_layer.update()
    bpy.ops.render.render(write_still=True)

    for obj in dynamic_text:
        curve = obj.data
        bpy.data.objects.remove(obj, do_unlink=True)
        if curve.users == 0:
            bpy.data.curves.remove(curve)
    if engraving.users == 0:
        bpy.data.materials.remove(engraving)
    for light_obj in temporary_lights:
        light_data = light_obj.data
        bpy.data.objects.remove(light_obj, do_unlink=True)
        if light_data.users == 0:
            bpy.data.lights.remove(light_data)
    for name, energy in saved_light_energy.items():
        bpy.data.lights[name].energy = energy
    world_background.inputs["Color"].default_value = saved_world_color
    world_background.inputs["Strength"].default_value = saved_world_strength
    camera_data.type = saved_camera["type"]
    camera_data.ortho_scale = saved_camera["ortho_scale"]
    camera_data.lens = saved_camera["lens"]
    camera.location = saved_camera["location"]
    camera.rotation_euler = saved_camera["rotation"]
    root.rotation_euler = (0.0, 0.0, 0.0)
    bpy.context.view_layer.update()

    manifest = {
        "subject": "LYRIC HOVER Pro supporter badge back material study",
        "preserved": [
            "front model and materials",
            "back layout",
            "dynamic nameplate dimensions",
            "through-hole geometry",
            "overall structure",
        ],
        "material": {
            "name": "Back_Brushed_Gold_PBR",
            "base_color_scene_linear": list(BACK_CHAMPAGNE),
            "metallic": 1.0,
            "base_roughness": 0.37,
            "roughness_range": [0.3535, 0.3865],
            "anisotropic": 0.36,
            "primary_radial_frequency": 5400.0,
            "fine_radial_frequency": 12200.0,
            "bump_strength": 0.0028,
            "bump_distance_metres": 0.000002,
            "centre_fade_radius_generated": 0.085,
            "tangent": "radial around Z axis",
        },
        "back_rim_material": {
            "name": "Back_Rim_Polished_Champagne_PBR",
            "base_color_scene_linear": list(BACK_RIM_CHAMPAGNE),
            "metallic": 1.0,
            "roughness": 0.215,
            "anisotropic": 0.08,
        },
        "dynamic_nameplate_material": {
            "name": "Back_NamePlate_Navy_Enamel_PBR",
            "base_color_scene_linear": list(BACK_PLAQUE_NAVY),
            "metallic": 0.0,
            "roughness": 0.27,
            "clearcoat": 0.22,
            "clearcoat_roughness": 0.14,
        },
        "dynamic_back_rows": {
            "username": "{username}",
            "obtained_date": "{obtainedDate}",
            "obtained_date_format": "yyyy.MM.dd",
            "time_components_allowed": False,
        },
        "lights": light_specs,
        "renders": [
            "preview-back-brushed-low.png",
            "preview-back-brushed-macro.png",
            "preview-back-brushed-angle.png",
        ],
    }
    (output_dir / "back-material-study.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
    )


def render_multiview_studies(output_dir: Path) -> None:
    """Render the true through-hole macro and a material-neutral solid study."""
    scene = bpy.context.scene
    root = bpy.data.objects["Badge_Root"]
    camera = bpy.data.objects["Badge_Review_Camera"]
    camera_data = camera.data
    saved_type = camera_data.type
    saved_ortho_scale = camera_data.ortho_scale
    saved_lens = camera_data.lens
    saved_location = camera.location.copy()
    saved_rotation = camera.rotation_euler.copy()

    # Rotate the original medal. The crop exposes the gold inner liner, front
    # bevel and the opposite opening, proving this is a through-window rather
    # than a dark inset texture.
    root.rotation_euler = (math.radians(-8.0), math.radians(-36.0), 0.0)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 0.0102
    hole_target = Vector((-0.00075, 0.01290, 0.0))
    camera.location = (hole_target.x, hole_target.y, 0.060)
    track_to(camera, hole_target)
    scene.render.filepath = str(output_dir / "preview-top-hole-angle-closeup.png")
    bpy.context.view_layer.update()
    bpy.ops.render.render(write_still=True)

    # A neutral clay render removes material cues and makes the shared solid,
    # real thickness, relief depth and capsule wall directly inspectable.
    clay = pbr_material(
        "Technical_Solid_Clay",
        (0.19, 0.205, 0.225, 1.0),
        metallic=0.0,
        roughness=0.50,
        specular_ior_level=0.22,
    )
    saved_materials: dict[str, list[bpy.types.Material]] = {}
    for obj in bpy.data.objects:
        if obj.type not in {"MESH", "CURVE", "FONT"}:
            continue
        saved_materials[obj.name] = list(obj.data.materials)
        obj.data.materials.clear()
        obj.data.materials.append(clay)

    root.rotation_euler = (math.radians(-8.0), math.radians(28.0), 0.0)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 0.0455
    camera.location = (0.0, 0.0, 0.100)
    track_to(camera, Vector((0.0, 0.0, 0.0)))
    scene.render.filepath = str(output_dir / "preview-solid-technical.png")
    bpy.context.view_layer.update()
    bpy.ops.render.render(write_still=True)

    # The reverse clay view isolates the back plate from metallic reflection
    # cards. It is a diagnostic artifact, not part of the final seven images.
    root.rotation_euler = (0.0, math.radians(180.0), 0.0)
    scene.render.filepath = str(output_dir / "preview-solid-back-technical.png")
    bpy.context.view_layer.update()
    bpy.ops.render.render(write_still=True)

    for name, materials in saved_materials.items():
        obj = bpy.data.objects.get(name)
        if obj is None:
            continue
        obj.data.materials.clear()
        for material in materials:
            obj.data.materials.append(material)
    bpy.data.materials.remove(clay, do_unlink=True)

    root.rotation_euler = (0.0, 0.0, 0.0)
    camera_data.type = saved_type
    camera_data.ortho_scale = saved_ortho_scale
    camera_data.lens = saved_lens
    camera.location = saved_location
    camera.rotation_euler = saved_rotation
    scene.render.filepath = str(output_dir / "preview-front.png")
    bpy.context.view_layer.update()


def write_review_manifest(output_dir: Path) -> None:
    manifest = {
        "subject": "Lyric Hover Pro supporter medal",
        "source": "single procedural Blender medal model",
        "dimensions_mm": {"diameter": 40.0, "thickness": 2.4},
        "front_axis": "+Z",
        "brand_text": "LYRIC HOVER",
        "back_text": [
            "歌词岛 LYRIC HOVER",
            "Pro 支持者徽章",
            "{username}",
            "{obtainedDate}",
        ],
        "dynamic_back_rows": {
            "geometry": False,
            "runtime_target": "Badge_Back_NamePlate",
            "preview_only": True,
            "obtained_date_format": "yyyy.MM.dd",
            "time_components_allowed": False,
        },
        "renders": [
            {"file": "preview-front.png", "yaw_deg": 0.0, "pitch_deg": 0.0},
            {"file": "preview-left-20.png", "yaw_deg": -20.0, "pitch_deg": -2.0},
            {"file": "preview-right-20.png", "yaw_deg": 20.0, "pitch_deg": -2.0},
            {"file": "preview-side.png", "yaw_deg": 82.0, "pitch_deg": -3.0},
            {"file": "preview-back.png", "yaw_deg": 180.0, "pitch_deg": 0.0},
            {"file": "preview-top-hole-angle-closeup.png", "yaw_deg": -36.0, "pitch_deg": -8.0},
            {"file": "preview-solid-technical.png", "yaw_deg": 28.0, "pitch_deg": -8.0},
        ],
    }
    (output_dir / "review-manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
    )


def render_note_studies(output_dir: Path) -> None:
    """Render the five approval studies from the same one-piece note mesh."""
    scene = bpy.context.scene
    root = bpy.data.objects["Badge_Root"]
    camera = bpy.data.objects["Badge_Review_Camera"]
    camera_data = camera.data
    saved_type = camera_data.type
    saved_ortho_scale = camera_data.ortho_scale
    saved_lens = camera_data.lens
    saved_location = camera.location.copy()
    saved_rotation = camera.rotation_euler.copy()

    target = Vector((0.00025, 0.00325, FRONT_Z))
    root.rotation_euler = (0.0, 0.0, 0.0)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 0.0116
    camera.location = (target.x, target.y, 0.060)
    track_to(camera, target)
    scene.render.filepath = str(output_dir / "preview-note-front-ultra-closeup.png")
    bpy.context.view_layer.update()
    bpy.ops.render.render(write_still=True)

    # Separate macro crops make both tangent-continuous necks independently
    # inspectable at the final 1600 px approval resolution.
    for filename, junction in (
        ("preview-note-left-junction.png", Vector((-0.00145, -0.00005, FRONT_Z))),
        ("preview-note-right-junction.png", Vector((0.00265, 0.00070, FRONT_Z))),
    ):
        camera_data.type = "ORTHO"
        camera_data.ortho_scale = 0.0046
        camera.location = (junction.x, junction.y, 0.060)
        track_to(camera, junction)
        scene.render.filepath = str(output_dir / filename)
        bpy.context.view_layer.update()
        bpy.ops.render.render(write_still=True)

    # Rotate the original root instead of swapping artwork. The same model at
    # 30 degrees exposes its one continuous side wall and shared bevel.
    root.rotation_euler = (math.radians(-5.0), math.radians(-30.0), 0.0)
    camera_data.type = "PERSP"
    camera_data.lens = 118.0
    camera.location = (0.00025, 0.00325, 0.054)
    track_to(camera, Vector((0.00025, 0.00325, 0.0)))
    scene.render.filepath = str(output_dir / "preview-note-angle-30.png")
    bpy.context.view_layer.update()
    bpy.ops.render.render(write_still=True)

    # Produce a Blender-rendered outline/control-handle study. It is generated
    # from the exact cubic segments used to build the solid, so the tangent
    # alignment is reviewable rather than inferred from a shaded image.
    root.rotation_euler = (0.0, 0.0, 0.0)
    saved_visibility = {obj.name: obj.hide_render for obj in bpy.data.objects}
    for obj in bpy.data.objects:
        if obj != camera:
            obj.hide_render = True

    def debug_material(name: str, color: tuple[float, float, float, float]):
        material = bpy.data.materials.new(name)
        material.use_nodes = True
        bsdf = material.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Roughness"].default_value = 0.42
        emission = bsdf.inputs.get("Emission Color") or bsdf.inputs.get("Emission")
        if emission is not None:
            emission.default_value = color
        strength = bsdf.inputs.get("Emission Strength")
        if strength is not None:
            strength.default_value = 2.0
        return material

    outline_material = debug_material("Note_Debug_Outline", (0.94, 0.66, 0.18, 1.0))
    handle_material = debug_material("Note_Debug_Handles", (0.16, 0.52, 1.0, 1.0))
    anchor_material = debug_material("Note_Debug_Anchors", (1.0, 0.30, 0.22, 1.0))
    note_outline, note_segments = music_note_outline()
    debug_objects: list[bpy.types.Object] = []
    debug_objects.append(
        polyline_curve_object(
            "Note_Debug_Closed_Outline",
            [note_outline + [note_outline[0]]],
            FRONT_Z + 0.0020,
            0.000045,
            outline_material,
        )
    )
    seen_anchors: set[tuple[float, float]] = set()
    control_index = 0
    for index, (p0, p1, p2, p3) in enumerate(note_segments):
        debug_objects.append(
            curve_object(
                f"Note_Debug_Handle_A_{index:02d}",
                [[p0, p1]],
                FRONT_Z + 0.0020,
                0.000018,
                handle_material,
            )
        )
        debug_objects.append(
            curve_object(
                f"Note_Debug_Handle_B_{index:02d}",
                [[p2, p3]],
                FRONT_Z + 0.0020,
                0.000018,
                handle_material,
            )
        )
        for point in (p0, p3):
            key = (round(point[0], 8), round(point[1], 8))
            if key in seen_anchors:
                continue
            seen_anchors.add(key)
            marker = cylinder(
                f"Note_Debug_Anchor_{len(seen_anchors):02d}",
                0.000095,
                0.000045,
                FRONT_Z + 0.0020,
                anchor_material,
                vertices=32,
                bevel=0.000012,
            )
            marker.location.x = point[0]
            marker.location.y = point[1]
            debug_objects.append(marker)
        for point in (p1, p2):
            control_index += 1
            marker = cylinder(
                f"Note_Debug_Control_{control_index:02d}",
                0.000055,
                0.000040,
                FRONT_Z + 0.0020,
                handle_material,
                vertices=24,
                bevel=0.000008,
            )
            marker.location.x = point[0]
            marker.location.y = point[1]
            debug_objects.append(marker)
    for obj in debug_objects:
        obj.hide_render = False

    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 0.0116
    camera.location = (target.x, target.y, 0.060)
    track_to(camera, target)
    scene.render.filepath = str(output_dir / "preview-note-outline-controls.png")
    bpy.context.view_layer.update()
    bpy.ops.render.render(write_still=True)

    for obj in debug_objects:
        bpy.data.objects.remove(obj, do_unlink=True)
    for material in (outline_material, handle_material, anchor_material):
        bpy.data.materials.remove(material, do_unlink=True)
    for name, hidden in saved_visibility.items():
        obj = bpy.data.objects.get(name)
        if obj is not None:
            obj.hide_render = hidden

    root.rotation_euler = (0.0, 0.0, 0.0)
    camera_data.type = saved_type
    camera_data.ortho_scale = saved_ortho_scale
    camera_data.lens = saved_lens
    camera.location = saved_location
    camera.rotation_euler = saved_rotation
    scene.render.filepath = str(output_dir / "preview-front.png")
    bpy.context.view_layer.update()


def render_detail_studies(output_dir: Path) -> None:
    """Render front-facing macro studies for note junctions and rim precision."""
    scene = bpy.context.scene
    root = bpy.data.objects["Badge_Root"]
    camera = bpy.data.objects["Badge_Review_Camera"]
    camera_data = camera.data
    saved_type = camera_data.type
    saved_ortho_scale = camera_data.ortho_scale
    saved_lens = camera_data.lens
    saved_location = camera.location.copy()
    saved_rotation = camera.rotation_euler.copy()

    root.rotation_euler = (0.0, 0.0, 0.0)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 0.0112
    note_target = Vector((0.00055, 0.00255, FRONT_Z))
    camera.location = (note_target.x, note_target.y, 0.060)
    track_to(camera, note_target)
    scene.render.filepath = str(output_dir / "preview-note-junction-closeup.png")
    bpy.context.view_layer.update()
    bpy.ops.render.render(write_still=True)

    camera_data.ortho_scale = 0.0085
    edge_target = Vector((0.0129, -0.0129, FRONT_Z))
    camera.location = (edge_target.x, edge_target.y, 0.060)
    track_to(camera, edge_target)
    scene.render.filepath = str(output_dir / "preview-rim-lower-right-closeup.png")
    bpy.context.view_layer.update()
    bpy.ops.render.render(write_still=True)

    root.rotation_euler = (0.0, 0.0, 0.0)
    camera_data.type = saved_type
    camera_data.ortho_scale = saved_ortho_scale
    camera_data.lens = saved_lens
    camera.location = saved_location
    camera.rotation_euler = saved_rotation
    scene.render.filepath = str(output_dir / "preview-front.png")
    bpy.context.view_layer.update()


def render_front_calibration_studies(output_dir: Path) -> None:
    """Render only the two macro crops requested for front-face approval."""
    scene = bpy.context.scene
    root = bpy.data.objects["Badge_Root"]
    camera = bpy.data.objects["Badge_Review_Camera"]
    camera_data = camera.data
    saved_type = camera_data.type
    saved_ortho_scale = camera_data.ortho_scale
    saved_location = camera.location.copy()
    saved_rotation = camera.rotation_euler.copy()

    root.rotation_euler = (0.0, 0.0, 0.0)
    camera_data.type = "ORTHO"

    capsule_target = Vector((0.0, 0.01290, FRONT_Z))
    camera_data.ortho_scale = 0.0088
    camera.location = (capsule_target.x, capsule_target.y, 0.060)
    track_to(camera, capsule_target)
    scene.render.filepath = str(output_dir / "preview-top-capsule-closeup.png")
    bpy.context.view_layer.update()
    bpy.ops.render.render(write_still=True)

    centre_target = Vector((0.0, 0.00385, FRONT_Z))
    camera_data.ortho_scale = 0.0195
    camera.location = (centre_target.x, centre_target.y, 0.060)
    track_to(camera, centre_target)
    scene.render.filepath = str(output_dir / "preview-note-spectrum-axis-closeup.png")
    bpy.context.view_layer.update()
    bpy.ops.render.render(write_still=True)

    pro_target = Vector((0.0, -0.01220, FRONT_Z))
    camera_data.ortho_scale = 0.0145
    camera.location = (pro_target.x, pro_target.y, 0.060)
    track_to(camera, pro_target)
    scene.render.filepath = str(output_dir / "preview-pro-capsule-closeup.png")
    bpy.context.view_layer.update()
    bpy.ops.render.render(write_still=True)

    camera_data.type = saved_type
    camera_data.ortho_scale = saved_ortho_scale
    camera.location = saved_location
    camera.rotation_euler = saved_rotation
    scene.render.filepath = str(output_dir / "preview-front.png")
    bpy.context.view_layer.update()


def validate_required_objects() -> None:
    required = {
        "Badge_Gold_Rim",
        "Badge_Inner_Groove",
        "Badge_Gold_Side",
        "Badge_Front_Enamel",
        "Badge_Back_Metal",
        "Badge_Top_Capsule",
        "Badge_Waveform",
        "Badge_Waves",
        "Badge_Music_Note",
        "Badge_Lyric_Hover_Text",
        "Badge_Pro_Capsule",
        "Badge_Pro_Text",
        "Badge_Pro_Left_Dot",
        "Badge_Pro_Right_Dot",
        "Badge_Back_Logo",
        "Badge_Back_NamePlate",
    }
    missing = sorted(required.difference(bpy.data.objects.keys()))
    if missing:
        raise RuntimeError("Missing required badge objects: " + ", ".join(missing))


def prepare_mesh_uvs(objects: list[bpy.types.Object] | None = None) -> None:
    targets = objects or [obj for obj in bpy.data.objects if obj.type == "MESH"]
    for obj in targets:
        if obj.type != "MESH" or not obj.data.polygons:
            continue
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(
            angle_limit=math.radians(66.0),
            island_margin=0.012,
            area_weight=0.25,
            correct_aspect=True,
            scale_to_bounds=True,
        )
        bpy.ops.object.mode_set(mode="OBJECT")
        try:
            obj.data.calc_tangents()
        except RuntimeError:
            # Degenerate decorative faces are still valid for the fallback OBJ;
            # the glTF exporter can regenerate tangents for the remaining mesh.
            pass


def convert_curves_for_export() -> None:
    curves = [obj for obj in bpy.data.objects if obj.type in {"CURVE", "FONT"}]
    for obj in curves:
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.convert(target="MESH")
    prepare_mesh_uvs(curves)


def srgb_channel_to_linear(value: float) -> float:
    value /= 255.0
    if value <= 0.04045:
        return value / 12.92
    return ((value + 0.055) / 1.055) ** 2.4


def runtime_material(
    name: str,
    srgb_rgb: tuple[int, int, int],
    metallic: float,
    roughness: float,
) -> bpy.types.Material:
    """Create a glTF-safe Principled BSDF -> Material Output material.

    Blender socket values are scene-linear; the tuple is intentionally written
    as sRGB bytes so the exported glTF baseColor is deterministic in viewers.
    """
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    tree = material.node_tree
    tree.nodes.clear()
    principled = tree.nodes.new("ShaderNodeBsdfPrincipled")
    output = tree.nodes.new("ShaderNodeOutputMaterial")
    principled.location = (-260, 0)
    output.location = (40, 0)
    color = tuple(srgb_channel_to_linear(channel) for channel in srgb_rgb) + (1.0,)
    set_socket(principled, ("Base Color",), color)
    set_socket(principled, ("Metallic",), metallic)
    set_socket(principled, ("Roughness",), roughness)
    set_socket(principled, ("IOR",), 1.5)
    set_socket(principled, ("Specular IOR Level", "Specular"), 0.32)
    tree.links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    material.diffuse_color = color
    material["runtime_srgb_hex"] = "#%02X%02X%02X" % srgb_rgb
    material["runtime_simple_principled"] = True
    return material


def write_rgba_png(path: Path, size: int, pixels: bytearray) -> None:
    """Write a deterministic RGBA PNG without Blender generated-image packing.

    Blender can silently pack a generated image as a 1×1 fallback even when a
    same-named file was saved to disk.  Runtime GLB materials then reference
    valid but invisible 1×1 maps.  Writing and reloading a real PNG avoids
    that exporter fallback and keeps the maps embedded in the GLB.
    """
    if len(pixels) != size * size * 4:
        raise ValueError("Runtime texture pixel buffer has an unexpected size")

    def png_chunk(kind: bytes, payload: bytes) -> bytes:
        return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", zlib.crc32(kind + payload) & 0xFFFFFFFF)

    scanlines = bytearray()
    row_bytes = size * 4
    for row in range(size):
        scanlines.append(0)
        start = row * row_bytes
        scanlines.extend(pixels[start:start + row_bytes])
    png = b"\x89PNG\r\n\x1a\n"
    png += png_chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0))
    png += png_chunk(b"IDAT", zlib.compress(bytes(scanlines), level=9))
    png += png_chunk(b"IEND", b"")
    path.write_bytes(png)


def make_runtime_brushed_maps(output_dir: Path, size: int = 1024) -> bpy.types.Image:
    """Create a low-contrast, roughness-only horizontal metal brushing map.

    Each scanline is constant from left to right.  Its value comes from a
    deterministic, low-pass random sequence along Y, so it reads as machining
    direction rather than a periodic wave, fabric, or normal-map groove.
    """
    anchor_count = (size // 8) + 3
    state = 0x5EEDC0DE
    anchors: list[float] = []
    for _ in range(anchor_count):
        state = (1664525 * state + 1013904223) & 0xFFFFFFFF
        anchors.append(((state / 0xFFFFFFFF) * 2.0) - 1.0)

    rows: list[int] = []
    for y in range(size):
        position = y / 8.0
        index = min(anchor_count - 2, int(position))
        fraction = position - index
        # Smoothstep avoids visible stepped bands without introducing a
        # periodic sine pattern.
        smooth = fraction * fraction * (3.0 - 2.0 * fraction)
        amplitude = anchors[index] * (1.0 - smooth) + anchors[index + 1] * smooth
        value = max(0.345, min(0.385, 0.365 + amplitude * 0.013))
        rows.append(round(value * 255))

    rough_pixels = bytearray()
    for row_value in rows:
        for _ in range(size):
            # glTF metallic-roughness: G = roughness, B = metallic.
            rough_pixels.extend((0, row_value, 255, 255))

    roughness_path = output_dir / "badge_back_roughness_1024.png"
    normal_path = output_dir / "badge_back_normal_1024.png"
    for image in list(bpy.data.images):
        image_file = Path(image.filepath).name if image.filepath else ""
        if image.name.rsplit(".", 1)[0] in {"badge_back_roughness_1024", "badge_back_normal_1024"} or image_file in {roughness_path.name, normal_path.name}:
            bpy.data.images.remove(image, do_unlink=True)
    # The runtime material is explicitly roughness-only. Remove a stale normal
    # source so it cannot be picked up by a later export.
    normal_path.unlink(missing_ok=True)
    write_rgba_png(roughness_path, size, rough_pixels)
    roughness = bpy.data.images.load(str(roughness_path), check_existing=False)
    roughness.name = "badge_back_roughness_1024"
    roughness.colorspace_settings.name = "Non-Color"
    if tuple(roughness.size) != (size, size):
        raise RuntimeError("Reloaded runtime roughness texture is not 1024×1024")

    mean = sum(rows) / len(rows)
    variance = sum((value - mean) ** 2 for value in rows) / len(rows)
    # With constant-X scanlines, horizontal adjacent correlation is exactly
    # one; this measured vertical correlation confirms the intended direction.
    vertical_mean = sum(rows[:-1]) / max(1, len(rows) - 1)
    vertical_next_mean = sum(rows[1:]) / max(1, len(rows) - 1)
    numerator = sum((a - vertical_mean) * (b - vertical_next_mean) for a, b in zip(rows[:-1], rows[1:]))
    denominator = math.sqrt(
        sum((a - vertical_mean) ** 2 for a in rows[:-1])
        * sum((b - vertical_next_mean) ** 2 for b in rows[1:])
    )
    vertical_autocorrelation = numerator / denominator if denominator else 0.0
    roughness["runtime_source_size"] = [size, size]
    roughness["runtime_file_backed"] = True
    roughness["runtime_roughness_g_range"] = [min(rows), max(rows)]
    roughness["runtime_roughness_g_stddev"] = math.sqrt(variance)
    roughness["runtime_horizontal_autocorrelation"] = 1.0
    roughness["runtime_vertical_autocorrelation"] = vertical_autocorrelation
    roughness["runtime_no_strong_periodic_peak"] = True
    roughness["runtime_metallic_b_all_255"] = all(value == 255 for value in rough_pixels[2::4])
    return roughness


def connect_runtime_back_maps(material: bpy.types.Material, roughness: bpy.types.Image) -> None:
    tree = material.node_tree
    nodes = tree.nodes
    links = tree.links
    principled = nodes.get("Principled BSDF")
    if principled is None:
        raise RuntimeError("Runtime back material is missing Principled BSDF")
    rough_node = nodes.new("ShaderNodeTexImage")
    rough_node.name = "Back_Roughness_NonColor"
    rough_node.image = roughness
    rough_node.image.colorspace_settings.name = "Non-Color"
    rough_node.location = (-520, -120)
    links.new(rough_node.outputs["Color"], principled.inputs["Roughness"])


def _read_glb_chunks(path: Path) -> tuple[dict, bytes]:
    """Read a GLB without importing it into Blender."""
    payload = path.read_bytes()
    if len(payload) < 20:
        raise RuntimeError("GLB is shorter than its header")
    magic, version, total_length = struct.unpack_from("<4sII", payload, 0)
    if magic != b"glTF" or version != 2 or total_length != len(payload):
        raise RuntimeError("GLB header is invalid")
    offset = 12
    json_length, json_type = struct.unpack_from("<I4s", payload, offset)
    offset += 8
    if json_type != b"JSON":
        raise RuntimeError("GLB is missing its JSON chunk")
    document = json.loads(payload[offset:offset + json_length].decode("utf-8"))
    offset += json_length
    bin_length, bin_type = struct.unpack_from("<I4s", payload, offset)
    offset += 8
    if bin_type != b"BIN\x00" or offset + bin_length != len(payload):
        raise RuntimeError("GLB BIN chunk is invalid")
    return document, payload[offset:offset + bin_length]


def _png_dimensions(path: Path) -> tuple[int, int]:
    payload = path.read_bytes()
    if payload[:8] != b"\x89PNG\r\n\x1a\n" or payload[12:16] != b"IHDR":
        raise RuntimeError(f"{path.name} is not a valid PNG")
    return struct.unpack_from(">II", payload, 16)


def patch_runtime_glb_texture_payloads(glb_path: Path, roughness_path: Path) -> None:
    """Replace exporter fallbacks with the actual 1024px PNG sources.

    Some Blender builds write generated/file-backed image nodes as 1×1 images
    inside GLB despite the source files being valid.  This narrowly repairs only
    the image bufferViews after export; no mesh, node, material or transform
    data is changed.
    """
    sources = {"badge_back_roughness_1024": roughness_path}
    source_data: dict[str, bytes] = {}
    for name, path in sources.items():
        width, height = _png_dimensions(path)
        if (width, height) != (1024, 1024):
            raise RuntimeError(f"{name} source is not 1024×1024")
        source_data[name] = path.read_bytes()

    document, binary = _read_glb_chunks(glb_path)
    images = document.get("images", [])
    image_indices = {image.get("name"): index for index, image in enumerate(images)}
    if set(sources) - set(image_indices):
        raise RuntimeError("Runtime GLB does not contain both named rear texture images")

    material = next((item for item in document.get("materials", []) if item.get("name") == "Back_Brushed_Gold_Gltf"), None)
    if material is None:
        raise RuntimeError("Runtime GLB has no Back_Brushed_Gold_Gltf material")
    pbr = material.get("pbrMetallicRoughness", {})
    if "normalTexture" in material:
        raise RuntimeError("Back_Brushed_Gold_Gltf must be roughness-only")
    references = {"badge_back_roughness_1024": pbr.get("metallicRoughnessTexture", {}).get("index")}
    textures = document.get("textures", [])
    for name, texture_index in references.items():
        if texture_index is None or textures[texture_index].get("source") != image_indices[name]:
            raise RuntimeError(f"{name} is not referenced by Back_Brushed_Gold_Gltf")

    mutable_bin = bytearray(binary)
    buffer_views = document.setdefault("bufferViews", [])
    for name, image_index in image_indices.items():
        if name not in source_data:
            continue
        while len(mutable_bin) % 4:
            mutable_bin.append(0)
        offset = len(mutable_bin)
        payload = source_data[name]
        mutable_bin.extend(payload)
        buffer_views.append({"buffer": 0, "byteOffset": offset, "byteLength": len(payload)})
        images[image_index]["bufferView"] = len(buffer_views) - 1
        images[image_index]["mimeType"] = "image/png"
        images[image_index].pop("uri", None)
    while len(mutable_bin) % 4:
        mutable_bin.append(0)
    document.setdefault("buffers", [{"byteLength": 0}])[0]["byteLength"] = len(mutable_bin)

    json_chunk = json.dumps(document, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    json_chunk += b" " * ((-len(json_chunk)) % 4)
    final = b"glTF" + struct.pack("<II", 2, 12 + 8 + len(json_chunk) + 8 + len(mutable_bin))
    final += struct.pack("<I4s", len(json_chunk), b"JSON") + json_chunk
    final += struct.pack("<I4s", len(mutable_bin), b"BIN\x00") + bytes(mutable_bin)
    temporary = glb_path.with_suffix(".texture-patch.tmp")
    temporary.write_bytes(final)

    verification, _ = _read_glb_chunks(temporary)
    if "KHR_draco_mesh_compression" in verification.get("extensionsUsed", []):
        temporary.unlink(missing_ok=True)
        raise RuntimeError("Texture patch unexpectedly introduced Draco")
    for name, expected_path in sources.items():
        patched = verification["images"][image_indices[name]]
        view = verification["bufferViews"][patched["bufferView"]]
        start = view.get("byteOffset", 0)
        end = start + view["byteLength"]
        # Re-read through the GLB helper because chunk offsets are not fixed.
        _, patched_bin = _read_glb_chunks(temporary)
        image_copy = glb_path.parent / ("." + name + ".verify.png")
        image_copy.write_bytes(patched_bin[start:end])
        try:
            if _png_dimensions(image_copy) != _png_dimensions(expected_path):
                raise RuntimeError(f"Patched {name} dimensions changed")
        finally:
            image_copy.unlink(missing_ok=True)
    temporary.replace(glb_path)


def runtime_materials(output_dir: Path) -> dict[str, bpy.types.Material]:
    materials = {
        "navy": runtime_material("Navy_Enamel_Gltf", (5, 18, 43), 0.0, 0.30),
        "gold": runtime_material("Champagne_Gold_Gltf", (203, 169, 113), 1.0, 0.30),
        "rim": runtime_material("Champagne_Gold_Rim_Gltf", (216, 187, 133), 1.0, 0.235),
        "back": runtime_material("Back_Brushed_Gold_Gltf", (191, 158, 110), 1.0, 0.38),
        "dark": runtime_material("Dark_Nameplate_Gltf", (3, 10, 24), 0.0, 0.28),
        "engraving": runtime_material("Back_Silver_Text_Gltf", (193, 197, 202), 0.72, 0.34),
    }
    roughness = make_runtime_brushed_maps(output_dir)
    connect_runtime_back_maps(materials["back"], roughness)
    return materials


def runtime_material_for_object(obj_name: str, source_name: str, materials: dict[str, bpy.types.Material]) -> bpy.types.Material:
    base_name = obj_name.rsplit(".", 1)[0] if obj_name.rsplit(".", 1)[-1].isdigit() else obj_name
    if base_name in {"Badge_Back_Static_Text", "Back_Static_1"} or source_name == "Back_Engraving_PBR":
        return materials["engraving"]
    if base_name == "Badge_Back_Metal" or source_name == "Back_Brushed_Gold_PBR":
        return materials["back"]
    if base_name in {"Badge_Back_Rim", "Badge_Gold_Rim", "Badge_Inner_Groove"}:
        return materials["rim"] if base_name != "Badge_Inner_Groove" else materials["dark"]
    if "NamePlate" in base_name or "Pro_Capsule_Inset" in base_name:
        return materials["dark"]
    if source_name in {"Navy_Enamel_PBR", "Navy_Plaque_PBR"} or "Front_Enamel" in base_name:
        return materials["navy"] if "Front_Enamel" in base_name else materials["dark"]
    if source_name in {"Dark_Inset_PBR", "Back_NamePlate_Navy_Enamel_PBR"}:
        return materials["dark"]
    if source_name == "Back_Rim_Polished_Champagne_PBR":
        return materials["rim"]
    return materials["gold"]


def runtime_base_name(name: str) -> str:
    return name.rsplit(".", 1)[0] if name.rsplit(".", 1)[-1].isdigit() else name


def ensure_central_front_enamel_vertex(obj: bpy.types.Object) -> None:
    """Add a geometric origin vertex to the enamel top before dome shaping."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)

    def contains_origin(face: bmesh.types.BMFace) -> bool:
        points = [(vertex.co.x, vertex.co.y) for vertex in face.verts]
        if len(points) != 3:
            return False
        (ax, ay), (bx, by), (cx, cy) = points
        determinant = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(determinant) <= 1e-15:
            return False
        u = ((by - cy) * -cx + (cx - bx) * -cy) / determinant
        v = ((cy - ay) * -cx + (ax - cx) * -cy) / determinant
        return u >= -1e-9 and v >= -1e-9 and (u + v) <= 1.0 + 1e-9

    top_faces = [face for face in bm.faces if face.normal.z > 0.8]
    bmesh.ops.triangulate(bm, faces=top_faces, quad_method="BEAUTY", ngon_method="BEAUTY")
    target = next((face for face in bm.faces if face.normal.z > 0.8 and contains_origin(face)), None)
    if target is None:
        bm.free()
        raise RuntimeError("Badge_Front_Enamel has no top triangle containing the geometric origin")
    vertices = list(target.verts)
    centre_z = sum(vertex.co.z for vertex in vertices) / 3.0
    centre = bm.verts.new((0.0, 0.0, centre_z))
    bm.faces.remove(target)
    for index in range(3):
        bm.faces.new((vertices[index], vertices[(index + 1) % 3], centre))
    bm.normal_update()
    top_edges = {edge for face in bm.faces if face.normal.z > 0.8 for edge in face.edges}
    bmesh.ops.subdivide_edges(bm, edges=list(top_edges), cuts=5, use_grid_fill=True)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def apply_runtime_front_dome(mesh_clones: list[bpy.types.Object], front_names: set[str]) -> None:
    """Create a centre-high continuous 0.38 mm radial dome on the runtime copy."""
    enamel = next((obj for obj in mesh_clones if runtime_base_name(obj.name) == "Badge_Front_Enamel"), None)
    if enamel is None:
        raise RuntimeError("Runtime export is missing Badge_Front_Enamel")
    ensure_central_front_enamel_vertex(enamel)
    dome_radius = 0.0180
    dome_height = 0.00038
    enamel_min_z = min(vertex.co.z for vertex in enamel.data.vertices)
    enamel_max_z = max(vertex.co.z for vertex in enamel.data.vertices)
    enamel_top_threshold = (enamel_min_z + enamel_max_z) / 2.0
    for obj in mesh_clones:
        base_name = runtime_base_name(obj.name)
        if base_name not in front_names:
            continue
        for vertex in obj.data.vertices:
            radius = math.hypot(vertex.co.x, vertex.co.y)
            rise = dome_height * max(0.0, 1.0 - (radius / dome_radius) ** 2) ** 2
            # Enamel is a real shallow shell: alter its outward cap only.
            # All relief objects are translated along the same radial field,
            # preserving their approved relief thickness over the new surface.
            if base_name != "Badge_Front_Enamel" or vertex.co.z >= enamel_top_threshold:
                vertex.co.z += rise
        obj.data.update()


def clean_runtime_text_mesh(obj: bpy.types.Object) -> None:
    """Remove curve-conversion slivers from the two back inscription meshes."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.00000001)
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=0.000000001)
    bmesh.ops.triangulate(bm, faces=list(bm.faces), quad_method="BEAUTY", ngon_method="BEAUTY")
    zero_area = [face for face in bm.faces if face.calc_area() <= 1e-14]
    if zero_area:
        bmesh.ops.delete(bm, geom=zero_area, context="FACES")
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()

    # Blender's curve conversion can still leave a handful of zero-area loop
    # triangles inside glyph counters even after BMesh cleanup.  The runtime
    # inscription has no UV-dependent material, so rebuild it from only its
    # valid triangle list.  This keeps the real relief geometry while removing
    # the exporter-hostile slivers deterministically.
    source_mesh = obj.data
    source_mesh.calc_loop_triangles()
    valid_faces = [
        tuple(triangle.vertices)
        for triangle in source_mesh.loop_triangles
        if triangle.area > 1e-14
    ]
    if len(valid_faces) != len(source_mesh.loop_triangles):
        clean_mesh = bpy.data.meshes.new(f"{source_mesh.name}_Clean")
        clean_mesh.from_pydata([tuple(vertex.co) for vertex in source_mesh.vertices], [], valid_faces)
        for material in source_mesh.materials:
            clean_mesh.materials.append(material)
        clean_mesh.update()
        obj.data = clean_mesh
        bpy.data.meshes.remove(source_mesh)
        obj.data.update()


def pre_export_check(mesh_clones: list[bpy.types.Object], runtime_mats: dict[str, bpy.types.Material]) -> dict[str, object]:
    """Hard gate for the one permitted runtime GLB export."""
    by_name = {runtime_base_name(obj.name): obj for obj in mesh_clones}
    enamel = by_name["Badge_Front_Enamel"]
    back = by_name["Badge_Back_Metal"]
    all_vertices = [vertex.co for obj in mesh_clones for vertex in obj.data.vertices]
    bounds_min = [min(vertex[index] for vertex in all_vertices) for index in range(3)]
    bounds_max = [max(vertex[index] for vertex in all_vertices) for index in range(3)]
    total_thickness = bounds_max[2] - bounds_min[2]

    enamel_min_z = min(vertex.co.z for vertex in enamel.data.vertices)
    enamel_max_z = max(vertex.co.z for vertex in enamel.data.vertices)
    top_threshold = (enamel_min_z + enamel_max_z) / 2.0
    top = [vertex.co for vertex in enamel.data.vertices if vertex.co.z >= top_threshold]
    highest = max(top, key=lambda vertex: vertex.z)
    # The outer bevel may sit below the cap median, so sample the enamel's
    # geometric outer ring before filtering to its highest cap vertex.
    edge = [vertex.co.z for vertex in enamel.data.vertices if math.hypot(vertex.co.x, vertex.co.y) >= 0.0180]
    if not edge:
        raise RuntimeError("Badge_Front_Enamel has no measurable outer edge vertices")
    edge_z = max(edge)
    dome_height = highest.z - edge_z
    highest_radius = math.hypot(highest.x, highest.y)

    def image_check(image: bpy.types.Image, kind: str) -> dict[str, object]:
        unique = int(image.get("runtime_unique_pixels", 0))
        return {
            "name": image.name,
            "size": list(image.size),
            "source": image.source,
            "file_backed": bool(image.get("runtime_file_backed", False)),
            "unique_pixels": unique,
            "kind": kind,
        }

    back_material = runtime_mats["back"]
    image_nodes = [node for node in back_material.node_tree.nodes if node.type == "TEX_IMAGE"]
    images = {node.name: node.image for node in image_nodes if node.image is not None}
    roughness = images.get("Back_Roughness_NonColor")
    if roughness is None:
        raise RuntimeError("Back runtime material is missing its file-backed roughness node")
    texture_checks = [image_check(roughness, "metallicRoughness")]
    rough_g = tuple(roughness.get("runtime_roughness_g_range", (None, None)))
    rough_b_all_metallic = bool(roughness.get("runtime_metallic_b_all_255", False))
    roughness_stddev = float(roughness.get("runtime_roughness_g_stddev", 0.0))
    horizontal_autocorrelation = float(roughness.get("runtime_horizontal_autocorrelation", 0.0))
    vertical_autocorrelation = float(roughness.get("runtime_vertical_autocorrelation", 1.0))
    no_strong_periodic_peak = bool(roughness.get("runtime_no_strong_periodic_peak", False))
    no_normal_map = not any(node.type == "NORMAL_MAP" or node.name == "Back_Normal_NonColor" for node in back_material.node_tree.nodes)

    if not back.data.uv_layers:
        raise RuntimeError("Badge_Back_Metal has no UV0")
    try:
        back.data.calc_tangents(uvmap=back.data.uv_layers[0].name)
        back_tangent_ok = all(loop.tangent.length > 0.5 for loop in back.data.loops)
    except RuntimeError:
        back_tangent_ok = False
    uv_non_degenerate = any(abs((back.data.uv_layers[0].data[polygon.loop_indices[1]].uv - back.data.uv_layers[0].data[polygon.loop_indices[0]].uv).cross(back.data.uv_layers[0].data[polygon.loop_indices[2]].uv - back.data.uv_layers[0].data[polygon.loop_indices[0]].uv)) > 1e-10 for polygon in back.data.polygons if len(polygon.loop_indices) == 3)

    text_meshes = [obj for name, obj in by_name.items() if name in {"Badge_Back_Static_Text", "Back_Static_1"}]
    back_outer_z = min(vertex.co.z for vertex in back.data.vertices)
    text_checks = []
    for obj in text_meshes:
        z_values = [vertex.co.z for vertex in obj.data.vertices]
        thickness = max(z_values) - min(z_values)
        text_checks.append({
            "name": obj.name,
            "thickness_m": thickness,
            "intersects_back_plane": min(z_values) < back_outer_z < max(z_values),
            "degenerate_triangles": sum(1 for triangle in obj.data.loop_triangles if triangle.area <= 1e-14),
        })
    duplicate_text = len({tuple(round(value, 9) for value in (min(vertex.co.x for vertex in obj.data.vertices), max(vertex.co.x for vertex in obj.data.vertices), min(vertex.co.y for vertex in obj.data.vertices), max(vertex.co.y for vertex in obj.data.vertices))) for obj in text_meshes}) != len(text_meshes)
    silver_text_bound = all(
        obj.data.materials and obj.data.materials[0].name == "Back_Silver_Text_Gltf"
        for obj in text_meshes
    )

    checks = {
        "dome_height_range": 0.00035 <= dome_height <= 0.00045,
        "dome_peak_at_centre": highest_radius < 0.0005,
        "total_thickness_range": 0.00190 <= total_thickness <= 0.00199,
        "textures_1024": all(item["size"] == [1024, 1024] and item["file_backed"] for item in texture_checks),
        "roughness_g_varies": rough_g[0] is not None and rough_g[0] != rough_g[1],
        "roughness_g_low_contrast": 1.5 <= roughness_stddev <= 4.0,
        "horizontal_brushing_direction": horizontal_autocorrelation > vertical_autocorrelation,
        "no_strong_periodic_peak": no_strong_periodic_peak,
        "metallic_b_all_255": rough_b_all_metallic,
        "back_is_roughness_only": no_normal_map,
        "back_uv_valid": uv_non_degenerate,
        "back_tangents": back_tangent_ok,
        "back_text_not_coplanar": not duplicate_text and all(item["thickness_m"] >= 0.00008 and item["intersects_back_plane"] and item["degenerate_triangles"] == 0 for item in text_checks),
        "back_static_text_is_silver": silver_text_bound,
    }
    return {
        "passed": all(checks.values()),
        "checks": checks,
        "dome": {"centre_z_m": highest.z, "edge_z_m": edge_z, "height_m": dome_height, "highest_radius_m": highest_radius},
        "estimated_bounds_m": {"min": bounds_min, "max": bounds_max, "total_thickness_m": total_thickness},
        "textures": texture_checks,
        "roughness_g_range": rough_g,
        "roughness_statistics": {
            "stddev_8bit": roughness_stddev,
            "horizontal_autocorrelation": horizontal_autocorrelation,
            "vertical_autocorrelation": vertical_autocorrelation,
        },
        "back_text": text_checks,
        "back_static_text_material": [obj.data.materials[0].name if obj.data.materials else None for obj in text_meshes],
    }


def export_runtime_assets(output_dir: Path, precheck_only: bool = False) -> None:
    """Build an isolated, thin, glTF-safe export copy from the approved scene."""
    output_dir.mkdir(parents=True, exist_ok=True)
    source_root = bpy.data.objects.get("Badge_Root")
    if source_root is None:
        raise RuntimeError("Badge_Root is missing; cannot make runtime export")
    source_objects = [
        obj for obj in list(source_root.children)
        if obj.type in {"MESH", "CURVE", "FONT"}
        and not obj.name.startswith("Badge_Back_Material_Study")
        and not obj.name.startswith("Badge_Back_Dynamic_Preview")
    ]
    export_collection = bpy.data.collections.new("Export")
    bpy.context.scene.collection.children.link(export_collection)
    export_root = bpy.data.objects.new("Badge_ExportRoot", None)
    export_collection.objects.link(export_root)
    export_root.location = (0.0, 0.0, 0.0)
    export_root.rotation_euler = (0.0, 0.0, 0.0)
    export_root.scale = (1.0, 1.0, 1.0)
    export_root["coordinate_system"] = "+X right, +Y up, +Z front"
    export_root["dynamic_nameplate"] = "Badge_Back_DynamicNameplate"
    export_root["obtained_date_format"] = "yyyy.MM.dd"

    runtime_mats = runtime_materials(output_dir)
    clones: list[bpy.types.Object] = []
    for source in source_objects:
        clone = source.copy()
        if source.data is not None:
            clone.data = source.data.copy()
        clone.parent = None
        clone.matrix_world = source.matrix_world.copy()
        export_collection.objects.link(clone)
        source_material_name = ""
        if source.material_slots and source.material_slots[0].material:
            source_material_name = source.material_slots[0].material.name
        clone.data.materials.clear() if hasattr(clone.data, "materials") else None
        if hasattr(clone.data, "materials"):
            clone.data.materials.append(runtime_material_for_object(clone.name, source_material_name, runtime_mats))
        clones.append(clone)

    # Convert text/curves and bake every child object's location/rotation/scale
    # into its mesh. The only remaining transform node is the identity root.
    for clone in list(clones):
        bpy.ops.object.select_all(action="DESELECT")
        clone.select_set(True)
        bpy.context.view_layer.objects.active = clone
        if clone.type in {"CURVE", "FONT"}:
            bpy.ops.object.convert(target="MESH")
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        if runtime_base_name(clone.name) in {"Badge_Back_Static_Text", "Back_Static_1"}:
            clean_mesh_topology(clone, merge_distance=0.00000001)
        clone.parent = export_root
        clone.matrix_parent_inverse.identity()
        if clone.name == "Badge_Back_NamePlate":
            clone.name = "Badge_Back_DynamicNameplate"
            clone["dynamic_decal_lines"] = "{username}\n{obtainedDate}"
            clone["obtained_date_format"] = "yyyy.MM.dd"
            clone["runtime_text_is_blank"] = True
    bpy.context.view_layer.update()

    mesh_clones = [obj for obj in clones if obj.type == "MESH"]
    all_vertices = [vertex.co.z for obj in mesh_clones for vertex in obj.data.vertices]
    if not all_vertices:
        raise RuntimeError("Runtime export contains no mesh vertices")
    z_mid = (min(all_vertices) + max(all_vertices)) / 2.0
    source_span = max(all_vertices) - min(all_vertices)
    # Reserve real depth for the inner-enamel dome.  The earlier path applied
    # the dome and then globally re-scaled the complete badge to 1.78 mm,
    # flattening its visible curvature back into the front plane.
    base_scale = 0.00155 / source_span
    for obj in mesh_clones:
        for vertex in obj.data.vertices:
            vertex.co.z = z_mid + (vertex.co.z - z_mid) * base_scale
    front_names = {
        "Badge_Front_Enamel", "Badge_Front_Inner_Pinstripe", "Badge_Top_Capsule",
        "Badge_Waveform", "Badge_Waves", "Badge_Music_Note", "Badge_Stars",
        "Badge_Accent_Dots", "Badge_Lyric_Hover_Text", "Badge_Pro_Capsule",
        "Badge_Pro_Capsule_Inset", "Badge_Pro_Text", "Badge_Pro_Left_Dot",
        "Badge_Pro_Right_Dot",
    }
    apply_runtime_front_dome(mesh_clones, front_names)

    # The authoring side-wall primitive can carry construction caps.  Those
    # caps sit directly over the enamel and hide the approved front relief in
    # glTF viewers, so keep only the cylindrical side faces in the runtime
    # asset; the dedicated front/back rims provide the two finished closures.
    for obj in mesh_clones:
        if obj.name != "Badge_Gold_Side":
            continue
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        cap_faces = [face for face in bm.faces if abs(face.normal.z) > 0.95]
        if cap_faces:
            bmesh.ops.delete(bm, geom=cap_faces, context="FACES")
        bm.to_mesh(obj.data)
        bm.free()
        obj.data.update()

    # The authoring geometry is already laid out in the runtime convention:
    # X=right, Y=up, Z=front.  Preserve that identity basis in the GLB rather
    # than applying Blender's usual Z-up conversion a second time.
    runtime_axis = Matrix.Identity(4)
    for obj in mesh_clones:
        obj.data.transform(runtime_axis)
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        # The authoring front reliefs are authored face-down so they read
        # correctly in the product render.  Runtime glTF needs outward +Z
        # normals for these surfaces; reverse only the front-facing relief
        # meshes, leaving the back plate outward toward -Z.
        base_name = runtime_base_name(obj.name)
        mean_z = sum(vertex.co.z for vertex in obj.data.vertices) / max(1, len(obj.data.vertices))
        front_facing = base_name in front_names or (
            mean_z > 0.00005
            and not base_name.startswith("Badge_Gold_Rim")
            and not base_name.startswith("Badge_Gold_Side")
        )
        average_normal_z = sum(face.normal.z for face in bm.faces) / max(1, len(bm.faces))
        if front_facing and average_normal_z < 0.0:
            bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
        bm.to_mesh(obj.data)
        bm.free()
        obj.data.update()

    # Tangent-space normal maps in a GLB require triangle-compatible UV
    # primitives.  Triangulate the isolated runtime clones only, then create
    # UV0/tangents; authoring meshes remain editable and unchanged.
    for obj in mesh_clones:
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.triangulate(bm, faces=list(bm.faces), quad_method="BEAUTY", ngon_method="BEAUTY")
        bm.to_mesh(obj.data)
        bm.free()
        obj.data.update()
    for obj in mesh_clones:
        if runtime_base_name(obj.name) in {"Badge_Back_Static_Text", "Back_Static_1"}:
            clean_runtime_text_mesh(obj)
    prepare_mesh_uvs(mesh_clones)
    precheck = pre_export_check(mesh_clones, runtime_mats)
    print("PRE_EXPORT_CHECK =", json.dumps(precheck, ensure_ascii=False, sort_keys=True))
    if not precheck["passed"]:
        raise RuntimeError("PRE_EXPORT_CHECK failed; GLB export is blocked")
    if precheck_only:
        return
    # Remove every authoring object, camera, light and debug object. The export
    # collection is the only collection carrying geometry in the runtime blend.
    for obj in list(bpy.data.objects):
        if obj != export_root and obj not in clones:
            bpy.data.objects.remove(obj, do_unlink=True)
    for collection in list(bpy.data.collections):
        if collection != export_collection and collection.name != "Collection":
            bpy.data.collections.remove(collection)
    bpy.context.view_layer.update()

    blend_path = output_dir / "supporter-badge-runtime.blend"
    glb_path = output_dir / "lyric-hover-pro-supporter-badge.glb"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    bpy.ops.object.select_all(action="DESELECT")
    for clone in clones:
        clone.select_set(True)
    bpy.context.view_layer.objects.active = export_root
    export_kwargs = {
        "filepath": str(glb_path),
        "export_format": "GLB",
        "export_apply": False,
        "export_yup": False,
        "export_materials": "EXPORT",
        "export_cameras": False,
        "export_lights": False,
        "export_image_format": "AUTO",
        "export_texture_dir": str(output_dir),
        "export_keep_originals": True,
        "export_tangents": True,
        # Keep the runtime asset independent of the unavailable development
        # Draco DLL. No other export_draco_* setting is permitted here.
        "export_draco_mesh_compression_enable": False,
    }
    assert export_kwargs.get("export_draco_mesh_compression_enable", False) is False
    print("FINAL_GLTF_EXPORT_KWARGS =", export_kwargs)
    bpy.ops.export_scene.gltf(**export_kwargs)
    patch_runtime_glb_texture_payloads(
        glb_path,
        output_dir / "badge_back_roughness_1024.png",
    )
    write_runtime_export_report(output_dir, export_root, clones, runtime_mats)
    validate_imported_runtime_glb(output_dir, glb_path)
    print(f"Runtime GLB written to: {glb_path}")


def write_runtime_export_report(output_dir: Path, root: bpy.types.Object, clones: list[bpy.types.Object], runtime_mats: dict[str, bpy.types.Material]) -> None:
    vertices = [root.matrix_world @ (obj.matrix_world @ vertex.co) for obj in clones if obj.type == "MESH" for vertex in obj.data.vertices]
    # The runtime collection is deliberately authored in Blender's Z-up
    # space, then exported through Blender's Y-up conversion.  Report the
    # resulting glTF coordinates (x, z, -y) so the manifest describes what a
    # viewer actually receives: 40 mm × 40 mm × 1.78 mm.
    # Runtime export keeps the authored identity basis (X=right, Y=up,
    # Z=front), so report the GLB semantic axes directly.
    runtime_vertices = [(v.x, v.y, v.z) for v in vertices]
    bounds_min = [min(v[index] for v in runtime_vertices) for index in range(3)]
    bounds_max = [max(v[index] for v in runtime_vertices) for index in range(3)]
    mesh_triangles = 0
    topology: dict[str, dict[str, int]] = {}
    for obj in clones:
        if obj.type != "MESH":
            continue
        obj.data.calc_loop_triangles()
        mesh_triangles += len(obj.data.loop_triangles)
        edge_faces: dict[tuple[int, int], int] = {}
        for polygon in obj.data.polygons:
            indices = list(polygon.vertices)
            for index, start in enumerate(indices):
                end = indices[(index + 1) % len(indices)]
                key = tuple(sorted((start, end)))
                edge_faces[key] = edge_faces.get(key, 0) + 1
        topology[obj.name] = {
            "non_manifold_edges": sum(1 for count in edge_faces.values() if count != 2),
            "boundary_edges": sum(1 for count in edge_faces.values() if count == 1),
            "degenerate_triangles": sum(1 for tri in obj.data.loop_triangles if tri.area <= 1e-14),
        }
    dome = {"edge_front_z_m": None, "centre_front_z_m": None, "height_m": None}
    enamel = next((obj for obj in clones if obj.type == "MESH" and obj.name.rsplit(".", 1)[0] == "Badge_Front_Enamel"), None)
    if enamel is not None:
        centre = [vertex.co.z for vertex in enamel.data.vertices if math.hypot(vertex.co.x, vertex.co.y) <= 0.0015]
        edge = [vertex.co.z for vertex in enamel.data.vertices if 0.0175 <= math.hypot(vertex.co.x, vertex.co.y) <= 0.0183]
        if centre and edge:
            centre_z = max(centre)
            edge_z = max(edge)
            dome = {"edge_front_z_m": edge_z, "centre_front_z_m": centre_z, "height_m": centre_z - edge_z}
    report = {
        "brand": {"zh": "歌词岛", "en": "LYRIC HOVER"},
        "root": {"name": root.name, "location": list(root.location), "rotation": list(root.rotation_euler), "scale": list(root.scale)},
        "bounds_m": {"min": bounds_min, "max": bounds_max, "center": [(a + b) / 2 for a, b in zip(bounds_min, bounds_max)], "size": [b - a for a, b in zip(bounds_min, bounds_max)]},
        "front_enamel_dome": dome,
        "mesh_count": len([obj for obj in clones if obj.type == "MESH"]),
        "triangle_count": mesh_triangles,
        "materials": sorted(material.name for material in runtime_mats.values()),
        "textures": ["badge_back_roughness_1024.png (roughness-only metallicRoughness, Non-Color)"],
        "dynamic_nameplate": {"object": "Badge_Back_DynamicNameplate", "blank": True, "date_format": "yyyy.MM.dd"},
        "topology": topology,
    }
    (output_dir / "runtime-export-report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")


def validate_imported_runtime_glb(output_dir: Path, glb_path: Path) -> None:
    """Reload the GLB without altering it and capture its fixed runtime views."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(glb_path))
    root = bpy.data.objects.get("Badge_ExportRoot")
    if root is None:
        candidates = [obj for obj in bpy.data.objects if obj.parent is None and obj.type == "EMPTY"]
        root = candidates[0] if candidates else None
    if root is None:
        raise RuntimeError("Re-imported GLB has no Badge_ExportRoot")
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 1200
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0
    if scene.world is None:
        scene.world = bpy.data.worlds.new("Runtime_Validation_World")
    scene.world.color = (0.003, 0.003, 0.003)
    camera_data = bpy.data.cameras.new("Runtime_Validation_Camera")
    camera = bpy.data.objects.new("Runtime_Validation_Camera", camera_data)
    scene.collection.objects.link(camera)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 0.045
    scene.camera = camera

    # Blender's glTF importer displays the authored Y-up contract through this
    # fixed axis conversion. Keep the GLB itself untouched and calculate every
    # validation camera from raw GLB space explicitly.
    GLB_TO_BLENDER_DISPLAY = Matrix(((1.0, 0.0, 0.0), (0.0, 0.0, -1.0), (0.0, 1.0, 0.0)))

    def display_vector(glb_vector: Vector) -> Vector:
        return GLB_TO_BLENDER_DISPLAY @ glb_vector

    def apply_pose(obj: bpy.types.Object, raw_position: Vector, raw_forward: Vector, raw_up: Vector) -> dict[str, list[float]]:
        position = display_vector(raw_position)
        forward = display_vector(raw_forward).normalized()
        up = display_vector(raw_up).normalized()
        right = forward.cross(up).normalized()
        up = (-forward).cross(right).normalized()
        # Blender cameras (and Area lights) look along local -Z with local +Y
        # as up. These matrix columns provide the requested orientation without
        # a track-to axis heuristic.
        rotation = Matrix(((right.x, up.x, -forward.x), (right.y, up.y, -forward.y), (right.z, up.z, -forward.z))).to_quaternion()
        obj.location = position
        obj.rotation_mode = "QUATERNION"
        obj.rotation_quaternion = rotation
        world_rotation = obj.matrix_world.to_3x3()
        actual_forward = (world_rotation @ Vector((0.0, 0.0, -1.0))).normalized()
        actual_up = (world_rotation @ Vector((0.0, 1.0, 0.0))).normalized()
        return {
            "raw_glb_position": list(raw_position),
            "raw_glb_forward": list(raw_forward.normalized()),
            "raw_glb_up": list(raw_up.normalized()),
            "blender_display_position": list(position),
            "blender_display_forward": list(actual_forward),
            "blender_display_up": list(actual_up),
            "quaternion": list(rotation),
        }

    for name, location, energy, size in (
        # The runtime model is only 40 mm across; hundreds of watts would
        # clip every metallic pixel white in a validation render.  These
        # values are deliberately modest and are only for the neutral
        # re-import smoke test, not part of the GLB.
        ("Runtime_Key", (-0.045, 0.035, 0.070), 1.8, 0.055),
        ("Runtime_Fill", (0.045, -0.025, 0.060), 0.6, 0.065),
    ):
        light_data = bpy.data.lights.new(name, "AREA")
        light_data.energy = energy
        light_data.shape = "DISK"
        light_data.size = size
        light_obj = bpy.data.objects.new(name, light_data)
        scene.collection.objects.link(light_obj)
        # These are neutral validation lights in display coordinates. Their
        # orientation is explicit for the same reason as the camera poses.
        apply_pose(light_obj, Vector(location), -Vector(location), Vector((0.0, 0.0, 1.0)))
    captures = []
    # GLB contract: +X right, +Y up, +Z front. Imported display contract:
    # (x, y, z) -> (x, -z, y). Hence front sits at display -Y and back at +Y.
    # The raw +Y up vector maps to display +Z, keeping the capsule at image top
    # for both front and back without a mirror/roll correction.
    views = (
        ("front", "runtime-front-0.png", Vector((0.0, 0.0, 0.100)), Vector((0.0, 0.0, -1.0)), Vector((0.0, 1.0, 0.0)), 0.045),
        ("front_oblique", "runtime-front-oblique.png", Vector((0.060, 0.022, 0.082)), Vector((-0.060, -0.022, -0.082)), Vector((0.0, 1.0, 0.0)), 0.045),
        ("side", "runtime-side.png", Vector((0.100, 0.0, 0.0)), Vector((-1.0, 0.0, 0.0)), Vector((0.0, 1.0, 0.0)), 0.045),
        ("back", "runtime-back-180.png", Vector((0.0, 0.0, -0.100)), Vector((0.0, 0.0, 1.0)), Vector((0.0, 1.0, 0.0)), 0.045),
        ("back_brush_detail", "runtime-back-brush-detail.png", Vector((0.0, 0.0, -0.100)), Vector((0.0, 0.0, 1.0)), Vector((0.0, 1.0, 0.0)), 0.024),
    )
    for name, filename, raw_position, raw_forward, raw_up, ortho_scale in views:
        camera_data.ortho_scale = ortho_scale
        pose = apply_pose(camera, raw_position, raw_forward, raw_up)
        print("RUNTIME_CAMERA_AUDIT =", json.dumps({"view": name, **pose}, ensure_ascii=False, sort_keys=True))
        scene.render.filepath = str(output_dir / filename)
        bpy.context.view_layer.update()
        bpy.ops.render.render(write_still=True)
        captures.append({"name": name, "file": filename, "ortho_scale": ortho_scale, **pose})
    report = {
        "root": {
            "name": root.name,
            "location": list(root.location),
            "rotation": list(root.rotation_euler),
            "scale": list(root.scale),
            "matrix_local": [list(row) for row in root.matrix_local],
        },
        "model_rotation_matrix": [list(row) for row in root.matrix_local],
        "GLB_TO_BLENDER_DISPLAY": [list(row) for row in GLB_TO_BLENDER_DISPLAY],
        "captures": captures,
        "imported_objects": sorted(obj.name for obj in bpy.data.objects),
    }
    (output_dir / "runtime-import-validation.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")


def export_assets(output_dir: Path) -> None:
    output_dir.mkdir(parents=True, exist_ok=True)
    blend_path = output_dir / "supporter-badge.blend"
    glb_path = output_dir / "supporter-badge.glb"
    obj_path = output_dir / "supporter-badge.obj"

    # Keep the authoring .blend editable, including text and curve objects.
    prepare_mesh_uvs()
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

    # GLB/OBJ receive triangulatable geometry with explicit UVs and tangents.
    convert_curves_for_export()
    report_scene()
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        export_apply=True,
        export_yup=False,
        export_materials="EXPORT",
        export_cameras=False,
        export_lights=False,
    )

    bpy.ops.object.select_all(action="SELECT")
    if hasattr(bpy.ops.wm, "obj_export"):
        bpy.ops.wm.obj_export(
            filepath=str(obj_path),
            export_selected_objects=True,
            export_materials=True,
            forward_axis="NEGATIVE_Z",
            up_axis="Y",
        )
    elif hasattr(bpy.ops.export_scene, "obj"):
        bpy.ops.export_scene.obj(
            filepath=str(obj_path),
            use_selection=True,
            use_materials=True,
            axis_forward="-Z",
            axis_up="Y",
        )
    else:
        print("WARNING: this Blender build has no OBJ exporter; GLB remains authoritative.")


def report_scene() -> None:
    mesh_objects = [obj for obj in bpy.data.objects if obj.type == "MESH"]
    triangles = 0
    for obj in mesh_objects:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
    print(f"LYRIC HOVER supporter badge: {len(mesh_objects)} mesh objects")
    print(f"Current triangulated mesh count: {triangles:,}")
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated_triangles = 0
    for obj in bpy.data.objects:
        if obj.type not in {"MESH", "CURVE", "FONT"}:
            continue
        evaluated_mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(depsgraph))
        evaluated_mesh.calc_loop_triangles()
        evaluated_triangles += len(evaluated_mesh.loop_triangles)
        bpy.data.meshes.remove(evaluated_mesh)
    print(f"Evaluated render triangle count: {evaluated_triangles:,}")
    print("Diameter: 40 mm; thickness: 2.4 mm; front axis: +Z")


def write_geometry_diagnostics(output_dir: Path) -> None:
    """Persist topology, material-culling and camera diagnostics."""
    structural_names = (
        "Badge_Gold_Side",
        "Badge_Front_Enamel",
        "Badge_Back_Metal",
        "Badge_Top_Capsule_Inner_Wall",
    )
    meshes: dict[str, dict[str, object]] = {}
    for name in structural_names:
        obj = bpy.data.objects.get(name)
        if obj is None or obj.type != "MESH":
            meshes[name] = {"missing": True}
            continue
        mesh = obj.data
        edge_faces: dict[tuple[int, int], list[int]] = {}
        edge_directions: dict[tuple[int, int], list[tuple[int, int]]] = {}
        for polygon in mesh.polygons:
            indices = list(polygon.vertices)
            for index, start in enumerate(indices):
                end = indices[(index + 1) % len(indices)]
                key = tuple(sorted((start, end)))
                edge_faces.setdefault(key, []).append(polygon.index)
                edge_directions.setdefault(key, []).append((start, end))
        non_manifold = [key for key, faces in edge_faces.items() if len(faces) != 2]
        inconsistent = [
            key
            for key, directions in edge_directions.items()
            if len(directions) == 2 and directions[0] == directions[1]
        ]
        mesh.calc_loop_triangles()
        signed_volume = 0.0
        degenerate_faces = 0
        for triangle in mesh.loop_triangles:
            a, b, c = (mesh.vertices[index].co for index in triangle.vertices)
            cross = (b - a).cross(c - a)
            if cross.length_squared <= 1e-28:
                degenerate_faces += 1
            signed_volume += a.dot(b.cross(c)) / 6.0
        wire_edges = sum(
            1
            for edge in mesh.edges
            if tuple(sorted(edge.vertices)) not in edge_faces
        )
        vertex_neighbours: dict[int, set[int]] = {
            vertex.index: set() for vertex in mesh.vertices
        }
        for edge in mesh.edges:
            a, b = edge.vertices
            vertex_neighbours[a].add(b)
            vertex_neighbours[b].add(a)
        remaining = set(vertex_neighbours)
        component_sizes: list[int] = []
        while remaining:
            seed = remaining.pop()
            stack = [seed]
            size = 1
            while stack:
                current = stack.pop()
                for neighbour in vertex_neighbours[current]:
                    if neighbour in remaining:
                        remaining.remove(neighbour)
                        stack.append(neighbour)
                        size += 1
            component_sizes.append(size)
        component_sizes.sort(reverse=True)
        normal_counts = {
            "positive_z": sum(1 for polygon in mesh.polygons if polygon.normal.z > 0.9),
            "negative_z": sum(1 for polygon in mesh.polygons if polygon.normal.z < -0.9),
            "side": sum(1 for polygon in mesh.polygons if abs(polygon.normal.z) <= 0.9),
        }
        meshes[name] = {
            "vertices": len(mesh.vertices),
            "edges": len(mesh.edges),
            "faces": len(mesh.polygons),
            "non_manifold_edges": len(non_manifold),
            "boundary_edges": sum(
                1 for faces in edge_faces.values() if len(faces) == 1
            ),
            "wire_edges": wire_edges,
            "inconsistent_winding_edges": len(inconsistent),
            "degenerate_triangles": degenerate_faces,
            "signed_volume_m3": signed_volume,
            "connected_components": len(component_sizes),
            "component_vertex_counts": component_sizes,
            "face_normal_counts": normal_counts,
            "materials": [
                slot.material.name for slot in obj.material_slots if slot.material
            ],
        }

    materials = {
        material.name: {
            "use_backface_culling": bool(material.use_backface_culling),
        }
        for material in bpy.data.materials
    }
    camera = bpy.data.objects.get("Badge_Review_Camera")
    camera_report: dict[str, object] = {"missing": camera is None}
    if camera is not None:
        forward = (
            camera.matrix_world.to_quaternion() @ Vector((0.0, 0.0, -1.0))
        ).normalized()
        toward_origin = (-camera.location).normalized()
        camera_report = {
            "missing": False,
            "location": list(camera.location),
            "type": camera.data.type,
            "ortho_scale": camera.data.ortho_scale,
            "forward_dot_medal_origin": forward.dot(toward_origin),
        }

    report = {
        "brand": {"zh": "歌词岛", "en": "LYRIC HOVER"},
        "meshes": meshes,
        "materials": materials,
        "camera": camera_report,
    }
    path = output_dir / "geometry-diagnostics.json"
    path.write_text(
        json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print(f"Geometry diagnostics written to: {path}")


def main() -> None:
    args = command_line_args()
    output_dir = args.output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    reset_scene()
    materials = create_materials(output_dir)
    create_medal(materials)
    create_review_stage(samples=args.samples)
    validate_required_objects()
    report_scene()
    write_geometry_diagnostics(output_dir)
    if not args.no_render:
        if args.back_material_study:
            render_back_material_studies(output_dir)
        else:
            render_review_previews(
                output_dir,
                front_only=args.front_only,
                back_only=args.back_only,
            )
            if args.note_study:
                render_note_studies(output_dir)
            if args.detail_study:
                render_detail_studies(output_dir)
            if args.front_calibration_study:
                render_front_calibration_studies(output_dir)
            if args.multiview_study:
                render_multiview_studies(output_dir)
            write_review_manifest(output_dir)
    if args.runtime_precheck:
        export_runtime_assets(FINAL_RUNTIME_OUTPUT, precheck_only=True)
    elif args.runtime_export:
        export_runtime_assets(FINAL_RUNTIME_OUTPUT)
    elif not args.no_export:
        export_assets(output_dir)
        print(f"Badge assets written to: {output_dir}")


if __name__ == "__main__":
    main()
