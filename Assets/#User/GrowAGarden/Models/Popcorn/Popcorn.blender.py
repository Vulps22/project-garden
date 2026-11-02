"""Builds a popped piece of popcorn in Blender and exports it for Unity.

Metaballs for the puffs (a small core, lobes burst outward and up), a knobbly displacement, and a golden hull
fragment at the base. Two exports: Popcorn (plant, detailed, puffs and hull) and Popcorn_Small (particles, light, puffs only: a particle
mesh draws its first sub-mesh only).
Run: blender --background --python popcorn.py -- <out_dir> <preview_dir>
"""
import math
import random
import sys

import bpy
import bmesh
from mathutils import Vector

out_dir, preview_dir = sys.argv[sys.argv.index("--") + 1:][:2]
random.seed(11)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


def material(name, colour, rough):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*colour, 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    m.diffuse_color = (*colour, 1.0)
    return m


puff_mat = material("Popcorn", (0.98, 0.93, 0.78), 0.85)
hull_mat = material("Popcorn_Hull", (0.80, 0.45, 0.10), 0.35)

# ── The puffs: metaballs ─────────────────────────────────────────────────────
mb = bpy.data.metaballs.new("PuffBalls")
mb.resolution = 0.035
mb.render_resolution = 0.035
mb.threshold = 0.6
core = mb.elements.new()
core.co = (0, 0, 0)
core.radius = 0.32

# Lobes burst outward from the core, favouring up and to the sides, like a "butterfly" flake.
lobes = []
for i in range(9):
    az = (i / 9) * math.tau + random.uniform(-0.35, 0.35)
    el = random.uniform(-0.15, 0.95)
    d = Vector((math.cos(az) * math.cos(el), math.sin(az) * math.cos(el), math.sin(el)))
    lobes.append(d)
    e = mb.elements.new()
    e.co = d * random.uniform(0.28, 0.42)
    e.radius = random.uniform(0.26, 0.36)
    # Smaller secondary puff on some lobes, so the surface is bumpy at two scales.
    if random.random() < 0.6:
        e2 = mb.elements.new()
        e2.co = d * random.uniform(0.5, 0.62) + Vector((random.uniform(-.1, .1), random.uniform(-.1, .1), random.uniform(-.05, .12)))
        e2.radius = random.uniform(0.14, 0.22)

mb_obj = bpy.data.objects.new("Puffs", mb)
scene.collection.objects.link(mb_obj)
bpy.context.view_layer.objects.active = mb_obj
mb_obj.select_set(True)
bpy.context.view_layer.update()
bpy.ops.object.convert(target="MESH")
puffs = bpy.context.view_layer.objects.active
puffs.name = "Popcorn"

# Clean, even topology before the surface detail.
rm = puffs.modifiers.new("Remesh", "REMESH")
rm.mode = "VOXEL"
rm.voxel_size = 0.014
bpy.ops.object.modifier_apply(modifier="Remesh")

# Knobbly surface: small-scale noise pushed along the normals.
tex = bpy.data.textures.new("Knobs", "CLOUDS")
tex.noise_scale = 0.07
tex.noise_depth = 2
disp = puffs.modifiers.new("Knobs", "DISPLACE")
disp.texture = tex
disp.strength = 0.05
disp.mid_level = 0.5
bpy.ops.object.modifier_apply(modifier="Knobs")
# Fine wrinkles on top, the crinkled skin of a popped kernel.
tex2 = bpy.data.textures.new("Wrinkles", "VORONOI")
tex2.noise_scale = 0.025
disp2 = puffs.modifiers.new("Wrinkles", "DISPLACE")
disp2.texture = tex2
disp2.strength = 0.018
disp2.mid_level = 0.5
bpy.ops.object.modifier_apply(modifier="Wrinkles")
sm = puffs.modifiers.new("Smooth", "SMOOTH")
sm.iterations = 1
sm.factor = 0.5
bpy.ops.object.modifier_apply(modifier="Smooth")
puffs.data.materials.append(puff_mat)

