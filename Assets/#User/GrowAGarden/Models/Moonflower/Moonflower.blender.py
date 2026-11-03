"""Builds the Moonflower and its seed in Blender and exports them for Unity.

The plant: a curved stem with heart-shaped leaves, and a Head (an empty at the stem's top) holding five petals and a
glowing centre. Each petal is its own object with its pivot at the flower's base, modelled pointing straight up, so
Unity opens the flower by tilting each petal outward about its own base. Real size: about 1.1 m tall.
The seed: a large dark rounded wedge, 1 unit tall, pivot at its centre.
Run: blender --background --factory-startup --python Moonflower.blender.py -- <out_dir> <preview_dir>
"""
import math
import sys

import bpy
import bmesh
from mathutils import Vector

out_dir, preview_dir = sys.argv[sys.argv.index("--") + 1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


def material(name, colour, rough, emit=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*colour, 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    if emit:
        bsdf.inputs["Emission Color"].default_value = (*emit, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 1.0
    m.diffuse_color = (*colour, 1.0)
    return m


petal_mat = material("Moonflower_Petal", (0.92, 0.94, 1.0), 0.6)
stem_mat = material("Moonflower_Stem", (0.16, 0.38, 0.14), 0.7)
glow_mat = material("Moonflower_Glow", (1.0, 0.95, 0.7), 0.4, emit=(1.0, 0.9, 0.6))
seed_mat = material("Moonflower_Seed", (0.12, 0.08, 0.06), 0.5)


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


# ── Stem: a tube along a gentle curve ────────────────────────────────────────
STEM_TOP = Vector((0.05, 0.0, 1.0))


def stem_point(t):
    # A soft S: leans out, then back under the flower.
    return Vector((0.05 * math.sin(t * math.pi * 1.2), 0.02 * math.sin(t * math.pi * 2.0), t * STEM_TOP.z))


bm = bmesh.new()
rings, sides, radius = 24, 10, 0.016
verts = []
for i in range(rings + 1):
    t = i / rings
    c = stem_point(t)
    tangent = (stem_point(min(1, t + 0.01)) - stem_point(max(0, t - 0.01))).normalized()
    side = tangent.cross(Vector((0, 1, 0))).normalized()
    up = side.cross(tangent).normalized()
    r = radius * (1.0 - 0.35 * t)
    ring = []
    for j in range(sides):
        a = j / sides * math.tau
        ring.append(bm.verts.new(c + (side * math.cos(a) + up * math.sin(a)) * r))
    verts.append(ring)
for i in range(rings):
    for j in range(sides):
        bm.faces.new((verts[i][j], verts[i][(j + 1) % sides], verts[i + 1][(j + 1) % sides], verts[i + 1][j]))
STEM_TOP = stem_point(1.0)
stem = mesh_object("Stem", bm, stem_mat)


# ── Leaves: hearts on short stalks ───────────────────────────────────────────
def leaf(name, at, yaw, size, droop):
    bm = bmesh.new()
    # A pointed heart reaching out from the stem along +Y, on a short stalk, cupped along its midrib and drooping.
    stalk, rows, cols = 0.03, 14, 6
    grid = []
    for ri in range(rows + 1):
        t = ri / rows
        y = stalk + t * size
        half = size * 0.42 * math.sin(math.pi * t ** 0.65) * (1.0 + 0.25 * max(0, 0.25 - t) / 0.25)
        row = []
        for ci in range(cols + 1):
            u = ci / cols * 2 - 1
            x = u * half
            z = 0.18 * abs(x) - droop * (y / size) ** 2 * size
            row.append(bm.verts.new((x, y, z)))
        grid.append(row)
    for ri in range(rows):
        for ci in range(cols):
            bm.faces.new((grid[ri][ci], grid[ri][ci + 1], grid[ri + 1][ci + 1], grid[ri + 1][ci]))
    # The stalk: a thin strip from the stem to the leaf.
    s0, s1 = bm.verts.new((-0.003, 0, 0)), bm.verts.new((0.003, 0, 0))
    bm.faces.new((s0, s1, grid[0][cols // 2 + 1], grid[0][cols // 2 - 1]))
    obj = mesh_object(name, bm, stem_mat)
    solidify(obj, 0.004)
    obj.location = at
    obj.rotation_euler = (math.radians(-20), 0, math.radians(yaw))
    return obj


leaves = [
    leaf("Leaf_0", stem_point(0.28), 20, 0.16, 0.25),
    leaf("Leaf_1", stem_point(0.52), 200, 0.14, 0.3),
    leaf("Leaf_2", stem_point(0.72), 110, 0.11, 0.2),
]

# ── The flower: a Head empty at the stem's top ───────────────────────────────
head = link(bpy.data.objects.new("Head", None))
head.location = STEM_TOP

PETALS, LENGTH = 5, 0.24


def petal(i):
    bm = bmesh.new()
    us, vs = 9, 14
    grid = []
    for vi in range(vs + 1):
        v = vi / vs
        half = 0.012 + 0.085 * v ** 1.1 - 0.03 * max(0, v - 0.85) / 0.15   # rounded tip
        row = []
        for ui in range(us + 1):
            u = ui / us * 2 - 1
            x = u * half
            # Cupped toward the flower's centre (-Y), with a soft frill at the rim.
            y = -0.35 * x * x / max(half, 0.02) * half + 0.006 * math.sin(u * math.pi * 2) * v
            row.append(bm.verts.new((x, y, v * LENGTH)))
        grid.append(row)
    for vi in range(vs):
        for ui in range(us):
            bm.faces.new((grid[vi][ui], grid[vi][ui + 1], grid[vi + 1][ui + 1], grid[vi + 1][ui]))
    obj = mesh_object(f"Petal_{i}", bm, petal_mat)
    solidify(obj, 0.003)
    # Stand it at the rim of the flower's base, facing the centre; pivot at its own base.
    a = i / PETALS * math.tau
    obj.location = Vector((math.cos(a), math.sin(a), 0)) * 0.012
    obj.rotation_euler = (0, 0, a + math.pi / 2)
    obj.parent = head
    return obj


petals = [petal(i) for i in range(PETALS)]

bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=0.022, location=(0, 0, 0.02))
glow = bpy.context.active_object
glow.name = "Glow"
glow.scale = (1, 1, 1.6)
glow.data.materials.append(glow_mat)
glow.parent = head
glow.location = (0, 0, 0.02)
bpy.ops.object.shade_smooth()

# ── The seed: a dark rounded wedge ───────────────────────────────────────────
bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=0.5, location=(3, 0, 0))
seed = bpy.context.active_object
seed.name = "Moonflower_Seed"
bm = bmesh.new()
bm.from_mesh(seed.data)
for v in bm.verts:
    # Flatten one side and pinch the top: the rounded-triangle seed of a moonflower.
    v.co.x *= 0.85
    v.co.y *= 0.6 if v.co.y < 0 else 0.75
    v.co.z *= 1.0 - 0.25 * max(0, v.co.z) / 0.5
bm.to_mesh(seed.data)
bm.free()
seed.data.materials.append(seed_mat)
bpy.ops.object.shade_smooth()


# ── Export ───────────────────────────────────────────────────────────────────
def export(objs, path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", apply_unit_scale=True, bake_space_transform=True,
                             mesh_smooth_type="FACE", use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False)


plant_parts = [stem, *leaves, head, *petals, glow]
export(plant_parts, f"{out_dir}/Moonflower.fbx")
seed_loc = seed.location.copy()
seed.location = (0, 0, 0)
export([seed], f"{out_dir}/Moonflower_Seed.fbx")
seed.location = seed_loc
seed.scale = (0.3, 0.3, 0.3)

# ── Preview: the flower half open, beside its seed ───────────────────────────
for i, p in enumerate(petals):
    p.rotation_euler.rotate_axis("X", math.radians(-55))
cam = link(bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")))
cam.location = (1.0, -2.6, 1.0)
cam.rotation_euler = (math.radians(82), 0, math.radians(14))
scene.camera = cam
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.render.resolution_x = 700
scene.render.resolution_y = 700
scene.render.filepath = f"{preview_dir}/moonflower_preview.png"
bpy.ops.render.render(write_still=True)
print("MOONFLOWER done")
