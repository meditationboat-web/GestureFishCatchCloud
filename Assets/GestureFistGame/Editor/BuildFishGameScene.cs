using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GestureFistGame.Editor
{
  public static class BuildFishGameScene
  {
    public const string OutputScene = "Assets/GestureFistGame/Scenes/GestureFishCatchGame.unity";
    private const string Root = "Assets/GestureFistGame";
    private const string DoroModel = Root + "/Art/Doro/tripo_convert_96d41456-dd42-4f72-8c4b-79d7fc7b8d05.fbx";
    private const string DoroMaterial = Root + "/Art/Doro/DoroToon.mat";
    private static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
    private static Font _font;

    [MenuItem("Gesture Fist Game/构建挥手抛网捕鱼场景")]
    public static void Build()
    {
      Directory.CreateDirectory(Root + "/Scenes");
      Directory.CreateDirectory(Root + "/Materials");
      AssetDatabase.Refresh();
      _font = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/NotoSansCJKsc-Regular.otf");
      if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      SetupMaterials();

      var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
      var root = new GameObject("GestureFishGame - 双人合作挥手抛网捕鱼");
      var game = root.AddComponent<FishGameController>();
      var runtimeChecks = root.AddComponent<FishRuntimeChecks>();
      runtimeChecks.game = game;
      game.roundSeconds = 30;
      game.autoStart = true;

      var arena = new GameObject("Arena - 水道与草岸").transform;
      arena.SetParent(root.transform, false);
      CreateArena(arena);

      var net = CreateNet(arena);
      CreatePlayerAvatars(arena);
      var spawnerGo = new GameObject("FishSpawner - 鱼群生成器");
      spawnerGo.transform.SetParent(root.transform, false);
      var spawner = spawnerGo.AddComponent<FishSpawner>();
      spawner.game = game;
      spawnerGo.transform.position = new Vector3(0, .7f, 0);
      spawner.prototypes = CreateFishPrototypes(spawnerGo.transform, game);

      var tracking = new GameObject("TrackingSystem - MediaPipe 手势");
      tracking.transform.SetParent(root.transform, false);
      var input = tracking.AddComponent<FishGestureInput>();
      var tracker = tracking.AddComponent<MediaPipeTracker>();
      var mouse = tracking.AddComponent<FishMouseInput>();
      input.game = game;
      input.tracker = tracker;
      input.mouseInput = mouse;
      mouse.gestureInput = input;
      input.mouseMode = true;
      input.startCameraOnMobile = true;
      tracker.input = null;
      tracker.fishInput = input;
      tracker.autoStart = false;
      tracker.usePose = false;
      tracker.inferenceFps = 20;
      mouse.game = game;

      game.net = net;
      game.spawner = spawner;
      game.input = input;
      net.game = game;

      CreateCamera(root.transform);
      CreateLighting(root.transform);
      CreateUI(root.transform, game, input, tracker);
      CreateWorldGuide(arena);
      new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

      EditorSceneManager.SaveScene(scene, OutputScene);
      UpdateBuildSettings();
      ConfigureMobileSettings();
      AssetDatabase.SaveAssets();
      ValidateScene();
      Debug.Log("FISH_SCENE_SAVED " + OutputScene);
    }

    [MenuItem("Gesture Fist Game/验证挥手捕鱼场景")]
    public static void ValidateScene()
    {
      var scene = SceneManager.GetActiveScene();
      if (scene.path != OutputScene) scene = EditorSceneManager.OpenScene(OutputScene, OpenSceneMode.Single);
      var game = Object.FindObjectOfType<FishGameController>();
      var net = Object.FindObjectOfType<FishNetController>();
      var spawner = Object.FindObjectOfType<FishSpawner>();
      var input = Object.FindObjectOfType<FishGestureInput>();
      var tracker = Object.FindObjectOfType<MediaPipeTracker>();
      var ui = Object.FindObjectOfType<FishGameUI>();
      var fish = Object.FindObjectsOfType<FishTarget>();
      var objects = Object.FindObjectsOfType<Transform>(true);
      if (game == null || net == null || spawner == null || input == null || tracker == null || ui == null)
      {
        Debug.LogError("FISH_CORE_MISSING game=" + (game != null) + " net=" + (net != null) + " spawner=" + (spawner != null) +
          " input=" + (input != null) + " tracker=" + (tracker != null) + " ui=" + (ui != null));
        throw new Exception("捕鱼场景核心对象或脚本缺失");
      }
      if (spawner.prototypes == null || spawner.prototypes.Length < 4) throw new Exception("鱼种原型不足");
      if (net.game != game || spawner.game != game || input.game != game || tracker.fishInput != input)
        throw new Exception("捕鱼场景脚本引用未连接");
      if (!input.requireTwoHands) throw new Exception("双手合作模式未启用");
      Directory.CreateDirectory("Artifacts");
      File.WriteAllText("Artifacts/fish-scene-validation.txt",
        "Scene: " + scene.path + "\nObjects: " + objects.Length + "\nFish prototypes: " + spawner.prototypes.Length +
        "\nFish direction: right-to-left\nNet: vertical center trigger\nTwo-hand cooperation: " + input.cooperationWindow.ToString("0.00") + " seconds\n" +
        "Fish catch loop: linked\nMediaPipe hand input: linked\nMouse fallback: linked\nMobile camera: usePose=false, 20 FPS\n");
      Debug.Log("FISH_SCENE_VALIDATED objects=" + objects.Length + " fishPrototypes=" + spawner.prototypes.Length);
    }

    public static void BuildWindows()
    {
      Directory.CreateDirectory("Builds/Windows");
      var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
      {
        scenes = new[] { OutputScene },
        locationPathName = "Builds/Windows/GestureFishCatchGame.exe",
        target = BuildTarget.StandaloneWindows64,
        options = BuildOptions.None
      });
      Debug.Log("FISH_WINDOWS_BUILD " + report.summary.result + " errors=" + report.summary.totalErrors + " warnings=" + report.summary.totalWarnings);
      if (report.summary.result != BuildResult.Succeeded) throw new Exception("Windows build failed");
    }

    public static void BuildAndroid()
    {
      Directory.CreateDirectory("Builds/Android");
      var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
      {
        scenes = new[] { OutputScene },
        locationPathName = "Builds/Android/GestureFishCatchGame.apk",
        target = BuildTarget.Android,
        options = BuildOptions.None
      });
      Debug.Log("FISH_ANDROID_BUILD " + report.summary.result + " errors=" + report.summary.totalErrors + " warnings=" + report.summary.totalWarnings);
      if (report.summary.result != BuildResult.Succeeded) throw new Exception("Android build failed");
    }

    private static void SetupMaterials()
    {
      Mats.Clear();
      Mat("Water", new Color(.06f, .55f, .75f), .15f);
      Mat("WaterEdge", new Color(.12f, .75f, .86f), .2f);
      Mat("Grass", new Color(.28f, .62f, .28f), .3f);
      Mat("Sand", new Color(.82f, .72f, .43f), .3f);
      Mat("Net", new Color(.1f, .82f, .95f), .15f);
      Mat("NetDark", new Color(.03f, .25f, .42f), .25f);
      Mat("Gold", new Color(1f, .65f, .08f), .2f);
      Mat("White", new Color(.96f, .96f, .93f), .2f);
      Mat("Ink", new Color(.12f, .16f, .2f), .2f);
      Mat("FishBlue", new Color(.15f, .55f, 1f), .2f);
      Mat("FishRed", new Color(1f, .24f, .18f), .2f);
      Mat("FishGold", new Color(1f, .7f, .08f), .2f);
      Mat("FishRainbow", new Color(.9f, .2f, .85f), .2f);
    }

    private static Material Mat(string name, Color color, float gloss)
    {
      var path = Root + "/Materials/Fish_" + name + ".mat";
      var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
      if (mat == null) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, path); }
      mat.color = color;
      mat.SetFloat("_Glossiness", gloss);
      EditorUtility.SetDirty(mat);
      Mats[name] = mat;
      return mat;
    }

    private static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material material, bool collider = true)
    {
      var go = GameObject.CreatePrimitive(type);
      go.name = name;
      go.transform.SetParent(parent, false);
      go.transform.localPosition = pos;
      go.transform.localScale = scale;
      go.GetComponent<Renderer>().sharedMaterial = material;
      if (!collider)
      {
        var c = go.GetComponent<Collider>();
        if (c != null) Object.DestroyImmediate(c);
      }
      return go;
    }

    private static void CreateArena(Transform parent)
    {
      // 横向水道：Doro 从右岸进入，穿过中央竖网后向左岸游出。
      Primitive("WaterChannel_RightToLeft", PrimitiveType.Cube, parent, new Vector3(0, 0, 0), new Vector3(8.4f, .25f, 8), Mats["Water"]);
      Primitive("LeftGrassBank", PrimitiveType.Cube, parent, new Vector3(-4.8f, .25f, 0), new Vector3(.8f, .5f, 8), Mats["Grass"]);
      Primitive("RightGrassBank", PrimitiveType.Cube, parent, new Vector3(4.8f, .25f, 0), new Vector3(.8f, .5f, 8), Mats["Grass"]);
      Primitive("LeftWaterEdge", PrimitiveType.Cube, parent, new Vector3(-4.3f, .18f, 0), new Vector3(.18f, .18f, 8), Mats["WaterEdge"], false);
      Primitive("RightWaterEdge", PrimitiveType.Cube, parent, new Vector3(4.3f, .18f, 0), new Vector3(.18f, .18f, 8), Mats["WaterEdge"], false);
      Primitive("FarBank", PrimitiveType.Cube, parent, new Vector3(0, .28f, 4.3f), new Vector3(10, .55f, 1.0f), Mats["Sand"]);
      Primitive("NearBank", PrimitiveType.Cube, parent, new Vector3(0, .28f, -4.3f), new Vector3(10, .55f, 1.0f), Mats["Sand"]);
      for (int i = 0; i < 10; i++)
      {
        float z = -4 + i * .9f;
        Primitive("WaterLine_" + i, PrimitiveType.Cube, parent, new Vector3(0, .14f, z), new Vector3(8.1f, .018f, .025f), Mats["WaterEdge"], false);
      }
    }

    private static FishNetController CreateNet(Transform parent)
    {
      var go = new GameObject("Net - 中央竖网");
      go.transform.SetParent(parent, false);
      go.transform.localPosition = new Vector3(0, .65f, 0);
      var net = go.AddComponent<FishNetController>();
      var body = go.AddComponent<Rigidbody>();
      body.isKinematic = true;
      body.useGravity = false;
      var trigger = go.AddComponent<BoxCollider>();
      trigger.isTrigger = true;
      trigger.center = new Vector3(0, .45f, 0);
      trigger.size = new Vector3(1.20f, 2.45f, 9.1f);
      var panel = Primitive("NetPanel_Vertical", PrimitiveType.Cube, go.transform, new Vector3(0, .70f, 0), new Vector3(1.15f, 2.25f, 9.0f), Mats["Net"], false);
      net.visual = panel.transform;
      Primitive("NetFrontPost", PrimitiveType.Cylinder, go.transform, new Vector3(0, .70f, -4.45f), new Vector3(.14f, 1.2f, .14f), Mats["NetDark"], false);
      Primitive("NetBackPost", PrimitiveType.Cylinder, go.transform, new Vector3(0, .70f, 4.45f), new Vector3(.14f, 1.2f, .14f), Mats["NetDark"], false);
      Primitive("NetTop_Vertical", PrimitiveType.Cube, go.transform, new Vector3(0, 1.88f, 0), new Vector3(1.25f, .12f, 9.1f), Mats["Gold"], false);
      return net;
    }

    private static FishTarget[] CreateFishPrototypes(Transform parent, FishGameController game)
    {
      var root = new GameObject("FishPrototypes - doro 鱼种").transform;
      root.SetParent(parent, false);
      var data = new[]
      {
        new { name = "BlueDoroFish_1分", display = "蓝色小鱼", points = 1, tint = new Color(.15f,.55f,1f), speed = 1.4f },
        new { name = "RedDoroFish_3分", display = "红色鱼", points = 3, tint = new Color(1f,.24f,.18f), speed = 1.8f },
        new { name = "GoldDoroFish_5分", display = "金色鱼", points = 5, tint = new Color(1f,.7f,.08f), speed = 2.1f },
        new { name = "RainbowDoroFish_10分", display = "稀有彩鱼", points = 10, tint = new Color(.9f,.2f,.85f), speed = 2.5f }
      };
      var result = new List<FishTarget>();
      for (int i = 0; i < data.Length; i++)
      {
        var go = new GameObject(data[i].name, typeof(Rigidbody), typeof(BoxCollider), typeof(FishTarget));
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(0, .7f, 8.5f + i * .1f);
        go.SetActive(false);
        var rb = go.GetComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        var collider = go.GetComponent<BoxCollider>(); collider.isTrigger = true; collider.size = new Vector3(.8f, 1.2f, .9f);
        var fish = go.GetComponent<FishTarget>();
        fish.game = game; fish.points = data[i].points; fish.displayName = data[i].display; fish.speed = data[i].speed;
        AddDoroFishVisual(go.transform, data[i].name, data[i].tint, i);
        result.Add(fish);
      }
      return result.ToArray();
    }

    private static void AddDoroFishVisual(Transform parent, string name, Color tint, int variant)
    {
      var source = AssetDatabase.LoadAssetAtPath<GameObject>(DoroModel);
      var visual = new GameObject(name + "_Visual").transform;
      visual.SetParent(parent, false);
      if (source != null)
      {
        var model = PrefabUtility.InstantiatePrefab(source, visual) as GameObject;
        model.name = "DoroFishModel";
        model.transform.localRotation = Quaternion.Euler(90, 0, 0) * new Quaternion(.5f, .5f, -.5f, -.5f);
        var baseMat = AssetDatabase.LoadAssetAtPath<Material>(DoroMaterial);
        var matPath = Root + "/Materials/Fish_DoroTint_" + variant + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null && baseMat != null) { mat = new Material(baseMat); AssetDatabase.CreateAsset(mat, matPath); }
        // Doro 保持原模型纹理颜色；稀有度由运行时轮廓色表达，不再给整只 Doro 染色。
        if (mat != null)
        {
          if (baseMat != null) mat.color = baseMat.color;
          mat.SetFloat("_RimStrength", .06f);
          EditorUtility.SetDirty(mat);
        }
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
          if (mat != null) r.sharedMaterials = r.sharedMaterials.Select(_ => mat).ToArray();
          r.gameObject.layer = 0;
        }
        var bounds = BoundsOf(renderers);
        float scale = 1.15f / Mathf.Max(.1f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
        model.transform.localScale = Vector3.one * scale;
      }
      else
      {
        Primitive("FishBody", PrimitiveType.Sphere, visual, Vector3.zero, new Vector3(1.2f, .6f, .8f), Mats["FishBlue"], false);
        Primitive("FishTail", PrimitiveType.Cube, visual, new Vector3(0, 0, -.65f), new Vector3(.18f, .5f, .5f), Mats["FishBlue"], false);
      }
    }

    private static Bounds BoundsOf(Renderer[] renderers)
    {
      if (renderers == null || renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
      var b = renderers[0].bounds;
      foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds);
      return b;
    }

    private static void CreatePlayerAvatars(Transform arena)
    {
      CreatePlayerAvatar(arena, "玩家1", new Vector3(0, .25f, -4.15f), false, Mats["Net"]);
      CreatePlayerAvatar(arena, "玩家2", new Vector3(0, .25f, 4.15f), true, Mats["Gold"]);
    }

    private static void CreatePlayerAvatar(Transform parent, string name, Vector3 position, bool faceAwayFromCamera, Material accent)
    {
      var root = new GameObject(name).transform;
      root.SetParent(parent, false);
      root.localPosition = position;
      root.localRotation = Quaternion.Euler(0, faceAwayFromCamera ? 180f : 0f, 0);
      Primitive("AvatarBody", PrimitiveType.Capsule, root, new Vector3(0, .9f, 0), new Vector3(.58f, .82f, .58f), accent, false);
      Primitive("AvatarHead", PrimitiveType.Sphere, root, new Vector3(0, 1.9f, 0), new Vector3(.62f, .62f, .62f), Mats["White"], false);
      Primitive("AvatarLeftArm", PrimitiveType.Capsule, root, new Vector3(.48f, 1.25f, -.28f), new Vector3(.18f, .68f, .18f), Mats["White"], false).transform.localRotation = Quaternion.Euler(0, 0, -48f);
      Primitive("AvatarRightArm", PrimitiveType.Capsule, root, new Vector3(.48f, 1.25f, .28f), new Vector3(.18f, .68f, .18f), Mats["White"], false).transform.localRotation = Quaternion.Euler(0, 0, -48f);
      Primitive("AvatarHandA", PrimitiveType.Sphere, root, new Vector3(.95f, 1.55f, -.28f), new Vector3(.24f, .24f, .24f), accent, false);
      Primitive("AvatarHandB", PrimitiveType.Sphere, root, new Vector3(.95f, 1.55f, .28f), new Vector3(.24f, .24f, .24f), accent, false);
      Primitive("AvatarLegA", PrimitiveType.Capsule, root, new Vector3(0, .35f, -.2f), new Vector3(.20f, .45f, .20f), Mats["NetDark"], false);
      Primitive("AvatarLegB", PrimitiveType.Capsule, root, new Vector3(0, .35f, .2f), new Vector3(.20f, .45f, .20f), Mats["NetDark"], false);
      Primitive("AvatarBase", PrimitiveType.Cylinder, root, new Vector3(0, .08f, 0), new Vector3(.85f, .10f, .85f), Mats["NetDark"], false);
      var label = new GameObject("AvatarLabel", typeof(TextMesh));
      label.transform.SetParent(root, false);
      label.transform.localPosition = new Vector3(0, 2.15f, 0);
      label.transform.localRotation = Quaternion.Euler(58f, 0, 0);
      var text = label.GetComponent<TextMesh>();
      text.text = name;
      text.characterSize = .12f;
      text.fontSize = 36;
      text.anchor = TextAnchor.MiddleCenter;
      text.alignment = TextAlignment.Center;
      text.color = new Color(.1f, .15f, .2f);
      label.AddComponent<FishWorldLabel>();
    }

    private static void CreateCamera(Transform parent)
    {
      var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
      go.transform.SetParent(parent, false);
      go.tag = "MainCamera";
      go.transform.position = new Vector3(0, 12.8f, -12.8f);
      go.transform.LookAt(new Vector3(0, .45f, 0));
      var camera = go.GetComponent<Camera>();
      camera.orthographic = true; camera.orthographicSize = 7.6f;
      camera.nearClipPlane = .05f; camera.farClipPlane = 80;
      camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.52f, .82f, .93f);
    }

    private static void CreateLighting(Transform parent)
    {
      var go = new GameObject("Sun - 水面主光", typeof(Light));
      go.transform.SetParent(parent, false); go.transform.rotation = Quaternion.Euler(45, -30, 0);
      var light = go.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.1f; light.color = new Color(1f, .95f, .82f);
      RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
      RenderSettings.ambientLight = new Color(.63f, .72f, .77f);
      RenderSettings.fog = false;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
      var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
      rect.SetParent(parent, false); rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = new Vector2(.5f, .5f);
      rect.anchoredPosition = pos; rect.sizeDelta = size;
      return rect;
    }

    private static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
    {
      var rect = Rect(name, parent, anchor, pos, size);
      var image = rect.gameObject.AddComponent<Image>(); image.color = color;
      image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"); image.type = Image.Type.Sliced;
      return rect;
    }

    private static Text Label(string name, Transform parent, string value, Vector2 pos, Vector2 size, int fontSize, Color color, TextAnchor align = TextAnchor.MiddleLeft)
    {
      var rect = Rect(name, parent, new Vector2(.5f, .5f), pos, size);
      var text = rect.gameObject.AddComponent<Text>(); text.font = _font; text.text = value; text.fontSize = fontSize; text.color = color;
      text.alignment = align; text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
      return text;
    }

    private static Button Button(Transform parent, string text, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction action)
    {
      var rect = Panel(text, parent, new Vector2(.5f, .5f), pos, size, Color.white);
      rect.GetComponent<Image>().sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
      var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>(); button.colors = ColorBlock.defaultColorBlock;
      Label("Label", rect, text, Vector2.zero, size - new Vector2(8, 4), 15, new Color(.18f, .18f, .18f), TextAnchor.MiddleCenter);
      UnityEventTools.AddPersistentListener(button.onClick, action);
      return button;
    }

    private static void CreateUI(Transform parent, FishGameController game, FishGestureInput input, MediaPipeTracker tracker)
    {
      var canvasGo = new GameObject("Canvas - 挥手捕鱼原生界面", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
      canvasGo.transform.SetParent(parent, false);
      canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
      var scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0f;
      var ui = canvasGo.AddComponent<FishGameUI>(); ui.game = game; ui.input = input; ui.tracker = tracker;
      var ink = new Color(.94f, .95f, .96f, .94f); var dark = new Color(.16f, .19f, .23f); var muted = new Color(.28f, .32f, .38f);
      var title = Panel("TitleCard", canvasGo.transform, new Vector2(0, 1), new Vector2(225, -105), new Vector2(430, 150), ink);
      Label("Title", title, "双人合作抛网捕鱼", new Vector2(0, 34), new Vector2(400, 54), 34, dark, TextAnchor.MiddleCenter);
      Label("Subtitle", title, "玩家1 + 玩家2  /  同步上挥", new Vector2(0, -28), new Vector2(400, 28), 18, muted, TextAnchor.MiddleCenter);
      var scorePanel = Panel("ScorePanel", canvasGo.transform, new Vector2(1, 1), new Vector2(-225, -105), new Vector2(430, 150), ink);
      ui.scoreText = Label("Score", scorePanel, "分数  0    连击  0", new Vector2(0, 34), new Vector2(400, 40), 26, dark, TextAnchor.MiddleCenter);
      ui.timeText = Label("Time", scorePanel, "时间  30 秒", new Vector2(-100, -30), new Vector2(180, 32), 21, muted, TextAnchor.MiddleCenter);
      ui.rankText = Label("Rank", scorePanel, "等级  C", new Vector2(110, -30), new Vector2(150, 32), 21, muted, TextAnchor.MiddleCenter);
      var statusPanel = Panel("StatusPanel", canvasGo.transform, new Vector2(.5f, 0), new Vector2(0, 150), new Vector2(900, 190), ink);
      ui.statusText = Label("Status", statusPanel, "左右双手一起向上挥，把鱼网抛起来", new Vector2(0, 48), new Vector2(860, 88), 22, dark, TextAnchor.MiddleCenter);
      ui.netText = Label("NetState", statusPanel, "网状态  Ready", new Vector2(0, -12), new Vector2(300, 28), 19, muted, TextAnchor.MiddleCenter);
      var gesture = Button(statusPanel, "手势控制", new Vector2(165, -65), new Vector2(145, 36), ui.UseGesture);
      var mouse = Button(statusPanel, "鼠标测试", new Vector2(330, -65), new Vector2(145, 36), ui.UseMouse);
      ui.gestureButton = gesture; ui.mouseButton = mouse; mouse.interactable = false;
      ui.resetButton = Button(statusPanel, "重新开始", new Vector2(-330, -65), new Vector2(145, 36), ui.ResetRound);
      ui.calibrateButton = Button(statusPanel, "重新连接摄像头", new Vector2(-165, -65), new Vector2(145, 36), ui.Calibrate);
      var previewPanel = Panel("CameraPanel", canvasGo.transform, new Vector2(0, 1), new Vector2(144, -315), new Vector2(260, 260), ink);
      var previewRect = Rect("CameraPreview", previewPanel, new Vector2(.5f, .5f), new Vector2(0, 20), new Vector2(230, 170));
      ui.cameraPreview = previewRect.gameObject.AddComponent<RawImage>(); tracker.preview = ui.cameraPreview;
      ui.cameraDebugText = Label("CameraHint", previewPanel, "摄像头诊断：等待启动", new Vector2(0, -100), new Vector2(230, 52), 11, muted, TextAnchor.MiddleCenter);
      ui.cameraPanel = previewPanel.gameObject; previewPanel.gameObject.SetActive(false);
      var finish = Panel("FinishPanel", canvasGo.transform, new Vector2(.5f, .5f), new Vector2(0, 40), new Vector2(460, 270), new Color(.98f, .98f, .98f, .97f));
      ui.finishPanel = finish.gameObject;
      ui.finishText = Label("FinishText", finish, "回合结束", new Vector2(0, 25), new Vector2(410, 150), 24, dark, TextAnchor.MiddleCenter);
      finish.gameObject.SetActive(false);
    }

    private static void CreateWorldGuide(Transform arena)
    {
      // Keep the playfield open. The control instruction is presented in the
      // status panel, where it remains legible on both desktop and mobile.
    }

    private static void UpdateBuildSettings()
    {
      var scenes = new List<EditorBuildSettingsScene>();
      scenes.Add(new EditorBuildSettingsScene(OutputScene, true));
      scenes.Add(new EditorBuildSettingsScene("Assets/GestureFistGame/Scenes/GestureFistGame.unity", true));
      foreach (var s in EditorBuildSettings.scenes)
        if (s.path != OutputScene && s.path != "Assets/GestureFistGame/Scenes/GestureFistGame.unity" && s.enabled) scenes.Add(s);
      EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void ConfigureMobileSettings()
    {
      PlayerSettings.productName = "挥手抛网捕鱼学习项目";
      PlayerSettings.companyName = "GestureFist";
      PlayerSettings.allowedAutorotateToPortrait = true;
      PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
      PlayerSettings.allowedAutorotateToLandscapeLeft = false;
      PlayerSettings.allowedAutorotateToLandscapeRight = false;
      PlayerSettings.defaultScreenWidth = 1080;
      PlayerSettings.defaultScreenHeight = 1920;
      PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.gesturefist.fishcatch");
      PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
      PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
      PlayerSettings.Android.buildApkPerCpuArchitecture = false;
    }
  }
}
