"""Builds Yggdrasil (one variant per Plot of the Garden), its falling leaf, and its seed, and exports them for Unity.

The tree stands behind its Plot: the trunk climbs out of the cliff below the Plot's back edge and rises about 90 m to a
broad flat canopy. Two great roots leave the trunk several metres up and arch down to meet the ground halfway along the
Plot, one on each side, so the tree straddles the back half of the Plot. The other roots pour down the cliff and wrap
the island's rock, fitted to the real rock of each Plot's corner: that is why there is one variant per Plot.
Every root is skinned to a bone chain (RootRig: RootBase, then Root_<root>_<bone>) so Unity can let them dangle.

Work frame: origin at the Plot's centre on the soil, +X towards the Plot's outer edge, +Y towards its back edge, +Z up.
Each variant is exported in its Plot's local space, so it is placed at the Plot's transform with no rotation or scale.
Run: blender --background --factory-startup --python Yggdrasil.blender.py -- <out_dir> <preview_dir> <garden_island.obj> [plot]
(with [plot], only that Plot's variant is built and previewed)
The .obj is the Garden island in Blender's axes, written by Grow A Garden → Export Garden Island For Blender.
"""
import math
import random
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

out_dir, preview_dir, island_obj = sys.argv[sys.argv.index("--") + 1:][:3]
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# The Garden's four Plots, from GardenIsland.prefab: Plot transform position (Unity), yaw, and which local x is outward.
PLOTS = [
    ((0.0, 0.0, 0.0), 0, -1),
    ((5.0, 0.0, 0.0), 0, +1),
    ((1.0, 0.0, 11.0), 180, +1),
    ((6.0, 0.0, 11.0), 180, -1),
]
PLOT_CENTRE_Z = -1.0          # the Plot's dirt is centred 1 m behind its transform (local z)
HALF_W, HALF_D = 2.1, 1.6     # the Plot's half width (x) and half depth (y) in the work frame


# ── Materials ────────────────────────────────────────────────────────────────
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


bark_mat = material("Yggdrasil_Bark", (0.3, 0.17, 0.09), 0.9)
leaf_mat = material("Yggdrasil_Leaves", (0.2, 0.42, 0.14), 0.8)
leaf_light_mat = material("Yggdrasil_LeavesLight", (0.42, 0.62, 0.2), 0.8)
vine_mat = material("Yggdrasil_Vine", (0.24, 0.26, 0.1), 0.9)
wing_mat = material("Yggdrasil_SeedWing", (0.78, 0.66, 0.42), 0.7)
pod_mat = material("Yggdrasil_SeedPod", (0.42, 0.3, 0.18), 0.6)
rune_mat = material("Yggdrasil_Rune", (0.55, 0.85, 1.0), 0.3, emit=(0.5, 0.85, 1.0))


# ── Mesh helpers: geometry is built as plain lists, then emitted per variant ─
class Geo:
    def __init__(self):
        self.verts, self.faces, self.groups = [], [], []   # groups: per vertex [(bone, weight)]

    def add(self, verts, faces, groups=None):
        base = len(self.verts)
        self.verts += verts
        self.faces += [tuple(i + base for i in f) for f in faces]
        self.groups += groups if groups else [[] for _ in verts]

    def bm(self):
        bm = bmesh.new()
        vs = [bm.verts.new(v) for v in self.verts]
        for f in self.faces:
            try:
                bm.faces.new([vs[i] for i in f])
            except ValueError:
                pass
        return bm


def frames(path):
    """Parallel-transported (tangent, side, up) along a polyline."""
    out, ref = [], Vector((0, 0, 1))
    for i in range(len(path)):
        t = (path[min(i + 1, len(path) - 1)] - path[max(i - 1, 0)]).normalized()
        if abs(t.dot(ref)) > 0.95:
            ref = Vector((1, 0, 0))
        side = t.cross(ref).normalized()
        up = side.cross(t).normalized()
        ref = up
        out.append((t, side, up))
    return out


def tube(path, radii, sides, cap_end=True, bones=None):
    """A tube along path; bones = per ring [(bone, weight)] for skinning."""
    verts, faces, groups = [], [], []
    for i, (p, (t, s, u)) in enumerate(zip(path, frames(path))):
        for j in range(sides):
            a = j / sides * math.tau
            verts.append(p + (s * math.cos(a) + u * math.sin(a)) * radii[i])
            groups.append(bones[i] if bones else [])
    for i in range(len(path) - 1):
        for j in range(sides):
            a, b = i * sides + j, i * sides + (j + 1) % sides
            faces.append((a + sides, b + sides, b, a))     # wound so the normals face out
    if cap_end:
        verts.append(path[-1] + frames(path)[-1][0] * radii[-1] * 1.5)
        groups.append(bones[-1] if bones else [])
        tip, last = len(verts) - 1, (len(path) - 1) * sides
        faces += [(tip, last + (j + 1) % sides, last + j) for j in range(sides)]
    return verts, faces, groups


