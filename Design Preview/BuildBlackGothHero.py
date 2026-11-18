import bpy, math, os
from mathutils import Vector

OUT = os.path.join(os.path.dirname(os.path.realpath(__file__)), 'BlackGothHero')
os.makedirs(OUT, exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for d in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
    for x in list(d):
        if x.users == 0: d.remove(x)

def material(n, color, metal=0, rough=.55, emission=0):
    c=tuple(int(color[i:i+2],16)/255 for i in (0,2,4)); m=bpy.data.materials.new(n); m.diffuse_color=(*c,1); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*c,1); p.inputs['Metallic'].default_value=metal; p.inputs['Roughness'].default_value=rough
    if emission: p.inputs['Emission Color'].default_value=(*c,1); p.inputs['Emission Strength'].default_value=emission
    return m
skin=material('Pale porcelain skin','F4EAF0',rough=.36); blush=material('Muted rose','A96979'); hair=material('Ink black hair','17151B',rough=.31); hairhi=material('Hair sheen','302A36',rough=.28)
shirt=material('Black high neck crop','121116'); coat=material('Oversized black jacket','1C1A20',rough=.64); coat2=material('Jacket panels','29252E'); red=material('Muted blood red','8E2838'); pants=material('Charcoal warrior cargo','17161B',rough=.78); pants2=material('Cargo panel cloth','242229',rough=.8)
leather=material('Boot leather','111116',rough=.28); sole=material('Rubber sole','24232A',rough=.48); steel=material('Gunmetal hardware','88838D',metal=.78,rough=.26); eye=material('Warm grey eye white','E7DDE1',rough=.28); iris=material('Red brown iris','8D4552',rough=.25); pupil=material('Pupil','17131A',rough=.2)

def mesh_obj(name, verts, faces, mat):
    me=bpy.data.meshes.new(name+' mesh'); me.from_pydata(verts,[],faces); me.materials.append(mat); me.update(); o=bpy.data.objects.new(name,me); bpy.context.collection.objects.link(o); return o
def ell(name,loc,scale,mat,seg=20,rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg,ring_count=rings,radius=1,location=loc); o=bpy.context.object; o.name=name; o.scale=scale; o.data.materials.append(mat)
    for p in o.data.polygons:p.use_smooth=True
    return o
def cube(name,loc,scale,mat,bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc); o=bpy.context.object;o.name=name;o.dimensions=scale; bpy.ops.object.transform_apply(location=False,rotation=False,scale=True); o.data.materials.append(mat)
    if bevel: b=o.modifiers.new('Soft tailored edges','BEVEL');b.width=bevel;b.segments=3;o.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
    return o
def rod(name,a,b,r,mat,verts=12):
    a,b=Vector(a),Vector(b); d=b-a; bpy.ops.mesh.primitive_cylinder_add(vertices=verts,radius=r,depth=d.length,location=(a+b)*.5); o=bpy.context.object;o.name=name;o.rotation_mode='QUATERNION';o.rotation_quaternion=d.to_track_quat('Z','Y');o.data.materials.append(mat);be=o.modifiers.new('Rounded edges','BEVEL');be.width=r*.28;be.segments=2;o.modifiers.new('Weighted normals','WEIGHTED_NORMAL');return o
def curve(name,pts,r,mat,bevres=4):
    c=bpy.data.curves.new(name,'CURVE');c.dimensions='3D';c.resolution_u=10;c.bevel_depth=r;c.bevel_resolution=bevres
    s=c.splines.new('POLY');s.points.add(len(pts)-1)
    for p,v in zip(s.points,pts):p.co=(*v,1)
    o=bpy.data.objects.new(name,c);bpy.context.collection.objects.link(o);o.data.materials.append(mat);return o
