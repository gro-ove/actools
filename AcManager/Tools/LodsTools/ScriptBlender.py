#!/usr/bin/env python3
# Embedded Blender helper. We launch headless Blender once per LOD stage as
#   blender --background --factory-startup --python <script> --python-exit-code 1 -- <args>
# Blender ships its own Python with bpy bundled, so unlike the MeshLab path we never need
# to pip install anything.
#
# Why FBX in/out (UseFbx = true): Blender's FBX importer/exporter is well-tested and
# produces layer element layouts kn5's LodFbxToKn5 already handles cleanly, so we can skip
# the DAE round trip + FbxConverter step entirely.
#
# Per-mesh decimation curve mirrors MeshLab's: a 10-triangle frame piece must not be told
# to drop to 2.

import bpy
import sys
import math
import argparse
import traceback
import bmesh
from mathutils.bvhtree import BVHTree

def err(msg):
    print(msg, file=sys.stderr, flush=True)

def progress(value):
    # Numeric stdout lines are interpreted as 0..100 progress by the host.
    print('{0:.2f}'.format(value), flush=True)

PROTECTION_VG = 'ACM_DecimateProtect'

# Meshes whose p90/p10 face-area ratio exceeds this are considered high-variance and
# get their collapse target floored to --high-variance-min-keep.
HIGH_VARIANCE_MESH_THRESHOLD = 100.0

def parse_args():
    if '--' in sys.argv:
        argv = sys.argv[sys.argv.index('--') + 1:]
    else:
        argv = []
    p = argparse.ArgumentParser()
    p.add_argument('--input', required=True)
    p.add_argument('--output', required=True)
    p.add_argument('--target-perc', type=float, default=0.5)
    p.add_argument('--min-keep', type=int, default=20)
    p.add_argument('--mode', default='collapse', choices=('collapse', 'planar', 'mixed'))
    p.add_argument('--planar-angle', type=float, default=5.0)
    p.add_argument('--delimit-uv', action='store_true')
    p.add_argument('--normal-weight', type=int, default=50,
                   help='Bias (1-100) for the post-decimation area-weighted vertex normal '
                        'recompute. 50 is balanced; higher pushes more toward large faces.')
    p.add_argument('--sharp-angle', type=float, default=30.0,
                   help='Dihedral angle in degrees beyond which an edge is treated as hard '
                        'during the area-weighted normal recompute. Edges below this stay smooth.')
    p.add_argument('--weighted-normals', action='store_true',
                   help='Skip the post-decimation area-weighted normal recompute entirely. '
                        'Use this if your artist-authored custom split normals must survive '
                        'unchanged through Blender.')
    p.add_argument('--min-target-perc', type=float, default=0.0,
                   help='Floor every mesh\'s keep ratio at this fraction (0 = disabled). '
                        'Optional manual cap.')
    p.add_argument('--boundary-corner-protect', action='store_true',
                   help='Disable vertex-group weighting that shields boundary corners from '
                        'decimation.')
    p.add_argument('--area-variance-threshold', type=float, default=80.0,
                   help='Per-vertex face-area ratio (max/min adjacent face) above which the '
                        'vertex is added to the protection group. Protects vertices that sit '
                        'between tiny and large triangles, which are the primary cause of '
                        'collapse "explosions".')
    p.add_argument('--high-variance-min-keep', type=float, default=0.05,
                   help='For meshes whose p90/p10 face-area ratio exceeds '
                        + str(int(HIGH_VARIANCE_MESH_THRESHOLD)) + ', floor the collapse '
                        'keep ratio at this fraction. Protects internal cover/filler meshes '
                        'with inherently uneven triangles from over-decimation. '
                        'Default 0.7 (keep at least 70%%). Set 0 to disable.')
    p.add_argument('--weld-distance', type=float, default=0.0,
                   help='After decimation and normal recompute, merge vertices closer than '
                        'this distance (scene units, metres by default). 0 disables. '
                        'Use 0.001 for 1 mm. Runs per-mesh after all other passes.')
    p.add_argument('--transfer-normals', action='store_true',
                   help='Instead of recomputing normals from the decimated geometry, sample '
                        'the original mesh\'s custom split normals via BVH lookup and '
                        'barycentric interpolation. Preserves artist-authored shading '
                        'including hard edges. Takes precedence over --no-weighted-normals.')
    p.add_argument('--max-vertex-drift', type=float, default=0.05,
                   help='After each collapse pass, measure the maximum distance any output '
                        'vertex has moved from the nearest point on the pre-collapse surface '
                        'using a BVH tree. If the worst vertex exceeds this limit the entire '
                        'collapse is reverted and the mesh is left at its pre-collapse state. '
                        '0 disables. Use e.g. 0.05 for a 5 cm safety margin.')
    return p.parse_args(argv)

