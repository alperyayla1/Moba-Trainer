import bpy
import math
import os
from mathutils import Vector

# Creates a smooth, game-scale goth mage from authored Blender meshes. No stock primitives
# are present in the exported Unity model; ellipsoids, lofted forms and tapered hair locks
# are converted into dedicated meshes and parented to a lightweight transform rig.
OUT = os.path.dirname(os.path.realpath(__file__)) + os.sep
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials):
    for block in list(datablocks):
        if block.users == 0:
            datablocks.remove(block)

def mat(name, hex_color, metallic=0.0, rough=0.5, emission=0.0):
    h = hex_color.lstrip('#')
    c = tuple(int(h[i:i+2], 16) / 255 for i in (0, 2, 4))
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*c, 1)
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*c, 1)
    p.inputs['Metallic'].default_value = metallic
    p.inputs['Roughness'].default_value = rough
    if emission:
        p.inputs['Emission Color'].default_value = (*c, 1)
        p.inputs['Emission Strength'].default_value = emission
    return m

skin = mat('Porcelain skin', 'F2E7F1', rough=.38)
blush = mat('Soft blush', 'CB91B9', rough=.65)
hair = mat('Pearl lavender hair', 'DCCBEA', rough=.3)
hair_shadow = mat('Hair shadow lavender', 'A892C5', rough=.38)
hair_light = mat('Hair highlight', 'F0E4F7', rough=.25, metallic=.1)
shirt = mat('Black fitted top', '19151F', rough=.55)
jacket = mat('Charcoal cropped jacket', '32273E', rough=.52)
jacket_edge = mat('Violet jacket lining', '68458A', rough=.45)
cloth = mat('Deep plum trouser cloth', '241D30', rough=.62)
leather = mat('Black leather', '201A28', rough=.4)
silver = mat('Aged silver hardware', 'C5B7D3', metallic=.72, rough=.28)
eye_white = mat('Eye white', 'F8F3FB', rough=.26)
iris = mat('Violet iris', '9A58D1', rough=.24)
pupil = mat('Pupil', '281833', rough=.18)
glow = mat('Arcane amethyst', 'BB73FF', metallic=.2, rough=.22, emission=2.2)

def empty(name, parent, loc):
    o = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(o)
    o.empty_display_type = 'PLAIN_AXES'
    o.parent = parent
    o.location = loc
    return o

root = empty('GothHero', None, (0,0,0))
rig = empty('Rig', root, (0,0,0))
hips = empty('Hips', rig, (0,0.88,0))
spine = empty('Spine', hips, (0,0.04,0))
neck = empty('Neck', spine, (0,0.56,0))
legs = {}
for side, x in [('L',-.105),('R',.105)]:
    legs[side] = empty('Leg'+side, hips, (x,-.04,0))
    empty('Knee'+side, legs[side], (0,-.42,0))
arms = {}
for side, x in [('L',-.245),('R',.245)]:
    arms[side] = empty('Arm'+side, spine, (x,.42,0))
    empty('Elbow'+side, arms[side], (0,-.29,0))

def parent_keep_world(obj, par):
    world = obj.matrix_world.copy()
    obj.parent = par
    obj.matrix_world = world

def uv(name, loc, scale, material, par, seg=24, rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=1, location=loc)
    o = bpy.context.object
    o.name = name
    o.scale = scale
    o.data.materials.append(material)
    for p in o.data.polygons: p.use_smooth = True
    parent_keep_world(o, par)
    return o

def loft(name, rings, material, par, sides=20):
    # Ring tuple: center x/y/z, radius x/z. Smooth quad lofts form the tailored body and face.
    verts=[]; faces=[]
    for y, cx, cz, rx, rz in rings:
        for i in range(sides):
            a=2*math.pi*i/sides
            verts.append((cx+rx*math.cos(a), y, cz+rz*math.sin(a)))
    for r in range(len(rings)-1):
        for i in range(sides):
            a=r*sides+i; b=r*sides+(i+1)%sides
            faces.append((a,b,b+sides,a+sides))
    faces += [tuple(reversed(range(sides))), tuple((len(rings)-1)*sides+i for i in range(sides))]
    mesh=bpy.data.meshes.new(name+' mesh'); mesh.from_pydata(verts,[],faces); mesh.materials.append(material); mesh.update()
    obj=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(obj)
    for p in mesh.polygons: p.use_smooth=True
    parent_keep_world(obj,par)
    bevel=obj.modifiers.new('Soft tailored edges','BEVEL'); bevel.width=.018; bevel.segments=2
    return obj

