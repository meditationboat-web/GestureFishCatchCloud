using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace GestureFistGame.Editor
{
  public static class GestureArtUpgrade
  {
    private const string Art="Assets/GestureFistGame/Art/";
    private const string Model=Art+"Doro/tripo_convert_96d41456-dd42-4f72-8c4b-79d7fc7b8d05.fbx";
    private const string Texture=Art+"Doro/tripo_convert_96d41456-dd42-4f72-8c4b-79d7fc7b8d05.fbm/tripo_rgb_a1546e9b-fc48-4a13-9c57-e55cb3b15d24.jpg";
    private static Material Load(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Art+"TriggerGate/"+name+".mat");
    private static Material PersistentMaterial(string path,string shader)
    {
      var material=AssetDatabase.LoadAssetAtPath<Material>(path);
      if(material==null)
      {
        var s=Shader.Find(shader);if(s==null) throw new Exception("Missing shader: "+shader);
        material=new Material(s);AssetDatabase.CreateAsset(material,path);
      }
      return material;
    }

    [MenuItem("Gesture Fist Game/更新 doro 小怪和蓝色提示门")]
    public static void Upgrade()
    {
      if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play before updating scene art.");
      var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
      if(scene.path!=BuildGestureFistScene.OutputScene)
      {
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        scene=EditorSceneManager.OpenScene(BuildGestureFistScene.OutputScene);
      }
      Directory.CreateDirectory("Artifacts/before-doro");
      string backup="Artifacts/before-doro/GestureFistGame.unity.backup";
      if(!File.Exists(backup)) File.Copy(BuildGestureFistScene.OutputScene,backup);
      var boards=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true))
        .Where(t=>t.name=="GuideBoard").ToArray();
      foreach(var board in boards)
      {
        var label=board.GetComponentInChildren<TextMesh>();
        string content=label!=null?label.text:"拳头落地\n下压 · 后划 · 松开";
        PopulateGate(board,content);
      }
      foreach(var dummy in Object.FindObjectsOfType<TrainingDummy>()) PopulateDoro(dummy);
      EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
      AssetDatabase.SaveAssets();BuildGestureFistScene.ValidateMainScene();ValidateArt();
      Debug.Log("GESTURE_ART_UPGRADE_OK");
    }

    public static void PopulateGate(Transform root,string content)
    {
      foreach(Transform child in root.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
      var pillar=Load("P1TriggerPillar");var curtain=Load("P1TriggerCurtain");
      if(pillar==null || curtain==null) throw new Exception("Trigger gate materials missing");
      for(int side=-1;side<=1;side+=2)
        Shape(side<0?"LeftPillar":"RightPillar",PrimitiveType.Cube,root,
          new Vector3(side*1.53f,1.15f,0),new Vector3(.22f,2.3f,.25f),pillar,true);
      Shape("BlueGradientCurtain",PrimitiveType.Quad,root,new Vector3(0,1.25f,0),new Vector3(2.84f,2.0f,1),curtain,false);
      var font=AssetDatabase.LoadAssetAtPath<Font>(BuildGestureFistScene.Root+"/Fonts/NotoSansCJKsc-Regular.otf");
      var textMaterial=PersistentMaterial(Art+"TriggerGate/GateText.mat","P1/Scene Text");
      textMaterial.color=Color.white;textMaterial.renderQueue=3001;EditorUtility.SetDirty(textMaterial);
      var label=new GameObject("Instruction",typeof(TextMesh));label.transform.SetParent(root,false);
      label.transform.localPosition=new Vector3(0,1.3f,-.035f);
      var text=label.GetComponent<TextMesh>();text.font=font;text.text=content;
      text.fontSize=64;text.characterSize=.039f;text.lineSpacing=1.35f;
      text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=Color.white;
      var renderer=label.GetComponent<MeshRenderer>();renderer.sharedMaterial=textMaterial;
      // Fit authored instructions between the pillars, including the longest Chinese line.
      font.RequestCharactersInTexture(content,text.fontSize,FontStyle.Normal);
      float width=renderer.bounds.size.x;
      if(width>2.55f) text.characterSize*=2.55f/width;
      label.AddComponent<GateLabelFont>();
    }
    private static GameObject Shape(string name,PrimitiveType primitive,Transform parent,Vector3 position,Vector3 scale,Material mat,bool solid)
    {
      var go=GameObject.CreatePrimitive(primitive);go.name=name;go.transform.SetParent(parent,false);
      go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;
      if(!solid) Object.DestroyImmediate(go.GetComponent<Collider>());else go.layer=8;
      return go;
    }

    public static void PopulateDoro(TrainingDummy dummy)
    {
      var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Model);
      if(asset==null) throw new Exception("Doro model missing");
      foreach(Transform child in dummy.transform.Cast<Transform>().ToArray())
        if(new[]{"Torso","Head","Helmet","Leg","Arm","DoroVisual"}.Contains(child.name)) Object.DestroyImmediate(child.gameObject);
      var visual=new GameObject("DoroVisual").transform;visual.SetParent(dummy.transform,false);
      var model=(GameObject)PrefabUtility.InstantiatePrefab(asset,visual);
      model.transform.localPosition=Vector3.zero;
      // The source model's bind pose faces downward with the runner's scene rotation.
      // Rotate that pose upright, with the face toward the player at the start of the lane.
      model.transform.localRotation=Quaternion.Euler(90,0,0)*new Quaternion(.5f,.5f,-.5f,-.5f);
      model.transform.localScale=Vector3.one;
      foreach(var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=10;
      foreach(var animator in model.GetComponentsInChildren<Animator>())
      { animator.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(animator); }
      var material=PersistentMaterial(Art+"Doro/DoroToon.mat","P1/Cute Toon");
      material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Texture);material.color=new Color(.68f,.68f,.68f,1);
      material.SetFloat("_RimStrength",.055f);
      EditorUtility.SetDirty(material);
      var renderers=model.GetComponentsInChildren<Renderer>();
      if(renderers.Length==0 || material.mainTexture==null) throw new Exception("Doro renderer/texture missing");
      foreach(var r in renderers)
      {
        r.sharedMaterials=r.sharedMaterials.Select(_=>material).ToArray();
        PrefabUtility.RecordPrefabInstancePropertyModifications(r);
      }
      PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
      foreach(var t in model.GetComponentsInChildren<Transform>(true)) PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
      var bounds=BoundsOf(renderers);visual.localScale=Vector3.one*(1.5f/bounds.size.y);
      bounds=BoundsOf(renderers);
      visual.position+=new Vector3(dummy.transform.position.x-bounds.center.x,
        dummy.transform.position.y-.825f-bounds.min.y,dummy.transform.position.z-bounds.center.z);
      dummy.bodyRenderer=renderers.OrderByDescending(r=>r.bounds.size.sqrMagnitude).First();
      var col=dummy.GetComponent<CapsuleCollider>();col.height=1.65f;col.center=Vector3.zero;
      col.radius=Mathf.Clamp(Mathf.Max(bounds.extents.x,bounds.extents.z)*.8f,.4f,.72f);
      EditorUtility.SetDirty(dummy);
    }
    private static Bounds BoundsOf(Renderer[] renderers)
    {
      var result=renderers[0].bounds;foreach(var r in renderers.Skip(1)) result.Encapsulate(r.bounds);return result;
    }

    public static void ValidateArt()
    {
      var enemies=Object.FindObjectsOfType<TrainingDummy>();
      var labels=Object.FindObjectsOfType<GateLabelFont>();
      if(enemies.Length!=5 || labels.Length!=4) throw new Exception("Expected five doro enemies and four gate labels.");
      var lines=enemies.Select(e=>e.name+" / "+e.bodyRenderer.sharedMaterial.name+" / size="+BoundsOf(e.GetComponentsInChildren<Renderer>()).size).ToArray();
      foreach(var e in enemies)
        if(e.transform.Find("DoroVisual")==null || e.bodyRenderer==null || e.bodyRenderer.sharedMaterial.mainTexture==null || e.GetComponent<CapsuleCollider>()==null)
          throw new Exception("Incomplete doro: "+e.name);
      File.WriteAllText("Artifacts/art-validation.txt","Doro enemies: 5\nBlue gate signs: 4\nScene models, colliders, hit renderer, texture: linked\n"+string.Join("\n",lines));
    }

    public static void Preview()
    {
      EditorSceneManager.OpenScene(BuildGestureFistScene.OutputScene);
      ValidateArt();
      var camera=Camera.main;
      foreach(var canvas in Object.FindObjectsOfType<Canvas>()) canvas.gameObject.SetActive(false);
      Render(camera,"Artifacts/doro-gates-overview.png",new Vector3(10,9,-12),new Vector3(0,1,10));
      Render(camera,"Artifacts/doro-closeup.png",new Vector3(.8f,2.2f,2.3f),new Vector3(-1.6f,.85f,5.5f));
      Render(camera,"Artifacts/gate-closeup.png",new Vector3(-3.6f,1.8f,-3.3f),new Vector3(-3.6f,1.25f,2));
      Debug.Log("GESTURE_ART_PREVIEW_OK");
    }
    public static void UpgradeAndPreview(){Upgrade();Preview();}
    public static void BuildFinal(){Upgrade();GestureValidation.BuildWindows();}
    public static void InspectDoro()
    {
      var source=AssetDatabase.LoadAssetAtPath<GameObject>(Model);
      EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
      var model=(GameObject)PrefabUtility.InstantiatePrefab(source);
      model.transform.rotation=Quaternion.Euler(0,180,0)*new Quaternion(.5f,.5f,-.5f,-.5f);
      var rs=model.GetComponentsInChildren<Renderer>();
      foreach(var r in rs) r.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Art+"Doro/DoroToon.mat");
      var b=BoundsOf(rs);model.transform.localScale*=1.5f/b.size.y;b=BoundsOf(rs);
      var camera=new GameObject("Preview Camera",typeof(Camera)).GetComponent<Camera>();
      camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.35f,.39f,.43f);
      var light=new GameObject("Preview Light",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;
      light.transform.rotation=Quaternion.Euler(30,-30,0);light.intensity=.7f;
      RenderSettings.ambientLight=new Color(.3f,.3f,.3f);RenderSettings.fog=false;
      var directions=new[]{Vector3.back,Vector3.right,Vector3.forward,Vector3.left,Vector3.up,Vector3.down};
      for(int i=0;i<directions.Length;i++) Render(camera,"Artifacts/doro-axis-"+i+".png",b.center+directions[i]*3.3f,b.center);
      File.WriteAllText("Artifacts/doro-import.txt",string.Join("\n",model.GetComponentsInChildren<Transform>().Select(t=>t.name+" position "+t.localPosition+" rotation "+t.localEulerAngles+" scale "+t.localScale)));
    }
    private static void Render(Camera camera,string path,Vector3 position,Vector3 target)
    {
      camera.transform.position=position;camera.transform.LookAt(target);
      foreach(var label in Object.FindObjectsOfType<GateLabelFont>()) label.SendMessage("LateUpdate");
      var rt=new RenderTexture(1280,800,24);rt.Create();camera.targetTexture=rt;camera.Render();
      var old=RenderTexture.active;RenderTexture.active=rt;
      var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
      File.WriteAllBytes(path,image.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;
      Object.DestroyImmediate(image);rt.Release();Object.DestroyImmediate(rt);
    }
  }
}
