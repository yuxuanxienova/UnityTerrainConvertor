#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import os
import re
import argparse
from collections import defaultdict
from PIL import Image, ImageDraw

# Debug helper
def dbg(*args):
    try:
        print("[UVDBG]", *args)
    except Exception:
        # Ensure debug never crashes
        pass

# ---------- Simple USDA parsers (text scan, robust enough for exported ASCII) ----------

MESH_DEF_RE = re.compile(r'^\s*def\s+Mesh\s+"([^"]+)"')
MAT_BIND_RE = re.compile(r'^\s*rel\s+material:binding\s*=\s*<([^>]+)>')
COUNTS_START_RE = re.compile(r'^\s*int\[\]\s*faceVertexCounts\s*=\s*\[')
INDICES_START_RE = re.compile(r'^\s*int\[\]\s*faceVertexIndices\s*=\s*\[')
ST_START_RE = re.compile(r'^\s*texCoord2f\[\]\s*primvars:st\s*=\s*\[')
ST_INTERP_RE = re.compile(r'^\s*uniform\s+token\s+primvars:st:interpolation\s*=\s*"([^"]+)"')

MAT_DEF_RE = re.compile(r'^\s*def\s+Material\s+"([^"]+)"')
ASSET_FILE_RE = re.compile(r'^\s*asset\s+inputs:file\s*=\s*@([^@]+)@')
SHADER_ID_RE = re.compile(r'^\s*uniform\s+token\s+(?:info:)?(?:shaderId|id)\s*=\s*"([^"]+)"')

def _read_brace_block(lines, i_start):
    depth = 0
    block = []
    i = i_start
    seen_open = False
    while i < len(lines):
        line = lines[i]
        if '{' in line:
            depth += line.count('{')
            seen_open = True
        if '}' in line:
            depth -= line.count('}')
        block.append((i, line))
        i += 1
        if seen_open and depth <= 0:
            break
    return block, i

def parse_usda_meshes(usda_path):
    dbg("Reading USDA:", usda_path)
    with open(usda_path, 'r', encoding='utf-8', errors='ignore') as f:
        lines = f.readlines()

    meshes = []
    i = 0
    while i < len(lines):
        m = MESH_DEF_RE.match(lines[i])
        if not m:
            i += 1
            continue
        mesh_name = m.group(1)
        dbg("Found Mesh:", mesh_name, "at line", i)
        block, i = _read_brace_block(lines, i)
        dbg("Mesh block lines:", len(block))
        counts, indices, st, interp, mat_binding = [], [], [], None, None

        def _collect_numbers(start_idx):
            vals = []
            j = start_idx + 1  # skip header line containing 'int[] ... = ['
            while j < len(block):
                s = block[j][1]
                nums = re.findall(r'[-+]?\d+', s)
                if nums:
                    vals.extend(nums)
                if ']' in s:
                    return [int(x) for x in vals], j
                j += 1
            return [int(x) for x in vals], j

        def _collect_uvs(start_idx):
            uvs = []
            j = start_idx + 1  # skip header line containing 'texCoord2f[] ... = ['
            while j < len(block):
                s = block[j][1]
                # collect tuples like (u, v)
                for tup in re.findall(r'\(\s*([-\d\.eE+]+)\s*,\s*([-\d\.eE+]+)\s*\)', s):
                    try:
                        u = float(tup[0])
                        v = float(tup[1])
                        uvs.append((u, v))
                    except Exception:
                        pass
                if ']' in s:
                    return uvs, j
                j += 1
            return uvs, j

        j = 0
        while j < len(block):
            line = block[j][1]
            if 'faceVertexCounts' in line and not COUNTS_START_RE.match(line):
                dbg("Counts line present but regex not matched:", line.strip())
            if 'faceVertexIndices' in line and not INDICES_START_RE.match(line):
                dbg("Indices line present but regex not matched:", line.strip())
            if 'primvars:st' in line and '[' in line and not ST_START_RE.match(line):
                dbg("ST line present but regex not matched:", line.strip())
            if COUNTS_START_RE.match(line):
                dbg("COUNTS header:", line.strip())
                for _k in range(1, 4):
                    if j + _k < len(block):
                        dbg("  next", _k, ":", block[j+_k][1].strip())
                counts, j = _collect_numbers(j)
                dbg("Collected counts, n=", len(counts), "next_j=", j)
            elif INDICES_START_RE.match(line):
                dbg("INDICES header:", line.strip())
                for _k in range(1, 4):
                    if j + _k < len(block):
                        dbg("  next", _k, ":", block[j+_k][1].strip())
                indices, j = _collect_numbers(j)
                dbg("Collected indices, n=", len(indices), "next_j=", j)
            elif ST_START_RE.match(line):
                dbg("ST header:", line.strip())
                for _k in range(1, 4):
                    if j + _k < len(block):
                        dbg("  next", _k, ":", block[j+_k][1].strip())
                st, j = _collect_uvs(j)
                dbg("Collected st, n=", len(st), "next_j=", j)
            else:
                m2 = ST_INTERP_RE.match(line)
                if m2:
                    interp = m2.group(1)
                    dbg("Found st interpolation:", interp)
                else:
                    m3 = MAT_BIND_RE.match(line)
                    if m3:
                        mat_binding = m3.group(1)
                        dbg("Found material binding:", mat_binding)
            j += 1

        mesh_info = {
            'name': mesh_name,
            'counts': counts,
            'indices': indices,
            'st': st,
            'interp': interp,
            'mat_binding': mat_binding
        }
        dbg("Parsed Mesh:", mesh_name,
            "counts_len=", len(counts), "sum_counts=", sum(counts) if counts else 0,
            "st_len=", len(st), "interp=", interp, "mat_binding=", mat_binding)
        meshes.append(mesh_info)
    return meshes