# ── The hull: a curved golden fragment at the base ───────────────────────────
bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=0.2, location=(0.03, -0.02, -0.3))
hull = bpy.context.active_object
hull.name = "Hull"
hull.scale = (1.0, 0.8, 0.45)
bpy.ops.object.transform_apply(scale=True)
# Keep the lower cap only: a shell, open on top where it meets the puffs.
bm = bmesh.new()
bm.from_mesh(hull.data)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z > -0.02], context="VERTS")
bm.to_mesh(hull.data)
bm.free()
sol = hull.modifiers.new("Thick", "SOLIDIFY")
sol.thickness = 0.02
bpy.ops.object.modifier_apply(modifier="Thick")
hull.rotation_euler = (math.radians(12), math.radians(-8), 0)
bpy.ops.object.transform_apply(rotation=True)
hull.data.materials.append(hull_mat)

# ── Join, centre, scale to a 1 unit size ─────────────────────────────────────
bpy.ops.object.select_all(action="DESELECT")
hull.select_set(True)
puffs.select_set(True)
bpy.context.view_layer.objects.active = puffs
bpy.ops.object.join()
piece = bpy.context.active_object
piece.name = "Popcorn"
bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="BOUNDS")
piece.location = (0, 0, 0)
size = max(piece.dimensions)
piece.scale = (1 / size,) * 3
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.object.shade_smooth()


def tris(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def decimated(src, name, target):
    dup = src.copy()
    dup.data = src.data.copy()
    dup.name = name
    scene.collection.objects.link(dup)
    dec = dup.modifiers.new("Decimate", "DECIMATE")
    dec.ratio = min(1.0, target / max(1, tris(src)))
    bpy.context.view_layer.objects.active = dup
    bpy.ops.object.select_all(action="DESELECT")
    dup.select_set(True)
    bpy.ops.object.modifier_apply(modifier="Decimate")
    return dup


plant = decimated(piece, "Popcorn_Plant", 3000)
small = decimated(piece, "Popcorn_Small", 320)

# Particle meshes draw one sub-mesh only, so the small piece is the puffs alone, in one material: the hull is too
# small to see on a flying piece anyway.
bm = bmesh.new()
bm.from_mesh(small.data)
hull_index = [i for i, m in enumerate(small.data.materials) if m == hull_mat]
if hull_index:
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.material_index == hull_index[0]], context="FACES")
bm.to_mesh(small.data)
bm.free()
small.data.materials.clear()
small.data.materials.append(puff_mat)
for poly in small.data.polygons:
    poly.material_index = 0
bpy.data.objects.remove(piece)
print(f"POPCORN plant tris={tris(plant)} small tris={tris(small)} dims={tuple(round(d, 3) for d in plant.dimensions)}")


def export(obj, path):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", apply_unit_scale=True, bake_space_transform=True,
                             mesh_smooth_type="FACE", use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False)


export(plant, f"{out_dir}/Popcorn.fbx")
export(small, f"{out_dir}/Popcorn_Small.fbx")

# ── Preview renders, to look at before Unity does ───────────────────────────
small.location = (1.1, 0.2, 0)
small.scale = (0.8, 0.8, 0.8)
cam_data = bpy.data.cameras.new("Cam")
cam = bpy.data.objects.new("Cam", cam_data)
scene.collection.objects.link(cam)
cam.location = (0.55, -2.9, 1.1)
cam.rotation_euler = (math.radians(73), 0, math.radians(0))
scene.camera = cam
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
sun.data.energy = 4
sun.rotation_euler = (math.radians(45), math.radians(15), math.radians(30))
scene.collection.objects.link(sun)
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_cavity = True
scene.render.resolution_x = 900
scene.render.resolution_y = 500
scene.render.filepath = f"{preview_dir}/popcorn_preview.png"
bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=f"{preview_dir}/popcorn.blend")
