#!/usr/bin/env python3
# Why it looks the way it does:
#   * pymeshlab's COLLADA importer flattens multi-<geometry> DAEs into one mesh, so we
#     split per geometry to keep names that LodFbxToKn5 matches against.
#   * pymeshlab's COLLADA importer also silently drops the UV source kn5.ExportCollada
#     emits (offset 2 inside <triangles>); decimated meshes come out with all UVs at
#     (0,0), which manifests as ""damaged indices"" downstream because every triangle
#     samples the same texel. OBJ is the only round-trippable format pymeshlab supports
#     well, so we transcode each kn5 geometry to OBJ before handing it off.
#   * pymeshlab's COLLADA exporter emits per-corner wedge layouts FbxConverter then
#     translates into FBX layer-element sizes that don't line up with kn5's reader. We
#     therefore rebuild the decimated <mesh> ourselves in the exact shape kn5.ExportCollada
#     produces (one source per channel, count == N, single shared index per attribute in
#     <triangles>) so FbxConverter reaches LodFbxToKn5 with a layout that already works.
import sys
import os
import math
import argparse
import traceback
import uuid
import xml.etree.ElementTree as ET

NS = 'http://www.collada.org/2005/11/COLLADASchema'
ET.register_namespace('', NS)

def t(name):
    return '{' + NS + '}' + name

def err(msg):
    print(msg, file=sys.stderr, flush=True)

def progress(value):
    # Lines on stdout that parse as numbers are interpreted as 0..100 progress by the host.
    print('{0:.2f}'.format(value), flush=True)

def find_attr(obj, *candidates):
    for name in candidates:
        if hasattr(obj, name):
            return getattr(obj, name)
    return None

def compute_target_faces(face_count, target_perc, min_keep=20):
    # Nonlinear per-mesh decimation rate so tiny meshes survive aggressive global rates.
    #
    # Why we need this:
    #   With a uniform per-mesh rate (e.g. global 20% keep), a 50k-triangle body neatly drops
    #   to 10k, but a 10-triangle frame piece is told to drop to 2 - meaningless. The artist
    #   put 10 triangles there because that is the minimum useful representation, and any rate
    #   driven by total-model statistics will over-decimate it.
    #
    # The curve:
    #   We linearly interpolate the *per-mesh* keep rate from 1.0 (at face_count=0) to the
    #   requested global target_perc at face_count=threshold, where threshold = min_keep /
    #   target_perc. Above threshold the rate stays at target_perc (full requested decimation).
    #   At threshold itself, target_perc * threshold == min_keep, so the curve hits the absolute
    #   floor exactly where the protective zone ends - smooth handoff, no kink in the result.
    #
    # Examples with target_perc=0.2, min_keep=20 (-> threshold=100):
    #     face=10   -> keep 9    (~92%)   instead of 2
    #     face=50   -> keep 30   (~60%)   instead of 10
    #     face=100  -> keep 20   (=20%)   matches global rate
    #     face=1000 -> keep 200  (=20%)
    #     face=50k  -> keep 10k  (=20%)
    #
    # The 'large' meshes (where most triangles live) still pay the requested rate, so the global
    # budget overshoots only by the small amount preserved across tiny meshes - typically <1% of
    # the total triangle count for a car LOD. Worth it to keep small detail meshes intact.
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
    # Floor: keep at least 4 triangles unless the mesh was already smaller.
    return max(min(face_count, 4), min(face_count, target))

def diagonal_from_positions(pos_flat):
    if not pos_flat or len(pos_flat) < 9:
        return 0.0
    n = len(pos_flat) // 3
    xs = [pos_flat[i * 3] for i in range(n)]
    ys = [pos_flat[i * 3 + 1] for i in range(n)]
    zs = [pos_flat[i * 3 + 2] for i in range(n)]
    dx = max(xs) - min(xs)
    dy = max(ys) - min(ys)
    dz = max(zs) - min(zs)
    return math.sqrt(dx * dx + dy * dy + dz * dz)

def span_score_percentile_threshold(scores, percentile):
    if len(scores) < 3:
        return float('inf')
    s = sorted(scores)
    n = len(s)
    p = max(0.0, min(100.0, float(percentile)))
    idx = int(round((p / 100.0) * (n - 1)))
    return s[idx]

