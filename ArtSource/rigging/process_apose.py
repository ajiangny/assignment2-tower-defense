"""Clean up an A-pose Rodin rig and export it for Unity.

Run inside Blender:
    exec(open('/Users/anjujiang/assignment2/ArtSource/rigging/process_apose.py').read())
    load('Shambler')           # import + texture + head-weight fix, for previewing
    export('Shambler', 1.3)    # decimate, scale to game height, write FBX + textures to Unity

Inputs per zombie in ArtSource/rigging/out_apose/<Name>/: rigged.fbx, texture_diffuse/normal/emissive.png
"""
import bpy, collections, os, shutil
import numpy as np

SRC = '/Users/anjujiang/assignment2/ArtSource/rigging/out_apose/'
DST = '/Users/anjujiang/assignment2/Assets/Models/Zombies/Rigged/'
REASSIGN = {'Head', 'Neck', 'LeftShoulder', 'RightShoulder', 'Spine', 'Spine1', 'Spine2'}
SKIN_HUE, SKIN_SAT = 82 / 360, 0.47   # the approved Shambler green
KEEP = {'Walk': ('ScaryClownWalk', 'InjuredRun', 'OrchammerLocoWalkFwdNorm'), 'Hit': ('HeadHit',), 'Die': ('StandingDeathBackward',),
        'Idle': ('OffensiveIdle', 'SpIdle'), 'Attack': ('SpThrow', 'PointingForward')}


def clear():
    for coll in (bpy.data.objects, bpy.data.actions, bpy.data.meshes, bpy.data.armatures, bpy.data.materials, bpy.data.images):
        for item in list(coll):
            coll.remove(item)


def imp(path):
    win = bpy.context.window_manager.windows[0]
    area = next(a for a in win.screen.areas if a.type == 'VIEW_3D')
    with bpy.context.temp_override(window=win, area=area):
        bpy.ops.import_scene.fbx(filepath=path)


def objects():
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    me = next(o for o in bpy.data.objects if o.type == 'MESH')
    return arm, me


def preview_material(me, folder):
    mat = bpy.data.materials.new('Preview')
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    base = nt.nodes.new('ShaderNodeTexImage'); base.image = bpy.data.images.load(folder + 'texture_diffuse.png')
    nt.links.new(base.outputs['Color'], bsdf.inputs['Base Color'])
    if os.path.exists(folder + 'texture_emissive.png'):
        emi = nt.nodes.new('ShaderNodeTexImage'); emi.image = bpy.data.images.load(folder + 'texture_emissive.png')
        nt.links.new(emi.outputs['Color'], bsdf.inputs['Emission Color'])
        bsdf.inputs['Emission Strength'].default_value = 3.0
    me.data.materials.clear(); me.data.materials.append(mat)


def head_counts(me, z_min):
    names = {g.index: g.name for g in me.vertex_groups}
    c = collections.Counter()
    for v in me.data.vertices:
        if (me.matrix_world @ v.co).z > z_min:
            c[names[max(v.groups, key=lambda g: g.weight).group] if v.groups else '<none>'] += 1
    return c.most_common(4)


def fix_head(arm, me):
    """Chibi heads get bound to the shoulders; move everything above the neck (except arms) onto Head."""
    names = {g.index: g.name for g in me.vertex_groups}
    neck = (arm.matrix_world @ arm.data.bones['Head'].head_local).z
    h = me.dimensions.z
    lo, hi = neck - 0.025 * h, neck + 0.03 * h
    probe = neck + 0.1 * h
    before = head_counts(me, probe)
    head = me.vertex_groups['Head']
    n = 0
    for v in me.data.vertices:
        z = (me.matrix_world @ v.co).z
        if z <= lo:
            continue
        dom = names[max(v.groups, key=lambda g: g.weight).group] if v.groups else None
        if dom is not None and dom not in REASSIGN:
            continue
        w = 1.0 if (z >= hi or not v.groups) else (z - lo) / (hi - lo)
        for g in list(v.groups):
            me.vertex_groups[g.group].add([v.index], g.weight * (1 - w), 'REPLACE')
        head.add([v.index], w, 'ADD')
        n += 1
    return before, head_counts(me, probe), n


def keep_clips(arm):
    keep = {}
    for a in bpy.data.actions:
        for name, keys in KEEP.items():
            if any(k in a.name for k in keys):
                keep[name] = a
    for a in list(bpy.data.actions):
        if a not in keep.values():
            bpy.data.actions.remove(a)
    for k, a in keep.items():
        a.name = k
        a.use_fake_user = True
    arm.animation_data.action = keep.get('Walk') or keep.get('Idle')
    return sorted(keep)