def compute_target_faces(face_count, target_perc, min_keep=20):
    # Same nonlinear curve we use for MeshLab so small detail meshes survive aggressive global
    # rates. See MeshLabToolDetails.cs for the full derivation.
    face_count = int(face_count)
    if face_count <= 0:
        return 0
    target_perc = max(0.001, min(1.0, float(target_perc)))
    if target_perc >= 1.0:
        return face_count
    threshold = max(4.0, float(min_keep) / target_perc)
    if face_count >= threshold:
        actual_perc = target_perc
    else:
        t = face_count / threshold
        actual_perc = 1.0 - t * (1.0 - target_perc)
    target = int(round(face_count * actual_perc))
    return max(min(face_count, 4), min(face_count, target))

def deselect_all():
    try:
        bpy.ops.object.select_all(action='DESELECT')
    except RuntimeError:
        # No objects yet, ignore.
        pass

def apply_modifier(obj, mod_name):
    deselect_all()
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod_name)

def apply_weighted_normals(obj, weight, sharp_angle_deg):
    # Re-derive vertex normals using face-area weighting. This is the Blender equivalent of
    # MeshLab's compute_normal_per_vertex(weightmode='By Area') and exists for the same reason:
    # when decimation collapses thin smoothing rows (e.g. the rounded edge of a door), the
    # default simple-average normal recompute lets the few surviving sliver triangles tilt the
    # normal of every nearby vertex, so a flat door ends up looking spherical. Area weighting
    # lets the big flat triangles dominate and the panel stays flat.
    #
    # CRITICAL: we must NOT force every polygon to smooth shading and we must NOT pin
    # auto_smooth_angle to 180 degrees - doing either erases the dihedral information the
    # WEIGHTED_NORMAL modifier uses to decide what to keep sharp, and the modifier then
    # overwrites every custom split normal with a single averaged value per vertex. That is
    # what was producing the 'spherical door' look. We respect whatever the FBX importer
    # brought in for per-face smooth flags and just configure auto-smooth with a real angle
    # so hard edges (door panel boundaries, wheel arches, etc.) survive the recompute.
    if not obj.data.polygons:
        return

    # Blender 3.x requires auto-smooth to be enabled for custom split normals to take effect,
    # and the angle controls which edges count as hard. In Blender 4.x the property was
    # removed and the modifier reads sharp-edge attributes directly, so we just swallow the
    # AttributeError there.
    try:
        obj.data.use_auto_smooth = True
        obj.data.auto_smooth_angle = math.radians(max(1.0, min(180.0, float(sharp_angle_deg))))
    except (AttributeError, TypeError):
        pass

    deselect_all()
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    wn = obj.modifiers.new(name='ACMWeightedNormal', type='WEIGHTED_NORMAL')
    wn.mode = 'FACE_AREA'
    wn.weight = max(1, min(100, int(weight)))
    wn.keep_sharp = True
    wn.use_face_influence = False
    apply_modifier(obj, wn.name)

def remove_vertex_group_if_exists(obj, name):
    if obj and name in obj.vertex_groups:
        obj.vertex_groups.remove(obj.vertex_groups[name])

# ---------------------------------------------------------------------------
# Protection vertex group
# ---------------------------------------------------------------------------

