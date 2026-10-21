"""Original Blender kit; FBX exports in Unity Assets, editable gallery beside this script."""
import bpy, math, os, json, random
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),"..",".."))
OUT=os.path.join(ROOT,"Assets","GestureFistGame","Art","BlenderKit")
os.makedirs(OUT,exist_ok=True)
bpy.ops.object.select_all(action="SELECT");bpy.ops.object.delete(use_global=False)
rng=random.Random(4182)
palette={"LeafMid":"648B69","LeafDark":"406D59","LeafLight":"799E4D","Trunk":"986957","Rock":"92919F","RockLight":"AAA8B3","Pumpkin":"F6A617","PumpkinLight":"FFC338","Wood":"C18B67","WoodEnd":"DEB18D","Flower":"FFF5D5","FlowerCenter":"EFC938","Cabbage":"F4E9B8","CabbageLeaf":"64986B","Gold":"F6BF32"}
mats={}
for n,h in palette.items():
 m=bpy.data.materials.new("BlenderKit_"+n);m.diffuse_color=tuple(int(h[i:i+2],16)/255 for i in (0,2,4))+(1,);mats[n]=m
kit=[];counts={};current=None
def start(name):
 global current
 current=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(current);kit.append(current)
def link(o):
 for c in list(o.users_collection):c.objects.unlink(o)
 current.objects.link(o);return o
def mesh(name,vs,fs,mat,smooth=False):
 me=bpy.data.meshes.new(name);me.from_pydata(vs,[],fs);me.update()
 o=bpy.data.objects.new(name,me);current.objects.link(o);me.materials.append(mats[mat])
 for f in me.polygons:f.use_smooth=smooth
 return o
def ellipsoid(name,loc,scale,mat,segments=14,rings=9,smooth=True,seed=0):
 vs=[(0,0,-1)];fs=[]
 for j in range(1,rings):
  a=-math.pi/2+j*math.pi/rings
  for i in range(segments):
   t=i*2*math.pi/segments;r=math.cos(a)*(1+.035*math.sin(t*3+seed)+.022*math.cos(t*5+a*3))
   vs.append((r*math.cos(t),r*math.sin(t),math.sin(a)))
 top=len(vs);vs.append((0,0,1))
 for i in range(segments):
  nxt=(i+1)%segments;fs.append((0,1+nxt,1+i))
  for j in range(rings-2):
   a=1+j*segments+i;b=1+j*segments+nxt;fs.append((a,b,b+segments,a+segments))
  fs.append((top,1+(rings-2)*segments+i,1+(rings-2)*segments+nxt))
 o=mesh(name,vs,fs,mat,smooth);o.location=loc;o.scale=scale;return o
def branch(name,a,b,r1,r2,mat="Trunk",sides=8):
 a,b=Vector(a),Vector(b);d=b-a;vs=[];fs=[]
 for h,r in ((0,r1),(d.length,r2)):
  for i in range(sides):
   t=i*math.tau/sides;vs.append((r*math.cos(t),r*math.sin(t),h))
 for i in range(sides):
  j=(i+1)%sides;fs.append((i,j,j+sides,i+sides))
 fs.extend([tuple(reversed(range(sides))),tuple(range(sides,2*sides))])
 o=mesh(name,vs,fs,mat);o.location=a;o.rotation_euler=d.to_track_quat('Z','Y').to_euler();return o
def box(name,loc,scale,mat,bevel=.025):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=link(bpy.context.object);o.name=name;o.scale=scale
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(mats[mat])
 if bevel:
  mod=o.modifiers.new("Hand-cut edge","BEVEL");mod.width=bevel;mod.segments=1
  bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
 return o