def smooth_path(pts, n=4):
    """Catmull-Rom resample, n points per span."""
    out = []
    for i in range(len(pts) - 1):
        p0, p1, p2, p3 = pts[max(i - 1, 0)], pts[i], pts[i + 1], pts[min(i + 2, len(pts) - 1)]
        for k in range(n):
            t = k / n
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                              + (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t))
    out.append(pts[-1])
    return out


# ── The trunk: rings of a superellipse whose centre drifts back as it climbs ─
# (z, centre x, centre y, half width, half depth front, half depth back, squareness, fluting)
TRUNK = [
    (-34, 0.8, 3.0, 2.5, 2.5, 2.5, 2.0, 0.0),    # below the soil it curls back under the Plot, into the rock
    (-24, 1.0, 5.5, 6.0, 6.0, 6.0, 2.2, 0.08),
    (-12, 1.3, 9.0, 8.5, 8.0, 8.5, 2.5, 0.10),
    (-2, 1.5, 11.0, 9.0, 8.8, 8.5, 2.7, 0.10),   # about 18 m across; front 2.2, just behind the Plot's back edge
    (8, 1.5, 11.0, 8.6, 9.4, 8.2, 2.6, 0.11),    # the flank leans out to the back edge
    (18, 2.0, 11.2, 7.6, 7.8, 7.6, 2.4, 0.12),
    (30, 2.6, 11.4, 6.6, 6.6, 6.6, 2.2, 0.12),   # a slight bend
    (40, 2.2, 11.4, 7.4, 7.4, 7.4, 2.2, 0.10),   # the crown flares into the dome's limbs
    (47, 2.2, 11.4, 8.4, 8.4, 8.4, 2.2, 0.06),
    (52, 2.2, 11.4, 3.0, 3.0, 3.0, 2.0, 0.0),
]


def trunk_at(z):
    for a, b in zip(TRUNK, TRUNK[1:]):
        if a[0] <= z <= b[0]:
            t = (z - a[0]) / (b[0] - a[0])
            t = t * t * (3 - 2 * t)
            return [x + (y - x) * t for x, y in zip(a, b)]
    return list(TRUNK[-1])


def trunk(sides, step_low, step_high):
    zs, z = [], TRUNK[0][0]
    while z < TRUNK[-1][0]:
        zs.append(z)
        z += step_low if z < 16 else step_high
    zs.append(TRUNK[-1][0])
    verts, faces = [], []
    for z in zs:
        _, cx, cy, rx, ryf, ryb, n, flute = trunk_at(z)
        for j in range(sides):
            a = j / sides * math.tau
            c, s = math.cos(a), math.sin(a)
            r = 1 + flute * math.cos(4 * a + z * 0.12)        # four strands twisting round: the braided look
            x = cx + rx * r * math.copysign(abs(c) ** (2 / n), c)
            y = cy + (ryb if s > 0 else ryf) * r * math.copysign(abs(s) ** (2 / n), s)
            verts.append(Vector((x, y, z)))
    for i in range(len(zs) - 1):
        for j in range(sides):
            a, b = i * sides + j, i * sides + (j + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    verts.append(Vector((TRUNK[-1][1], TRUNK[-1][2], TRUNK[-1][0] + 1.5)))
    top, last = len(verts) - 1, (len(zs) - 1) * sides
    faces += [(last + j, last + (j + 1) % sides, top) for j in range(sides)]
    verts.append(Vector((TRUNK[0][1], TRUNK[0][2], TRUNK[0][0] - 1.5)))
    bottom = len(verts) - 1
    faces += [((j + 1) % sides, j, bottom) for j in range(sides)]
    return verts, faces


# ── The crown: halfway up the trunk opens like an upturned dome into a ring of sub-trees ──
TOP = Vector((2.2, 11.4, 0))     # the crown's axis
SUBTREES = 7


def subtree(wood, ends, path, radii, lod, rnd, crown_index, scale=1.0):
    """One sub-tree: its limb-and-trunk tube, branches off its top, and big overlapping leaf clumps round its crown."""
    wood.add(*tube(path, radii, 6 if lod else 9)[:2])
    top = path[-1]
    ends.append((top + Vector((0, 0, 3)), 14.0 * scale, 0, crown_index))
    n = len(path)
    for k in range(4 if lod else 5):
        q = path[int(n * (0.72 + 0.05 * k))]
        a = k / 5 * math.tau + rnd.uniform(-0.4, 0.4)
        out = Vector((math.cos(a), math.sin(a), 0))
        tip = q + out * rnd.uniform(9, 12) * scale + Vector((0, 0, rnd.uniform(3, 6)))
        if not lod:
            b = smooth_path([q, q.lerp(tip, 0.5) + Vector((0, 0, 1.5)), tip], 2)
            wood.add(*tube(b, [1.3 * scale - 0.9 * scale * i / (len(b) - 1) for i in range(len(b))], 6)[:2])
        ends.append((tip, rnd.uniform(11, 13) * scale, k % 2, crown_index))


def crown(lod):
    """Wood, each crown's leaf clumps in two greens [(dark, light)], and a point on each dome limb (for lianas)."""
    rnd = random.Random(7)
    wood = Geo()
    crowns = [(Geo(), Geo()) for _ in range(SUBTREES + 1)]
    ends, anchors = [], []
    for i in range(SUBTREES):
        a = i / SUBTREES * math.tau + rnd.uniform(-0.2, 0.2)
        out = Vector((math.cos(a), math.sin(a), 0))
        reach = rnd.uniform(40, 50)
        z0 = rnd.uniform(40, 46)
        base = TOP + Vector((0, 0, z0))
        height = rnd.uniform(72, 80)
        lean = rnd.uniform(1, 4)
        # Out along the dome's underside, then turning to the sky as its own trunk.
        pts = [base + out * 2, base + out * reach * 0.35 + Vector((0, 0, 2)), base + out * reach * 0.75 + Vector((0, 0, 7)),
               base + out * reach + Vector((0, 0, 14)), TOP + out * (reach + lean * 0.5) + Vector((0, 0, (z0 + 14 + height) / 2)),
               TOP + out * (reach + lean) + Vector((0, 0, height))]
        path = smooth_path(pts, 2 if lod else 4)
        n = len(path)
        radii = [4.2 - 1.6 * min(1, k / (n * 0.5)) - 1.8 * max(0, (k - n * 0.5) / (n * 0.5)) for k in range(n)]
        subtree(wood, ends, path, radii, lod, rnd, i)
        anchors.append((path[int(n * 0.3)], radii[int(n * 0.3)]))
    # The middle one, rising straight out of the dome.
    path = smooth_path([TOP + Vector((0, 0, 44)), TOP + Vector((1, 0.5, 55)), TOP + Vector((0, 0, 66)),
                        TOP + Vector((0.5, -0.5, 76))], 2 if lod else 4)     # level with the ring, filling the middle
    subtree(wood, ends, path, [4.5 - 3.6 * k / (len(path) - 1) for k in range(len(path))], lod, rnd, SUBTREES, 1.25)
    for c, r, shade, ci in ends:
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=0 if lod else 1, radius=r * (1.2 if lod else 1.0))
        for v in bm.verts:
            v.co.z *= 0.5
            v.co += Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-0.6, 0.6))) * r * 0.12
            v.co += c
        crowns[ci][1 if shade else 0].add([v.co.copy() for v in bm.verts], [tuple(v.index for v in f.verts) for f in bm.faces])
        bm.free()
    return wood, crowns, anchors