def loft(name, rings, mat, sides=24):
    # rings are (z, xcenter, ycenter, x radius, y radius)
    vs=[];fs=[]
    for z,cx,cy,rx,ry in rings:
        for i in range(sides):
            a=2*math.pi*i/sides;vs.append((cx+rx*math.cos(a),cy+ry*math.sin(a),z))
    for j in range(len(rings)-1):
        for i in range(sides):a=j*sides+i;b=j*sides+(i+1)%sides;fs.append((a,b,b+sides,a+sides))
    fs.extend([tuple(reversed(range(sides))),tuple((len(rings)-1)*sides+i for i in range(sides))]);o=mesh_obj(name,vs,fs,mat)
    for p in o.data.polygons:p.use_smooth=True
    bevel=o.modifiers.new('Cloth silhouette soften','BEVEL');bevel.width=.012;bevel.segments=2
    return o

# Bone rig (Y-up in Blender coordinates), with rigidly attached authored mesh pieces.
arm=bpy.data.armatures.new('Goth Hero Skeleton');rig=bpy.data.objects.new('GothHero_Rig',arm);bpy.context.collection.objects.link(rig);bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
spec={'root':((0,0,0),(0,0,.88),None),'spine':((0,0,.88),(0,0,1.48),'root'),'head':((0,0,1.48),(0,0,2.03),'spine')}
for side,s in [('L',-1),('R',1)]:
    spec['thigh.'+side]=((s*.12,0,.91),(s*.12,0,.48),'root');spec['shin.'+side]=((s*.12,0,.48),(s*.12,0,.13),'thigh.'+side);spec['foot.'+side]=((s*.12,-.02,.13),(s*.12,-.19,.08),'shin.'+side)
    spec['upper_arm.'+side]=((s*.28,0,1.39),(s*.44,0,1.02),'spine');spec['forearm.'+side]=((s*.44,0,1.02),(s*.48,-.01,.73),'upper_arm.'+side);spec['hand.'+side]=((s*.48,-.01,.73),(s*.48,-.05,.61),'forearm.'+side)
bones={}
for n,(h,t,par) in spec.items():
    b=arm.edit_bones.new(n);b.head=h;b.tail=t
    if par:b.parent=arm.edit_bones[par];b.use_connect=False
    bones[n]=b
bpy.ops.object.mode_set(mode='OBJECT');rig.show_in_front=True;rig.display_type='WIRE'
def attach(o,bone):
    # Rigid skin each authored part to one skeleton bone. Unlike bone-parenting,
    # this preserves the modeled world placement and exports as a real deform rig.
    if o.type != 'MESH':
        bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
        bpy.ops.object.convert(target='MESH');o=bpy.context.object
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    vg=o.vertex_groups.new(name=bone)
    if o.data.vertices: vg.add(list(range(len(o.data.vertices))),1.0,'REPLACE')
    mod=o.modifiers.new('GothHero skeleton','ARMATURE');mod.object=rig
    return o

