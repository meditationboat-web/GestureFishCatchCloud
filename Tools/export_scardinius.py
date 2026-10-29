import bpy
bpy.ops.wm.open_mainfile(filepath='E:/NOMA/fish/scardinius.blend')
for ob in bpy.data.objects:
 print('MODEL_OBJECT',ob.name,ob.type,tuple(ob.dimensions),tuple(ob.rotation_euler))
if bpy.context.object and bpy.context.object.mode != 'OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
for ob in bpy.context.scene.objects: ob.select_set(False)
for ob in bpy.context.scene.objects:
 if ob.type in {'MESH','ARMATURE'}: ob.select_set(True)
bpy.ops.export_scene.fbx(filepath='D:/UnityProject/手势识别打拳项目/MediaPipeUnityPlugin-all-stripped-B and P/MediaPipeUnityPlugin-all-stripped(2)/Assets/GestureFistGame/Art/Scardinius/Scardinius.fbx',use_selection=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y')