def ivy_clump(geo, at, r, rnd, normal):
    """A leaf pad: a low four-sided pyramid lying on what it grows on (normal: away from it), 5 faces."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=4, radius1=r, radius2=0, depth=r * 0.5)
    turn = Vector((0, 0, 1)).rotation_difference(normal.normalized()).to_matrix().to_4x4()
    spin = Matrix.Rotation(rnd.uniform(0, math.tau), 4, "Z")
    for v in bm.verts:
        v.co = turn @ spin @ (v.co * rnd.uniform(0.85, 1.15)) + at
    geo.add([v.co.copy() for v in bm.verts], [tuple(v.index for v in f.verts) for f in bm.faces])
    bm.free()


def trunk_surface(z, a, out):
    """A point `out` metres off the trunk's surface, at height z and angle a round it."""
    _, cx, cy, rx, ryf, ryb, n, flute = trunk_at(z)
    c, s = math.cos(a), math.sin(a)
    r = 1 + flute * math.cos(4 * a + z * 0.12)
    return Vector((cx + (rx * r + out) * math.copysign(abs(c) ** (2 / n), c),
                   cy + ((ryb if s > 0 else ryf) * r + out) * math.copysign(abs(s) ** (2 / n), s), z))


def vines(anchors):
    """Ivy spiralling up the trunk to the dome, and lianas sagging between the dome's limbs: (stems, leaves)."""
    rnd = random.Random(23)
    stems, leaves = Geo(), Geo()
    for i in range(5):
        z0, z1 = rnd.uniform(-8, 2), rnd.uniform(36, 44)
        a0, turns = i / 5 * math.tau + rnd.uniform(-0.3, 0.3), rnd.uniform(0.7, 1.2) * rnd.choice((-1, 1))
        steps = 48
        path = [trunk_surface(z0 + (z1 - z0) * t / steps, a0 + turns * math.tau * t / steps,
                              0.5 + 0.25 * math.sin(t * 0.8 + i)) for t in range(steps + 1)]
        radii = [0.5 - 0.35 * t / steps for t in range(steps + 1)]
        stems.add(*tube(path, radii, 5)[:2])
        for t in range(1, steps):
            for side in (-0.12, 0.12):        # leaves either side of the stem
                if rnd.random() < 0.3:
                    continue
                a = a0 + turns * math.tau * t / steps + side
                at = trunk_surface(path[t].z + rnd.uniform(-0.6, 0.6), a, 0.7)
                _, cx, cy, _, _, _, _, _ = trunk_at(at.z)
                ivy_clump(leaves, at, rnd.uniform(0.6, 1.0), rnd, Vector((at.x - cx, at.y - cy, 0)))
    for i in range(0, len(anchors), 2 if len(anchors) > 5 else 1):
        (a, ra), (b, rb) = anchors[i], anchors[(i + 1) % len(anchors)]
        a, b = a - Vector((0, 0, ra * 0.7)), b - Vector((0, 0, rb * 0.7))     # hung from the limbs' undersides
        sag = rnd.uniform(7, 12)
        mid = (a + b) / 2 - Vector((0, 0, sag * 2))
        path = [a * (1 - t) ** 2 + mid * 2 * t * (1 - t) + b * t * t for t in (k / 18 for k in range(19))]
        stems.add(*tube(path, [0.4] * len(path), 5)[:2])
        for k in range(2, 17, 2):
            ivy_clump(leaves, path[k] + Vector((0, 0, -0.4)), rnd.uniform(0.6, 0.9), rnd, Vector((0, 0, 1)))
    return stems, leaves


