"""Builds the Spotlight Tulip, its lamp and its bulb in Blender and exports them for Unity.

The plant (SpotlightTulip.fbx): a stem with two strap leaves, and a Cup (an empty at the stem's top) of six petals,
each its own object with its pivot at the cup's base, modelled upright so Unity can open them. About 0.6 m tall.
The lamp (Spotlight_Lamp.fbx): a stage light's can, 0.6 m long, pointing along +Z in Blender (Unity +Y), pivot at its
middle: housing, cooling fins, a back cap and a lens (its own material, which glows).
The bulb (SpotlightTulip_Bulb.fbx): a tulip bulb, 1 unit tall, pivot at its centre.
Run: blender --background --factory-startup --python SpotlightTulip.blender.py -- <out_dir> <preview_dir>
"""
import math
import sys

import bpy
import bmesh
from mathutils import Vector

out_dir, preview_dir = sys.argv[sys.argv.index("--") + 1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


def material(name, colour, rough, metal=0.0, emit=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*colour, 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    if emit:
        bsdf.inputs["Emission Color"].default_value = (*emit, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 1.0
    m.diffuse_color = (*colour, 1.0)
    return m


petal_mat = material("SpotlightTulip_Petal", (0.85, 0.12, 0.45), 0.5)
stem_mat = material("SpotlightTulip_Stem", (0.2, 0.45, 0.18), 0.7)
body_mat = material("Spotlight_Body", (0.08, 0.08, 0.09), 0.35, metal=0.8)
lens_mat = material("Spotlight_Lens", (1.0, 1.0, 0.95), 0.1, emit=(1.0, 1.0, 0.95))
bulb_mat = material("SpotlightTulip_Bulb", (0.55, 0.36, 0.2), 0.8)


def link(obj):
    scene.collection.objects.link(obj)
    return obj


def mesh_object(name, bm, mat):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    for p in me.polygons:
        p.use_smooth = True
    return link(bpy.data.objects.new(name, me))


def solidify(obj, thickness):
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    mod = obj.modifiers.new("Thick", "SOLIDIFY")
    mod.thickness = thickness
    mod.offset = 0
    bpy.ops.object.modifier_apply(modifier="Thick")


def lathe(name, profile, mat, sides=24):
    """profile: (radius, z) pairs bottom to top; closed at both ends when radius is 0."""
    bm = bmesh.new()
    rings = []
    for r, z in profile:
        rings.append([bm.verts.new((r * math.cos(a), r * math.sin(a), z))
                      for a in (j / sides * math.tau for j in range(sides))])
    for i in range(len(rings) - 1):
        for j in range(sides):
            bm.faces.new((rings[i][j], rings[i][(j + 1) % sides], rings[i + 1][(j + 1) % sides], rings[i + 1][j]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    return mesh_object(name, bm, mat)


# ── Stem and leaves ──────────────────────────────────────────────────────────
STEM_H = 0.5
stem = lathe("Stem", [(0.012, 0), (0.011, STEM_H * 0.5), (0.009, STEM_H)], stem_mat, sides=10)


def strap_leaf(name, yaw, length, lean):
    bm = bmesh.new()
    rows, cols = 16, 4
    grid = []
    for ri in range(rows + 1):
        t = ri / rows
        half = 0.035 * math.sin(math.pi * min(1, t * 0.9 + 0.1)) * (1 - t * 0.6)
        # Rises, then arches outward and over.
        y = lean * length * t * t
        z = length * math.sin(t * math.pi * 0.42)
        row = []
        for ci in range(cols + 1):
            u = ci / cols * 2 - 1
            row.append(bm.verts.new((u * half, y + 0.01 * u * u, z)))
        grid.append(row)
    for ri in range(rows):
        for ci in range(cols):
            bm.faces.new((grid[ri][ci], grid[ri][ci + 1], grid[ri + 1][ci + 1], grid[ri + 1][ci]))
    obj = mesh_object(name, bm, stem_mat)
    solidify(obj, 0.003)
    obj.rotation_euler = (0, 0, math.radians(yaw))
    return obj


leaves = [strap_leaf("Leaf_0", 30, 0.38, 0.5), strap_leaf("Leaf_1", 210, 0.32, 0.6)]

# ── The cup: six petals around an empty ──────────────────────────────────────
cup = link(bpy.data.objects.new("Cup", None))
cup.location = (0, 0, STEM_H)
PETALS, LENGTH = 6, 0.11


def petal(i):
    bm = bmesh.new()
    us, vs = 8, 12
    grid = []
    inner = i % 2
    for vi in range(vs + 1):
        v = vi / vs
        half = 0.022 * math.sin(math.pi * (0.15 + 0.85 * v) ** 0.8) + 0.004
        row = []
        for ui in range(us + 1):
            u = ui / us * 2 - 1
            x = u * half
            # Deeply cupped toward the centre (-Y), bellying out at the middle like a tulip.
            y = -0.6 * x * x / max(half, 0.01) + 0.018 * math.sin(math.pi * v)
            row.append(bm.verts.new((x, y, v * LENGTH * (0.95 if inner else 1.0))))
        grid.append(row)
    for vi in range(vs):
        for ui in range(us):
            bm.faces.new((grid[vi][ui], grid[vi][ui + 1], grid[vi + 1][ui + 1], grid[vi + 1][ui]))
    obj = mesh_object(f"Petal_{i}", bm, petal_mat)
    solidify(obj, 0.003)
    a = i / PETALS * math.tau
    obj.location = Vector((math.cos(a), math.sin(a), 0)) * (0.012 if inner else 0.016)
    obj.rotation_euler = (0, 0, a + math.pi / 2)
    obj.parent = cup
    return obj


petals = [petal(i) for i in range(PETALS)]

# ── The lamp: a stage light's can, lens toward +Z ────────────────────────────
L = 0.6
housing = lathe("Housing", [(0.0, -L / 2), (0.15, -L / 2), (0.19, -L / 2 + 0.06), (0.2, L / 2 - 0.02),
                            (0.23, L / 2 - 0.01), (0.23, L / 2), (0.19, L / 2)], body_mat, sides=32)
fins = []
for k in range(4):
    z = -L / 2 + 0.1 + k * 0.06
    fins.append(lathe(f"Fin_{k}", [(0.0, z), (0.24, z), (0.24, z + 0.02), (0.0, z + 0.02)], body_mat, sides=32))
lens = lathe("Lens", [(0.0, L / 2 - 0.03), (0.19, L / 2 - 0.03), (0.17, L / 2 - 0.005), (0.0, L / 2 + 0.01)],
             lens_mat, sides=32)
bpy.ops.object.select_all(action="DESELECT")
for o in [housing, *fins]:
    o.select_set(True)
bpy.context.view_layer.objects.active = housing
bpy.ops.object.join()
housing = bpy.context.active_object
housing.name = "Housing"
lamp = link(bpy.data.objects.new("Lamp", None))
housing.parent = lamp
lens.parent = lamp
lamp.location = (1.5, 0, 0.4)

# ── The bulb ─────────────────────────────────────────────────────────────────
profile = []
for k in range(15):
    t = k / 14
    # Wide round base, narrowing to a point: an onion's teardrop.
    r = 0.42 * math.sin(math.pi * min(1, t * 1.15)) ** 0.75 * (1 - t) ** 0.35 if t < 1 else 0.0
    profile.append((max(0.0, r), t - 0.45))
profile[0] = (0.0, -0.45)
profile[-1] = (0.0, 0.55)
bulb = lathe("SpotlightTulip_Bulb", profile, bulb_mat, sides=24)
bulb.location = (-1.5, 0, 0)


def export(objs, path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", apply_unit_scale=True, bake_space_transform=True,
                             mesh_smooth_type="FACE", use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False)


export([stem, *leaves, cup, *petals], f"{out_dir}/SpotlightTulip.fbx")
lamp.location = (0, 0, 0)
export([lamp, housing, lens], f"{out_dir}/Spotlight_Lamp.fbx")
lamp.location = (1.2, 0, 0.5)
lamp.rotation_euler = (math.radians(-60), 0, math.radians(30))
lamp.scale = (0.7, 0.7, 0.7)
bulb.location = (0, 0, 0)
export([bulb], f"{out_dir}/SpotlightTulip_Bulb.fbx")
bulb.location = (-0.6, 0, 0.1)
bulb.scale = (0.2, 0.2, 0.2)

cam = link(bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")))
cam.location = (0.4, -3.0, 0.7)
cam.rotation_euler = (math.radians(84), 0, math.radians(5))
scene.camera = cam
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.render.resolution_x = 900
scene.render.resolution_y = 600
scene.render.filepath = f"{preview_dir}/tulip_preview.png"
bpy.ops.render.render(write_still=True)
print("TULIP done")
