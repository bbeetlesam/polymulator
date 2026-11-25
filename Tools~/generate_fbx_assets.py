"""Generates custom sculpted hard-surface & architectural Binary FBX 7.4 assets for POLYMULATOR.

Outputs standard Blender-compatible & Unity-compatible Binary FBX 7.4 files into
polymulator-src/Assets/Models/FBX/.
"""

from __future__ import annotations

import math
import os
from typing import List, Sequence, Tuple

from fbx_writer import MeshPart, export_fbx

OUT_DIR = os.path.abspath(
    os.path.join(
        os.path.dirname(__file__),
        "..",
        "Assets",
        "Models",
        "FBX",
    )
)

DEFAULT_MATS = [
    ("Primary", (0.72, 0.75, 0.85)),
    ("Secondary", (0.27, 0.28, 0.35)),
    ("TrimMetal", (0.42, 0.44, 0.54)),
    ("Accent", (0.45, 0.78, 0.92)),
]


def _ear_clip_2d(poly: Sequence[Tuple[float, float]]) -> List[Tuple[int, int, int]]:
    """Triangulates any simple (including concave) 2D XZ polygon cleanly."""
    idxs = list(range(len(poly)))
    if len(idxs) < 3:
        return []
    # Compute signed area to determine winding
    area = sum(
        poly[i][0] * poly[(i + 1) % len(poly)][1] - poly[(i + 1) % len(poly)][0] * poly[i][1]
        for i in range(len(poly))
    )
    if area < 0:
        idxs.reverse()

    def cross(a: Tuple[float, float], b: Tuple[float, float], c: Tuple[float, float]) -> float:
        return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])

    def in_tri(p: Tuple[float, float], a: Tuple[float, float], b: Tuple[float, float], c: Tuple[float, float]) -> bool:
        return cross(a, b, p) >= -1e-6 and cross(b, c, p) >= -1e-6 and cross(c, a, p) >= -1e-6

    tris: List[Tuple[int, int, int]] = []
    guard = 0
    while len(idxs) > 3 and guard < 200:
        guard += 1
        ear_found = False
        m = len(idxs)
        for i in range(m):
            i_prev, i_curr, i_next = idxs[(i - 1) % m], idxs[i], idxs[(i + 1) % m]
            a, b, c = poly[i_prev], poly[i_curr], poly[i_next]
            if cross(a, b, c) <= 1e-6:
                continue
            if any(in_tri(poly[other], a, b, c) for other in idxs if other not in (i_prev, i_curr, i_next)):
                continue
            tris.append((i_prev, i_curr, i_next))
            del idxs[i]
            ear_found = True
            break
        if not ear_found:
            break
    if len(idxs) == 3:
        tris.append((idxs[0], idxs[1], idxs[2]))
    return tris


def add_curved_loft(
    part: MeshPart,
    profile_xz: Sequence[Tuple[float, float]],
    y_bot: float,
    y_top: float,
    mat_slot: int = 0,
) -> None:
    """Lofts any simple 2D XZ polygon cleanly from y_bot to y_top with triangulated concave caps."""
    cx = sum(p[0] for p in profile_xz) / len(profile_xz)
    cz = sum(p[1] for p in profile_xz) / len(profile_xz)
    bot = [(x, y_bot, z) for x, z in profile_xz]
    top = [(x, y_top, z) for x, z in profile_xz]

    for i0, i1, i2 in _ear_clip_2d(profile_xz):
        part.add_face([top[i0], top[i1], top[i2]], mat_slot, outward_hint=(0.0, 1.0, 0.0))
        part.add_face([bot[i0], bot[i2], bot[i1]], mat_slot, outward_hint=(0.0, -1.0, 0.0))

    n = len(profile_xz)
    for i in range(n):
        j = (i + 1) % n
        mx = 0.5 * (profile_xz[i][0] + profile_xz[j][0]) - cx
        mz = 0.5 * (profile_xz[i][1] + profile_xz[j][1]) - cz
        part.add_face([bot[i], top[i], top[j], bot[j]], mat_slot, outward_hint=(mx, 0.0, mz))