# ── Roots: fitted to the rock under one Plot ─────────────────────────────────
def load_island(path):
    """Returns rock (verts, faces) in Blender world axes, from the exporter's .obj (objects named rock|...)."""
    verts, faces, take = [], [], False
    with open(path) as f:
        for line in f:
            if line.startswith("o "):
                take = line[2:].startswith("rock|")
            elif line.startswith("v "):
                verts.append(Vector(map(float, line.split()[1:4])))
            elif line.startswith("f ") and take:
                faces.append(tuple(int(t.split("/")[0]) - 1 for t in line.split()[1:4]))
    used = sorted({i for f in faces for i in f})
    remap = {o: n for n, o in enumerate(used)}
    return [verts[i] for i in used], [tuple(remap[i] for i in f) for f in faces]


def plot_transform(plot):
    """Work frame → Blender-axes Garden space, as a Matrix (may mirror)."""
    (px, py, pz), yaw, outer = plot
    # Work (X out, Y back, Z up) → Unity Plot local: x = outer*X, y = Z, z = -1 - Y.
    work_to_local = Matrix(((outer, 0, 0, 0), (0, 0, 1, 0), (0, -1, 0, PLOT_CENTRE_Z), (0, 0, 0, 1)))
    c, s = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
    local_to_unity = Matrix(((c, 0, s, px), (0, 1, 0, py), (-s, 0, c, pz), (0, 0, 0, 1)))
    # Unity → Blender axes, as the FBX export maps them: (x, y, z)ᵤ → (−x, −z, y).
    unity_to_blender = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
    return unity_to_blender @ local_to_unity @ work_to_local, unity_to_blender @ work_to_local


ARCH_ROOTS = [   # start inside the trunk, leave it ~13 m up, bow out over the Plot, land halfway along it, dive in
    [Vector((-0.6, 6.5, 17.0)), Vector((-2.0, 2.8, 13.0)), Vector((-3.0, 1.0, 8.5)), Vector((-3.1, -0.2, 4.5)),
     Vector((-2.75, -0.3, 1.6)), Vector((-2.5, 0.0, 0.0)), Vector((-2.48, 0.3, -2.5))],
    [Vector((3.2, 6.5, 17.0)), Vector((4.2, 2.8, 13.0)), Vector((4.7, 1.0, 8.5)), Vector((4.0, -0.2, 4.5)),
     Vector((3.2, -0.3, 1.6)), Vector((2.8, 0.0, 0.0)), Vector((2.8, 0.3, -2.5))],
]
ARCH_RADII = (1.9, 0.34)    # at the soil the inner foot fits the 0.78 m strip between Plots


def outside(rock, centre, q):
    """The rock's outer surface level with q, seen from beyond the island; the rocks overlap, so the nearest
    surface is often one buried inside another rock. Returns (point, outward normal) or (None, None)."""
    h = Vector((q.x - centre.x, q.y - centre.y, 0))
    if h.length < 1e-3:
        return None, None
    h.normalize()
    hit, normal, _, _ = rock.ray_cast(Vector((centre.x, centre.y, q.z)) + h * 200, -h)
    if hit is None:
        return None, None
    return hit, (normal if normal.dot(h) > 0 else -normal)