def get_material_symbol(geom):
    mesh = geom.find(t('mesh'))
    if mesh is None:
        return None
    for tag in ('triangles', 'polylist', 'polygons'):
        prim = mesh.find(t(tag))
        if prim is not None and prim.get('material'):
            return prim.get('material')
    return None

def _read_floats(elem):
    if elem is None or not elem.text:
        return []
    return [float(x) for x in elem.text.split()]

def parse_kn5_geometry(geom):
    # Pull positions/normals/UVs and the per-corner index stream out of a kn5-style <geometry>.
    # Returns a dict with everything needed to write an equivalent OBJ, or None if the geometry
    # is unusable.
    mesh = geom.find(t('mesh'))
    if mesh is None:
        return None

    sources = {}
    for src in mesh.findall(t('source')):
        sid = src.get('id')
        if sid:
            sources[sid] = _read_floats(src.find(t('float_array')))

    verts_id_to_pos_source = {}
    for vs in mesh.findall(t('vertices')):
        vid = vs.get('id')
        for inp in vs.findall(t('input')):
            if inp.get('semantic') == 'POSITION':
                verts_id_to_pos_source[vid] = inp.get('source', '').lstrip('#')

    pos = None
    norm = None
    uv = None
    has_uv2 = False
    material_symbol = None
    triangles = []  # list of (v_idx, n_idx, t_idx) per corner

    for prim in list(mesh):
        tag = prim.tag.split('}', 1)[-1]
        if tag not in ('triangles', 'polylist', 'polygons'):
            continue
        if material_symbol is None and prim.get('material'):
            material_symbol = prim.get('material')

        inputs = []
        for inp in prim.findall(t('input')):
            sem = inp.get('semantic')
            src = (inp.get('source') or '').lstrip('#')
            try:
                offset = int(inp.get('offset', '0'))
            except ValueError:
                offset = 0
            inputs.append((sem, src, offset))

        if not inputs:
            continue

        stride = max(off for _, _, off in inputs) + 1

        v_off = n_off = t_off = c_off = None
        for sem, src, off in inputs:
            if sem == 'VERTEX':
                v_off = off
                resolved = verts_id_to_pos_source.get(src, src)
                if pos is None and resolved in sources:
                    pos = sources[resolved]
            elif sem == 'NORMAL':
                n_off = off
                if norm is None and src in sources:
                    norm = sources[src]
            elif sem == 'TEXCOORD':
                t_off = off
                if uv is None and src in sources:
                    uv = sources[src]
            elif sem == 'COLOR':
                c_off = off
                has_uv2 = True

        # Read polygon-vertex stream. For polylist we honour <vcount>, otherwise assume triangles.
        p_text = (prim.find(t('p')).text or '') if prim.find(t('p')) is not None else ''
        raw = [int(x) for x in p_text.split()]
        if not raw:
            continue
        n_corners = len(raw) // stride

        if tag == 'polylist':
            vcount = [int(x) for x in (prim.find(t('vcount')).text or '').split()]
            cursor = 0
            for vc in vcount:
                if vc < 3:
                    cursor += vc
                    continue
                # Triangle-fan polygon -> triangles.
                first = cursor
                for k in range(1, vc - 1):
                    for c_idx in (first, cursor + k, cursor + k + 1):
                        base = c_idx * stride
                        v_i = raw[base + v_off] if v_off is not None else 0
                        n_i = raw[base + n_off] if n_off is not None else v_i
                        t_i = raw[base + t_off] if t_off is not None else v_i
                        triangles.append((v_i, n_i, t_i))
                cursor += vc
        else:
            for c_idx in range(n_corners):
                base = c_idx * stride
                v_i = raw[base + v_off] if v_off is not None else 0
                n_i = raw[base + n_off] if n_off is not None else v_i
                t_i = raw[base + t_off] if t_off is not None else v_i
                triangles.append((v_i, n_i, t_i))
        # Discard offset for COLOR (UV2) - we do not preserve it.
        _ = c_off

    if pos is None or len(pos) < 3 or not triangles:
        return None

    return {
        'positions': pos,
        'normals': norm or [],
        'uvs': uv or [],
        'triangles': triangles,
        'material_symbol': material_symbol,
        'has_uv2': has_uv2,
    }