def add_angled_strut(
    part: MeshPart,
    p0: Tuple[float, float, float],
    p1: Tuple[float, float, float],
    half_w: float,
    half_d: float,
    mat_slot: int = 0,
) -> None:
    """Creates an oriented rectangular beam connecting p0 to p1."""
    dx, dy, dz = p1[0] - p0[0], p1[1] - p0[1], p1[2] - p0[2]
    length = math.sqrt(dx * dx + dy * dy + dz * dz)
    if length < 1e-5:
        return
    ux, uy, uz = dx / length, dy / length, dz / length
    # Choose perpendicular basis
    ref = (1.0, 0.0, 0.0) if abs(uy) > 0.9 else (0.0, 1.0, 0.0)
    rx = uy * ref[2] - uz * ref[1]
    ry = uz * ref[0] - ux * ref[2]
    rz = ux * ref[1] - uy * ref[0]
    rmag = math.sqrt(rx * rx + ry * ry + rz * rz)
    rx, ry, rz = rx / rmag, ry / rmag, rz / rmag
    fx = uy * rz - uz * ry
    fy = uz * rx - ux * rz
    fz = ux * ry - uy * rx

    def pt(base, sx, sz):
        return (base[0] + rx * sx + fx * sz, base[1] + ry * sx + fy * sz, base[2] + rz * sx + fz * sz)

    b0 = [pt(p0, -half_w, -half_d), pt(p0, half_w, -half_d), pt(p0, half_w, half_d), pt(p0, -half_w, half_d)]
    b1 = [pt(p1, -half_w, -half_d), pt(p1, half_w, -half_d), pt(p1, half_w, half_d), pt(p1, -half_w, half_d)]
    mid = ((p0[0] + p1[0]) * 0.5, (p0[1] + p1[1]) * 0.5, (p0[2] + p1[2]) * 0.5)
    for k in range(4):
        kn = (k + 1) % 4
        part._emit_oriented_face([b0[k], b1[k], b1[kn], b0[kn]], mid, mat_slot)
    part._emit_oriented_face(b0, mid, mat_slot)
    part._emit_oriented_face(b1, mid, mat_slot)