def roots(rock, centre, lod):
    """Every root as (path, radii). centre: the island's vertical axis in the work frame."""
    out = []
    for pts in ARCH_ROOTS:
        path = smooth_path(pts, 2 if lod else 4)
        r0, r1 = ARCH_RADII
        out.append((path, [r0 + (r1 - r0) * (k / (len(path) - 1)) ** 0.7 for k in range(len(path))]))
    rnd = random.Random(11)
    # Fanned round the side facing the island: the trunk stands behind it, and a root leaving the far side finds no rock.
    starts = [(-3, -95), (-6, -65), (-9, -35), (-4, -10), (-12, 15), (-7, 40), (-16, 70), (-20, 100), (-24, 0), (-14, -50),
              (-18, 30)]
    for n, (z, deg) in enumerate(starts):
        _, cx, cy, rx, ryf, ryb, _, _ = trunk_at(z)
        a = math.atan2(centre.y - cy, centre.x - cx) + math.radians(deg)
        p = Vector((cx, cy, z))   # start on the trunk's axis, so the root's end stays buried
        d = (Vector((math.cos(a), math.sin(a), -1.0))).normalized()
        spin = 1 if n % 2 else -1
        path, step, count = [p], 1.6, rnd.randint(30, 42)
        r0 = rnd.uniform(1.9, 2.7)
        dangling, k = 0, 0
        while k < count:
            q = p + d * step
            _, tcx, tcy, trx, _, _, _, _ = trunk_at(q.z)
            if (Vector((q.x - tcx, q.y - tcy))).length < trx and len(path) < 14:     # still leaving the trunk: free
                path.append(q)
                p = q
                continue
            k += 1
            hit, normal = (None, None) if dangling else outside(rock, centre, q)
            if hit is None:
                # Nothing to hold on to (past the island's bottom, or never reached it): hang and sway.
                dangling += 1
                if dangling > 9:
                    break
                d = (d * 0.6 + Vector((0, 0, -1)) * 0.4
                     + Vector((math.sin(k * 0.9 + n), math.cos(k * 0.7 + n), 0)) * 0.12).normalized()
                path.append(q)
                p = q
                continue
            # Sit on the rock's outside, about three quarters proud; never climb onto the Garden's top.
            r = r0 * (1 - k / (count + 1)) ** 1.1
            target = hit + normal * r * 0.45
            q = q.lerp(target, min(1.0, 0.9 * step / max((target - q).length, 1e-3)))
            around = Vector((0, 0, 1)).cross(Vector((q.x - centre.x, q.y - centre.y, 0))).normalized() * spin
            want = (around + Vector((0, 0, -0.4))).normalized()     # wrap around more than down
            want = (want - normal * want.dot(normal)).normalized()
            d = (d * 0.55 + want * 0.45 + Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), 0)) * 0.08).normalized()
            q.z = min(q.z, -1.2)
            path.append(q)
            p = q
        # Taper over the length it actually reached, so every root ends in a point.
        radius = [max(0.1, r0 * (1 - i / len(path)) ** 1.1) for i in range(len(path))]
        if lod:
            path, radius = path[::2], radius[::2]
        if len(path) >= 10:
            out.append((path, radius))
    return out


BONE_EVERY = 4


def root_geo(root_list, lod, bones_out=None):
    """Tubes for the roots; with bones_out, also the bone chains and their weights."""
    g = Geo()
    for ri, (path, radii) in enumerate(root_list):
        weights = None
        if bones_out is not None:
            joints = path[::BONE_EVERY]
            if (len(path) - 1) % BONE_EVERY:
                joints.append(path[-1])
            names = [f"Root_{ri}_{b}" for b in range(len(joints) - 1)]
            bones_out += [(nm, joints[b], joints[b + 1], names[b - 1] if b else "RootBase")
                          for b, nm in enumerate(names)]
            weights = []
            for k in range(len(path)):
                f = min(k / BONE_EVERY, len(names) - 1e-3)
                b, t = int(f), f - int(f)
                w = [(names[b], 1 - t)]
                if t > 0 and b + 1 < len(names):
                    w.append((names[b + 1], t))
                weights.append(w)
        g.add(*tube(path, radii, 4 if lod else 6, bones=weights))
    return g


# ── Emit, export, preview ─────────────────────────────────────────────────────
def link(o):
    scene.collection.objects.link(o)
    return o


def emit(name, geo, mat, xf, mirrored):
    bm = geo.bm()
    bm.transform(xf)
    if mirrored:
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    for p in me.polygons:
        p.use_smooth = False
    ob = link(bpy.data.objects.new(name, me))
    if any(geo.groups):
        for vi, ws in enumerate(geo.groups):
            for bone, w in ws:
                vg = ob.vertex_groups.get(bone) or ob.vertex_groups.new(name=bone)
                vg.add([vi], w, "REPLACE")
    return ob


def emit_multi(name, parts, xf, mirrored):
    """One mesh from several (geo, material) parts."""
    obs = [emit(f"{name}_{i}", g, m, xf, mirrored) for i, (g, m) in enumerate(parts)]
    bpy.ops.object.select_all(action="DESELECT")
    for o in obs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    bpy.ops.object.join()
    obs[0].name = obs[0].data.name = name
    return obs[0]


def rig(name, bones, xf):
    arm = bpy.data.armatures.new(name)
    ob = link(bpy.data.objects.new(name, arm))
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode="EDIT")
    base = arm.edit_bones.new("RootBase")
    base.head = xf @ Vector((TOP.x, 5.0, -4.0))
    base.tail = xf @ Vector((TOP.x, 5.0, -1.0))
    made = {"RootBase": base}
    for nm, h, t, parent in bones:
        b = arm.edit_bones.new(nm)
        b.head, b.tail = xf @ h, xf @ t
        b.parent = made[parent]
        b.use_connect = parent != "RootBase"
        made[nm] = b
    bpy.ops.object.mode_set(mode="OBJECT")
    return ob


def check_outward(ob):
    """Fails the build if a part's faces mostly point inward (a negative signed volume)."""
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    volume = bm.calc_volume(signed=True)
    bm.free()
    if volume <= 0:
        raise RuntimeError(f"{ob.name}: normals point inward (signed volume {volume:.1f})")