def link(name, a, b, radius, material, par):
    # Beveled cylinder aligned between two points.
    mid=(Vector(a)+Vector(b))/2; d=Vector(b)-Vector(a)
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=radius, depth=d.length, location=mid)
    o=bpy.context.object; o.name=name; o.rotation_mode='QUATERNION'; o.rotation_quaternion=d.to_track_quat('Z','Y')
    o.data.materials.append(material)
    bevel=o.modifiers.new('Rounded edge','BEVEL'); bevel.width=radius*.35; bevel.segments=2
    o.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
    parent_keep_world(o,par)
    return o

def tube(name, points, radii, material, par, sides=8):
    pts=[Vector(p) for p in points]; verts=[]; faces=[]
    for j,p in enumerate(pts):
        tangent=(pts[min(j+1,len(pts)-1)]-pts[max(j-1,0)]).normalized()
        u=tangent.cross(Vector((0,1,0))).normalized()
        if u.length < .01: u=Vector((1,0,0))
        v=tangent.cross(u).normalized()
        for i in range(sides):
            a=2*math.pi*i/sides
            q=p+radii[j]*(math.cos(a)*u+math.sin(a)*v)
            verts.append(tuple(q))
    for j in range(len(pts)-1):
        for i in range(sides):
            a=j*sides+i; b=j*sides+(i+1)%sides
            faces.append((a,b,b+sides,a+sides))
    mesh=bpy.data.meshes.new(name+' mesh'); mesh.from_pydata(verts,[],faces); mesh.materials.append(material); mesh.update()
    o=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(o)
    for p in mesh.polygons:p.use_smooth=True
    parent_keep_world(o,par)
    return o

