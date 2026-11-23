"""Clean Autodesk ASCII FBX 7.4.0 exporter & hard-surface geometry kit.

Produces valid `.fbx` files parsed cleanly by Unity's built-in FBXImporter
with hierarchical named sub-meshes, clean outward vertex normals, UVs, and materials.
"""

from __future__ import annotations

import math
import os
from dataclasses import dataclass, field
from typing import Dict, List, Optional, Sequence, Tuple


@dataclass
class MeshPart:
    """A single mesh object in FBX space (meters, Y-up)."""

    name: str
    parent_name: Optional[str] = None
    translation: Tuple[float, float, float] = (0.0, 0.0, 0.0)
    rotation_deg: Tuple[float, float, float] = (0.0, 0.0, 0.0)
    scale: Tuple[float, float, float] = (1.0, 1.0, 1.0)
    material_name: str = "Primary"
    vertices: List[Tuple[float, float, float]] = field(default_factory=list)
    polygons: List[Tuple[int, ...]] = field(default_factory=list)
    poly_materials: List[int] = field(default_factory=list)
    uvs: List[Tuple[float, float]] = field(default_factory=list)

    def add_face(
        self,
        verts: Sequence[Tuple[float, float, float]],
        mat_slot: int = 0,
        uv_coords: Optional[Sequence[Tuple[float, float]]] = None,
        outward_hint: Optional[Tuple[float, float, float]] = None,
    ) -> None:
        ordered = list(verts)
        if outward_hint is not None and len(ordered) >= 3:
            nx, ny, nz = _compute_face_normal(ordered)
            if nx * outward_hint[0] + ny * outward_hint[1] + nz * outward_hint[2] < 0.0:
                ordered.reverse()
        base = len(self.vertices)
        self.vertices.extend(ordered)
        self.polygons.append(tuple(base + i for i in range(len(ordered))))
        self.poly_materials.append(mat_slot)
        if uv_coords and len(uv_coords) == len(ordered):
            self.uvs.extend(uv_coords)
        else:
            for i, _ in enumerate(ordered):
                self.uvs.append(((1.0 if i in (1, 2) else 0.0), (1.0 if i in (2, 3) else 0.0)))

    def _emit_oriented_face(
        self,
        verts: Sequence[Tuple[float, float, float]],
        center: Tuple[float, float, float],
        mat_slot: int = 0,
    ) -> None:
        fcx = sum(v[0] for v in verts) / len(verts)
        fcy = sum(v[1] for v in verts) / len(verts)
        fcz = sum(v[2] for v in verts) / len(verts)
        self.add_face(verts, mat_slot, outward_hint=(fcx - center[0], fcy - center[1], fcz - center[2]))

    def add_box(
        self,
        cx: float,
        cy: float,
        cz: float,
        sx: float,
        sy: float,
        sz: float,
        bevel: float = 0.0,
        mat_slot: int = 0,
    ) -> None:
        center = (cx, cy, cz)
        hx, hy, hz = sx * 0.5, sy * 0.5, sz * 0.5
        b = min(bevel, hx * 0.45, hy * 0.45, hz * 0.45)
        if b <= 1e-5:
            p = [
                (cx - hx, cy - hy, cz - hz),
                (cx + hx, cy - hy, cz - hz),
                (cx + hx, cy + hy, cz - hz),
                (cx - hx, cy + hy, cz - hz),
                (cx - hx, cy - hy, cz + hz),
                (cx + hx, cy - hy, cz + hz),
                (cx + hx, cy + hy, cz + hz),
                (cx - hx, cy + hy, cz + hz),
            ]
            faces = [
                (p[3], p[7], p[6], p[2]),
                (p[0], p[1], p[5], p[4]),
                (p[4], p[5], p[6], p[7]),
                (p[1], p[0], p[3], p[2]),
                (p[5], p[1], p[2], p[6]),
                (p[0], p[4], p[7], p[3]),
            ]
            for f in faces:
                self._emit_oriented_face(f, center, mat_slot)
            return

        ix, iy, iz = hx - b, hy - b, hz - b

        def v(x: float, y: float, z: float) -> Tuple[float, float, float]:
            return (cx + x, cy + y, cz + z)

        raw_faces = [
            [v(-ix, hy, -iz), v(-ix, hy, iz), v(ix, hy, iz), v(ix, hy, -iz)],
            [v(-ix, -hy, iz), v(-ix, -hy, -iz), v(ix, -hy, -iz), v(ix, -hy, iz)],
            [v(-ix, -iy, hz), v(ix, -iy, hz), v(ix, iy, hz), v(-ix, iy, hz)],
            [v(ix, -iy, -hz), v(-ix, -iy, -hz), v(-ix, iy, -hz), v(ix, iy, -hz)],
            [v(hx, -iy, iz), v(hx, -iy, -iz), v(hx, iy, -iz), v(hx, iy, iz)],
            [v(-hx, -iy, -iz), v(-hx, -iy, iz), v(-hx, iy, iz), v(-hx, iy, -iz)],
            [v(-ix, hy, iz), v(-ix, iy, hz), v(ix, iy, hz), v(ix, hy, iz)],
            [v(ix, hy, -iz), v(ix, iy, -hz), v(-ix, iy, -hz), v(-ix, hy, -iz)],
            [v(ix, hy, iz), v(hx, iy, iz), v(hx, iy, -iz), v(ix, hy, -iz)],
            [v(-ix, hy, -iz), v(-hx, iy, -iz), v(-hx, iy, iz), v(-ix, hy, iz)],
            [v(ix, -hy, iz), v(ix, -iy, hz), v(-ix, -iy, hz), v(-ix, -hy, iz)],
            [v(-ix, -hy, -iz), v(-ix, -iy, -hz), v(ix, -iy, -hz), v(ix, -hy, -iz)],
            [v(ix, -hy, -iz), v(hx, -iy, -iz), v(hx, -iy, iz), v(ix, -hy, iz)],
            [v(-ix, -hy, iz), v(-hx, -iy, iz), v(-hx, -iy, -iz), v(-ix, -hy, -iz)],
            [v(ix, -iy, hz), v(hx, -iy, iz), v(hx, iy, iz), v(ix, iy, hz)],
            [v(-hx, -iy, iz), v(-ix, -iy, hz), v(-ix, iy, hz), v(-hx, iy, iz)],
            [v(-ix, -iy, -hz), v(-hx, -iy, -iz), v(-hx, iy, -iz), v(-ix, iy, -hz)],
            [v(hx, -iy, -iz), v(ix, -iy, -hz), v(ix, iy, -hz), v(hx, iy, -iz)],
            [v(ix, hy, iz), v(ix, iy, hz), v(hx, iy, iz)],
            [v(-ix, hy, iz), v(-hx, iy, iz), v(-ix, iy, hz)],
            [v(ix, hy, -iz), v(hx, iy, -iz), v(ix, iy, -hz)],
            [v(-ix, hy, -iz), v(-ix, iy, -hz), v(-hx, iy, -iz)],
            [v(ix, -hy, iz), v(hx, -iy, iz), v(ix, -iy, hz)],
            [v(-ix, -hy, iz), v(-ix, -iy, hz), v(-hx, -iy, iz)],
            [v(ix, -hy, -iz), v(ix, -iy, -hz), v(hx, -iy, -iz)],
            [v(-ix, -hy, -iz), v(-hx, -iy, -iz), v(-ix, -iy, -hz)],
        ]
        for rf in raw_faces:
            self._emit_oriented_face(rf, center, mat_slot)

    def add_prism_y(
        self,
        cx: float,
        cy: float,
        cz: float,
        r_bottom: float,
        r_top: float,
        height: float,
        segments: int = 12,
        rot_y_deg: float = 0.0,
        mat_slot: int = 0,
    ) -> None:
        center = (cx, cy, cz)
        hy = height * 0.5
        rot_rad = math.radians(rot_y_deg)
        bot_ring: List[Tuple[float, float, float]] = []
        top_ring: List[Tuple[float, float, float]] = []
        for s in range(segments):
            ang = rot_rad + (2.0 * math.pi * s) / segments
            ca, sa = math.cos(ang), math.sin(ang)
            bot_ring.append((cx + r_bottom * ca, cy - hy, cz + r_bottom * sa))
            top_ring.append((cx + r_top * ca, cy + hy, cz + r_top * sa))

        for s in range(segments):
            n = (s + 1) % segments
            self._emit_oriented_face([bot_ring[s], top_ring[s], top_ring[n], bot_ring[n]], center, mat_slot)

        self.add_face(top_ring, mat_slot, outward_hint=(0.0, 1.0, 0.0))
        self.add_face(bot_ring, mat_slot, outward_hint=(0.0, -1.0, 0.0))

    def add_ring_y(
        self,
        cx: float,
        cy: float,
        cz: float,
        r_outer: float,
        r_inner: float,
        height: float,
        segments: int = 20,
        rot_y_deg: float = 0.0,
        mat_slot: int = 0,
    ) -> None:
        hy = height * 0.5
        rot_rad = math.radians(rot_y_deg)
        ot, ob, it, ib = [], [], [], []
        for s in range(segments):
            ang = rot_rad + (2.0 * math.pi * s) / segments
            ca, sa = math.cos(ang), math.sin(ang)
            ot.append((cx + r_outer * ca, cy + hy, cz + r_outer * sa))
            ob.append((cx + r_outer * ca, cy - hy, cz + r_outer * sa))
            it.append((cx + r_inner * ca, cy + hy, cz + r_inner * sa))
            ib.append((cx + r_inner * ca, cy - hy, cz + r_inner * sa))

        for s in range(segments):
            n = (s + 1) % segments
            ang_mid = rot_rad + (2.0 * math.pi * (s + 0.5)) / segments
            rad_x, rad_z = math.cos(ang_mid), math.sin(ang_mid)
            self.add_face([ob[s], ot[s], ot[n], ob[n]], mat_slot, outward_hint=(rad_x, 0.0, rad_z))
            self.add_face([ib[n], it[n], it[s], ib[s]], mat_slot, outward_hint=(-rad_x, 0.0, -rad_z))
            self.add_face([ot[s], it[s], it[n], ot[n]], mat_slot, outward_hint=(0.0, 1.0, 0.0))
            self.add_face([ib[s], ob[s], ob[n], ib[n]], mat_slot, outward_hint=(0.0, -1.0, 0.0))

    def add_tapered_frustum(
        self,
        cx: float,
        cy: float,
        cz: float,
        bot_w: float,
        bot_d: float,
        top_w: float,
        top_d: float,
        height: float,
        bevel_x: float = 0.0,
        mat_slot: int = 0,
    ) -> None:
        center = (cx, cy, cz)
        hy = height * 0.5
        bw, bd = bot_w * 0.5, bot_d * 0.5
        tw, td = top_w * 0.5, top_d * 0.5
        b = min(bevel_x, bw * 0.25, tw * 0.25)
        if b <= 1e-5:
            bot = [
                (cx - bw, cy - hy, cz - bd),
                (cx + bw, cy - hy, cz - bd),
                (cx + bw, cy - hy, cz + bd),
                (cx - bw, cy - hy, cz + bd),
            ]
            top = [
                (cx - tw, cy + hy, cz - td),
                (cx + tw, cy + hy, cz - td),
                (cx + tw, cy + hy, cz + td),
                (cx - tw, cy + hy, cz + td),
            ]
            self.add_face(top, mat_slot, outward_hint=(0.0, 1.0, 0.0))
            self.add_face(bot, mat_slot, outward_hint=(0.0, -1.0, 0.0))
            for s in range(4):
                n = (s + 1) % 4
                self._emit_oriented_face([bot[n], top[n], top[s], bot[s]], center, mat_slot)
            return

        def ring(rx: float, rz: float, y: float) -> List[Tuple[float, float, float]]:
            bx = max(0.01, rx - b)
            bz = max(0.01, rz - b)
            return [
                (cx - bx, y, cz - rz),
                (cx + bx, y, cz - rz),
                (cx + rx, y, cz - bz),
                (cx + rx, y, cz + bz),
                (cx + bx, y, cz + rz),
                (cx - bx, y, cz + rz),
                (cx - rx, y, cz + bz),
                (cx - rx, y, cz - bz),
            ]

        bot = ring(bw, bd, cy - hy)
        top = ring(tw, td, cy + hy)
        for s in range(8):
            n = (s + 1) % 8
            self._emit_oriented_face([bot[n], top[n], top[s], bot[s]], center, mat_slot)
        self.add_face(top, mat_slot, outward_hint=(0.0, 1.0, 0.0))
        self.add_face(bot, mat_slot, outward_hint=(0.0, -1.0, 0.0))