def write_obj(parsed, obj_path):
    pos = parsed['positions']
    norms = parsed['normals']
    uvs = parsed['uvs']
    tris = parsed['triangles']
    n_v = len(pos) // 3
    n_n = len(norms) // 3
    n_uv = len(uvs) // 2
    has_n = n_n > 0
    has_uv = n_uv > 0
    with open(obj_path, 'w', encoding='utf-8', newline='\n') as f:
        f.write('# Auto-generated for AcManager MeshLab decimator\n')
        for i in range(n_v):
            f.write('v {0} {1} {2}\n'.format(pos[i * 3], pos[i * 3 + 1], pos[i * 3 + 2]))
        if has_uv:
            for i in range(n_uv):
                # OBJ V is bottom-left origin; kn5 already wrote -Y in its DAE so passing through
                # is correct (decimator preserves whatever convention we feed it).
                f.write('vt {0} {1}\n'.format(uvs[i * 2], uvs[i * 2 + 1]))
        if has_n:
            for i in range(n_n):
                f.write('vn {0} {1} {2}\n'.format(norms[i * 3], norms[i * 3 + 1], norms[i * 3 + 2]))

        # Faces: emit triangles three corners at a time. OBJ uses 1-based indices; clamp to the
        # available data so degenerate inputs do not break loading.
        max_v = n_v
        max_uv = n_uv
        max_n = n_n
        for tri_i in range(len(tris) // 3):
            corners = tris[tri_i * 3:tri_i * 3 + 3]
            parts = []
            for v_i, n_i, t_i in corners:
                v_token = str((v_i % max_v) + 1) if max_v > 0 else '1'
                if has_uv and has_n:
                    parts.append('{0}/{1}/{2}'.format(
                            v_token, (t_i % max_uv) + 1, (n_i % max_n) + 1))
                elif has_uv:
                    parts.append('{0}/{1}'.format(v_token, (t_i % max_uv) + 1))
                elif has_n:
                    parts.append('{0}//{1}'.format(v_token, (n_i % max_n) + 1))
                else:
                    parts.append(v_token)
            f.write('f ' + ' '.join(parts) + '\n')

def write_kn5_mesh(geom, name, positions, normals, uvs, indices, material_symbol):
    # Replace <mesh> in geom with kn5's preferred COLLADA layout. Caller passes per-vertex arrays
    # (positions/normals/uvs all sized N) plus an int index array of length 3*M; the same index is
    # repeated three times per polygon-vertex in <p>, matching kn5.ExportCollada exactly.
    old = geom.find(t('mesh'))
    if old is not None:
        geom.remove(old)
    mesh = ET.SubElement(geom, t('mesh'))

    n_v = positions.shape[0]
    n_tri = len(indices) // 3

    def write_source(sid, data, stride, param_names):
        src = ET.SubElement(mesh, t('source'))
        src.set('id', sid)
        fa = ET.SubElement(src, t('float_array'))
        fa.set('id', sid + '-array')
        fa.set('count', str(int(data.size)))
        fa.text = ' '.join(repr(float(v)) for v in data.flatten())
        tc = ET.SubElement(src, t('technique_common'))
        acc = ET.SubElement(tc, t('accessor'))
        acc.set('source', '#' + sid + '-array')
        acc.set('count', str(int(data.shape[0])))
        acc.set('stride', str(stride))
        for pn in param_names:
            p = ET.SubElement(acc, t('param'))
            p.set('name', pn)
            p.set('type', 'float')

    write_source(name + '-mesh-positions', positions, 3, ('X', 'Y', 'Z'))
    write_source(name + '-mesh-normals', normals, 3, ('X', 'Y', 'Z'))
    write_source(name + '-mesh-map-0', uvs, 2, ('S', 'T'))

    vs = ET.SubElement(mesh, t('vertices'))
    vs.set('id', name + '-mesh-vertices')
    inp = ET.SubElement(vs, t('input'))
    inp.set('semantic', 'POSITION')
    inp.set('source', '#' + name + '-mesh-positions')

    tri = ET.SubElement(mesh, t('triangles'))
    if material_symbol:
        tri.set('material', material_symbol)
    tri.set('count', str(n_tri))

    for sem, src, off in (
            ('VERTEX', '#' + name + '-mesh-vertices', '0'),
            ('NORMAL', '#' + name + '-mesh-normals', '1'),
            ('TEXCOORD', '#' + name + '-mesh-map-0', '2')):
        ip = ET.SubElement(tri, t('input'))
        ip.set('semantic', sem)
        ip.set('source', src)
        ip.set('offset', off)
        if sem == 'TEXCOORD':
            ip.set('set', '0')

    p = ET.SubElement(tri, t('p'))
    p.text = ' '.join('{0} {0} {0}'.format(int(i)) for i in indices)

def expand_to_per_corner(m, np):
    # Pull pymeshlab mesh data and expand to per-corner unique vertices, mirroring kn5's layout.
    # Returns (positions, normals, uvs, indices) or None if the mesh is degenerate.
    fm = m.face_matrix()
    if fm is None or fm.shape[0] == 0:
        return None
    vm = m.vertex_matrix()
    f_count = fm.shape[0]
    flat = fm.flatten().astype(np.int64)

    pos = vm[flat]

    # Vertex normals if available, otherwise compute per-face and replicate to corners.
    norm = None
    try:
        nm = m.vertex_normal_matrix()
        if nm is not None and nm.shape[0] == vm.shape[0]:
            norm = nm[flat]
    except Exception:
        norm = None
    if norm is None:
        v0 = vm[fm[:, 0]]
        v1 = vm[fm[:, 1]]
        v2 = vm[fm[:, 2]]
        face_n = np.cross(v1 - v0, v2 - v0)
        n_len = np.linalg.norm(face_n, axis=1, keepdims=True)
        n_len[n_len == 0] = 1.0
        face_n = face_n / n_len
        norm = np.repeat(face_n, 3, axis=0)

    # Wedge tex coords are per-corner; vertex tex coords are per-vertex. Prefer wedge.
    uv = None
    try:
        wm = m.wedge_tex_coord_matrix()
        if wm is not None and wm.shape[0] == f_count * 3:
            uv = wm
    except Exception:
        uv = None
    if uv is None:
        try:
            vtm = m.vertex_tex_coord_matrix()
            if vtm is not None and vtm.shape[0] == vm.shape[0]:
                uv = vtm[flat]
        except Exception:
            uv = None
    if uv is None:
        uv = np.zeros((f_count * 3, 2), dtype=np.float32)

    indices = np.arange(f_count * 3, dtype=np.int64)
    return pos.astype(np.float32), norm.astype(np.float32), uv.astype(np.float32), indices

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--input', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--target-perc', type=float, default=0.5)
    parser.add_argument('--quality-threshold', type=float, default=0.3)
    parser.add_argument('--boundary-weight', type=float, default=1.0)
    parser.add_argument('--preserve-boundary', action='store_true')
    parser.add_argument('--preserve-normal', action='store_true')
    parser.add_argument('--preserve-topology', action='store_true')
    parser.add_argument('--planar-quadric', action='store_true')
    parser.add_argument('--with-texture', action='store_true')
    parser.add_argument('--apply-welding', action='store_true')
    parser.add_argument('--welding-threshold', type=float, default=0.0001)
    parser.add_argument('--min-keep', type=int, default=20,
                        help='Per-mesh triangle floor used by the nonlinear decimation curve. '
                             'Meshes whose original face count is below min-keep/target-perc '
                             'are softened toward keep-all to protect small detail geometries.')
    parser.add_argument('--min-target-perc', type=float, default=0.0,
                        help='Optional floor on keep ratio for every geometry (0 disables).')
    parser.add_argument('--no-auto-span-protect', action='store_true',
                        help='Disable bbox-span heuristic for large low-poly sheets.')
    parser.add_argument('--auto-span-keep-perc', type=float, default=0.5)
    parser.add_argument('--auto-span-percentile', type=float, default=72.0)
    parser.add_argument('--keep-temp', action='store_true')
    args = parser.parse_args()

    try:
        import pymeshlab
    except ImportError:
        err('PyMeshLabImportError: pymeshlab is not installed for this Python interpreter. '
            'Run: ""' + sys.executable + '"" -m pip install pymeshlab')
        sys.exit(2)
    except Exception as e:
        err('PyMeshLabImportError: failed to import pymeshlab: ' + str(e))
        sys.exit(2)

    try:
        import numpy as np
    except ImportError:
        err('NumpyImportError: numpy is not installed (it should ship with pymeshlab).')
        sys.exit(2)

    try:
        tree = ET.parse(args.input)
    except Exception as e:
        err('DAEParseError: failed to read input DAE: ' + str(e))
        sys.exit(3)

    root = tree.getroot()
    lib_geo = root.find(t('library_geometries'))
    if lib_geo is None:
        err('DAEFormatError: <library_geometries> is missing from input DAE')
        sys.exit(3)

    asset = root.find(t('asset'))
    geometries = list(lib_geo.findall(t('geometry')))
    total = len(geometries)
    if total == 0:
        err('DAEFormatError: input DAE has no <geometry> elements')
        sys.exit(3)

    work_dir = os.path.dirname(os.path.abspath(args.input)) or '.'

    # Unique-per-invocation tag so concurrent runs that share work_dir (e.g. several LOD
    # stages of the same model decimating in parallel) do not stomp on each other's
    # _mlab_in_* OBJ scratch files. Each script process picks its own random tag once.
    run_tag = uuid.uuid4().hex[:12]

    err('MeshLab: decimating {0} geometries (target keep ratio {1:.2f}%)'.format(
            total, args.target_perc * 100))

    target_perc = max(0.001, min(0.999, float(args.target_perc)))
    temp_files = []

    geom_span_scores = []
    for geom in geometries:
        parsed0 = parse_kn5_geometry(geom)
        if parsed0 is None:
            geom_span_scores.append(None)
            continue
        fc0 = len(parsed0['triangles']) // 3
        if fc0 <= 0:
            geom_span_scores.append(None)
            continue
        d0 = diagonal_from_positions(parsed0['positions'])
        geom_span_scores.append(d0 / math.sqrt(float(fc0)))
    span_score_list = [s for s in geom_span_scores if s is not None]
    auto_span_thresh = span_score_percentile_threshold(span_score_list, args.auto_span_percentile)
    if args.no_auto_span_protect:
        auto_span_thresh = float('inf')

    for idx, geom in enumerate(geometries):
        geom_id = geom.get('id', 'g_' + str(idx))
        geom_name = geom.get('name') or geom_id

        parsed = parse_kn5_geometry(geom)
        if parsed is None:
            err('MeshLab: skipping ""' + geom_name + '"" (could not parse geometry)')
            continue
        material_symbol = parsed['material_symbol']

        in_path = os.path.join(work_dir, '_mlab_in_{0}_{1}.obj'.format(run_tag, idx))
        try:
            write_obj(parsed, in_path)
        except Exception as we:
            err('MeshLab: failed to write OBJ for ""' + geom_name + '"": ' + str(we))
            continue
        temp_files.append(in_path)

        try:
            ms = pymeshlab.MeshSet()
            try:
                ms.load_new_mesh(in_path)
            except Exception as e:
                err('MeshLoadError: could not load geometry ""' + geom_name + '"": ' + str(e))
                continue

            current = ms.current_mesh()
            face_count = current.face_number()
            if face_count <= 0:
                err('MeshLab: skipping ""' + geom_name + '"" (no faces)')
                continue

            target = compute_target_faces(face_count, target_perc, args.min_keep)
            if args.min_target_perc > 0:
                floor_faces = int(math.ceil(
                        face_count * max(0.001, min(1.0, float(args.min_target_perc)))))
                target = max(target, floor_faces)
            gss = geom_span_scores[idx] if idx < len(geom_span_scores) else None
            if (len(span_score_list) >= 3 and gss is not None and gss >= auto_span_thresh):
                k = max(0.001, min(1.0, float(args.auto_span_keep_perc)))
                target = max(target, int(math.ceil(face_count * k)))
            target = min(target, face_count)

            if args.apply_welding:
                weld = find_attr(ms, 'meshing_merge_close_vertices', 'merge_close_vertices')
                if weld is not None:
                    try:
                        thr_pct = args.welding_threshold * 100.0
                        thr_ctor = find_attr(pymeshlab, 'PercentageValue', 'Percentage')
                        if thr_ctor is not None:
                            try:
                                weld(threshold=thr_ctor(thr_pct))
                            except Exception:
                                weld(threshold=thr_pct)
                        else:
                            weld(threshold=thr_pct)
                    except Exception as we:
                        err('MeshLab: welding failed on ""' + geom_name + '"": ' + str(we))

                dedup = find_attr(ms, 'meshing_remove_duplicate_vertices', 'remove_duplicate_vertices')
                if dedup is not None:
                    try:
                        dedup()
                    except Exception:
                        pass

            if target < face_count:
                used = False
                if args.with_texture:
                    f_tex = find_attr(ms,
                            'meshing_decimation_quadric_edge_collapse_with_texture',
                            'simplification_quadric_edge_collapse_decimation_with_texture')
                    if f_tex is not None:
                        try:
                            f_tex(targetfacenum=target,
                                  qualitythr=args.quality_threshold,
                                  preserveboundary=args.preserve_boundary,
                                  boundaryweight=args.boundary_weight,
                                  preservenormal=args.preserve_normal,
                                  optimalplacement=True,
                                  planarquadric=args.planar_quadric)
                            used = True
                        except Exception as et:
                            err('MeshLab: textured decimation failed on ""' + geom_name + '"" ('
                                    + str(et) + '); falling back to plain decimation')
                if not used:
                    f_plain = find_attr(ms,
                            'meshing_decimation_quadric_edge_collapse',
                            'simplification_quadric_edge_collapse_decimation')
                    if f_plain is None:
                        err('FilterMissingError: no quadric edge collapse filter; check pymeshlab version')
                        sys.exit(4)
                    f_plain(targetfacenum=target,
                            qualitythr=args.quality_threshold,
                            preserveboundary=args.preserve_boundary,
                            boundaryweight=args.boundary_weight,
                            preservenormal=args.preserve_normal,
                            preservetopology=args.preserve_topology,
                            optimalplacement=True,
                            planarquadric=args.planar_quadric,
                            autoclean=True)

            # Recompute vertex normals on the simplified topology, weighting each face's
            # contribution by its surface area. This is the fix for the classic ""flat panel
            # with thin smoothing edge"" case (e.g. a door): when decimation collapses the
            # tiny edge rows, simple averaging would let the few surviving sliver triangles
            # tilt the normal of every nearby vertex. Area weighting lets the big flat
            # triangles dominate so the panel keeps looking flat.
            recompute = find_attr(ms,
                    'compute_normal_per_vertex',
                    'compute_normals_for_point_sets',
                    'recompute_vertex_normals')
            if recompute is not None:
                ok = False
                # Try the modern keyword/value first, then fall back through older spellings
                # so the script keeps working across pymeshlab releases.
                for kwargs in (
                        {'weightmode': 'By Area'},
                        {'weight_mode': 'By Area'},
                        {'weight': 'By Area'},
                        {'weight': 1}):
                    try:
                        recompute(**kwargs)
                        ok = True
                        break
                    except Exception:
                        continue
                if not ok:
                    try:
                        recompute()
                    except Exception:
                        pass

            current = ms.current_mesh()
            data = expand_to_per_corner(current, np)
            if data is None:
                err('MeshLab: empty result for ""' + geom_name + '""; keeping original')
                continue
            positions, normals, uvs, indices = data
            write_kn5_mesh(geom, geom_name, positions, normals, uvs, indices, material_symbol)
        except SystemExit:
            raise
        except Exception as ex:
            err('MeshLabError: decimation failed on ""' + geom_name + '"": ' + str(ex))
            traceback.print_exc(file=sys.stderr)

        progress((idx + 1) * 100.0 / total)

    try:
        tree.write(args.output, xml_declaration=True, encoding='utf-8')
    except Exception as e:
        err('DAEWriteError: failed to write output DAE: ' + str(e))
        sys.exit(5)

    if not args.keep_temp:
        for fp in temp_files:
            try:
                os.remove(fp)
            except Exception:
                pass

    progress(100.0)

if __name__ == '__main__':
    main()