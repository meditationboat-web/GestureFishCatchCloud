using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace GestureFistGame.Editor
{
  public static class BuildGestureFistScene
  {
    public const string OutputScene="Assets/GestureFistGame/Scenes/GestureFistGame.unity";
    public const string Root="Assets/GestureFistGame";
    private static Font _font;
    private static readonly Dictionary<string,Material> Mats=new Dictionary<string,Material>();
    private static PhysicMaterial _friction;
    [MenuItem("Gesture Fist Game/重建完整场景")]
    public static void BuildMainScene()
    {
      foreach(var path in new[]{"Scenes","Materials","Prefabs","Audio"}) Directory.CreateDirectory(Root+"/"+path);
      AssetDatabase.Refresh();
      _font=AssetDatabase.LoadAssetAtPath<Font>(Root+"/Fonts/NotoSansCJKsc-Regular.otf");
      if(_font==null) _font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      EnsureLayers();
      var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
      var world=new GameObject("GestureFistGame");
      var game=world.AddComponent<FistGameManager>();
      var level=new GameObject("Level - 可编辑关卡").transform;
      level.SetParent(world.transform);
      SetupMaterials();
      CreateLevel(level,game);

      var rig=new GameObject("PlayerRig").transform; rig.SetParent(world.transform);
      var playerObject=new GameObject("Player"); playerObject.transform.SetParent(rig);
      playerObject.transform.position=new Vector3(0,1.25f,0);
      var rb=playerObject.AddComponent<Rigidbody>(); rb.mass=4; rb.drag=.25f;
      rb.constraints=RigidbodyConstraints.FreezeRotation; rb.interpolation=RigidbodyInterpolation.Interpolate;
      rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
      var capsule=playerObject.AddComponent<CapsuleCollider>(); capsule.height=1.8f; capsule.radius=.68f; capsule.material=_friction;
      var player=playerObject.AddComponent<FistPlayerController>(); playerObject.layer=9;
      var heroVisual=new GameObject("CharacterVisual").transform;heroVisual.SetParent(playerObject.transform,false);
      heroVisual.localRotation=Quaternion.Euler(0,180,0);CreateHero(heroVisual);
      player.leftFist=CreateFist(rig,"LeftFist - 左拳",new Vector3(-1.1f,.65f,1),Mats["Cyan"]);
      player.rightFist=CreateFist(rig,"RightFist - 右拳",new Vector3(1.1f,.65f,1),Mats["Coral"]);
      game.player=player;
      PrefabUtility.SaveAsPrefabAssetAndConnect(rig.gameObject,Root+"/Prefabs/PlayerRig.prefab",InteractionMode.AutomatedAction);

      var tracking=new GameObject("TrackingSystem - MediaPipe").transform; tracking.SetParent(world.transform);
      var input=tracking.gameObject.AddComponent<FistGestureInput>(); input.player=player;
      var tracker=tracking.gameObject.AddComponent<MediaPipeTracker>(); tracker.input=input; input.tracker=tracker;
      input.mouseControl=tracking.gameObject.AddComponent<MouseFistInput>();input.mouseControl.player=player;
      input.mouseMode=true;tracker.autoStart=false;
      game.input=input;
      var enemies=new GameObject("Enemies - 可击飞守卫").transform; enemies.SetParent(world.transform);
      var dummies=new List<TrainingDummy>();
      dummies.Add(CreateEnemy(enemies,player,new Vector3(-1.6f,.95f,5.5f),false,"TrainingTarget"));
      dummies.Add(CreateEnemy(enemies,player,new Vector3(1.5f,.95f,10),true,"Guard_01"));
      dummies.Add(CreateEnemy(enemies,player,new Vector3(-1.6f,.95f,12.7f),true,"Guard_02"));
      dummies.Add(CreateEnemy(enemies,player,new Vector3(1.1f,2.65f,23.8f),true,"Guard_03"));
      dummies.Add(CreateEnemy(enemies,player,new Vector3(-1.6f,3.8f,31),true,"Guard_04"));
      game.enemies=dummies.ToArray();

      var cameraObject=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener),typeof(CameraFollow));
      cameraObject.tag="MainCamera";
      var camera=cameraObject.GetComponent<Camera>(); camera.fieldOfView=53; camera.nearClipPlane=.08f; camera.farClipPlane=150;
      camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.85f,.89f,.83f);
      var follow=cameraObject.GetComponent<CameraFollow>(); follow.target=player.transform;
      cameraObject.transform.position=player.transform.position+follow.offset;
      cameraObject.transform.LookAt(player.transform.position+follow.lookOffset);
      var sun=new GameObject("Sun - 暖色主光",typeof(Light));
      sun.transform.rotation=Quaternion.Euler(45,-35,0);
      var light=sun.GetComponent<Light>(); light.type=LightType.Directional; light.color=new Color(1,.87f,.7f); light.intensity=1.25f;
      light.shadows=LightShadows.Soft;
      RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
      RenderSettings.ambientLight=new Color(.6f,.68f,.75f);
      RenderSettings.fog=true; RenderSettings.fogMode=FogMode.Linear; RenderSettings.fogColor=camera.backgroundColor;
      RenderSettings.fogStartDistance=45; RenderSettings.fogEndDistance=120;
      QualitySettings.shadowDistance=45;
      CreateUI(world.transform,game,input,tracker);
      new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
      var fall=Primitive("FallResetZone",PrimitiveType.Cube,level,new Vector3(0,-5,15),new Vector3(60,1,90),"Rock");
      fall.GetComponent<Renderer>().enabled=false;
      fall.GetComponent<Collider>().isTrigger=true; fall.AddComponent<GameResetZone>().game=game;
      EditorSceneManager.SaveScene(scene,OutputScene);
      var existing=EditorBuildSettings.scenes.Where(s=>s.path!=OutputScene).ToList();
      existing.Insert(0,new EditorBuildSettingsScene(OutputScene,true)); EditorBuildSettings.scenes=existing.ToArray();
      PlayerSettings.productName="拳行山谷"; PlayerSettings.companyName="GestureFist";
      PlayerSettings.defaultScreenWidth=1280; PlayerSettings.defaultScreenHeight=720;
      PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
      AssetDatabase.SaveAssets();
      ValidateMainScene();
      Debug.Log("GESTURE_SCENE_SAVED "+OutputScene);
      JapaneseStreetScene.Upgrade();
    }
    [MenuItem("Gesture Fist Game/添加鼠标控制与原生界面")]
    public static void UpgradeMouseControlsAndUI()
    {
      var scene=EditorSceneManager.OpenScene(OutputScene,OpenSceneMode.Single);
      _font=AssetDatabase.LoadAssetAtPath<Font>(Root+"/Fonts/NotoSansCJKsc-Regular.otf");
      var input=Object.FindObjectOfType<FistGestureInput>();
      var tracker=Object.FindObjectOfType<MediaPipeTracker>();
      var game=Object.FindObjectOfType<FistGameManager>();
      var oldUI=Object.FindObjectOfType<FistGameUI>();
      if(oldUI!=null) Object.DestroyImmediate(oldUI.gameObject);
      var mouse=input.GetComponent<MouseFistInput>();
      if(mouse==null) mouse=input.gameObject.AddComponent<MouseFistInput>();
      mouse.player=game.player;input.mouseControl=mouse;
      input.mouseMode=true;input.keyboardMode=false;tracker.autoStart=false;
      CreateUI(game.transform,game,input,tracker);
      EditorSceneManager.SaveScene(scene,OutputScene);
      AssetDatabase.SaveAssets();ValidateMainScene();
      Debug.Log("GESTURE_MOUSE_UI_SAVED");
    }
    private static void EnsureLayers()
    {
      var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
      var layers=settings.FindProperty("layers");
      layers.GetArrayElementAtIndex(8).stringValue="FistSupport";
      layers.GetArrayElementAtIndex(9).stringValue="FistPlayer";
      layers.GetArrayElementAtIndex(10).stringValue="FistEnemy";
      settings.ApplyModifiedProperties();
    }
    private static void SetupMaterials()
    {
      Mats.Clear();
      Material("Sand",new Color(.73f,.62f,.43f));
      Material("Grass",new Color(.40f,.53f,.36f));
      Material("Rock",new Color(.33f,.40f,.43f));
      Material("Wood",new Color(.4f,.24f,.15f));
      Material("Dark",new Color(.10f,.15f,.18f));
      Material("Cream",new Color(.94f,.84f,.64f));
      Material("Coral",new Color(1,.29f,.19f));
      Material("Cyan",new Color(.08f,.72f,.79f));
      Material("Gold",new Color(1,.66f,.14f));
      Material("Foliage",new Color(.22f,.43f,.33f));
      Material("White",new Color(.96f,.95f,.89f));
      _friction=AssetDatabase.LoadAssetAtPath<PhysicMaterial>(Root+"/Materials/BodyFriction.physicMaterial");
      if(_friction==null) { _friction=new PhysicMaterial("BodyFriction"); AssetDatabase.CreateAsset(_friction,Root+"/Materials/BodyFriction.physicMaterial"); }
      _friction.dynamicFriction=.12f; _friction.staticFriction=.12f; _friction.bounciness=0;
      _friction.frictionCombine=PhysicMaterialCombine.Minimum;
    }
    private static Material Material(string name,Color color)
    {
      var path=Root+"/Materials/"+name+".mat";
      var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
      if(mat==null){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);}
      mat.color=color; mat.SetFloat("_Glossiness",.26f); EditorUtility.SetDirty(mat);
      Mats[name]=mat; return mat;
    }
    private static GameObject Primitive(string name,PrimitiveType type,Transform parent,Vector3 local,Vector3 scale,string material,bool solid=true)
    {
      var go=GameObject.CreatePrimitive(type);go.name=name;
      go.transform.SetParent(parent,false);go.transform.localPosition=local;go.transform.localScale=scale;
      go.GetComponent<Renderer>().sharedMaterial=Mats[material];
      if(!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
      else go.layer=8;
      return go;
    }
    private static void CreateLevel(Transform level,FistGameManager game)
    {
      Platform(level,"起步平台",0,1,16,9);
      Platform(level,"守卫平台",0,12,6,9);
      Platform(level,"峡谷对岸",.5f,18.2f,3,7);
      Platform(level,"台阶一",1.05f,21,2.6f,6);
      Platform(level,"中途营地",1.65f,24,3.4f,8);
      Platform(level,"台阶二",2.2f,27,2.6f,6);
      Platform(level,"山顶平台",2.8f,32,7.4f,10);
      var checkpoints=new List<Transform>();
      foreach(var pos in new[]{new Vector3(0,1.3f,0),new Vector3(0,1.3f,11.7f),new Vector3(0,2.95f,24)})
      {
        var point=new GameObject("Checkpoint_"+checkpoints.Count).transform; point.SetParent(level); point.position=pos;
        checkpoints.Add(point);
        Primitive("CheckpointPad",PrimitiveType.Cylinder,point,new Vector3(0,-1.26f,0),new Vector3(1.5f,.025f,1.5f),"Cyan",false);
      }
      game.checkpoints=checkpoints.ToArray(); game.checkpoint=checkpoints[0];
      var finish=new GameObject("FinishArea").transform;finish.SetParent(level);finish.position=new Vector3(0,3.8f,33);
      game.finish=finish;
      Primitive("FinishLeft",PrimitiveType.Cube,level,new Vector3(-2,4.7f,33),new Vector3(.35f,3.8f,.35f),"Gold");
      Primitive("FinishRight",PrimitiveType.Cube,level,new Vector3(2,4.7f,33),new Vector3(.35f,3.8f,.35f),"Gold");
      Primitive("FinishBeam",PrimitiveType.Cube,level,new Vector3(0,6.5f,33),new Vector3(4.4f,.4f,.4f),"Gold");
      Board(level,new Vector3(-3.6f,0,2),"01 / 拳头落地\n下压 · 后划 · 松开");
      Board(level,new Vector3(3.6f,0,10),"02 / 击飞守卫\n快速挥拳");
      Board(level,new Vector3(-3.6f,0,14),"03 / 跨越峡谷\n双拳下压，释放惯性");
      Board(level,new Vector3(3.5f,1.65f,24),"04 / 向上攀登\n抬拳搭台，向下撑起");

      for(int i=0;i<14;i++)
      {
        float z=-3+i*2.6f;
        float top=z<15?0:z<19.7f?.5f:z<22.3f?1.05f:z<25.7f?1.65f:z<28.3f?2.2f:2.8f;
        if(z>15 && z<16.7f) continue;
        Primitive("路线标记_"+i,PrimitiveType.Cube,level,new Vector3(0,top+.028f,z),new Vector3(.14f,.025f,.45f),"Gold",false);
      }
      for(int side=-1;side<=1;side+=2)
      {
        for(int i=0;i<3;i++)
        {
          Primitive("WoodPost",PrimitiveType.Cube,level,new Vector3(side*4.15f,.55f,3+i*1.7f),new Vector3(.16f,1.1f,.16f),"Wood");
        }
        Primitive("Rail",PrimitiveType.Cube,level,new Vector3(side*4.15f,.8f,4.7f),new Vector3(.13f,.15f,3.7f),"Wood");
      }
      var scenery=new GameObject("Scenery - 几何体山谷").transform; scenery.SetParent(level);
      Primitive("ValleyBed",PrimitiveType.Cube,scenery,new Vector3(0,-6.5f,25),new Vector3(130,1,130),"Foliage",false);
      Primitive("ValleyLeft",PrimitiveType.Cube,scenery,new Vector3(-17,-3.2f,23),new Vector3(22,5.6f,70),"Grass",false);
      Primitive("ValleyRight",PrimitiveType.Cube,scenery,new Vector3(17,-3.2f,23),new Vector3(22,5.6f,70),"Grass",false);
      var random=new System.Random(841);
      for(int i=0;i<32;i++)
      {
        float side=i%2==0?-1:1;
        float x=side*(6.5f+(float)random.NextDouble()*8), z=-4+(float)random.NextDouble()*48;
        float height=2.2f+(float)random.NextDouble()*2.2f;
        var tree=new GameObject("Tree_"+i).transform;tree.SetParent(scenery);tree.position=new Vector3(x,-.2f,z);
        Primitive("Trunk",PrimitiveType.Cylinder,tree,new Vector3(0,height*.4f,0),new Vector3(.2f,height*.4f,.2f),"Wood");
        Primitive("Canopy",PrimitiveType.Sphere,tree,new Vector3(0,height,0),new Vector3(1.8f,height,1.8f),"Foliage",false);
        Primitive("Rock",PrimitiveType.Sphere,scenery,new Vector3(x*.9f,-.5f,z+1),new Vector3(2.7f,2.6f,3.1f),"Rock",false);
      }
      for(int i=0;i<9;i++)
      {
        var peak=Primitive("Mountain_"+i,PrimitiveType.Sphere,scenery,new Vector3(-36+i*9,3,52),new Vector3(14,13+(i%3)*5,17),"Rock",false);
        peak.transform.rotation=Quaternion.Euler(0,17*i,15);
      }
    }
    private static void Platform(Transform parent,string name,float top,float z,float length,float width)
    {
      Primitive(name,PrimitiveType.Cube,parent,new Vector3(0,top-.65f,z),new Vector3(width,1.3f,length),"Sand");
      Primitive(name+" / 草沿",PrimitiveType.Cube,parent,new Vector3(-width*.5f+.25f,top+.015f,z),new Vector3(.5f,.03f,length),"Grass",false);
      Primitive(name+" / 岩基",PrimitiveType.Cube,parent,new Vector3(0,top-2,z),new Vector3(width-.2f,1.45f,length-.15f),"Rock");
    }
    private static void Board(Transform parent,Vector3 basePosition,string content)
    {
      var root=new GameObject("GuideBoard").transform;root.SetParent(parent);root.localPosition=basePosition;
      GestureArtUpgrade.PopulateGate(root,content);
    }
    private static void CreateHero(Transform body)
    {
      Primitive("Core",PrimitiveType.Sphere,body,Vector3.zero,new Vector3(1.4f,1.7f,1.25f),"Coral",false);
      Primitive("FacePlate",PrimitiveType.Sphere,body,new Vector3(0,.1f,.53f),new Vector3(1.05f,1.05f,.36f),"Cream",false);
      Primitive("Brow",PrimitiveType.Cube,body,new Vector3(0,.43f,.73f),new Vector3(.85f,.13f,.13f),"Dark",false);
      foreach(float side in new[]{-1f,1f})
      {
        Primitive("Ear",PrimitiveType.Sphere,body,new Vector3(side*.7f,.24f,0),new Vector3(.38f,.5f,.4f),"Cream",false);
        Primitive("Eye",PrimitiveType.Sphere,body,new Vector3(side*.22f,.24f,.75f),new Vector3(.13f,.16f,.09f),"Dark",false);
      }
      Primitive("Mouth",PrimitiveType.Cube,body,new Vector3(0,-.2f,.75f),new Vector3(.42f,.065f,.04f),"Dark",false);
      Primitive("TopStripe",PrimitiveType.Cube,body,new Vector3(0,.78f,0),new Vector3(.25f,.18f,.72f),"Gold",false);
    }
    private static FistAnchor CreateFist(Transform parent,string name,Vector3 position,Material material)
    {
      var root=new GameObject(name,typeof(Rigidbody),typeof(SphereCollider),typeof(FistAnchor));
      root.transform.SetParent(parent);root.transform.position=position;root.layer=9;
      var anchor=root.GetComponent<FistAnchor>();
      var audio=root.AddComponent<AudioSource>();audio.playOnAwake=false;audio.volume=.45f;audio.spatialBlend=.35f;
      audio.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Audio/FistImpact.wav");anchor.hitAudio=audio;
      var rb=root.GetComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;rb.interpolation=RigidbodyInterpolation.Interpolate;
      root.GetComponent<SphereCollider>().radius=.42f;root.GetComponent<SphereCollider>().isTrigger=true;
      var glove=Primitive("Glove",PrimitiveType.Sphere,root.transform,Vector3.zero,new Vector3(.88f,.82f,1),"Cream",false);
      glove.GetComponent<Renderer>().sharedMaterial=material;anchor.glove=glove.GetComponent<Renderer>();
      Primitive("WristBand",PrimitiveType.Cylinder,root.transform,new Vector3(0,0,-.35f),new Vector3(.59f,.16f,.59f),"Dark",false).transform.localRotation=Quaternion.Euler(90,0,0);
      for(int i=0;i<3;i++) Primitive("Knuckle",PrimitiveType.Sphere,root.transform,new Vector3(-.22f+i*.22f,.1f,.4f),new Vector3(.25f,.24f,.25f),"Cream",false);
      var marker=Primitive("SupportAnchor",PrimitiveType.Cylinder,parent,Vector3.zero,new Vector3(.95f,.017f,.95f),"Cyan",false);
      anchor.supportMarker=marker.transform;marker.SetActive(false);
      var trail=root.AddComponent<TrailRenderer>();trail.time=.15f;trail.startWidth=.2f;trail.endWidth=0;trail.emitting=false;
      trail.sharedMaterial=Mats["Gold"];anchor.trail=trail;
      var effect=new GameObject("HitSpark",typeof(ParticleSystem));effect.transform.SetParent(root.transform,false);
      var ps=effect.GetComponent<ParticleSystem>(); var main=ps.main;main.loop=false;main.playOnAwake=false;main.duration=.2f;main.startLifetime=.25f;
      main.startSpeed=4;main.startSize=.12f;main.startColor=new Color(1,.65f,.2f);main.simulationSpace=ParticleSystemSimulationSpace.World;
      var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,14)});
      ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=Mats["Gold"];ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);anchor.impact=ps;
      return anchor;
    }
    private static TrainingDummy CreateEnemy(Transform parent,FistPlayerController player,Vector3 position,bool mobile,string name)
    {
      var go=new GameObject(name,typeof(Rigidbody),typeof(CapsuleCollider),typeof(TrainingDummy));
      go.transform.SetParent(parent);go.transform.position=position;go.layer=10;
      var rb=go.GetComponent<Rigidbody>();rb.mass=1.3f;rb.drag=.7f;rb.angularDrag=.8f;rb.interpolation=RigidbodyInterpolation.Interpolate;
      rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
      var col=go.GetComponent<CapsuleCollider>();col.height=1.65f;col.radius=.32f;
      var dummy=go.GetComponent<TrainingDummy>();dummy.target=player;dummy.mobile=mobile;
      GestureArtUpgrade.PopulateDoro(dummy);
      return dummy;
    }
    private static RectTransform Rect(string name,Transform parent,Vector2 anchorMin,Vector2 anchorMax,Vector2 position,Vector2 size)
    {
      var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
      rect.anchorMin=anchorMin;rect.anchorMax=anchorMax;rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;
      return rect;
    }
    private static RectTransform Panel(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size,Color color)
    {
      var rect=Rect(name,parent,anchor,anchor,position,size);
      var image=rect.gameObject.AddComponent<Image>();image.color=color;
      image.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");image.type=Image.Type.Sliced;
      return rect;
    }
    private static Text Label(string name,Transform parent,string value,Vector2 position,Vector2 size,int fontSize,Color color,TextAnchor alignment=TextAnchor.MiddleLeft)
    {
      var rect=Rect(name,parent,new Vector2(.5f,.5f),new Vector2(.5f,.5f),position,size);
      var text=rect.gameObject.AddComponent<Text>();text.font=_font;text.text=value;text.fontSize=fontSize;text.color=color;
      text.alignment=alignment;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
      return text;
    }
    private static Button Button(Transform parent,string text,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action)
    {
      var rect=Panel(text,parent,new Vector2(.5f,.5f),pos,size,Color.white);
      rect.GetComponent<Image>().sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
      var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();
      button.colors=ColorBlock.defaultColorBlock;
      Label("Label",rect,text,Vector2.zero,size-new Vector2(8,4),15,new Color(.196f,.196f,.196f),TextAnchor.MiddleCenter);
      UnityEventTools.AddPersistentListener(button.onClick,action);return button;
    }
    private static void CreateUI(Transform parent,FistGameManager game,FistGestureInput input,MediaPipeTracker tracker)
    {
      Color ink=new Color(.88f,.88f,.88f,.96f), cream=new Color(.196f,.196f,.196f),muted=new Color(.3f,.3f,.3f);
      var canvasGo=new GameObject("Canvas - 中文游戏界面",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
      canvasGo.transform.SetParent(parent);canvasGo.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
      var scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
      var ui=canvasGo.AddComponent<FistGameUI>();ui.game=game;ui.input=input;ui.tracker=tracker;
      var top=Panel("TitleCard",canvasGo.transform,new Vector2(0,1),new Vector2(225,-64),new Vector2(402,90),ink);
      Label("GameTitle",top,"拳 行 山 谷",new Vector2(0,15),new Vector2(362,48),28,cream);
      Label("Subtitle",top,"GESTURE FIST  /  用双拳撑起每一步",new Vector2(0,-20),new Vector2(362,22),13,muted);
      var score=Panel("Progress",canvasGo.transform,new Vector2(1,1),new Vector2(-211,-63),new Vector2(374,88),ink);
      ui.stageText=Label("Stage",score,game.Stage,new Vector2(0,15),new Vector2(338,32),22,cream);
      ui.scoreText=Label("Stats",score,"0 次命中  ·  0 次掉落",new Vector2(0,-20),new Vector2(338,24),14,muted);

      var bottom=Panel("ControlStatus",canvasGo.transform,new Vector2(.5f,0),new Vector2(146,67),new Vector2(922,110),ink);
      ui.leftText=Label("Left",bottom,"左拳 / 自由",new Vector2(-308,29),new Vector2(260,28),19,cream);
      ui.rightText=Label("Right",bottom,"右拳 / 自由",new Vector2(-58,29),new Vector2(220,28),19,cream);
      ui.statusText=Label("Action",bottom,"左 / 右键选拳 · 向下拖动下压撑地并前进 · 滚轮微调高度 · 松键释放",new Vector2(-84,-20),new Vector2(705,51),15,cream);
      ui.calibrateButton=Button(bottom,"重新校准 C",new Vector2(347,27),new Vector2(180,32),input.Recalibrate);
      ui.calibrateButton.interactable=input.UsesCamera;
      Button(bottom,"操作说明 H",new Vector2(347,-16),new Vector2(180,32),ui.ToggleHelp);
      var mode=Panel("InputModePanel - 输入模式",canvasGo.transform,new Vector2(0,0),new Vector2(159,127),new Vector2(270,230),ink);
      ui.modeText=Label("CurrentMode",mode,"当前：鼠标游玩",new Vector2(0,83),new Vector2(240,34),18,cream);
      ui.mouseButton=Button(mode,"鼠标游玩 F3",new Vector2(0,40),new Vector2(236,32),input.UseMouse);ui.mouseButton.interactable=false;
      ui.cameraButton=Button(mode,"手势控制 F1",new Vector2(0,0),new Vector2(236,32),input.UseCamera);
      ui.keyboardButton=Button(mode,"键盘控制 F2",new Vector2(0,-40),new Vector2(236,32),input.UseKeyboard);
      Button(mode,"回到检查点 / 重新开始",new Vector2(0,-84),new Vector2(236,32),ui.ResetPlayer);
      var previewPanel=Panel("CameraPanel",canvasGo.transform,new Vector2(0,1),new Vector2(147,-258),new Vector2(246,254),ink);
      ui.cameraPanel=previewPanel.gameObject;
      var previewRect=Rect("CameraPreview",previewPanel,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,25),new Vector2(224,168));
      tracker.preview=previewRect.gameObject.AddComponent<RawImage>();tracker.preview.color=new Color(.75f,.75f,.75f);tracker.preview.raycastTarget=false;
      var overlay=Rect("LandmarkOverlay",previewRect,new Vector2(0,0),new Vector2(1,1),Vector2.zero,Vector2.zero).gameObject.AddComponent<GestureOverlay>();overlay.input=input;overlay.raycastTarget=false;
      ui.trackerText=Label("CameraStatus",previewPanel,"摄像头准备中",new Vector2(0,-76),new Vector2(220,32),11,muted);
      Button(previewPanel,"重试摄像头",new Vector2(-52,-110),new Vector2(105,25),tracker.RetryCamera);
      Button(previewPanel,"切换",new Vector2(65,-110),new Vector2(105,25),tracker.NextCamera);
      previewPanel.gameObject.SetActive(input.UsesCamera);
      var help=Panel("HelpPanel",canvasGo.transform,new Vector2(.5f,.5f),new Vector2(0,15),new Vector2(670,418),ink);
      ui.helpPanel=help.gameObject;input.helpPanel=help.gameObject;
      Label("HelpTitle",help,"鼠标与手势操作",new Vector2(0,160),new Vector2(610,48),28,cream);
      Label("HelpText",help,
        "01  鼠标左键控制左拳，右键控制右拳，可同时按住两键。\n\n02  按住后左右拖动调整方向；向下拖动会同时压低并收回拳头。\n\n03  拳头碰地后继续向下拖，身体会向前并向上撑起；滚轮用于高度微调。\n\n04  向上拖抬拳搭台阶，松键释放支点；快速上拖可出拳。复位可点左下角按钮。\n\n05  手势模式：肩膀双手入镜校准，握拳接地后后划或下压。",
        new Vector2(0,3),new Vector2(610,252),17,cream);
      Label("DebugHelp",help,"F1 手势 / F2 键盘 / F3 鼠标 · H 说明 · R 复位\n键盘：Q/E 选拳 + WASD/↑↓ 调整 · Z/X 出拳 · 空格松开",new Vector2(0,-139),new Vector2(610,50),13,muted);
      Button(help,"开始练习",new Vector2(0,-186),new Vector2(170,32),ui.ToggleHelp);
      help.gameObject.SetActive(false);
      var finish=Panel("CompletionPanel",canvasGo.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(560,290),ink);
      ui.finishPanel=finish.gameObject;
      ui.finishText=Label("Victory",finish,"抵达山顶",new Vector2(0,32),new Vector2(500,150),25,cream,TextAnchor.MiddleCenter);
      Button(finish,"重新开始",new Vector2(0,-86),new Vector2(190,44),ui.ResetPlayer);
      finish.gameObject.SetActive(false);
    }
    [MenuItem("Gesture Fist Game/验证场景引用")]
    public static void ValidateMainScene()
    {
      var scene=EditorSceneManager.OpenScene(OutputScene,OpenSceneMode.Single);
      var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
      foreach(var t in all)
        if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)
          throw new Exception("Missing script on "+t.name);
      var player=Object.FindObjectOfType<FistPlayerController>();
      var input=Object.FindObjectOfType<FistGestureInput>();
      var tracker=Object.FindObjectOfType<MediaPipeTracker>();
      var game=Object.FindObjectOfType<FistGameManager>();
      if(player==null || player.leftFist==null || player.rightFist==null || input==null || input.player!=player || tracker==null || tracker.input!=input)
        throw new Exception("Scene references incomplete");
      if(game.checkpoints.Length<3 || game.finish==null || game.enemies.Length<5) throw new Exception("Level incomplete");
      var ui=Object.FindObjectOfType<FistGameUI>();
      if(input.mouseControl==null || input.mouseControl.player!=player || ui==null || ui.mouseButton==null ||
        ui.cameraButton==null || ui.keyboardButton==null || ui.mouseButton.onClick.GetPersistentEventCount()!=1)
        throw new Exception("Mouse input / scene UI references incomplete");
      Directory.CreateDirectory("Artifacts");
      File.WriteAllText("Artifacts/scene-validation.txt",
        "Objects: "+all.Length+"\nMissing scripts: 0\nPlayer, fists, input, tracker, UI, checkpoints, enemies, finish: linked\nMouse input, native UI and persistent mode buttons: linked\nScene: "+scene.path);
      Debug.Log("GESTURE_SCENE_VALIDATED objects="+all.Length+" missingScripts=0");
    }
  }
}