def build_rect_character() -> List[MeshPart]:
    """Articulated studio-grade mascot rig parts matching RectVisualRig hierarchy."""
    # 1. Hover turbine base skirt (chassis dark)
    hover_base = MeshPart("Rect_HoverBase", material_name="Secondary")
    hover_base.add_prism_y(0.0, 0.08, 0.0, 0.39, 0.33, 0.11, segments=16, rot_y_deg=11.25)
    hover_base.add_ring_y(0.0, 0.145, 0.0, 0.33, 0.24, 0.03, segments=16, rot_y_deg=11.25)
    for ang_deg in (45.0, 135.0, 225.0, 315.0):
        rad = math.radians(ang_deg)
        hover_base.add_box(math.cos(rad) * 0.34, 0.085, math.sin(rad) * 0.34, 0.08, 0.08, 0.08, bevel=0.015)

    # 2. Hover glow turbine ring (Accent material slot for mode switch!)
    hover_glow = MeshPart("Rect_HoverGlowRing", material_name="Accent")
    hover_glow.add_ring_y(0.0, 0.025, 0.0, 0.35, 0.25, 0.028, segments=16, rot_y_deg=11.25)

    # 3. Torso sculpted armor chassis + chamfered pauldrons + thruster pack
    torso = MeshPart("Rect_Torso", material_name="Primary")
    torso.add_tapered_frustum(0.0, 0.37, 0.0, 0.66, 0.54, 0.56, 0.46, 0.40, bevel_x=0.075)
    torso.add_tapered_frustum(0.0, 0.58, 0.0, 0.54, 0.44, 0.34, 0.30, 0.06, bevel_x=0.05)  # Neck collar bevel
    # Pauldron wing shoulders
    torso.add_tapered_frustum(-0.34, 0.42, 0.0, 0.12, 0.36, 0.08, 0.28, 0.26, bevel_x=0.02)
    torso.add_tapered_frustum(0.34, 0.42, 0.0, 0.12, 0.36, 0.08, 0.28, 0.26, bevel_x=0.02)

    torso_trim = MeshPart("Rect_TorsoDarkTrim", material_name="Secondary")
    torso_trim.add_box(0.0, 0.39, 0.24, 0.26, 0.22, 0.05, bevel=0.015)  # Front chest cavity plate
    torso_trim.add_tapered_frustum(0.0, 0.39, -0.25, 0.34, 0.12, 0.28, 0.09, 0.30, bevel_x=0.03)  # Backpack
    torso_trim.add_prism_y(-0.09, 0.26, -0.28, 0.055, 0.04, 0.08, segments=10)
    torso_trim.add_prism_y(0.09, 0.26, -0.28, 0.055, 0.04, 0.08, segments=10)

    chest_core = MeshPart("Rect_ChestCore", material_name="Accent")
    # Forward-facing diamond emblem bevel protruding cleanly from torso chest front (+Z)
    cx, cy, cz = 0.0, 0.39, 0.275
    d_r, d_z = 0.095, 0.045
    front_tip = (cx, cy, cz + d_z)
    rim = [(cx, cy + d_r, cz), (cx + d_r, cy, cz), (cx, cy - d_r, cz), (cx - d_r, cy, cz)]
    for i in range(4):
        j = (i + 1) % 4
        chest_core.add_face([rim[i], rim[j], front_tip], outward_hint=(0.0, 0.0, 1.0))
    chest_core.add_face(rim, outward_hint=(0.0, 0.0, -1.0))

    back_fin = MeshPart("Rect_ModeFin", material_name="Accent")
    back_fin.add_tapered_frustum(0.0, 0.42, -0.32, 0.06, 0.08, 0.03, 0.04, 0.26, bevel_x=0.01)

    # 4. Head parts (centered cleanly around local HeadPivot origin (0, 0, 0))
    head_cowl = MeshPart("Rect_HeadCowl", material_name="Primary")
    head_cowl.add_tapered_frustum(0.0, 0.0, 0.0, 0.56, 0.48, 0.48, 0.42, 0.34, bevel_x=0.075)
    head_cowl.add_box(0.0, 0.175, -0.02, 0.38, 0.045, 0.32, bevel=0.015)  # Crown crest
    head_cowl.add_prism_y(-0.295, 0.0, 0.0, 0.09, 0.07, 0.14, segments=8)  # Ear sensor L
    head_cowl.add_prism_y(0.295, 0.0, 0.0, 0.09, 0.07, 0.14, segments=8)  # Ear sensor R

    visor_mask = MeshPart("Rect_VisorRecess", material_name="Secondary")
    visor_mask.add_tapered_frustum(0.0, 0.005, 0.215, 0.46, 0.08, 0.42, 0.06, 0.17, bevel_x=0.025)
    visor_mask.add_prism_y(0.18, 0.24, -0.06, 0.024, 0.014, 0.17, segments=8)  # Antenna mast

    optic_visor = MeshPart("Rect_OpticVisor", material_name="Accent")
    optic_visor.add_box(0.0, 0.005, 0.25, 0.36, 0.082, 0.045, bevel=0.014)

    antenna_beacon = MeshPart("Rect_AntennaBeacon", material_name="Accent")
    # Faceted diamond crystal beacon
    antenna_beacon.add_prism_y(0.18, 0.37, -0.06, 0.055, 0.012, 0.06, segments=6)
    antenna_beacon.add_prism_y(0.18, 0.315, -0.06, 0.012, 0.055, 0.055, segments=6)

    # 5. Articulated modular arm (centered around shoulder pivot at (0, 0, 0), hanging down -Y)
    arm_chassis = MeshPart("Rect_ArmChassis", material_name="Primary")
    # Upper arm housing
    arm_chassis.add_tapered_frustum(0.0, -0.11, 0.0, 0.135, 0.135, 0.115, 0.115, 0.20, bevel_x=0.025)
    # Tapered forearm cuff
    arm_chassis.add_tapered_frustum(0.0, -0.34, 0.02, 0.145, 0.155, 0.125, 0.135, 0.21, bevel_x=0.028)

    arm_trim = MeshPart("Rect_ArmTrim", material_name="Secondary")
    # Shoulder ball joint & elbow actuator ring
    arm_trim.add_prism_y(0.0, -0.01, 0.0, 0.075, 0.075, 0.08, segments=10)
    arm_trim.add_prism_y(0.0, -0.225, 0.01, 0.068, 0.068, 0.065, segments=10)
    # Dual clamp fingers at palm end
    arm_trim.add_box(0.0, -0.475, 0.065, 0.095, 0.075, 0.045, bevel=0.012)
    arm_trim.add_box(0.0, -0.475, -0.015, 0.095, 0.075, 0.045, bevel=0.012)

    arm_glow = MeshPart("Rect_ArmGlow", material_name="Accent")
    # Glowing magnetic grip emitter pad at wrist/palm
    arm_glow.add_ring_y(0.0, -0.435, 0.025, 0.088, 0.056, 0.028, segments=12)

    return [
        hover_base, hover_glow, torso, torso_trim, chest_core, back_fin,
        head_cowl, visor_mask, optic_visor, antenna_beacon,
        arm_chassis, arm_trim, arm_glow,
    ]