def load(name, src=None):
    clear()
    folder = (src or SRC) + name + '/'
    imp(folder + 'rigged.fbx')
    arm, me = objects()
    preview_material(me, folder)
    before, after, n = fix_head(arm, me)
    clips = keep_clips(arm)
    return f'{name}: polys={len(me.data.polygons)} h={me.dimensions.z:.2f} reweighted={n} before={before} after={after} clips={clips}'


def _hsv(a):
    mx = a.max(1); mn = a.min(1); d = mx - mn
    r, g, b = a[:, 0], a[:, 1], a[:, 2]; dd = np.where(d == 0, 1, d)
    h = np.where(mx == r, ((g - b) / dd) % 6, np.where(mx == g, (b - r) / dd + 2, (r - g) / dd + 4)) / 6
    return np.where(d == 0, 0, h), np.where(mx == 0, 0, d / np.where(mx == 0, 1, mx)), mx


def _rgb(h, s, v):
    i = np.floor(h * 6).astype(int) % 6; f = h * 6 - np.floor(h * 6)
    p, q, t = v * (1 - s), v * (1 - f * s), v * (1 - (1 - f) * s)
    out = np.zeros((len(h), 3), np.float32)
    for k, (R, G, B) in enumerate([(v, t, p), (q, v, p), (p, v, t), (p, q, v), (t, p, v), (v, p, q)]):
        m = i == k; out[m, 0] = R[m]; out[m, 1] = G[m]; out[m, 2] = B[m]
    return out


def tint_skin(img):
    """Pull the green skin cluster onto the approved Shambler green. Leaves cloth, metal and brains alone."""
    px = np.array(img.pixels[:], dtype=np.float32).reshape(-1, 4)
    h, s, v = _hsv(px[:, :3])
    hd = h * 360
    cand = (hd > 60) & (hd < 185) & (s > 0.12) & (v > 0.25)
    if cand.sum() < 100:
        return 0.0
    med = np.median(hd[cand]); lo, hi = max(60, med - 40), min(185, med + 40)
    w = np.clip(np.minimum(hd - lo, hi - hd) / 10, 0, 1) * np.clip((s - 0.06) / 0.08, 0, 1)
    m = w > 0
    core = w > 0.99
    k = min(SKIN_SAT / max(np.median(s[core]), 0.05), 2.3)
    nh = (SKIN_HUE + (h - np.median(h[core])) * 0.3) % 1
    new = _rgb(nh[m], np.clip(s[m] * k, 0, 0.85), v[m])
    px[m, :3] = px[m, :3] * (1 - w[m, None]) + new * w[m, None]
    img.pixels.foreach_set(px.ravel())
    return float(m.mean())


def export(name, game_height, tris=4000, tint=True, src=None, dst=None):
    folder = (src or SRC) + name + '/'
    out = dst or DST
    arm, me = objects()
    # Reduce only now, on the skinned mesh: the modifier keeps vertex groups.
    ratio = min(1.0, tris / max(1, sum(len(p.vertices) - 2 for p in me.data.polygons)))
    if ratio < 1.0:
        bpy.ops.object.select_all(action='DESELECT'); me.select_set(True); bpy.context.view_layer.objects.active = me
        mod = me.modifiers.new('Decimate', 'DECIMATE'); mod.ratio = ratio; mod.use_collapse_triangulate = True
        bpy.ops.object.modifier_move_to_index(modifier=mod.name, index=0)
        bpy.ops.object.modifier_apply(modifier=mod.name)
    # Scale the armature object (not applied) so bone animation stays consistent.
    s = game_height / me.dimensions.z
    arm.scale = [c * s for c in arm.scale]
    bpy.context.view_layer.update()

    bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); me.select_set(True)
    bpy.context.view_layer.objects.active = arm
    os.makedirs(out, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=out + name + '.fbx', use_selection=True, object_types={'ARMATURE', 'MESH'},
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', add_leaf_bones=False,
        bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, path_mode='STRIP', embed_textures=False, mesh_smooth_type='FACE')

    # Textures sit next to the rigged FBX; the Unity builder builds the rigged material from them.
    tex = out
    base = bpy.data.images.load(folder + 'texture_diffuse.png')
    tinted = tint_skin(base) if tint else 0.0
    maps = [(base, 'BaseColor'), (bpy.data.images.load(folder + 'texture_normal.png'), 'Normal')]
    if os.path.exists(folder + 'texture_emissive.png'):
        maps.append((bpy.data.images.load(folder + 'texture_emissive.png'), 'Emission'))
    for img, suffix in maps:
        c = img.copy(); c.scale(1024, 1024); c.filepath_raw = f'{tex}{name}_{suffix}.png'; c.file_format = 'PNG'; c.save()
        bpy.data.images.remove(c)
    return f'{name}: exported h={me.dimensions.z * s:.2f} tris~{tris} tinted={tinted:.2f}'