def export():
 bpy.ops.object.select_all(action="DESELECT");obs=list(current.objects)
 for o in obs:o.select_set(True)
 bpy.context.view_layer.objects.active=obs[0]
 bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,current.name+".fbx"),use_selection=True,object_types={"MESH"},apply_unit_scale=True,apply_scale_options="FBX_SCALE_ALL",axis_forward="-Z",axis_up="Y",bake_anim=False,add_leaf_bones=False)
 counts[current.name]=sum(len(o.data.polygons) for o in obs)
 for o in obs:o.location.x+=((len(kit)-1)%4)*5;o.location.y+=((len(kit)-1)//4)*5
def tree(name,variant):
 start(name)
 branch("FlaredRoots",(0,0,0),(.06,0,.28),.36,.22)
 branch("CrookedTrunk",(.06,0,.22),(-.09,.04,1.63),.22,.14)
 branch("LeftFork",(-.03,.02,1.12),(-.53,.02,2.1),.13,.06)
 branch("RightFork",(-.08,.04,1.43),(.53,.06,2.15),.11,.055)
 leaf="LeafLight" if variant else "LeafMid"
 ellipsoid("Canopy",(0,0,2.42),(1.15,.98,1.13),leaf,seed=variant+1)
 ellipsoid("CanopyLobe",(-.56,.08,2.34),(.63,.76,.71),leaf,seed=3)
 ellipsoid("CanopyLobeTop",(.24,.16,3.08),(.55,.54,.51),leaf,seed=6);export()
tree("Tree_Stylized_A",0);tree("Tree_Stylized_B",1)
start("Bush_Stylized");ellipsoid("Bush",(0,0,.58),(.9,.7,.65),"LeafMid",12,8,True,3);export()
start("Rock_Stylized");vs=[];fs=[];sides=7
for j,(z,r) in enumerate([(0,.71),(.57,.92),(1.08,.54)]):
 for i in range(sides):
  t=math.tau*i/sides;rr=r*(1+rng.uniform(-.16,.16))
  vs.append((rr*math.cos(t)+j*.07,rr*math.sin(t)*.8,z+rng.uniform(-.06,.06)))
for j in range(2):
 for i in range(sides):
  a=j*sides+i;b=j*sides+(i+1)%sides;fs.append((a,b,b+sides,a+sides))
fs.extend([tuple(reversed(range(sides))),tuple(range(2*sides,3*sides))]);mesh("AngularBoulder",vs,fs,"Rock");export()
start("Pumpkin_Stylized");o=ellipsoid("LobedPumpkin",(0,0,.43),(.52,.52,.46),"Pumpkin",24,10,True)
for v in o.data.vertices:
 t=math.atan2(v.co.y,v.co.x);f=1+.10*math.cos(t*10);v.co.x*=f;v.co.y*=f
branch("Stem",(0,0,.81),(.05,0,1.02),.075,.05,"LeafDark",7)
ellipsoid("Leaf",(.14,0,.88),(.19,.10,.035),"LeafMid",8,4);export()
start("Cabbage_Stylized")
for i in range(7):
 t=math.tau*i/7;o=ellipsoid("CuppedLeaf",(.30*math.cos(t),.30*math.sin(t),.19),(.16,.32,.23),"CabbageLeaf",8,5)
 o.rotation_euler=(.2*math.sin(t),.2*math.cos(t),t)
ellipsoid("Cauliflower",(0,0,.40),(.34,.34,.30),"Cabbage",12,7)
for i in range(5):
 t=i*math.tau/5;ellipsoid("Floret",(.18*math.cos(t),.18*math.sin(t),.56),(.19,.18,.17),"Cabbage",10,6)
export()
start("Fence_Stylized")
for i,x in enumerate((-1.5,0,1.5)):
 b=(x+.028*math.sin(i*2),0,1.06+.035*math.cos(i))
 branch("Post",(x,0,0),b,.115,.095,"Wood",9)
 branch("Endgrain",b,(b[0],b[1],b[2]+.008),.084,.084,"WoodEnd",10)
 branch("EndgrainHeart",(b[0],b[1],b[2]+.009),(b[0],b[1],b[2]+.014),.034,.034,"Wood",8)
for x in (-.75,.75):
 for z in (.38,.79):
  o=box("Rail",(x,-.01,z),(1.65,.12,.16),"Wood",.022);o.rotation_euler.y=.032 if x>0 else -.035
export()
start("Flower_Stylized");branch("Stem",(0,0,0),(0,0,.22),.015,.011,"LeafDark",6)
ellipsoid("LeafLeft",(-.09,0,.065),(.13,.065,.035),"LeafMid",8,4)
ellipsoid("LeafRight",(.07,.025,.075),(.12,.065,.035),"LeafMid",8,4)
for i in range(5):
 t=i*math.tau/5;ellipsoid("Petal",(.095*math.cos(t),.095*math.sin(t),.235),(.09,.085,.043),"Flower",10,5)
ellipsoid("Center",(0,0,.277),(.054,.054,.028),"FlowerCenter",10,4);export()
start("Crate_Stylized")
for side in (-1,1):
 for j in range(3):
  box("SidePlank",(0,side*.41,.16+j*.21),(.92,.085,.185),"Wood",.015)
  box("EndPlank",(side*.43,0,.16+j*.21),(.085,.80,.185),"WoodEnd",.015)
for i in range(4):box("LidPlank",(-.33+i*.22,0,.72),(.20,.88,.075),"Wood",.012)
for x in (-.32,.32):box("LidStrap",(x,0,.775),(.07,.90,.034),"WoodEnd",.007)
export()
start("Dock_Stylized")
for x in (-.75,.75):
 for y in (-.55,.55):branch("DockPost",(x,y,0),(x,y,.65),.08,.07,"Wood",8)
for i in range(7):box("DeckPlank",(-.78+i*.26,0,.40),(.245,1.4,.11),"Wood" if i%3 else "WoodEnd",.02)
export()
start("Chest_Stylized");box("ChestBox",(0,0,.30),(.95,.65,.55),"Wood",.06)
box("ChestLid",(0,0,.64),(1.0,.68,.22),"WoodEnd",.09)
for x in (-.34,.34):
 box("BrassBand",(x,-.335,.39),(.085,.025,.57),"Gold",.004)
 box("LidBand",(x,0,.76),(.085,.58,.025),"Gold",.004)
box("Lock",(0,-.37,.49),(.20,.06,.22),"Gold",.022)
box("Keyhole",(0,-.406,.48),(.036,.012,.075),"Trunk",.003);export()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,"Tools","Blender","StylizedEnvironmentKit.blend"))
with open(os.path.join(OUT,"mesh-manifest.json"),"w") as f:json.dump(counts,f,indent=2)
print("BLENDER_KIT_OK",counts)