# Body base and fitted high-neck crop top; jacket deliberately open to show it.
o=loft('Black sleeveless high-neck crop',[(1.02,0,-.006,.245,.137),(1.08,0,-.006,.252,.14),(1.28,0,-.006,.235,.13),(1.38,0,-.006,.292,.145),(1.43,0,-.006,.225,.12)],shirt);attach(o,'spine')
for s in (-1,1):
    # shoulders and oversized jacket torso halves, sleeves, cuff stripe
    o=ell('Jacket shoulder',(s*.29,0,1.36),(.105,.115,.095),coat);attach(o,'spine')
    o=loft('Jacket open front '+str(s),[(.99,s*.20,.004,.125,.115),(1.12,s*.20,.004,.13,.12),(1.33,s*.20,.004,.155,.13),(1.42,s*.20,.004,.13,.105)],coat,16);attach(o,'spine')
    # loose puff sleeves as connected segments, clearly articulated
    a=(s*.31,0,1.32);b=(s*.41,0,1.04);c=(s*.45,-.005,.70)
    for name,p,q,r in [('Sleeve upper',a,b,.085),('Sleeve forearm',b,c,.09)]:
        o=rod(name,p,q,r,coat,16);attach(o,'upper_arm.'+('L' if s<0 else 'R') if name=='Sleeve upper' else 'forearm.'+('L' if s<0 else 'R'))
    o=ell('Sleeve elbow',b,(.095,.095,.10),coat);attach(o,'forearm.'+('L' if s<0 else 'R'))
    o=rod('Ribbed red cuff',(.45*s,-.015,.75),(.46*s,-.015,.69),.078,red,16);attach(o,'forearm.'+('L' if s<0 else 'R'))
    # Red 3-stripe crossed sleeve insignia, attached to outer left sleeve.
    if s<0:
        o=cube('Insignia cloth patch',(-.385,-.102,1.22),(.13,.014,.115),coat2,.008);o.rotation_euler[1]=-.18;attach(o,'upper_arm.L')
        for z in (1.185,1.22,1.255):
            o=cube('Red sleeve band',(-.385,-.113,z),(.13,.012,.012),red,.002);o.rotation_euler[1]=-.18;attach(o,'upper_arm.L')
        for x in (-.425,-.385,-.345):
            o=cube('Red sleeve grid', (x,-.113,1.22),(.012,.012,.09),red,.002);o.rotation_euler[1]=-.18;attach(o,'upper_arm.L')
    # Hands
    o=ell('Pale hand',(s*.48,-.045,.68),(.052,.038,.068),skin);attach(o,'hand.'+('L' if s<0 else 'R'))
    for j in range(3):
        o=rod('Finger',(s*(.455+j*.023),-.055,.65),(s*(.455+j*.023),-.06,.59),.012,skin,8);attach(o,'hand.'+('L' if s<0 else 'R'))
    # Jacket hem, pocket flap and asymmetric lapel
    o=cube('Jacket hanging hem',(s*.22,.002,.99),(.17,.245,.15),coat,.035);attach(o,'spine')
    o=cube('Chest pocket flap',(s*.235,-.13,1.29),(.15,.035,.06),coat2,.012);attach(o,'spine')
    o=curve('Open zipper edge',[(s*.105,-.145,1.41),(s*.14,-.153,1.24),(s*.16,-.151,1.06)],.008,steel);attach(o,'spine')
    for z in (1.13,1.23,1.33):
        o=ell('Jacket snap',(s*.30,-.132,z),(.012,.008,.012),steel,12,8);attach(o,'spine')

# Choker, ring and small earrings.
o=ell('Neck',(0,0,1.49),(.085,.078,.13),skin);attach(o,'head')
o=loft('Face',[(1.58,0,-.006,.065,.066),(1.64,0,-.018,.13,.105),(1.76,0,-.016,.145,.118),(1.88,0,-.005,.13,.112),(1.94,0,0,.09,.078)],skin,24);attach(o,'head')
o=curve('Black choker',[(.081*math.cos(i*2*math.pi/32),.081*math.sin(i*2*math.pi/32),1.56) for i in range(33)],.012,leather);attach(o,'head')
o=ell('Choker ring',(0,-.092,1.555),(.026,.01,.032),steel,16,10);attach(o,'head')
for s in (-1,1):
    o=ell('Ear', (s*.136,-.005,1.72),(.025,.044,.055),skin);attach(o,'head')
    # eye whites with red-brown iris facing front (-Y)
    o=ell('Eye',(s*.058,-.106,1.77),(.045,.022,.026),eye);attach(o,'head')
    o=ell('Iris',(s*.058,-.128,1.77),(.018,.012,.021),iris);attach(o,'head')
    o=ell('Pupil',(s*.058,-.137,1.77),(.008,.007,.015),pupil);attach(o,'head')
    o=curve('Eyeliner',[(s*.105,-.126,1.78),(s*.063,-.139,1.80),(s*.02,-.125,1.79)],.006,hair);attach(o,'head')