def build_core_unit() -> List[MeshPart]:
    """Bespoke CoreUnit.fbx: exoskeleton frame + recessed faceted core + directional visor + gyro collar."""
    core_body = MeshPart("Core_InnerChamber", material_name="Primary")
    # Faceted octagonal core containment block (fits cleanly inside 0.92m cube envelope)
    core_body.add_box(0.0, 0.0, 0.0, 0.68, 0.68, 0.68, bevel=0.075)

    core_frame = MeshPart("Core_CageFrame", material_name="Secondary")
    # Corner exoskeleton brackets + gyro ring collar cleanly within 0.92m collider footprint
    for sx in (-1.0, 1.0):
        for sz in (-1.0, 1.0):
            core_frame.add_box(sx * 0.365, 0.0, sz * 0.365, 0.13, 0.88, 0.13, bevel=0.022)
    for sy in (-0.365, 0.365):
        core_frame.add_box(0.0, sy, 0.365, 0.72, 0.10, 0.10, bevel=0.02)
        core_frame.add_box(0.0, sy, -0.365, 0.72, 0.10, 0.10, bevel=0.02)
        core_frame.add_box(0.365, sy, 0.0, 0.10, 0.10, 0.72, bevel=0.02)
        core_frame.add_box(-0.365, sy, 0.0, 0.10, 0.10, 0.72, bevel=0.02)
    core_frame.add_ring_y(0.0, 0.0, 0.0, 0.485, 0.415, 0.08, segments=16, rot_y_deg=11.25)

    core_visor = MeshPart("Core_FrontVisor", material_name="Accent")
    # Arrow-sloped directional front visor on +Z face so rotation orientation reads instantly
    core_visor.add_tapered_frustum(0.0, 0.08, 0.425, 0.42, 0.075, 0.22, 0.055, 0.15, bevel_x=0.02)
    core_visor.add_box(0.0, 0.435, 0.12, 0.13, 0.045, 0.32, bevel=0.014)

    return [core_body, core_frame, core_visor]


