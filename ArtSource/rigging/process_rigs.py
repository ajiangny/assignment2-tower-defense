"""Clean up Rodin auto-rigs for Unity.

Run inside Blender:  exec(open('/Users/anjujiang/assignment2/ArtSource/rigging/process_rigs.py').read())
For each zombie: import the Rodin rig, move chibi-head vertices that the auto-rigger
bound to the shoulders onto the Head bone, keep Walk/Hit/Die clips, export FBX to Unity.
"""
import bpy, collections, os

SRC = '/Users/anjujiang/assignment2/ArtSource/rigging/out/'
DST = '/Users/anjujiang/assignment2/Assets/Models/Zombies/Rigged/'
REASSIGN = {'Head', 'Neck', 'LeftShoulder', 'RightShoulder', 'Spine', 'Spine1', 'Spine2'}
WALK = {
    'Shambler': 'ScaryClownWalk', 'Runner': 'InjuredRun', 'Clubber': 'OrchammerLocoWalkFwdNorm',
    'PlagueBloater': 'ScaryClownWalk', 'ArmoredRevenant': 'ScaryClownWalk', 'ZombieKing': 'OrchammerLocoWalkFwdNorm',
}


def clear():
    for o in list(bpy.data.objects): bpy.data.objects.remove(o)
    for a in list(bpy.data.actions): bpy.data.actions.remove(a)
    for m in list(bpy.data.meshes): bpy.data.meshes.remove(m)
    for a in list(bpy.data.armatures): bpy.data.armatures.remove(a)


def imp(path):
    win = bpy.context.window_manager.windows[0]
    area = next(a for a in win.screen.areas if a.type == 'VIEW_3D')
    with bpy.context.temp_override(window=win, area=area):
        bpy.ops.import_scene.fbx(filepath=path)


def dominant(me, v, names):
    return names[max(v.groups, key=lambda g: g.weight).group] if v.groups else None


def head_counts(me, z_min):
    names = {g.index: g.name for g in me.vertex_groups}
    c = collections.Counter()
    for v in me.data.vertices:
        if (me.matrix_world @ v.co).z > z_min:
            c[dominant(me, v, names) or '<none>'] += 1
    return c.most_common(4)


def fix_head(arm, me):
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
        dom = dominant(me, v, names)
        if dom is not None and dom not in REASSIGN:
            continue  # arms and hands keep their weights
        w = 1.0 if (z >= hi or not v.groups) else (z - lo) / (hi - lo)
        for g in list(v.groups):
            me.vertex_groups[g.group].add([v.index], g.weight * (1 - w), 'REPLACE')
        head.add([v.index], w, 'ADD')
        n += 1
    return before, head_counts(me, probe), n


def process(name):
    clear()
    if name == 'Shambler':
        # The walk comes from the second Shambler rig (same mesh and skeleton names).
        imp(SRC + 'Shambler/Shambler_walks.fbx')
        walk = next(a for a in bpy.data.actions if WALK[name] in a.name)
        walk.use_fake_user = True
        for o in list(bpy.data.objects): bpy.data.objects.remove(o)
        for a in list(bpy.data.actions):
            if a is not walk: bpy.data.actions.remove(a)
        imp(SRC + 'Shambler/Shambler_rigged.fbx')
    else:
        imp(SRC + f'{name}/{name}_rigged.fbx')

    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    me = next(o for o in bpy.data.objects if o.type == 'MESH')
    before, after, n = fix_head(arm, me)

    keep = {}
    for a in bpy.data.actions:
        if WALK[name] in a.name: keep['Walk'] = a
        elif 'HeadHit' in a.name: keep['Hit'] = a
        elif 'StandingDeathBackward' in a.name: keep['Die'] = a
    for a in list(bpy.data.actions):
        if a not in keep.values(): bpy.data.actions.remove(a)
    for k, a in keep.items():
        a.name = k
        a.use_fake_user = True
    arm.animation_data.action = keep['Walk']

    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True); me.select_set(True)
    bpy.context.view_layer.objects.active = arm
    os.makedirs(DST, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=DST + name + '.fbx', use_selection=True, object_types={'ARMATURE', 'MESH'},
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', add_leaf_bones=False,
        bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, path_mode='STRIP', embed_textures=False, mesh_smooth_type='FACE')
    return f'{name}: h={me.dimensions.z:.2f} reweighted={n} before={before} after={after} clips={sorted(keep)}'


def run(names=tuple(WALK)):
    for n in names:
        try:
            print(process(n))
        except Exception as e:
            print(n, 'FAILED', repr(e))