o=ell('Nose',(0,-.123,1.705),(.014,.018,.022),skin);attach(o,'head')
o=curve('Mouth',[(-.025,-.122,1.66),(0,-.127,1.654),(.026,-.122,1.66)],.004,blush);attach(o,'head')

# Black layered anime hair: cap plus long tapered locks swept across forehead and nape.
o=ell('Hair crown',(0,.006,1.94),(.15,.12,.115),hair,28,18);attach(o,'head')
o=ell('Back hair',(0,.035,1.82),(.15,.11,.13),hair,24,16);attach(o,'head')
locks=[([(-.14,-.09,1.97),(-.12,-.14,1.88),(-.07,-.145,1.78),(-.04,-.12,1.73)], [.065,.058,.033,.001],hair),
([(-.07,-.1,2.045),(-.055,-.15,1.96),(.005,-.16,1.86),(.05,-.13,1.81)],[.06,.055,.038,.001],hairhi),
([(.015,-.095,2.07),(.065,-.14,1.98),(.07,-.155,1.88),(.11,-.12,1.83)],[.065,.052,.03,.001],hair),
([(.13,-.07,2.0),(.16,-.12,1.91),(.18,-.10,1.82),(.15,-.08,1.76)],[.05,.045,.03,.001],hairhi),
([(-.15,.02,1.9),(-.18,.05,1.82),(-.17,.04,1.73),(-.13,.03,1.68)],[.05,.04,.025,.001],hair),
([(.14,.02,1.9),(.18,.05,1.81),(.17,.04,1.72),(.13,.03,1.68)],[.05,.04,.025,.001],hairhi)]
for idx,(pts,rs,m) in enumerate(locks):
    # tapered curved polyline tubes with fine hair-like form
    cu=bpy.data.curves.new('Hair lock','CURVE');cu.dimensions='3D';cu.resolution_u=8;cu.bevel_depth=1;cu.bevel_resolution=3;sp=cu.splines.new('BEZIER');sp.bezier_points.add(len(pts)-1)
    for bp,co,r in zip(sp.bezier_points,pts,rs):bp.co=co;bp.radius=r;bp.handle_left_type='AUTO';bp.handle_right_type='AUTO'
    ob=bpy.data.objects.new('Layered black hair lock',cu);bpy.context.collection.objects.link(ob);ob.data.materials.append(m);attach(ob,'head')

# Low waist dark warrior cargo trousers with layered panels and restrained straps.
o=loft('Low waist cargo trousers',[(.88,0,.006,.26,.14),(.94,0,.006,.265,.145),(1.01,0,.006,.25,.14)],pants,28);attach(o,'root')
o=cube('Minimal low waist belt',(0,-.008,.936),(.49,.292,.042),leather,.014);attach(o,'root')
o=cube('Small belt buckle',(0,-.16,.937),(.075,.018,.055),steel,.008);attach(o,'root')
for s in (-1,1):
    side='L' if s<0 else 'R';x=s*.12
    # moderately loose articulated upper and lower legs; jogger cuffs tuck into boots
    o=ell('Warrior trouser thigh',(x,0,.70),(.145,.14,.27),pants,24,16);attach(o,'thigh.'+side)
    o=ell('Reinforced knee',(x,-.008,.48),(.132,.145,.115),pants2);attach(o,'shin.'+side)
    o=ell('Tapered gathered trouser calf',(x,0,.29),(.118,.125,.23),pants,24,16);attach(o,'shin.'+side)
    # angled fabric panel wraps front outer thigh, small cargo flap
    o=cube('Layered thigh panel',(x+s*.085,-.127,.70),(.075,.018,.24),pants2,.012);o.rotation_euler[1]=s*-.12;attach(o,'thigh.'+side)
    o=cube('Utility pocket',(x+s*.135,-.132,.73),(.09,.035,.14),pants2,.012);attach(o,'thigh.'+side)
    o=cube('Cargo flap',(x+s*.135,-.155,.80),(.10,.018,.035),coat2,.006);attach(o,'thigh.'+side)
    # New Rock inspired platform boot, modest scale, straps and gunmetal buckles.
    o=ell('Boot ankle',(x,0,.115),(.105,.12,.13),leather);attach(o,'foot.'+side)
    o=ell('Rounded boot toe',(x,-.13,.09),(.115,.20,.082),leather);attach(o,'foot.'+side)
    o=cube('Chunky platform sole',(x,-.08,.035),(.245,.36,.06),sole,.025);attach(o,'foot.'+side)
    o=cube('Raised platform welt',(x,-.08,.063),(.238,.355,.018),steel,.008);attach(o,'foot.'+side)
    for z in (.095,.145):
        o=cube('Boot strap',(x,-.115,z),(.22,.025,.025),coat2,.008);attach(o,'foot.'+side)
    o=cube('Boot metal buckle',(x+s*.07,-.135,.145),(.045,.018,.038),steel,.006);attach(o,'foot.'+side)
    for j in range(3):
        o=ell('Boot rivet',(x+s*.105,-.18+j*.085,.08),(.009,.009,.009),steel,10,6);attach(o,'foot.'+side)