def parse_material_textures(looks_path):
    # Return map: material_prim_path_endName -> first UsdUVTexture inputs:file
    if not os.path.isfile(looks_path):
        dbg("Looks file not found:", looks_path)
        return {}
    dbg("Reading Looks:", looks_path)
    with open(looks_path, 'r', encoding='utf-8', errors='ignore') as f:
        lines = f.readlines()

    tex_by_mat = {}
    i = 0
    while i < len(lines):
        m = MAT_DEF_RE.match(lines[i])
        if not m:
            i += 1
            continue
        mat_name = m.group(1)
        block, i = _read_brace_block(lines, i)
        found_tex = None
        in_uvtex = False
        # Heuristic: pick first UsdUVTexture in the material
        for _, line in block:
            m_id = SHADER_ID_RE.match(line)
            if m_id:
                in_uvtex = (m_id.group(1) == 'UsdUVTexture')
            m_file = ASSET_FILE_RE.match(line)
            if m_file and in_uvtex and found_tex is None:
                found_tex = m_file.group(1)
        if found_tex:
            tex_by_mat[mat_name] = found_tex
            dbg("Material tex:", mat_name, "->", found_tex)
        else:
            dbg("Material has no UsdUVTexture file:", mat_name)
    return tex_by_mat

# ---------- UV drawing ----------

def resolve_texture_path(tex_path, base_dir):
    # Absolute → return; Relative → try resolve under OUTPUT then project
    if os.path.isabs(tex_path) and os.path.isfile(tex_path):
        dbg("Resolved absolute texture:", tex_path)
        return tex_path
    candidates = [
        os.path.join(base_dir, 'Assets', 'OUTPUT', tex_path),
        os.path.join(base_dir, tex_path),
        os.path.join(base_dir, 'Assets', 'OUTPUT', 'assets', os.path.basename(tex_path)),
        os.path.join(base_dir, 'Assets', os.path.basename(tex_path)),
    ]
    dbg("Resolve texture candidates:", candidates)
    for c in candidates:
        if os.path.isfile(c):
            dbg("Resolved texture:", c)
            return c
    dbg("Failed to resolve texture:", tex_path)
    return None