def _compute_face_normal(verts: Sequence[Tuple[float, float, float]]) -> Tuple[float, float, float]:
    ax, ay, az = 0.0, 0.0, 0.0
    n = len(verts)
    for i in range(n):
        x0, y0, z0 = verts[i]
        x1, y1, z1 = verts[(i + 1) % n]
        ax += (y0 - y1) * (z0 + z1)
        ay += (z0 - z1) * (x0 + x1)
        az += (x0 - x1) * (y0 + y1)
    mag = math.sqrt(ax * ax + ay * ay + az * az)
    if mag < 1e-8:
        return (0.0, 1.0, 0.0)
    return (ax / mag, ay / mag, az / mag)


def _fmt_floats(vals: Sequence[float], cols: int = 9) -> str:
    parts = [f"{v:.6f}" for v in vals]
    lines: List[str] = []
    for i in range(0, len(parts), cols):
        lines.append("\t\t\t" + ",".join(parts[i : i + cols]))
    return ",\n".join(lines)


def _fmt_ints(vals: Sequence[int], cols: int = 16) -> str:
    parts = [str(v) for v in vals]
    lines: List[str] = []
    for i in range(0, len(parts), cols):
        lines.append("\t\t\t" + ",".join(parts[i : i + cols]))
    return ",\n".join(lines)