# Chain and hanging thigh hardware, intentionally limited to match ref.
def chain_pts(a,b,count=11,sag=.035):
    a,b=Vector(a),Vector(b);return [tuple(a.lerp(b,i/(count-1))+Vector((0, sag*math.sin(math.pi*i/(count-1)),0))) for i in range(count)]
o=curve('Silver hip chain',chain_pts((-.18,-.17,.92),(-.39,-.13,.70),13,.10),.008,steel);attach(o,'root')
for s in (-1,1):
    o=rod('Hip chain drop',(s*.23,-.15,.90),(s*.25,-.13,.75),.006,steel,8);attach(o,'root')

# Subtle accessory on jacket chest, earring crosses, and open jacket lapels.
for s in (-1,1):
    o=curve('Pendant earring',[(s*.15,-.02,1.72),(s*.16,-.035,1.67)],.006,steel);attach(o,'head')
    o=cube('Open jacket lapel',(s*.155,-.15,1.36),(.085,.03,.22),coat2,.012);o.rotation_euler[1]=s*.17;attach(o,'spine')
o=cube('Chest patch',( .245,-.158,1.19),(.085,.02,.14),pants2,.01);attach(o,'spine')

# Animation actions on actual skeleton: looping walk and readable one-arm skill throw.
def action(name, frames, poses, loop=False):
    bpy.context.scene.frame_start=frames[0];bpy.context.scene.frame_end=frames[-1]
    bpy.ops.object.mode_set(mode='OBJECT') if bpy.context.object and bpy.context.object.mode!='OBJECT' else None
    bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='POSE')
    for bn in rig.pose.bones: bn.rotation_mode='XYZ'
    for fr, vals in zip(frames,poses):
        bpy.context.scene.frame_set(fr)
        for bn,rot in vals.items():
            pb=rig.pose.bones[bn];pb.rotation_euler=rot;pb.keyframe_insert(data_path='rotation_euler',frame=fr,group=bn)
    bpy.ops.object.mode_set(mode='OBJECT')
    act=rig.animation_data.action;act.name=name
    # Blender 5.2 stores action curves in layered action slots; the inserted
    # keyframes use Bezier interpolation by default. NLA repeats the walk clip.
    return act