def build_anchor_pedestal() -> List[MeshPart]:
    """Sculpted AnchorPedestal.fbx: octagonal tiered plinth, containment bowl, and 4 angled claws."""
    plinth = MeshPart("Anchor_Plinth", material_name="Secondary")
    plinth.add_prism_y(0.0, -0.58, 0.0, 0.86, 0.78, 0.24, segments=12, rot_y_deg=15.0)
    plinth.add_ring_y(0.0, -0.40, 0.0, 0.68, 0.44, 0.14, segments=12, rot_y_deg=15.0)

    claws = MeshPart("Anchor_Claws", material_name="TrimMetal")
    # 4 inward-angled containment claws framing the socket
    for ang_deg in (45.0, 135.0, 225.0, 315.0):
        rad = math.radians(ang_deg)
        ca, sa = math.cos(rad), math.sin(rad)
        p0 = (ca * 0.64, -0.44, sa * 0.64)
        p1 = (ca * 0.54, 0.22, sa * 0.54)
        add_angled_strut(claws, p0, p1, 0.09, 0.09)
        claws.add_box(ca * 0.50, 0.26, sa * 0.50, 0.18, 0.10, 0.18, bevel=0.02)

    ring = MeshPart("Anchor_Halo", material_name="Accent")
    ring.add_ring_y(0.0, -0.25, 0.0, 0.64, 0.57, 0.045, segments=16, rot_y_deg=11.25)

    return [plinth, claws, ring]


def build_grand_piano() -> List[MeshPart]:
    """Sculpted S-curved Grand Piano + lid + prop rod + keyboard + bench."""
    case = MeshPart("Piano_Case", material_name="Secondary")
    # Curved Grand Piano harp wing contour (looking top-down XZ, keyboard facing -Z)
    wing = [
        (-0.82, -0.55),
        (-0.82, 0.62),
        (-0.68, 0.92),
        (-0.32, 1.05),
        (0.06, 0.96),
        (0.32, 0.68),
        (0.56, 0.42),
        (0.76, 0.16),
        (0.84, -0.15),
        (0.84, -0.55),
    ]
    add_curved_loft(case, wing, 0.72, 1.08)
    # Keybed shelf protruding toward -Z
    case.add_box(0.0, 0.76, -0.68, 1.62, 0.12, 0.34, bevel=0.025)
    # Cheek blocks flanking keyboard
    case.add_box(-0.78, 0.88, -0.68, 0.10, 0.14, 0.34, bevel=0.015)
    case.add_box(0.78, 0.88, -0.68, 0.10, 0.14, 0.34, bevel=0.015)
    # 3 tapered legs + pedal lyre
    for lx, lz in ((-0.70, -0.56), (0.70, -0.56), (-0.18, 0.80)):
        case.add_prism_y(lx, 0.36, lz, 0.055, 0.085, 0.72, segments=8)
    case.add_box(0.0, 0.36, -0.58, 0.22, 0.62, 0.07, bevel=0.015)
    # Hinged open lid slightly tilted cleanly upward from bass spine (-0.78) toward treble (+0.80)
    lid_xz = [
        (-0.78, -0.40),
        (-0.78, 0.60),
        (-0.64, 0.88),
        (-0.30, 1.01),
        (0.06, 0.92),
        (0.32, 0.64),
        (0.56, 0.38),
        (0.76, 0.14),
        (0.80, -0.15),
        (0.80, -0.40),
    ]
    def lid_y(x_val: float) -> float:
        return 1.10 + (x_val + 0.78) * 0.18

    lid_bot = [(x, lid_y(x), z) for (x, z) in lid_xz]
    lid_top = [(x, y + 0.04, z) for (x, y, z) in lid_bot]
    for i0, i1, i2 in _ear_clip_2d(lid_xz):
        case.add_face([lid_top[i0], lid_top[i1], lid_top[i2]], outward_hint=(0.0, 1.0, 0.0))
        case.add_face([lid_bot[i0], lid_bot[i2], lid_bot[i1]], outward_hint=(0.0, -1.0, 0.0))
    for i in range(len(lid_bot)):
        j = (i + 1) % len(lid_bot)
        case.add_face([lid_bot[i], lid_top[i], lid_top[j], lid_bot[j]], outward_hint=(lid_bot[i][0], 0.0, lid_bot[i][2]))

    brass = MeshPart("Piano_Brass", material_name="TrimMetal")
    add_angled_strut(brass, (0.64, 1.07, -0.10), (0.72, 1.35, -0.10), 0.018, 0.018)  # Lid prop stick
    for px in (-0.06, 0.0, 0.06):
        brass.add_box(px, 0.05, -0.60, 0.03, 0.025, 0.10, bevel=0.005)  # Pedals

    keys = MeshPart("Piano_Keys", material_name="Primary")
    keys.add_box(0.0, 0.84, -0.71, 1.42, 0.04, 0.24, bevel=0.008)
    for k in range(-6, 7):
        if k in (-4, 0, 4):
            continue
        case.add_box(k * 0.095, 0.865, -0.66, 0.045, 0.03, 0.13, bevel=0.004)
    # Piano bench cushion + legs
    case.add_box(0.0, 0.48, -1.25, 0.96, 0.08, 0.42, bevel=0.02)
    for bx in (-0.40, 0.40):
        for bz in (-1.40, -1.10):
            case.add_box(bx, 0.23, bz, 0.06, 0.46, 0.06, bevel=0.01)

    return [case, brass, keys]