def collect_boundary_corner_weights(obj):
    # Walk manifold boundary loops: at each boundary vertex with two incident boundary edges,
    # measure the turning angle between (prev->v) and (v->next). ~0 on a subdivided straight
    # rim; large at real corners. Returns {vertex_index: weight}.
    mesh = obj.data
    if not mesh.polygons:
        return {}
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.verts.ensure_lookup_table()
    bm.edges.ensure_lookup_table()
    if not any(e.is_boundary for e in bm.edges):
        bm.free()
        return {}
    min_a = math.radians(10)
    protect = {}
    for v in bm.verts:
        if not v.is_boundary:
            continue
        bedges = [e for e in v.link_edges if e.is_boundary]
        if len(bedges) != 2:
            continue
        a = bedges[0].other_vert(v)
        b = bedges[1].other_vert(v)
        la = (v.co - a.co).length
        lb = (b.co - v.co).length
        if la < 1e-12 or lb < 1e-12:
            continue
        d1 = (v.co - a.co) / la
        d2 = (b.co - v.co) / lb
        maxDistance = 0.2 if d1.angle(d2) < min_a else 0.05
        if la > maxDistance or lb > maxDistance:
            protect[v.index] = 1.0
    bm.free()
    return protect

def collect_area_variance_weights(obj, area_ratio_threshold):
    # For each vertex, compare its largest and smallest adjacent face areas. When the ratio
    # exceeds area_ratio_threshold the vertex sits on the boundary between coarse and fine
    # geometry -- exactly where collapse tends to spike: it collapses the small triangle by
    # moving its vertex into the plane of the large neighbour, producing a huge stretched face.
    # Protecting these vertices keeps the tiny triangles intact and prevents the spike.
    # Returns {vertex_index: weight} with weight in (0, 1].
    mesh = obj.data
    if not mesh.polygons:
        return {}
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.verts.ensure_lookup_table()
    protect = {}
    log_scale = math.log(area_ratio_threshold + 1.0)
    for v in bm.verts:
        areas = [f.calc_area() for f in v.link_faces]
        if len(areas) < 2:
            continue
        min_a = min(areas)
        max_a = max(areas)
        if min_a < 1e-12:
            # Degenerate adjacent face: always protect.
            protect[v.index] = 1.0
            continue
        ratio = max_a / min_a
        if ratio >= area_ratio_threshold:
            # Logarithmic weight: ratio == threshold -> small weight; ratio >> threshold -> 1.
            weight = min(1.0, math.log(ratio / area_ratio_threshold + 1.0) / log_scale)
            protect[v.index] = max(protect.get(v.index, 0.0), weight)
    bm.free()
    return protect

def mesh_area_variance_ratio(obj):
    # Returns the p90/p10 ratio of face areas. High values indicate a mesh with very uneven
    # triangle sizes (e.g. internal cover meshes) that collapse decimation handles poorly.
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    areas = sorted(f.calc_area() for f in bm.faces)
    bm.free()
    n = len(areas)
    if n < 10:
        return 1.0
    p10 = areas[n // 10]
    p90 = areas[int(n * 0.9)]
    if p10 < 1e-12:
        return 1000.0
    return p90 / p10

def build_protection_vertex_group(obj, args):
    # Merge boundary-corner weights and area-variance weights into one vertex group.
    # The Decimate modifier only accepts a single vertex group, so we take the max weight
    # from both sources at each vertex.
    corner_w = collect_boundary_corner_weights(obj) if args.boundary_corner_protect else {}
    area_w = collect_area_variance_weights(obj, args.area_variance_threshold)

    combined = dict(corner_w)
    for idx, w in area_w.items():
        combined[idx] = max(combined.get(idx, 0.0), w)

    if not combined:
        return None

    remove_vertex_group_if_exists(obj, PROTECTION_VG)
    vg = obj.vertex_groups.new(name=PROTECTION_VG)
    for idx, w in combined.items():
        vg.add([idx], w, 'REPLACE')
    return PROTECTION_VG

def weld_vertices(obj, distance):
    # Merge vertices closer than `distance` using bmesh remove_doubles. Runs after normals
    # so the geometry is already final; at weld sites Blender averages the existing split
    # normals, which is acceptable for LOD use. bmesh is used directly (rather than the WELD
    # modifier) so this works on Blender 2.83+.
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=distance)
    bm.to_mesh(mesh)
    mesh.update()
    bm.free()