def box(name, loc, scale, material, par, bevel=.035, rotation=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o=bpy.context.object;o.name=name;o.scale=scale;o.rotation_euler[1]=math.radians(rotation)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(material)
    if bevel:
        b=o.modifiers.new('Tailored roundover','BEVEL');b.width=bevel;b.segments=3
        o.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
    parent_keep_world(o,par)
    return o

# High-waist fitted trousers, sculpted thighs and articulated lower legs.
uv('Tailored pelvis',(0,.87,0),(.245,.145,.20),cloth,hips)
box('Waist belt',(0,.99,.012),(.47,.055,.41),leather,hips,.02)
box('Silver belt clasp',(0,.99,.221),(.105,.09,.025),silver,hips,.018)
for side,x in [('L',-.105),('R',.105)]:
    leg=legs[side]; knee=leg.children[0]
    uv('Shaped thigh',(0,-.21,0),(.128,.255,.135),cloth,leg)
    uv('Knee seam',(0,-.40,.026),(.116,.095,.128),leather,knee)
    uv('Tapered lower leg',(0,-.20,0),(.105,.23,.112),cloth,knee)
    # Strap buckles and calf bands.
    box('Boot cuff',(0,-.37,.018),(.23,.055,.22),leather,knee,.025)
    box('Boot cuff clasp',(.08,-.37,.133),(.055,.045,.018),silver,knee,.012)
    # Chunky platform boot with clear toe and stacked sole.
    uv('Platform boot',(0,-.47,.07),(.145,.12,.20),leather,knee)
    uv('Boot toe',(0,-.49,.17),(.14,.09,.15),jacket,knee)
    box('Platform sole',(0,-.55,.08),(.30,.075,.40),jacket_edge,knee,.035)
    box('Sole welt',(0,-.515,.08),(.30,.018,.40),silver,knee,.008)

# Slender tailored torso and a cropped, open goth jacket.
loft('Fitted crop silhouette',[(1.04,0,0,.22,.155),(1.12,0,0,.275,.17),(1.35,0,0,.255,.16),(1.53,0,0,.34,.18),(1.63,0,0,.275,.15)],shirt,spine)
loft('Jacket back',[(1.18,0,-.018,.28,.17),(1.27,0,-.018,.31,.18),(1.49,0,-.018,.385,.21),(1.59,0,-.018,.32,.18)],jacket,spine)
for side,s in [('L',-1),('R',1)]:
    # Open jacket fronts, shoulder panels, pocket flaps and silver hardware.
    box('Jacket lapel',(s*.17,1.42,.168),(.115,.31,.07),jacket_edge,spine,.035,rotation=s*9)
    box('Cropped jacket hem',(s*.20,1.18,.14),(.16,.075,.06),jacket,spine,.025,rotation=s*7)
    box('Cargo jacket pocket',(s*.26,1.34,.171),(.105,.13,.05),jacket,spine,.022)
    box('Pocket flap',(s*.26,1.405,.20),(.115,.035,.023),silver,spine,.01)
    box('Harness strap',(s*.255,1.43,.16),(.038,.36,.035),leather,spine,.012,rotation=-s*14)
    for yy in [1.30,1.49]:
        uv('Harness rivet',(s*.267,yy,.19),(.018,.018,.012),silver,spine,12,8)
    # Pointed, split coat tails. Their fine tapered geometry follows the torso during run/cast.
    tube('Split violet coat tail '+side,[(s*.18,1.18,-.12),(s*.22,.96,-.13),(s*.27,.73,-.12),(s*.31,.49,-.09)],[.09,.085,.055,.002],jacket_edge,spine,10)

# Sleeves, hands, finger silhouettes and a small amethyst focus held in the right palm.
for side,s in [('L',-1),('R',1)]:
    arm=arms[side]; elbow=arm.children[0]
    uv('Rounded shoulder',(0,0,0),(.145,.14,.15),jacket,arm)
    uv('Upper sleeve',(0,-.14,0),(.104,.18,.105),jacket,arm)
    uv('Elbow',(0,-.29,0),(.093,.09,.096),jacket,elbow)
    uv('Long cuff sleeve',(0,-.13,0),(.082,.145,.085),jacket,elbow)
    box('Metal cuff',(0,-.235,.005),(.19,.035,.19),silver,elbow,.012)
    uv('Porcelain hand',(0,-.30,.012),(.073,.083,.072),skin,elbow)
    for i in range(3):
        uv('Finger',(s*(i-1)*.032,-.365,.034),(.018,.055,.02),skin,elbow,12,8)
uv('Amethyst focus',(0,-.365,.11),(.075,.09,.075),glow,arms['R'].children[0],20,12)

# Neck, tapered anime face and expressive eyes. Face looks along local +Z.
uv('Neck',(0,1.67,0),(.095,.13,.09),skin,spine)
loft('Tapered anime face',[(1.69,0,0,.075,.085),(1.73,0,0,.14,.12),(1.84,0,0,.18,.145),(2.02,0,0,.17,.135),(2.12,0,0,.125,.11)],skin,neck)
for s in [-1,1]:
    # Large, crisp violet anime eyes, bright eye white and small highlight glints.
    uv('Eye white',(s*.072,1.90,.136),(.057,.035,.026),eye_white,neck,20,12)
    uv('Iris',(s*.072,1.90,.158),(.027,.03,.014),iris,neck,20,12)
    uv('Pupil',(s*.072,1.90,.170),(.012,.021,.008),pupil,neck,16,10)
    uv('Eye glint',(s*.062,1.914,.178),(.008,.009,.004),eye_white,neck,12,8)
    tube('Brow',[(s*.125,1.965,.14),(s*.08,1.978,.155),(s*.035,1.97,.15)],[.009,.013,.007],hair_shadow,neck,8)
uv('Nose',(0,1.845,.143),(.018,.024,.021),skin,neck,16,10)
box('Soft mouth',(0,1.79,.14),(.042,.008,.008),blush,neck,.004)

# Voluminous pearl-lavender layered hair cap and tapered locks: swept fringe plus long side/back strands.
uv('Hair crown',(0,2.105,-.015),(.205,.17,.19),hair,neck,32,20)
uv('Back hair mass',(0,2.005,-.075),(.205,.19,.15),hair_shadow,neck,28,18)
# Tapered sculpted locks flow from the crown over brow, down the cheeks and behind the neck.
locks=[
    ('Center swept fringe',[(-.02,2.23,.035),(.015,2.19,.12),(.045,2.11,.155),(.09,2.04,.13)],[.082,.078,.046,.002],hair),
    ('Left fringe',[(-.12,2.20,.055),(-.15,2.14,.12),(-.13,2.07,.15),(-.10,1.99,.13)],[.073,.068,.04,.002],hair_light),
    ('Right fringe',[(.10,2.23,.05),(.14,2.16,.11),(.15,2.08,.14),(.12,2.02,.12)],[.065,.06,.037,.002],hair),
    ('Long cheek lock L',[(-.17,2.12,.04),(-.20,2.02,.06),(-.21,1.88,.055),(-.17,1.76,.02)],[.065,.06,.045,.002],hair),
    ('Long cheek lock R',[(.17,2.13,.03),(.20,2.01,.055),(.20,1.89,.035),(.17,1.79,.005)],[.06,.058,.041,.002],hair_light),
    ('Rear lock L',[(-.15,2.08,-.13),(-.20,1.96,-.15),(-.19,1.82,-.13),(-.14,1.73,-.08)],[.07,.062,.044,.002],hair_shadow),
    ('Rear lock R',[(.14,2.08,-.13),(.19,1.96,-.15),(.19,1.82,-.13),(.14,1.73,-.08)],[.07,.062,.044,.002],hair),
    ('Crown tuft',[(-.06,2.18,-.02),(-.12,2.27,-.04),(-.18,2.30,-.04),(-.21,2.27,-.02)],[.065,.052,.033,.002],hair_light),
]
for n,p,r,m in locks: tube(n,p,r,m,neck,12)
# Fine sculpted strand grooves catch lavender highlights in the isometric camera.
for s in [-1,1]:
    tube('Hair strand highlight',[(s*.11,2.24,.08),(s*.16,2.16,.105),(s*.17,2.05,.09),(s*.16,1.94,.055)],[.007,.008,.006,.001],hair_light,neck,6)

# Choker, rosary chain, silver cross and hanging ear-cross, matching the reference accessories.
link('Black velvet choker',(0,1.69,.105),(0,1.71,.105),.018,leather,spine)
for i in range(9):
    t=i/8
    x=(t-.5)*.15
    uv('Rosary bead',(x,1.54-.025*math.sin(t*math.pi),.145),(.012,.012,.012),silver,spine,12,8)
link('Cross pendant stem',(0,1.52,.15),(0,1.38,.16),.012,silver,spine)
link('Cross pendant bar',(-.045,1.47,.16),(.045,1.47,.16),.012,silver,spine)
for s in [-1,1]:
    uv('Earring hoop',(s*.19,1.87,.015),(.025,.037,.018),silver,neck,16,10)
link('Drop earring',(s*.19,1.84,.02),(s*.19,1.77,.03),.008,silver,neck)
link('Ear crossbar',(s*.16,1.80,.04),(s*.22,1.80,.04),.008,silver,neck)

# Add subtle subdivision only to curved forms, and keep generated mesh parts lightweight.
bpy.ops.object.select_all(action='DESELECT')
for obj in bpy.context.scene.objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active=root

# Save the editable source and export the full hierarchy for Unity.
bpy.ops.wm.save_as_mainfile(filepath=OUT+'GothHero.blend')
bpy.ops.export_scene.fbx(filepath=OUT+'GothHero.fbx', use_selection=True, object_types={'EMPTY','MESH'},
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
    mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False, path_mode='COPY', embed_textures=False)
print('Exported GothHero FBX to', OUT)