def build_pasar_stall() -> List[MeshPart]:
    """Archipelago street-market kiosk gazebo + lantern + crates for Chapter 1 (Translasi)."""
    frame = MeshPart("Pasar_Frame", material_name="Secondary")
    for px in (-1.10, 1.10):
        for pz in (-0.75, 0.75):
            frame.add_box(px, 1.05, pz, 0.12, 2.10, 0.12, bevel=0.02)
    # Counter front + side crate
    frame.add_box(0.0, 0.48, -0.66, 2.16, 0.88, 0.44, bevel=0.03)
    frame.add_box(-0.72, 0.34, 0.30, 0.62, 0.62, 0.62, bevel=0.03)
    frame.add_box(-0.68, 0.88, 0.32, 0.46, 0.46, 0.46, bevel=0.025)

    awning = MeshPart("Pasar_Awning", material_name="Accent")
    # Sloped corrugated ribbed awning canopy
    for r in range(7):
        x_strip = -1.20 + r * 0.40
        mat_slot = 0
        awning.add_tapered_frustum(x_strip, 2.22, -0.05, 0.36, 1.95, 0.36, 1.85, 0.10, bevel_x=0.02, mat_slot=mat_slot)

    lantern = MeshPart("Pasar_Lantern", material_name="Primary")
    lantern.add_prism_y(0.75, 1.25, -0.65, 0.11, 0.14, 0.26, segments=8)
    lantern.add_prism_y(-0.75, 1.90, -0.82, 0.09, 0.12, 0.24, segments=8)

    return [frame, awning, lantern]


def build_rusun_balcony() -> List[MeshPart]:
    """Apartment balcony planter box + guard rail + louvered condenser unit for Chapter 2 (Elevasi)."""
    shell = MeshPart("Rusun_Structure", material_name="Secondary")
    shell.add_box(0.0, 0.12, 0.0, 2.40, 0.24, 0.92, bevel=0.03)  # Concrete slab sill
    # Vertical handrail balusters + top bar
    shell.add_box(0.0, 1.04, -0.38, 2.38, 0.07, 0.08, bevel=0.015)
    for bx in (-1.10, -0.66, -0.22, 0.22, 0.66, 1.10):
        shell.add_box(bx, 0.60, -0.38, 0.055, 0.88, 0.055, bevel=0.01)

    condenser = MeshPart("Rusun_ACUnit", material_name="TrimMetal")
    condenser.add_box(0.74, 0.56, 0.08, 0.72, 0.56, 0.38, bevel=0.03)
    condenser.add_ring_y(0.66, 0.86, 0.08, 0.22, 0.10, 0.05, segments=12)

    foliage = MeshPart("Rusun_PlanterGreens", material_name="Accent")
    shell.add_box(-0.58, 0.42, 0.08, 1.04, 0.34, 0.44, bevel=0.025)
    for fx in (-0.88, -0.58, -0.28):
        foliage.add_prism_y(fx, 0.74, 0.08, 0.18, 0.09, 0.32, segments=6)

    return [shell, condenser, foliage]