def build_bvh(obj):
    # Build a BVHTree from the current mesh state. Used to measure how far each vertex of the
    # decimated result has drifted from the original surface.
    mesh = obj.data
    verts = [v.co.copy() for v in mesh.vertices]
    polys = [list(p.vertices) for p in mesh.polygons]
    return BVHTree.FromPolygons(verts, polys, all_triangles=False)

def max_vertex_drift(bvh, obj):
    # For each vertex in obj, find the nearest point on bvh and return the worst distance.
    # O(V log N) where V = decimated vertex count and N = original triangle count.
    worst = 0.0
    for v in obj.data.vertices:
        _loc, _nor, _idx, dist = bvh.find_nearest(v.co)
        if dist is not None and dist > worst:
            worst = dist
    return worst

def build_normal_bvh(obj):
    # Capture the mesh's custom split normals into a BVH for later transfer onto the
    # decimated result. We fan-triangulate every face upfront so the BVH index always
    # maps 1:1 to a triangle, making barycentric lookup unambiguous for quads/ngons.
    # Returns (bvh, tri_data) where tri_data[i] = ((v0,v1,v2), (n0,n1,n2)).
    mesh = obj.data
    if not mesh.polygons:
        return None, None
    # calc_normals_split() was removed in Blender 4.1; split normals are always current there.
    try:
        mesh.calc_normals_split()
    except AttributeError:
        pass
    tri_verts = []
    tri_data  = []
    for poly in mesh.polygons:
        pverts  = [mesh.vertices[vi].co.copy() for vi in poly.vertices]
        pnormals = [mesh.loops[li].normal.copy() for li in poly.loop_indices]
        for i in range(1, len(pverts) - 1):
            v0, v1, v2 = pverts[0], pverts[i], pverts[i + 1]
            n0, n1, n2 = pnormals[0], pnormals[i], pnormals[i + 1]
            tri_verts.extend([v0, v1, v2])
            tri_data.append(((v0, v1, v2), (n0, n1, n2)))
    flat_polys = [(i * 3, i * 3 + 1, i * 3 + 2) for i in range(len(tri_data))]
    bvh = BVHTree.FromPolygons(tri_verts, flat_polys, all_triangles=True)
    return bvh, tri_data

def barycentric_weights(p, a, b, c):
    # Möller–Trumbore-style barycentric coords of p projected onto triangle (a,b,c).
    # Returns (u, v, w) with u+v+w = 1. Falls back to centroid for degenerate triangles.
    v0 = b - a;  v1 = c - a;  v2 = p - a
    d00 = v0.dot(v0);  d01 = v0.dot(v1);  d11 = v1.dot(v1)
    d20 = v2.dot(v0);  d21 = v2.dot(v1)
    denom = d00 * d11 - d01 * d01
    if abs(denom) < 1e-12:
        return 1.0 / 3, 1.0 / 3, 1.0 / 3
    v = (d11 * d20 - d01 * d21) / denom
    w = (d00 * d21 - d01 * d20) / denom
    return 1.0 - v - w, v, w

def transfer_normals_from_bvh(bvh, tri_data, obj):
    # For every loop in the decimated mesh, find the nearest original triangle,
    # barycentrically interpolate its three split-normal corners, and write the
    # result as a custom split normal. Per-loop lookup means hard edges survive:
    # two loops of the same vertex query independently and can land on different
    # original triangles with different normals, preserving the sharp crease.
    mesh = obj.data
    if not mesh.polygons or not mesh.loops or not tri_data:
        return
    try:
        mesh.use_auto_smooth = True
    except (AttributeError, TypeError):
        pass
    n_loops = len(mesh.loops)
    custom_normals = [None] * n_loops
    for poly in mesh.polygons:
        for li in poly.loop_indices:
            loop  = mesh.loops[li]
            v_co  = mesh.vertices[loop.vertex_index].co
            loc, _nor, tri_idx, dist = bvh.find_nearest(v_co)
            if tri_idx is None or tri_idx >= len(tri_data):
                custom_normals[li] = poly.normal.copy()
                continue
            (v0, v1, v2), (n0, n1, n2) = tri_data[tri_idx]
            u, v, w = barycentric_weights(loc, v0, v1, v2)
            # Clamp to [0,1] to guard against tiny floating-point overshoots.
            u = max(0.0, u);  v = max(0.0, v);  w = max(0.0, w)
            n = u * n0 + v * n1 + w * n2
            length = n.length
            custom_normals[li] = (n / length) if length > 1e-6 else poly.normal.copy()
    mesh.normals_split_custom_set(custom_normals)
    mesh.update()