def make_blank_checker(w=1024, h=1024, cell=64):
    img = Image.new('RGB', (w, h), (200, 200, 200))
    draw = ImageDraw.Draw(img)
    for y in range(0, h, cell):
        for x in range(0, w, cell):
            if ((x // cell) + (y // cell)) % 2 == 0:
                draw.rectangle([x, y, x+cell, y+cell], fill=(220, 220, 220))
    return img

def draw_uvs_on_image(img, counts, st, color=(255, 0, 0), alpha=180, wrap='repeat'):
    w, h = img.size
    draw = ImageDraw.Draw(img, 'RGBA')

    def map_uv(u, v):
        if wrap == 'repeat':
            u = u % 1.0
            v = v % 1.0
        else:
            u = max(0.0, min(1.0, u))
            v = max(0.0, min(1.0, v))
        x = u * (w - 1)
        y = (1.0 - v) * (h - 1)  # flip V for image origin
        return (x, y)

    offset = 0
    for c in counts:
        if offset + c > len(st):
            dbg("Counts exceed st length:", offset, "+", c, ">", len(st))
            break
        face_uvs = [map_uv(*st[offset + k]) for k in range(c)]
        offset += c
        # draw outline
        if len(face_uvs) >= 2:
            draw.line(face_uvs + [face_uvs[0]], fill=(*color, alpha), width=1)

def ensure_dir(p):
    if not os.path.isdir(p):
        os.makedirs(p, exist_ok=True)

def main():
    ap = argparse.ArgumentParser(description='Visualize primvars:st UVs over their textures.')
    ap.add_argument('--usda', default='/Users/yuxuan/git/UnityTerrainConvertor/Assets/OUTPUT/Map.usda')
    ap.add_argument('--looks', default='/Users/yuxuan/git/UnityTerrainConvertor/Assets/OUTPUT/Map_Looks.usda')
    ap.add_argument('--base', default='/Users/yuxuan/git/UnityTerrainConvertor')
    ap.add_argument('--outdir', default='/Users/yuxuan/git/UnityTerrainConvertor/python_code/output')
    ap.add_argument('--wrap', default='repeat', choices=['repeat', 'clamp'])
    args = ap.parse_args()

    dbg("Args:", args)
    ensure_dir(args.outdir)
    meshes = parse_usda_meshes(args.usda)
    dbg("Total meshes parsed:", len(meshes))
    mat_tex = parse_material_textures(args.looks)
    dbg("Total materials with textures:", len(mat_tex))

    # Build a mapping from bound material path to material name (last path token)
    def mat_name_from_binding(path_str):
        # Example: </World/Looks/MyMat> => 'MyMat'
        return os.path.basename(path_str.strip('/<>'))

    for m in meshes:
        if not m['st'] or not m['counts']:
            dbg("Skip mesh (missing st or counts):", m['name'], "st_len=", len(m['st']), "counts_len=", len(m['counts']))
            continue
        # FaceVarying sanity: len(st) == sum(counts)
        if sum(m['counts']) != len(m['st']):
            # still try to draw sequentially
            dbg("FaceVarying length mismatch:", m['name'], "sum(counts)=", sum(m['counts']), "st_len=", len(m['st']))

        tex_img = None
        tex_path_resolved = None

        if m.get('mat_binding'):
            mat_name = mat_name_from_binding(m['mat_binding'])
            tex_path = mat_tex.get(mat_name)
            dbg("Mesh mat_binding:", m['name'], "->", m['mat_binding'], "mat_name=", mat_name, "tex=", tex_path)
            if tex_path:
                tex_path_resolved = resolve_texture_path(tex_path, args.base)
        if tex_path_resolved and os.path.isfile(tex_path_resolved):
            try:
                tex_img = Image.open(tex_path_resolved).convert('RGB')
                dbg("Opened texture for mesh:", m['name'], tex_path_resolved)
            except Exception:
                dbg("Failed to open texture for mesh:", m['name'], tex_path_resolved)
                tex_img = None

        if tex_img is None:
            dbg("Using checker texture for mesh:", m['name'])
            tex_img = make_blank_checker(1024, 1024)

        draw_uvs_on_image(tex_img, m['counts'], m['st'], color=(255, 60, 60), alpha=200, wrap=args.wrap)

        mat_part = ''
        if m.get('mat_binding'):
            mat_part = '_' + mat_name_from_binding(m['mat_binding'])
        out_name = f'uv_{m["name"]}{mat_part}.png'
        out_path = os.path.join(args.outdir, out_name)
        tex_img.save(out_path)
        print(f'Wrote {out_path} (texture={tex_path_resolved or "checker"})')

if __name__ == '__main__':
    main()