def build_archive_rack() -> List[MeshPart]:
    """Tall archive tape/server cabinet + reading lectern for Chapter 4 & Showcase."""
    cabinet = MeshPart("Archive_Cabinet", material_name="Secondary")
    cabinet.add_box(0.0, 1.45, 0.0, 1.75, 2.90, 0.74, bevel=0.04)
    # Recessed side pillars + plinth
    cabinet.add_box(-0.88, 1.48, 0.0, 0.16, 2.96, 0.84, bevel=0.025)
    cabinet.add_box(0.88, 1.48, 0.0, 0.16, 2.96, 0.84, bevel=0.025)

    drawers = MeshPart("Archive_TapeModules", material_name="TrimMetal")
    for row in range(5):
        y = 0.55 + row * 0.46
        drawers.add_box(-0.38, y, -0.35, 0.66, 0.30, 0.10, bevel=0.015)
        drawers.add_box(0.38, y, -0.35, 0.66, 0.30, 0.10, bevel=0.015)

    glow_strips = MeshPart("Archive_IndicatorNodes", material_name="Accent")
    for row in range(5):
        y = 0.55 + row * 0.46
        glow_strips.add_box(-0.62, y, -0.405, 0.08, 0.12, 0.02, bevel=0.005)
        glow_strips.add_box(0.62, y, -0.405, 0.08, 0.12, 0.02, bevel=0.005)

    return [cabinet, drawers, glow_strips]


def build_flood_turbine() -> List[MeshPart]:
    """Heavy coastal sluice pylon & turbine rotor for Chapter 5 (Badai)."""
    pylon = MeshPart("Storm_Pylon", material_name="Secondary")
    pylon.add_tapered_frustum(0.0, 0.90, 0.0, 1.50, 1.30, 1.05, 0.95, 1.80, bevel_x=0.10)
    # Reinforced coastal buttress fins
    pylon.add_tapered_frustum(-0.78, 0.48, 0.0, 0.34, 0.98, 0.14, 0.62, 0.96, bevel_x=0.03)
    pylon.add_tapered_frustum(0.78, 0.48, 0.0, 0.34, 0.98, 0.14, 0.62, 0.96, bevel_x=0.03)

    rotor = MeshPart("Storm_TurbineShroud", material_name="TrimMetal")
    rotor.add_ring_y(0.0, 1.98, 0.0, 0.52, 0.34, 0.32, segments=12, rot_y_deg=15.0)
    rotor.add_prism_y(0.0, 2.02, 0.0, 0.16, 0.10, 0.40, segments=8)

    beacon = MeshPart("Storm_WarningCap", material_name="Accent")
    beacon.add_prism_y(0.0, 2.34, 0.0, 0.18, 0.12, 0.18, segments=8)
    beacon.add_box(0.0, 1.25, -0.56, 0.68, 0.10, 0.08, bevel=0.015)

    return [pylon, rotor, beacon]


def main() -> None:
    os.makedirs(OUT_DIR, exist_ok=True)
    catalog = [
        ("RectCharacter.fbx", build_rect_character()),
        ("CoreUnit.fbx", build_core_unit()),
        ("AnchorPedestal.fbx", build_anchor_pedestal()),
        ("GrandPiano.fbx", build_grand_piano()),
        ("PasarStall.fbx", build_pasar_stall()),
        ("RusunBalconyUnit.fbx", build_rusun_balcony()),
        ("ArchiveMonolithRack.fbx", build_archive_rack()),
        ("FloodTurbinePylon.fbx", build_flood_turbine()),
    ]
    for filename, parts in catalog:
        out_path = os.path.join(OUT_DIR, filename)
        export_fbx(out_path, parts, DEFAULT_MATS)
        total_v = sum(len(p.vertices) for p in parts)
        total_f = sum(len(p.polygons) for p in parts)
        print(f"Exported {filename:24s} -> {len(parts):2d} submeshes, {total_v:4d} verts, {total_f:4d} faces ({os.path.getsize(out_path):,} bytes)")


if __name__ == "__main__":
    main()