def try_assign_decimate_vertex_group(mod, vg_name):
    # RNA: vertex_group / invert are COLLAPSE only. Never set them on DISSOLVE (planar) —
    # Blender still accepts the fields but behavior is undefined and can destroy the mesh.
    if not vg_name or getattr(mod, 'decimate_type', None) != 'COLLAPSE':
        return
    if hasattr(mod, 'vertex_group'):
        try:
            mod.vertex_group = vg_name
            if hasattr(mod, 'invert_vertex_group'):
                # True => decimate prefers vertices *outside* the group, preserving corners in it.
                mod.invert_vertex_group = True
            if hasattr(mod, 'vertex_group_factor'):
                mod.vertex_group_factor = 1.0
        except Exception:
            pass

def main():
    args = parse_args()

    # Wipe scene so we start from a known empty state regardless of user prefs.
    bpy.ops.wm.read_factory_settings(use_empty=True)

    try:
        # Match the importer's axis convention on export to keep round-trip identity.
        bpy.ops.import_scene.fbx(
                filepath=args.input,
                axis_forward='-Z',
                axis_up='Y',
                use_custom_normals=True,
                use_image_search=False,
                automatic_bone_orientation=False,
                ignore_leaf_bones=True)
    except Exception as e:
        err('FbxImportError: ' + str(e))
        traceback.print_exc(file=sys.stderr)
        sys.exit(2)

    meshes = [obj for obj in bpy.data.objects if obj.type == 'MESH']
    if not meshes:
        err('NoMeshesError: input FBX contains no mesh objects')
        sys.exit(3)

    err('Blender: decimating {0} meshes (target keep ratio {1:.2f}%, mode={2})'.format(
            len(meshes), args.target_perc * 100, args.mode))

    # Delimit options: the Decimate modifier can be told to never collapse across these
    # boundaries. UV/MATERIAL delimiters are usually what you want for cars - they keep texture
    # seams and material splits intact.
    delimit_collapse = {'SEAM', 'SHARP'}
    delimit_planar = {'NORMAL', 'SEAM', 'SHARP'}
    if args.delimit_uv:
        delimit_collapse.add('UV')
        delimit_planar.add('UV')

    total = len(meshes)
    for idx, obj in enumerate(meshes):
        try:
            face_count = len(obj.data.polygons)
            if face_count <= 0:
                progress((idx + 1) * 100.0 / total)
                continue

            target = compute_target_faces(face_count, args.target_perc, args.min_keep)
            if args.min_target_perc > 0:
                floor_faces = int(math.ceil(
                        face_count * max(0.001, min(1.0, float(args.min_target_perc)))))
                target = max(target, floor_faces)
            target = min(target, face_count)
            if target >= face_count:
                # Mesh is not being decimated. Leave its normals strictly alone so the
                # artist-authored custom split normals (which encode the hard edges on door
                # panels, wheel arches, etc.) survive untouched. Running the weighted-normal
                # recompute here would overwrite those split normals with face-area averages
                # for no benefit.
                progress((idx + 1) * 100.0 / total)
                continue

            # Snapshot the original split normals before ANY pass (planar included) so that
            # --transfer-normals always samples the true artist-authored shading data.
            normal_bvh      = None
            normal_tri_data = None
            if args.transfer_normals:
                normal_bvh, normal_tri_data = build_normal_bvh(obj)

            # Planar pass first when requested (or in mixed mode). Dissolves coplanar geometry
            # without changing silhouette - shines on flat car panels with smoothing rows on the
            # edges that artists added for shading.
            if args.mode in ('planar', 'mixed'):
                deselect_all()
                obj.select_set(True)
                bpy.context.view_layer.objects.active = obj
                planar = obj.modifiers.new(name='ACMPlanar', type='DECIMATE')
                planar.decimate_type = 'DISSOLVE'
                planar.angle_limit = math.radians(max(0.1, float(args.planar_angle)))
                planar.delimit = delimit_planar
                planar.use_dissolve_boundaries = False
                apply_modifier(obj, planar.name)

                # Triangulate to get the triangle count right.
                deselect_all()
                obj.select_set(True)
                bpy.context.view_layer.objects.active = obj
                tri = obj.modifiers.new(name='ACMTriDis', type='TRIANGULATE')
                tri.quad_method = 'BEAUTY'
                tri.ngon_method = 'BEAUTY'
                apply_modifier(obj, tri.name)

            # Collapse pass to hit the exact target count (skipped in pure-planar mode).
            if args.mode in ('collapse', 'mixed'):
                face_count = len(obj.data.polygons)
                if face_count > target:
                    # Snapshot the pre-collapse surface so we can measure vertex drift afterwards.
                    # The BVH is built from the post-planar geometry (planar never moves vertices,
                    # only dissolves coplanar edges, so the surface is identical). We only build
                    # it when the check is actually enabled to avoid the overhead on every mesh.
                    bm_snap = None
                    snap_bvh = None
                    if args.max_vertex_drift > 0:
                        bm_snap = bmesh.new()
                        bm_snap.from_mesh(obj.data)
                        snap_bvh = build_bvh(obj)

                    # High-variance mesh detection: if p90/p10 face area ratio is extreme this
                    # mesh probably has internal cover geometry with very uneven triangles. Clamp
                    # the collapse target so we don't over-decimate it and risk spikes.
                    if face_count < 2000 and args.high_variance_min_keep > 0:
                        var_ratio = mesh_area_variance_ratio(obj)
                        if var_ratio > HIGH_VARIANCE_MESH_THRESHOLD:
                            hv_floor = int(math.ceil(face_count * args.high_variance_min_keep))
                            if hv_floor > target:
                                err('  {}: high area variance ({:.0f}x p90/p10), '
                                    'flooring keep at {:.0%} ({} -> {} faces)'.format(
                                    obj.name, var_ratio, args.high_variance_min_keep,
                                    target, hv_floor))
                                target = hv_floor

                    ratio = max(0.001, min(1.0, target / face_count))
                    keepTrying = True
                    if ratio < 0.1:
                        protect_vg = build_protection_vertex_group(obj, args)
                        deselect_all()
                        obj.select_set(True)
                        bpy.context.view_layer.objects.active = obj
                        collapse = obj.modifiers.new(name='ACMCollapsePre', type='DECIMATE')
                        collapse.decimate_type = 'COLLAPSE'
                        collapse.ratio = 0.95
                        collapse.use_collapse_triangulate = True
                        collapse.delimit = delimit_collapse
                        try_assign_decimate_vertex_group(collapse, protect_vg)
                        apply_modifier(obj, collapse.name)
                        remove_vertex_group_if_exists(obj, PROTECTION_VG)
                        deselect_all()
                        obj.select_set(True)
                        bpy.context.view_layer.objects.active = obj
                        tri = obj.modifiers.new(name='ACMTriPre', type='TRIANGULATE')
                        tri.quad_method = 'BEAUTY'
                        tri.ngon_method = 'BEAUTY'
                        apply_modifier(obj, tri.name)
                        face_count = len(obj.data.polygons)

                        if snap_bvh is not None:
                            drift = max_vertex_drift(snap_bvh, obj)
                            if drift > args.max_vertex_drift:
                                err('  {}: pre-collapse reverted — max vertex drift {:.4f} m '
                                    'exceeds limit {:.4f} m'.format(
                                    obj.name, drift, args.max_vertex_drift))
                                bm_snap.to_mesh(obj.data)
                                obj.data.update()
                                keepTrying = False
                            else:
                                err('  {}: max vertex drift {:.4f} m OK'.format(obj.name, drift))
                            bm_snap.free()

                    for attempt in range(4):
                        if keepTrying:
                            protect_vg = build_protection_vertex_group(obj, args)
                            deselect_all()
                            obj.select_set(True)
                            bpy.context.view_layer.objects.active = obj
                            collapse = obj.modifiers.new(name='ACMCollapse', type='DECIMATE')
                            collapse.decimate_type = 'COLLAPSE'
                            collapse.ratio = ratio + (1 - ratio) * attempt / 3
                            collapse.use_collapse_triangulate = True
                            collapse.delimit = delimit_collapse
                            try_assign_decimate_vertex_group(collapse, protect_vg)
                            apply_modifier(obj, collapse.name)
                            remove_vertex_group_if_exists(obj, PROTECTION_VG)

                            # Drift check: measure how far any output vertex moved from the original
                            # surface. A spiked vertex will be far from every original triangle and
                            # shows up immediately as a large drift value. If it exceeds the limit we
                            # revert the entire collapse for this mesh and leave it untouched.
                            if snap_bvh is not None:
                                drift = max_vertex_drift(snap_bvh, obj)
                                err(('{}, drift={}, attempt={}, collapse.ratio={}').format(obj.name, drift, attempt, collapse.ratio))
                                if drift > args.max_vertex_drift:
                                    err('  {}: collapse reverted — max vertex drift {:.4f} m '
                                        'exceeds limit {:.4f} m'.format(
                                        obj.name, drift, args.max_vertex_drift))
                                    bm_snap.to_mesh(obj.data)
                                    obj.data.update()
                                else:
                                    err('  {}: max vertex drift {:.4f} m OK'.format(obj.name, drift))
                                    keepTrying = False
                                bm_snap.free()

            # Triangulate so the kn5 reader does not have to deal with quads/n-gons.
            deselect_all()
            obj.select_set(True)
            bpy.context.view_layer.objects.active = obj
            tri = obj.modifiers.new(name='ACMTri', type='TRIANGULATE')
            tri.quad_method = 'BEAUTY'
            tri.ngon_method = 'BEAUTY'
            apply_modifier(obj, tri.name)

            if args.weld_distance > 0:
                weld_vertices(obj, args.weld_distance)

            # Normal pass: either transfer from original BVH or recompute from decimated geometry.
            if normal_bvh is not None:
                # --transfer-normals: barycentrically sample the original split normals for
                # every loop in the decimated mesh. Preserves artist shading and hard edges
                # exactly; does not use the decimated topology for normal math at all.
                transfer_normals_from_bvh(normal_bvh, normal_tri_data, obj)
            elif args.weighted_normals:
                # Default: recompute area-weighted normals from the decimated topology.
                # Good fallback when --transfer-normals is not used.
                apply_weighted_normals(obj, args.normal_weight, args.sharp_angle)

        except Exception as ex:
            err('BlenderError: failed on mesh ""' + obj.name + '"": ' + str(ex))
            traceback.print_exc(file=sys.stderr)

        progress((idx + 1) * 100.0 / total)

    try:
        bpy.ops.export_scene.fbx(
                filepath=args.output,
                # Match import axes so the round trip is identity.
                axis_forward='-Z',
                axis_up='Y',
                # Geometry passes through unchanged.
                use_selection=False,
                use_active_collection=False,
                global_scale=1.0,
                apply_unit_scale=False,
                apply_scale_options='FBX_SCALE_NONE',
                use_space_transform=False,
                bake_space_transform=False,
                # Mesh data: we already applied modifiers, do not re-bake them.
                use_mesh_modifiers=False,
                use_mesh_modifiers_render=False,
                mesh_smooth_type='OFF',
                use_subsurf=False,
                use_mesh_edges=False,
                use_tspace=False,
                use_triangles=True,
                use_custom_props=False,
                # No animation / armature work for LODs.
                add_leaf_bones=False,
                bake_anim=False,
                # Textures stay external.
                path_mode='COPY',
                embed_textures=False,
                batch_mode='OFF',
                object_types={'MESH', 'EMPTY', 'ARMATURE', 'OTHER'})
    except Exception as e:
        err('FbxExportError: ' + str(e))
        traceback.print_exc(file=sys.stderr)
        sys.exit(5)

    progress(100.0)

if __name__ == '__main__':
    main()