def export(objs, path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", apply_unit_scale=True, bake_space_transform=True,
                             mesh_smooth_type="FACE", use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
                             primary_bone_axis="Y", secondary_bone_axis="X", armature_nodetype="NULL")


island_v, island_f = load_island(island_obj)
trunk_hi, trunk_lo = trunk(20, 2.0, 4.0), trunk(10, 5.0, 8.0)
wood_hi, crowns_hi, anchors = crown(False)
wood_lo, crowns_lo, _ = crown(True)
vine_hi, ivy_hi = vines(anchors)
rock_centre_unity = Vector((6.7, 0.0, 6.3))    # the island's vertical axis (the Dome's centre), Unity Garden space
stats = []
ONLY = int(sys.argv[sys.argv.index("--") + 4]) if len(sys.argv) > sys.argv.index("--") + 4 else None
PREVIEW_PLOT = ONLY or 0

for pi, plot in enumerate(PLOTS):
    if ONLY is not None and pi != ONLY:
        continue
    to_garden, to_local = plot_transform(plot)
    mirrored = to_local.determinant() < 0
    from_garden = to_garden.inverted()
    rock = BVHTree.FromPolygons([from_garden @ v for v in island_v], island_f)
    c = from_garden @ Vector((-rock_centre_unity.x, -rock_centre_unity.z, 0))
    hi_roots, lo_roots = roots(rock, c, False), roots(rock, c, True)

    trunk_g = Geo(); trunk_g.add(*trunk_hi)
    trunk_g2 = Geo(); trunk_g2.add(*trunk_lo)
    bones = []
    roots_hi = root_geo(hi_roots, False, bones)
    objs = [
        emit_multi("Trunk_LOD0", [(trunk_g, bark_mat), (wood_hi, bark_mat)], to_local, mirrored),
        *[emit_multi(f"Crown_{ci}_LOD0", [(d, leaf_mat), (l, leaf_light_mat)], to_local, mirrored)   # one per crown, so each can shed
          for ci, (d, l) in enumerate(crowns_hi)],
        emit_multi("Vines_LOD0", [(vine_hi, vine_mat), (ivy_hi, leaf_light_mat)], to_local, mirrored),
        emit_multi("Trunk_LOD1", [(trunk_g2, bark_mat), (wood_lo, bark_mat), (root_geo(lo_roots, True), bark_mat)],
                   to_local, mirrored),
        emit_multi("Canopy_LOD1", [(g, leaf_mat if k == 0 else leaf_light_mat) for pair in crowns_lo for k, g in enumerate(pair)],
                   to_local, mirrored),
    ]
    roots_ob = emit("Roots_LOD0", roots_hi, bark_mat, to_local, mirrored)
    pivot = link(bpy.data.objects.new("Pivot", None))    # the trunk's base on the soil: the tree grows and topples about it
    pivot.location = to_local @ Vector((TOP.x, TOP.y, 0))
    arm = rig("RootRig", bones, to_local)
    roots_ob.parent = arm
    roots_ob.modifiers.new("Rig", "ARMATURE").object = arm
    for o in (roots_ob, *objs):
        check_outward(o)
    export([arm, roots_ob, pivot, *objs], f"{out_dir}/Yggdrasil_Plot{pi}.fbx")
    stats.append((pi, sum(len(o.data.polygons) for o in objs if o.name.endswith("LOD0")) + len(roots_ob.data.polygons),
                  sum(len(o.data.polygons) for o in objs if o.name.endswith("LOD1")), len(bones)))
    if pi == PREVIEW_PLOT:
        keep = (arm, roots_ob, *objs)
        for o in (*keep, pivot):
            o.name += "_Preview"    # frees the names for the next variant
    else:
        for o in (arm, roots_ob, pivot, *objs):
            bpy.data.objects.remove(o, do_unlink=True)


# ── The falling leaf: an ash leaflet, both faces, 0.5 m ──────────────────────
bm = bmesh.new()
outline = [(0, 0), (0.05, 0.08), (0.075, 0.2), (0.06, 0.34), (0.0, 0.5), (-0.06, 0.34), (-0.075, 0.2), (-0.05, 0.08)]
vs = [bm.verts.new((x, y - 0.25, 0.02 * math.sin(y * 6))) for x, y in outline]
bm.faces.new(vs)
bmesh.ops.triangulate(bm, faces=bm.faces[:])
dup = bmesh.ops.duplicate(bm, geom=bm.faces[:])
bmesh.ops.reverse_faces(bm, faces=[e for e in dup["geom"] if isinstance(e, bmesh.types.BMFace)])
me = bpy.data.meshes.new("Yggdrasil_Leaf")
bm.to_mesh(me)
bm.free()
me.materials.append(leaf_mat)
leaf = link(bpy.data.objects.new("Yggdrasil_Leaf", me))
export([leaf], f"{out_dir}/Yggdrasil_Leaf.fbx")