walkposes=[{'thigh.L':(0,-.34,0),'thigh.R':(0,.34,0),'upper_arm.L':(.10,.26,0),'upper_arm.R':(-.10,-.26,0),'forearm.L':(-.18,0,0),'forearm.R':(-.18,0,0),'spine':(.02,0,0)}, {'thigh.L':(0,.34,0),'thigh.R':(0,-.34,0),'upper_arm.L':(-.10,.26,0),'upper_arm.R':(.10,-.26,0),'forearm.L':(-.18,0,0),'forearm.R':(-.18,0,0),'spine':(-.02,0,0)}, {'thigh.L':(0,-.34,0),'thigh.R':(0,.34,0),'upper_arm.L':(.10,.26,0),'upper_arm.R':(-.10,-.26,0),'forearm.L':(-.18,0,0),'forearm.R':(-.18,0,0),'spine':(.02,0,0)}]
walk=action('Walk_Loop',(1,9,17),walkposes,True)
# Keep walk as NLA strip before making skill action, ensuring both clips are available.
rig.animation_data.action=None;tr=rig.animation_data.nla_tracks.new();tr.name='Walk Loop';strip=tr.strips.new('Walk_Loop',1,walk);strip.action_frame_start=1;strip.action_frame_end=17;strip.repeat=2
skillposes=[{}, {'spine':(0,0,-.12),'upper_arm.R':(-.5,0,-.55),'forearm.R':(-.3,0,-.1)}, {'spine':(0,0,-.20),'upper_arm.R':(-1.05,0,-.35),'forearm.R':(-.75,0,-.05)}, {'spine':(0,0,.16),'upper_arm.R':(-1.18,0,.5),'forearm.R':(-.25,0,.08),'upper_arm.L':(.10,0,-.12)}, {'spine':(0,0,.05),'upper_arm.R':(-.35,0,.20),'forearm.R':(-.35,0,0)}, {}]
skill=action('Skill_Throw',(25,29,33,37,41,45),skillposes,False)
rig.animation_data.action=None;tr2=rig.animation_data.nla_tracks.new();tr2.name='Skill Throw';tr2.strips.new('Skill_Throw',25,skill)
bpy.ops.object.mode_set(mode='OBJECT') if bpy.context.object and bpy.context.object.mode!='OBJECT' else None

# Studio camera/lights and groundless dark studio render for standalone preview.
world=bpy.data.worlds.new('Night studio');bpy.context.scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs['Color'].default_value=(.012,.01,.02,1);world.node_tree.nodes['Background'].inputs['Strength'].default_value=.18
def area(name,loc,power,size,color):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;d.color=color;o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
area('Key softbox',(-3,-5,4),260,4,(.78,.73,1));area('Warm fill',(3,-3,2.8),120,3,(1,.75,.78));area('Hair rim',(1,2,3.6),330,2,(.56,.46,1))
camd=bpy.data.cameras.new('Character preview camera');cam=bpy.data.objects.new('Character preview camera',camd);bpy.context.collection.objects.link(cam);cam.location=(1.25,-8.5,2.4);target=Vector((0,0,1.04));cam.rotation_euler=(target-Vector(cam.location)).to_track_quat('-Z','Y').to_euler();camd.type='ORTHO';camd.ortho_scale=2.75;bpy.context.scene.camera=cam
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.render.resolution_x=900;scene.render.resolution_y=1250;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.render.filepath=os.path.join(OUT,'BlackGothHero_Preview.png');scene.view_settings.view_transform='AgX'
scene.frame_set(1)
# Set pose/animation playback defaults and save editable project.
scene.frame_start=1;scene.frame_end=45;scene.render.fps=24
for a in bpy.context.screen.areas if bpy.context.screen else []:
    if a.type=='VIEW_3D':a.spaces.active.region_3d.view_distance=3.5
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'BlackGothHero.blend'))
# Export model and animations; skeletal animation clips bake from the armature.
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
for ob in bpy.context.scene.objects:
    if ob.type in {'MESH','CURVE'} and ob.parent==rig: ob.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'BlackGothHero.fbx'),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_nla_strips=True,bake_anim_use_all_actions=True,mesh_smooth_type='FACE',path_mode='COPY')
# Render after export at a clean neutral frame.
scene.frame_set(1);tr.mute=True;tr2.mute=True
for pb in rig.pose.bones: pb.rotation_euler=(0,0,0);pb.location=(0,0,0)
bpy.context.view_layer.update();bpy.ops.render.render(write_still=True);tr.mute=False;tr2.mute=False
print('Saved Blender preview, armature and FBX in',OUT)
