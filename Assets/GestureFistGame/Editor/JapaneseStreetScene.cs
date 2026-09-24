using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace GestureFistGame.Editor
{
  public static class JapaneseStreetScene
  {
    private const string Source="Assets/Japanese_Street/";
    private const string Materials="Assets/GestureFistGame/Materials/JapaneseStreet/";
    private const string EnvironmentName="Japanese Street - 日式街巷";
    private static readonly List<string> Audit=new List<string>();
    private static Material _road,_concrete,_wall,_metal,_white,_yellow;

    private static GameObject Asset(string path)
    {
      var result=AssetDatabase.LoadAssetAtPath<GameObject>(Source+"Prefabs/"+path+".prefab");
      if(result==null) throw new Exception("Missing Japanese street prefab: "+path);
      return result;
    }
    private static Bounds BoundsOf(GameObject go)
    {
      var rs=go.GetComponentsInChildren<Renderer>().Where(r=>!(r is ParticleSystemRenderer)).ToArray();
      if(rs.Length==0) throw new Exception("No environment mesh: "+go.name);
      var bounds=rs[0].bounds;foreach(var r in rs.Skip(1)) bounds.Encapsulate(r.bounds);return bounds;
    }
    private static Material Mat(string name,string source,Color tint,float tiling=1)
    {
      var path=Materials+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
      if(mat==null)
      {
        var original=source==null?null:AssetDatabase.LoadAssetAtPath<Material>(Source+"Materials/"+source+".mat");
        mat=original==null?new Material(Shader.Find("Standard")):new Material(original);
        AssetDatabase.CreateAsset(mat,path);
      }
      mat.color=tint;mat.mainTextureScale=Vector2.one*tiling;
      mat.SetFloat("_Glossiness",.15f);mat.SetFloat("_GlossMapScale",.15f);
      mat.SetFloat("_Metallic",0);mat.EnableKeyword("_NORMALMAP");
      mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
    }
    private static void SetupMaterials()
    {
      Directory.CreateDirectory(Materials);AssetDatabase.Refresh();
      _road=Mat("LaneAsphalt","Street/AE_Road",Color.white,3);
      _concrete=Mat("Sidewalk","House/AE_Concrete_01",new Color(.88f,.89f,.87f),2);
      _wall=Mat("RetainingWall","House/AE_Wall_Tile_01",new Color(.7f,.72f,.7f),3);
      _metal=Mat("DarkMetal",null,new Color(.2f,.25f,.27f));
      _white=Mat("RoadPaint",null,new Color(.92f,.91f,.83f));
      _yellow=Mat("SafetyYellow",null,new Color(.95f,.64f,.08f));
    }
    private static GameObject Cube(string name,Transform parent,Vector3 position,Vector3 size,Material mat,bool solid=false)
    {
      var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
      go.transform.localPosition=position;go.transform.localScale=size;
      go.GetComponent<Renderer>().sharedMaterial=mat;
      if(solid) go.layer=8;else Object.DestroyImmediate(go.GetComponent<Collider>());
      return go;
    }
    private static GameObject Place(string path,Transform parent,Vector3 bottom,float height,float yaw,bool solid=false)
    {
      var go=(GameObject)PrefabUtility.InstantiatePrefab(Asset(path),parent);
      // Keep meshes/material assets linked, while making the layout and collision authoring editable.
      PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
      go.transform.localPosition=Vector3.zero;go.transform.localRotation=Quaternion.Euler(0,yaw,0);
      foreach(var script in go.GetComponentsInChildren<MonoBehaviour>(true)) if(script!=null) Object.DestroyImmediate(script);
      foreach(var canvas in go.GetComponentsInChildren<Canvas>(true)) Object.DestroyImmediate(canvas.gameObject);
      foreach(var t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
      foreach(var collider in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
      foreach(var rb in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
      var bounds=BoundsOf(go);
      if(height>0)go.transform.localScale*=height/Mathf.Max(bounds.size.y,.01f);
      bounds=BoundsOf(go);go.transform.position+=new Vector3(bottom.x-bounds.center.x,bottom.y-bounds.min.y,bottom.z-bounds.center.z);
      bounds=BoundsOf(go);
      if(solid)
      {
        var collision=new GameObject("Simple support collider");collision.transform.SetParent(go.transform,false);collision.layer=8;
        var box=collision.AddComponent<BoxCollider>();box.center=go.transform.InverseTransformPoint(bounds.center);
        box.size=new Vector3(bounds.size.x/Mathf.Abs(go.transform.lossyScale.x),bounds.size.y/Mathf.Abs(go.transform.lossyScale.y),bounds.size.z/Mathf.Abs(go.transform.lossyScale.z));
        // The buildings are scenery; gameplay support is authored on the street and stairs.
        if(Mathf.Abs(yaw%180)>1)box.size=new Vector3(box.size.z,box.size.y,box.size.x);
      }
      Audit.Add(go.name+" position="+go.transform.position+" bounds="+bounds.size);
      return go;
    }
    private static float HeightAt(float z)=>z<15?0:z<19.7f?.5f:z<22.3f?1.05f:z<25.7f?1.65f:z<28.3f?2.2f:2.8f;

    [MenuItem("Gesture Fist Game/应用日式街道场景")]
    public static void Upgrade()
    {
      if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play before changing the environment.");
      var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
      if(scene.path!=BuildGestureFistScene.OutputScene)
      {
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        scene=EditorSceneManager.OpenScene(BuildGestureFistScene.OutputScene);
      }
      Directory.CreateDirectory("Artifacts/before-japanese");
      string backup="Artifacts/before-japanese/GestureFistGame.unity.backup";
      if(!File.Exists(backup))File.Copy(BuildGestureFistScene.OutputScene,backup);
      SetupMaterials();Audit.Clear();
      var game=Object.FindObjectOfType<FistGameManager>();
      game.gapStage="03 / 排水沟与台阶";game.finalStage="04 / 街巷攀登";game.finishMessage="抵达街巷终点";
      var level=game.transform.Find("Level - 可编辑关卡");
      if(level==null)throw new Exception("Level hierarchy missing");
      foreach(var name in new[]{"Scenery - 几何体山谷",EnvironmentName})
      {var existing=level.Find(name);if(existing!=null)Object.DestroyImmediate(existing.gameObject);}
      foreach(var t in level.Cast<Transform>().ToArray())
      {
        if(t.name=="WoodPost" || t.name=="Rail")Object.DestroyImmediate(t.gameObject);
        else if(t.name.EndsWith(" / 草沿") || t.name.EndsWith(" / 路沿"))
        {t.GetComponent<Renderer>().sharedMaterial=_concrete;t.name=t.name.Replace("草沿","路沿");}
        else if(t.name.EndsWith(" / 岩基") || t.name.EndsWith(" / 挡土墙"))
        {t.GetComponent<Renderer>().sharedMaterial=_wall;t.name=t.name.Replace("岩基","挡土墙");}
        else if(t.name.StartsWith("路线标记_"))t.GetComponent<Renderer>().sharedMaterial=_white;
        else if(t.name.StartsWith("Finish" ) && t.GetComponent<Renderer>()!=null)t.GetComponent<Renderer>().sharedMaterial=_yellow;
      }
      var platformNames=new[]{"起步平台","守卫平台","峡谷对岸","台阶一","中途营地","台阶二","山顶平台"};
      foreach(string name in platformNames)
      {
        var t=level.Find(name);if(t==null)throw new Exception("Course platform missing: "+name);
        t.GetComponent<Renderer>().sharedMaterial=name=="起步平台" || name=="守卫平台"?_road:_concrete;
      }
      var env=new GameObject(EnvironmentName).transform;env.SetParent(level,false);
      Cube("街区远景地面",env,new Vector3(0,-5.9f,25),new Vector3(140,.3f,160),_concrete);
      // Side foundations follow the climb; the original central gap and reset zone remain functional.
      for(int side=-1;side<=1;side+=2)
      {
        for(int i=0;i<7;i++)
        {
          float z=-6+i*8;float top=HeightAt(z);
          Cube("街区基座_"+side+"_"+i,env,new Vector3(side*12,top-1.5f,z),new Vector3(13,3,8),_wall,true);
          Cube("侧街地面_"+side+"_"+i,env,new Vector3(side*12,top-.07f,z),new Vector3(13,.14f,8),_concrete,true);
        }
        for(int i=0;i<5;i++)
        {
          float z=side<0?-1+i*10:5+i*10;int index=(i+(side<0?0:2))%4;
          string[] houses={"House/AE_House_01","House/AE_House_03","House/AE_House_05","House/AE_House_08"};
          var house=Place(houses[index],env,new Vector3(side*11.7f,HeightAt(z),z),i%2==0?6.7f:7.8f,side<0?180:0,true);
          house.name="街边房屋_"+side+"_"+i+"_"+house.name;
        }
        for(int i=0;i<4;i++)
        {
          float z=1+i*11;float top=HeightAt(z);
          Place("Street/AE_Electric_Post_01",env,new Vector3(side*6.9f,top,z),6.7f,side<0?0:180);
        }
      }
      for(int i=0;i<3;i++)
      {
        float z=7+i*12;float side=i%2==0?-1:1;float top=HeightAt(z);
        Place("Props/AE_Vending_Machine_01",env,new Vector3(side*6.0f,top,z),2.15f,side<0?-90:90,true);
        Place("Props/AE_Vending_Machine_03",env,new Vector3(side*6.0f,top,z+1.5f),2.15f,side<0?-90:90,true);
        Place("Auto/AE_Bicycle_01",env,new Vector3(-side*6,top,z+2),1.2f,25);
        Place("Props/AE_Flower_Pot_01",env,new Vector3(side*5.4f,top,z-1.6f),.75f,0);
      }
      Place("Street/AE_Road_Sign_Pole_01",env,new Vector3(-5.3f,0,-2),2.7f,0);
      for(int i=0;i<5;i++)
        Place("House/AE_House_"+(i%2==0?"03":"08"),env,new Vector3(-18+i*9,2.8f,52),7.5f+(i%2)*1.5f,-90);
      // Painted edge lines and curbs make the reachable lane clear against detailed buildings.
      foreach(string name in platformNames)
      {
        var platform=level.Find(name);float top=platform.position.y+platform.localScale.y*.5f;
        for(int side=-1;side<=1;side+=2)
        {
          float x=side*(platform.localScale.x*.5f-.13f);
          Cube("道路边线_"+name+"_"+side,env,new Vector3(x,top+.024f,platform.position.z),new Vector3(.10f,.018f,platform.localScale.z-.15f),_white);
          Cube("低路沿_"+name+"_"+side,env,new Vector3(x+side*.24f,top+.04f,platform.position.z),new Vector3(.27f,.08f,platform.localScale.z),_concrete);
        }
      }
      for(int i=0;i<6;i++)Cube("斑马线_"+i,env,new Vector3(-2.65f+i*1.05f,.027f,-3.5f),new Vector3(.55f,.025f,1.8f),_white);
      for(int i=0;i<5;i++)Cube("沟沿警示_"+i,env,new Vector3(-2+i, .035f,14.85f),new Vector3(.5f,.035f,.18f),_yellow);
      for(int side=-1;side<=1;side+=2)
      {
        Place("Street/AE_Street_Fence_01",env,new Vector3(side*4.7f,0,12.4f),1.0f,90);
        Place("Street/AE_Street_Fence_01",env,new Vector3(side*4.7f,2.8f,32.4f),1.0f,90);
      }
      // A low drainage bed communicates the gap while staying below the fall-reset threshold.
      Cube("排水沟底",env,new Vector3(0,-5.9f,16),new Vector3(11,.3f,8),_wall);
      foreach(var label in level.GetComponentsInChildren<TextMesh>())
      {
        label.text=label.text.Replace("跨越峡谷","跨越排水沟");
        if(label.text.StartsWith("04"))label.text="04 / 街巷攀登\n抬拳搭台，向下撑起";
      }
      foreach(var label in Object.FindObjectsOfType<Text>(true))label.text=label.text.Replace("拳 行 山 谷","拳 行 街 巷").Replace("拳行山谷","拳行街巷").Replace("抵达山顶","抵达街巷终点");
      var camera=Camera.main;camera.backgroundColor=new Color(.69f,.8f,.88f);
      var follow=camera.GetComponent<CameraFollow>();follow.offset=new Vector3(5.5f,7.8f,-10.5f);
      camera.transform.position=game.player.transform.position+follow.offset;camera.transform.LookAt(game.player.transform.position+follow.lookOffset);
      var sun=Object.FindObjectsOfType<Light>().First(l=>l.type==LightType.Directional);
      sun.transform.rotation=Quaternion.Euler(48,-28,0);sun.color=new Color(1,.96f,.9f);sun.intensity=1.0f;
      RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
      RenderSettings.ambientSkyColor=new Color(.53f,.63f,.73f);RenderSettings.ambientEquatorColor=new Color(.48f,.51f,.54f);RenderSettings.ambientGroundColor=new Color(.32f,.34f,.36f);
      RenderSettings.fog=true;RenderSettings.fogColor=camera.backgroundColor;RenderSettings.fogStartDistance=48;RenderSettings.fogEndDistance=100;
      QualitySettings.shadowDistance=55;PlayerSettings.productName="拳行街巷";
      EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
      Validate();File.WriteAllLines("Artifacts/japanese-placement.txt",Audit);
      Debug.Log("JAPANESE_STREET_SCENE_SAVED");
    }
    public static void Validate()
    {
      BuildGestureFistScene.ValidateMainScene();GestureArtUpgrade.ValidateArt();
      var env=GameObject.Find(EnvironmentName);if(env==null)throw new Exception("Japanese environment not saved");
      var rs=env.GetComponentsInChildren<Renderer>();
      foreach(var r in rs)
      {
        if(r is MeshRenderer && (r.GetComponent<MeshFilter>()==null || r.GetComponent<MeshFilter>().sharedMesh==null))throw new Exception("Missing environment mesh: "+r.name);
        foreach(var m in r.sharedMaterials)
          if(m==null || m.shader==null || m.shader.name=="Hidden/InternalErrorShader" || !m.shader.isSupported)throw new Exception("Broken street material on "+r.name);
      }
      File.WriteAllText("Artifacts/japanese-scene-validation.txt","Japanese Street environment saved\nRenderers: "+rs.Length+"\nMissing scripts/meshes/materials: 0\nOriginal support platforms, checkpoints, input, five doro and four blue gates: retained\n");
    }
    public static void InspectAssets()
    {
      EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);Audit.Clear();
      var camera=new GameObject("Camera",typeof(Camera)).GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.7f,.78f,.84f);
      var light=new GameObject("Sun",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;light.intensity=1;light.transform.rotation=Quaternion.Euler(45,-30,0);
      RenderSettings.ambientLight=new Color(.6f,.6f,.6f);RenderSettings.fog=false;
      for(int i=0;i<4;i++)
      {
        string[] names={"01","03","05","08"};var go=Place("House/AE_House_"+names[i],null,Vector3.zero,6.7f,0);
        var b=BoundsOf(go);Capture(camera,"Artifacts/japanese-house-"+names[i]+".png",b.center+new Vector3(-1,.5f,1.6f).normalized*b.size.magnitude*1.05f,b.center);
        Object.DestroyImmediate(go);
      }
      File.WriteAllLines("Artifacts/japanese-assets.txt",Audit);
    }
    public static void Preview()
    {
      EditorSceneManager.OpenScene(BuildGestureFistScene.OutputScene);Validate();
      var camera=Camera.main;foreach(var canvas in Object.FindObjectsOfType<Canvas>())canvas.gameObject.SetActive(false);
      Capture(camera,"Artifacts/japanese-overview.png",new Vector3(12,16,-13),new Vector3(0,1,16));
      Capture(camera,"Artifacts/japanese-street.png",new Vector3(3,3.8f,-4),new Vector3(-1,1.8f,14));
      Debug.Log("JAPANESE_STREET_PREVIEW_OK");
    }
    private static void Capture(Camera camera,string path,Vector3 position,Vector3 target)
    {
      camera.transform.position=position;camera.transform.LookAt(target);
      foreach(var label in Object.FindObjectsOfType<GateLabelFont>())label.SendMessage("LateUpdate");
      var rt=new RenderTexture(1280,800,24);rt.Create();camera.targetTexture=rt;camera.Render();
      var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(1280,800,TextureFormat.RGB24,false);
      tex.ReadPixels(new Rect(0,0,1280,800),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());
      RenderTexture.active=old;camera.targetTexture=null;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);
    }
    public static void UpgradeAndPreview(){Upgrade();Preview();}
    public static void UpgradeAndRunChecks(){Upgrade();Preview();GestureValidation.Run();}
    [MenuItem("Gesture Fist Game/修复鼠标向下撑地操作")]
    public static void UpgradeMouseDownControls()
    {
      if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play before updating mouse controls.");
      var scene=EditorSceneManager.OpenScene(BuildGestureFistScene.OutputScene,OpenSceneMode.Single);
      var input=Object.FindObjectOfType<FistGestureInput>();
      if(input==null || input.mouseControl==null)throw new Exception("Mouse input component is missing.");
      input.mouseControl.heightDragScale=6;
      var ui=Object.FindObjectOfType<FistGameUI>();
      if(ui!=null)
      {
        if(ui.statusText!=null)ui.statusText.text="左 / 右键选拳 · 向下拖动下压撑地并前进 · 滚轮微调高度 · 松键释放";
        if(ui.helpPanel!=null)
          foreach(var label in ui.helpPanel.GetComponentsInChildren<Text>(true))
            label.text=label.text.Replace("按住后拖动：左右调整方向，上推伸拳，下拉后划。\n\n03  下滚压低拳头接地，支撑后下拉前进；上滚抬拳搭台阶。",
              "按住后左右拖动调整方向；向下拖动会同时压低并收回拳头。\n\n03  拳头碰地后继续向下拖，身体会向前并向上撑起；滚轮用于高度微调。")
              .Replace("向上拖动可出拳","向上拖动可出拳");
      }
      foreach(var label in Object.FindObjectsOfType<TextMesh>(true))
        label.text=label.text.Replace("滚轮抬高 / 下压","向下拖动下压撑地 · 滚轮微调高度");
      EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
      Debug.Log("MOUSE_DOWN_SUPPORT_UPGRADE_OK");
    }
  }
}