# ── The seed: a samara, a wing on a pod ringed with glowing runes, 1 unit long ─
def seed():
    parts = []
    # Pod: a slim ellipsoid at the bottom.
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=7, radius=1)
    for v in bm.verts:
        v.co = Vector((v.co.x * 0.075, v.co.y * 0.06, v.co.z * 0.15 - 0.33))
    parts.append((bm, pod_mat))
    # Wing: one straight edge, one bellied, slightly twisted, thin.
    bm = bmesh.new()
    rows = 9
    left, right = [], []
    for i in range(rows + 1):
        t = i / rows
        z = -0.22 + t * 0.72
        width = 0.19 * math.sin(math.pi * t) ** 0.6 + 0.012
        twist = t * 0.35
        edge = -0.03 + 0.07 * t ** 3                 # the straight edge curls in to a rounded tip
        for side, lst in ((edge, left), (edge + width, right)):
            x, y = side * math.cos(twist), side * math.sin(twist)
            lst.append((bm.verts.new((x, y + 0.006, z)), bm.verts.new((x, y - 0.006, z))))
    for i in range(rows):
        bm.faces.new((left[i][0], right[i][0], right[i + 1][0], left[i + 1][0]))
        bm.faces.new((left[i + 1][1], right[i + 1][1], right[i][1], left[i][1]))
        bm.faces.new((left[i][1], left[i][0], left[i + 1][0], left[i + 1][1]))
        bm.faces.new((right[i][0], right[i][1], right[i + 1][1], right[i + 1][0]))
    bm.faces.new((left[0][0], left[0][1], right[0][1], right[0][0]))
    bm.faces.new((left[-1][1], left[-1][0], right[-1][0], right[-1][1]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    parts.append((bm, wing_mat))
    # Runes: five Elder Futhark glyphs as raised strokes round the pod's waist.
    glyphs = {
        "algiz": [((0, -1), (0, 1)), ((0, 0.2), (-0.6, 1)), ((0, 0.2), (0.6, 1))],
        "fehu": [((-0.3, -1), (-0.3, 1)), ((-0.3, 0.1), (0.4, 0.6)), ((-0.3, 0.6), (0.4, 1.0))],
        "ansuz": [((-0.3, -1), (-0.3, 1)), ((-0.3, 1.0), (0.4, 0.4)), ((-0.3, 0.4), (0.4, -0.2))],
        "tiwaz": [((0, -1), (0, 1)), ((0, 1), (-0.55, 0.4)), ((0, 1), (0.55, 0.4))],
        "dagaz": [((-0.6, -1), (-0.6, 1)), ((0.6, -1), (0.6, 1)), ((-0.6, 1), (0.6, -1)), ((-0.6, -1), (0.6, 1))],
    }
    bm = bmesh.new()
    for gi, strokes in enumerate(glyphs.values()):
        a = gi / len(glyphs) * math.tau
        for (x0, y0), (x1, y1) in strokes:
            h0 = Vector((x0 * 0.025, 0, y0 * 0.04))
            h1 = Vector((x1 * 0.025, 0, y1 * 0.04))
            d = (h1 - h0)
            n = Vector((0, -1, 0)).cross(d).normalized() * 0.0045
            quad = [h0 - n, h1 - n, h1 + n, h0 + n]
            out, top = [], []
            for q in quad:
                # Wrap onto the pod's waist: x along the circumference, z up the pod.
                ang = a + q.x / 0.072
                r = 0.072
                base = Vector((math.cos(ang) * r * 1.04, math.sin(ang) * r * 0.83, q.z - 0.33))
                rad = Vector((math.cos(ang), math.sin(ang), 0))
                out.append(bm.verts.new(base))
                top.append(bm.verts.new(base + rad * 0.006))
            bm.faces.new(top)
            for k in range(4):
                bm.faces.new((out[k], out[(k + 1) % 4], top[(k + 1) % 4], top[k]))
    parts.append((bm, rune_mat))
    obs = []
    for i, (b, m) in enumerate(parts):
        me = bpy.data.meshes.new(f"Seed_{i}")
        b.to_mesh(me)
        b.free()
        me.materials.append(m)
        obs.append(link(bpy.data.objects.new(f"Seed_{i}", me)))
    bpy.ops.object.select_all(action="DESELECT")
    for o in obs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    bpy.ops.object.join()
    s = obs[0]
    s.name = s.data.name = "Yggdrasil_Seed"
    # Pivot at its centre, 1 unit long.
    bb = [Vector(c) for c in s.bound_box]
    mid = sum(bb, Vector()) / 8
    length = max(c.z for c in bb) - min(c.z for c in bb)
    s.data.transform(Matrix.Scale(1 / length, 4) @ Matrix.Translation(-mid))
    return s


seed_ob = seed()
check_outward(seed_ob)


# ── The sapling: what grows in the slot before the tree is built, 1.2 m, pivot at its base ──
def sapling():
    rnd = random.Random(5)
    wood, leaves = Geo(), Geo()
    stem = smooth_path([Vector((0, 0, -0.05)), Vector((0.03, 0.01, 0.4)), Vector((-0.02, 0.0, 0.8)), Vector((0.01, -0.02, 1.2))], 4)
    wood.add(*tube(stem, [0.045 - 0.035 * k / (len(stem) - 1) for k in range(len(stem))], 6)[:2])
    tips = [stem[-1]]
    for k, (a, h) in enumerate(((0.4, 0.55), (2.6, 0.75), (4.5, 0.95))):
        b0 = stem[int(len(stem) * h)]
        tip = b0 + Vector((math.cos(a) * 0.32, math.sin(a) * 0.32, 0.2))
        b = smooth_path([b0, b0.lerp(tip, 0.5) + Vector((0, 0, 0.05)), tip], 2)
        wood.add(*tube(b, [0.018 - 0.012 * q / (len(b) - 1) for q in range(len(b))], 5)[:2])
        tips.append(tip)
    for tip in tips:
        for q in range(5):    # an ash leaf: leaflets in pairs along a stalk, as low pads
            ivy_clump(leaves, tip + Vector((rnd.uniform(-0.12, 0.12), rnd.uniform(-0.12, 0.12), rnd.uniform(-0.06, 0.1))),
                      rnd.uniform(0.07, 0.1), rnd, Vector((rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3), 1)))
    ob = emit_multi("Yggdrasil_Sapling", [(wood, bark_mat), (leaves, leaf_light_mat)], Matrix.Identity(4), False)
    check_outward(ob)
    return ob


sapling_ob = sapling()
export([sapling_ob], f"{out_dir}/Yggdrasil_Sapling.fbx")
sapling_ob.hide_render = True
export([seed_ob], f"{out_dir}/Yggdrasil_Seed.fbx")

for pi, hi, lo, nb in stats:
    print(f"YGGDRASIL plot {pi}: LOD0 {hi} faces, LOD1 {lo} faces, {nb} root bones")

# ── Previews: one Plot's tree (default 0; a 4th argument picks another) on the island, close and far; the seed ──────────
if preview_dir != "-":
    arm, roots_ob, *parts = keep
    for o in parts:
        o.hide_render = "LOD1" in o.name
    isl = bpy.data.meshes.new("Island")
    isl.from_pydata([v for v in load_island(island_obj)[0]], [], load_island(island_obj)[1])
    island = link(bpy.data.objects.new("Island", isl))
    island.data.materials.append(material("Preview_Rock", (0.42, 0.42, 0.45), 1.0))
    # Plots and the rest of the Garden, for scale.
    take = False
    with open(island_obj) as f:
        gv, gf = [], []
        for line in f:
            if line.startswith("v "):
                gv.append(Vector(map(float, line.split()[1:4])))
            elif line.startswith("o "):
                take = not line[2:].startswith("rock|")
            elif line.startswith("f ") and take:
                gf.append(tuple(int(t) - 1 for t in line.split()[1:4]))
    gm = bpy.data.meshes.new("Garden")
    gm.from_pydata(gv, [], gf)
    garden = link(bpy.data.objects.new("Garden", gm))
    garden.data.materials.append(material("Preview_Garden", (0.45, 0.6, 0.35), 1.0))
    # The tree was emitted in Plot 0's local space; put it back in Garden space for the preview.
    to_garden, to_local = plot_transform(PLOTS[PREVIEW_PLOT])
    for o in (arm, *parts):
        o.matrix_world = to_garden @ to_local.inverted()
    seed_ob.location = (40, 0, 0)
    cam = link(bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")))
    scene.camera = cam
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.render.resolution_x = scene.render.resolution_y = 900
    centre = to_garden @ Vector((0, 0, 0))

    def shot(name, frm, at, lens=30):
        cam.location = frm
        cam.data.lens = lens
        cam.rotation_euler = (Vector(at) - Vector(frm)).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = f"{preview_dir}/{name}.png"
        bpy.ops.render.render(write_still=True)

    road = to_garden @ Vector((0, -12, 3)) - centre
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    shot("ygg_road", to_garden @ Vector((0.3, -7, 1.7)), to_garden @ Vector((0.3, 2, 7)), 18)
    shot("ygg_far", centre + road * 22 + Vector((0, 0, -20)), centre + Vector((0, 0, 40)), 30)
    shot("ygg_town", to_garden @ Vector((-150, -190, 20)), to_garden @ Vector((0, 0, 35)), 30)
    shot("ygg_top", to_garden @ Vector((2, 1, 300)), to_garden @ Vector((2, 11, 60)), 24)
    shot("ygg_cliff", centre + (to_garden @ Vector((25, 45, -25)) - centre), centre + Vector((0, 0, -10)), 26)
    shot("ygg_under", to_garden @ Vector((-45, 20, -40)), to_garden @ Vector((-5, 3, -18)), 24)
    shot("ygg_side", to_garden @ Vector((14, -10, 4)), to_garden @ Vector((0.3, 1.5, 6)), 22)
    seed_ob.location = (0, 0, 0)
    for o in scene.objects:
        o.hide_render = o is not seed_ob
    seed_ob.color = (0.7, 0.6, 0.4, 1)
    scene.display.shading.color_type = "MATERIAL"
    shot("ygg_seed", Vector((1.0, -1.6, 0.3)), Vector((0, 0, 0)), 50)
print("YGGDRASIL done")