def export_fbx(
    output_path: str,
    parts: Sequence[MeshPart],
    materials: Sequence[Tuple[str, Tuple[float, float, float]]],
) -> None:
    """Exports MeshParts and Materials into valid Autodesk ASCII FBX 7.4.0."""
    os.makedirs(os.path.dirname(output_path), exist_ok=True)

    next_id = 1000000

    def alloc_id() -> int:
        nonlocal next_id
        next_id += 137
        return next_id

    mat_ids: Dict[str, int] = {m_name: alloc_id() for m_name, _ in materials}
    model_ids: Dict[str, int] = {}
    geom_ids: Dict[str, int] = {}
    for p in parts:
        model_ids[p.name] = alloc_id()
        geom_ids[p.name] = alloc_id()

    out: List[str] = []
    out.append("; FBX 7.4.0 project file")
    out.append("; Created by POLYMULATOR FBX 7.4 Asset Pipeline")
    out.append("; ----------------------------------------------------")
    out.append("")
    out.append("FBXHeaderExtension:  {")
    out.append("\tFBXHeaderVersion: 1003")
    out.append("\tFBXVersion: 7400")
    out.append("\tCreationTimeStamp:  {")
    out.append("\t\tVersion: 1000")
    out.append("\t\tYear: 2026")
    out.append("\t\tMonth: 11")
    out.append("\t\tDay: 23")
    out.append("\t\tHour: 4")
    out.append("\t\tMinute: 0")
    out.append("\t\tSecond: 0")
    out.append("\t\tMillisecond: 0")
    out.append("\t}")
    out.append('\tCreator: "POLYMULATOR FBX Generator"')
    out.append("}")
    out.append("GlobalSettings:  {")
    out.append("\tVersion: 1000")
    out.append("\tProperties70:  {")
    out.append('\t\tP: "UpAxis", "int", "Integer", "",1')
    out.append('\t\tP: "UpAxisSign", "int", "Integer", "",1')
    out.append('\t\tP: "FrontAxis", "int", "Integer", "",2')
    out.append('\t\tP: "FrontAxisSign", "int", "Integer", "",1')
    out.append('\t\tP: "CoordAxis", "int", "Integer", "",0')
    out.append('\t\tP: "CoordAxisSign", "int", "Integer", "",1')
    out.append('\t\tP: "OriginalUpAxis", "int", "Integer", "",1')
    out.append('\t\tP: "OriginalUpAxisSign", "int", "Integer", "",1')
    # 1.0 file unit = 1 meter -> Unity's UseFileScale divides/multiplies by UnitScaleFactor = 100cm = 1.0m!
    out.append('\t\tP: "UnitScaleFactor", "double", "Number", "",100')
    out.append('\t\tP: "OriginalUnitScaleFactor", "double", "Number", "",100')
    out.append("\t}")
    out.append("}")
    out.append("Documents:  {")
    out.append("\tCount: 1")
    out.append(f'\tDocument: {alloc_id()}, "Scene", "Scene" {{')
    out.append("\t\tProperties70:  {")
    out.append('\t\t\tP: "SourceObject", "object", "", ""')
    out.append('\t\t\tP: "ActiveAnimStackName", "KString", "", "", ""')
    out.append("\t\t}")
    out.append("\t\tRootNode: 0")
    out.append("\t}")
    out.append("}")
    out.append("References:  {")
    out.append("}")
    out.append("Definitions:  {")
    out.append("\tVersion: 100")
    out.append(f"\tCount: {1 + len(parts) * 2 + len(materials)}")
    for otype, cnt in (
        ("GlobalSettings", 1),
        ("Model", len(parts)),
        ("Geometry", len(parts)),
        ("Material", len(materials)),
    ):
        out.append(f'\tObjectType: "{otype}" {{')
        out.append(f"\t\tCount: {cnt}")
        out.append("\t}")
    out.append("}")
    out.append("")
    out.append("Objects:  {")

    for p in parts:
        flat_verts: List[float] = []
        for vx, vy, vz in p.vertices:
            flat_verts.extend((vx, vy, vz))

        poly_idx: List[int] = []
        flat_normals: List[float] = []
        flat_uvs: List[float] = []
        uv_cursor = 0
        for poly in p.polygons:
            face_verts = [p.vertices[idx] for idx in poly]
            nx, ny, nz = _compute_face_normal(face_verts)
            for k, idx in enumerate(poly):
                is_last = k == len(poly) - 1
                poly_idx.append(-(idx + 1) if is_last else idx)
                flat_normals.extend((nx, ny, nz))
                u, v_c = p.uvs[uv_cursor] if uv_cursor < len(p.uvs) else (0.0, 0.0)
                flat_uvs.extend((u, v_c))
                uv_cursor += 1

        gid = geom_ids[p.name]
        out.append(f'\tGeometry: {gid}, "Geometry::{p.name}", "Mesh" {{')
        out.append(f"\t\tVertices: *{len(flat_verts)} {{")
        out.append("\t\t\ta: " + _fmt_floats(flat_verts).lstrip())
        out.append("\t\t}")
        out.append(f"\t\tPolygonVertexIndex: *{len(poly_idx)} {{")
        out.append("\t\t\ta: " + _fmt_ints(poly_idx).lstrip())
        out.append("\t\t}")
        out.append("\t\tGeometryVersion: 124")
        out.append("\t\tLayerElementNormal: 0 {")
        out.append("\t\t\tVersion: 101")
        out.append('\t\t\tName: ""')
        out.append('\t\t\tMappingInformationType: "ByPolygonVertex"')
        out.append('\t\t\tReferenceInformationType: "Direct"')
        out.append(f"\t\t\tNormals: *{len(flat_normals)} {{")
        out.append("\t\t\t\ta: " + _fmt_floats(flat_normals).lstrip())
        out.append("\t\t\t}")
        out.append("\t\t}")
        out.append("\t\tLayerElementUV: 0 {")
        out.append("\t\t\tVersion: 101")
        out.append('\t\t\tName: "UVMap"')
        out.append('\t\t\tMappingInformationType: "ByPolygonVertex"')
        out.append('\t\t\tReferenceInformationType: "Direct"')
        out.append(f"\t\t\tUV: *{len(flat_uvs)} {{")
        out.append("\t\t\t\ta: " + _fmt_floats(flat_uvs).lstrip())
        out.append("\t\t\t}")
        out.append("\t\t}")
        out.append("\t\tLayerElementMaterial: 0 {")
        out.append("\t\t\tVersion: 101")
        out.append('\t\t\tName: ""')
        out.append('\t\t\tMappingInformationType: "AllSame"')
        out.append('\t\t\tReferenceInformationType: "IndexToDirect"')
        out.append("\t\t\tMaterials: *1 {")
        out.append("\t\t\t\ta: 0")
        out.append("\t\t\t}")
        out.append("\t\t}")
        out.append("\t\tLayer: 0 {")
        out.append("\t\t\tVersion: 100")
        for etype in ("LayerElementNormal", "LayerElementMaterial", "LayerElementUV"):
            out.append("\t\t\tLayerElement:  {")
            out.append(f'\t\t\t\tType: "{etype}"')
            out.append("\t\t\t\tTypedIndex: 0")
            out.append("\t\t\t}")
        out.append("\t\t}")
        out.append("\t}")

        mid = model_ids[p.name]
        tx, ty, tz = p.translation
        rx, ry, rz = p.rotation_deg
        sx, sy, sz = p.scale
        out.append(f'\tModel: {mid}, "Model::{p.name}", "Mesh" {{')
        out.append("\t\tVersion: 232")
        out.append("\t\tProperties70:  {")
        out.append('\t\t\tP: "RotationOrder", "enum", "", "",0')
        out.append('\t\t\tP: "DefaultAttributeIndex", "int", "Integer", "",0')
        out.append(f'\t\t\tP: "Lcl Translation", "Lcl Translation", "", "A",{tx},{ty},{tz}')
        out.append(f'\t\t\tP: "Lcl Rotation", "Lcl Rotation", "", "A",{rx},{ry},{rz}')
        out.append(f'\t\t\tP: "Lcl Scaling", "Lcl Scaling", "", "A",{sx},{sy},{sz}')
        out.append("\t\t}")
        out.append("\t\tShading: T")
        out.append('\t\tCulling: "CullingOff"')
        out.append("\t}")

    for m_name, (r, g_c, b_c) in materials:
        mat_id = mat_ids[m_name]
        out.append(f'\tMaterial: {mat_id}, "Material::{m_name}", "" {{')
        out.append("\t\tVersion: 102")
        out.append('\t\tShadingModel: "lambert"')
        out.append("\t\tMultiLayer: 0")
        out.append("\t\tProperties70:  {")
        out.append(f'\t\t\tP: "DiffuseColor", "Color", "", "A",{r},{g_c},{b_c}')
        out.append("\t\t}")
        out.append("\t}")

    out.append("}")
    out.append("")
    out.append("Connections:  {")
    for p in parts:
        parent_id = model_ids[p.parent_name] if p.parent_name in model_ids else 0
        out.append(f'\tC: "OO",{model_ids[p.name]},{parent_id}')
        out.append(f'\tC: "OO",{geom_ids[p.name]},{model_ids[p.name]}')
        m_id = mat_ids.get(p.material_name)
        if m_id is not None:
            out.append(f'\tC: "OO",{m_id},{model_ids[p.name]}')
    out.append("}")
    out.append("")

    with open(output_path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(out))
