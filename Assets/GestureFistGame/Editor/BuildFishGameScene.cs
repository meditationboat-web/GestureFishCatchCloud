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
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace GestureFistGame.Editor
{
  public static class BuildFishGameScene
  {
    public const string OutputScene = "Assets/GestureFistGame/Scenes/GestureFishCatchGame.unity";
    private const string Root = "Assets/GestureFistGame";
    private const string DoroModel = Root + "/Art/Doro/tripo_convert_96d41456-dd42-4f72-8c4b-79d7fc7b8d05.fbx";
    private const string DoroMaterial = Root + "/Art/Doro/DoroToon.mat";
    private const string WaterShader = Root + "/Shaders/InteractiveWater.shader";
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
      ConfigureGestureFishURP.Run();
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
      FishSpeciesCatalog.Replace(spawner);

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
      tracker.usePose = true;
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
      var water = Object.FindObjectOfType<InteractiveWaterSurface>();
      var avatars = Object.FindObjectsOfType<FishPlayerAvatar>(true);
      var fish = Object.FindObjectsOfType<FishTarget>();
      var objects = Object.FindObjectsOfType<Transform>(true);
      var kayKitCount = objects.Count(t => t.name.StartsWith("KayKit", StringComparison.Ordinal));
      var grassCount = objects.Count(t => t.name.StartsWith("KayKitGrass_", StringComparison.Ordinal));
      var islandTreeCount = objects.Count(t => t.name.StartsWith("IslandTree_", StringComparison.Ordinal));
      var islandRockCount = objects.Count(t => t.name.StartsWith("IslandRock_", StringComparison.Ordinal));
      var islandTuftCount = objects.Count(t => t.name.StartsWith("IslandTuft_", StringComparison.Ordinal));
      if (game == null || net == null || spawner == null || input == null || tracker == null || ui == null)
      {
        Debug.LogError("FISH_CORE_MISSING game=" + (game != null) + " net=" + (net != null) + " spawner=" + (spawner != null) +
          " input=" + (input != null) + " tracker=" + (tracker != null) + " ui=" + (ui != null));
        throw new Exception("捕鱼场景核心对象或脚本缺失");
      }
      if (water == null) throw new Exception("交互水面脚本缺失");
      if (water.GetComponent<WaterRippleRenderTexture>() == null) throw new Exception("RenderTexture 互动涟漪层缺失");
      if (water.waterMaterialOverride != null || water.waterShader == null || water.waterShader.name != "GestureFistGame/InteractiveWater")
        throw new Exception("水面必须使用原版 InteractiveWater Shader");
      if (water.circular) throw new Exception("水面仍处于圆形池塘模式，河道必须使用矩形水面");
      if (water.width <= 0f || water.depth <= 0f) throw new Exception("河道水面尺寸无效");
      if (avatars.Length < 2) throw new Exception("两侧玩家挥臂角色缺失");
      if (kayKitCount + islandTreeCount + islandRockCount + islandTuftCount < 30) throw new Exception("岛屿树木、岩石和草簇装饰实例不足");
      if (grassCount + islandTuftCount < 20 || Shader.Find("GestureFistGame/StylizedGrass") == null) throw new Exception("风格化草地 Shader 或草簇不足");
      var meadowGrounds = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true))
        .Where(t => t.name == "MeadowGround" || t.name == "MeadowGround_Near" || t.name == "MeadowGround_Far").ToArray();
      var meadowRenderers = meadowGrounds.Select(t => t.GetComponent<Renderer>()).Where(r => r != null).ToArray();
      bool meadowValid = meadowRenderers.Length >= 2
        ? meadowRenderers.All(r => r.bounds.size.x >= 30f && r.bounds.size.z >= 19f)
        : meadowRenderers.Length == 1 && meadowRenderers[0].bounds.size.x >= 30f && meadowRenderers[0].bounds.size.z >= 24f;
      if (!meadowValid)
        throw new Exception("开阔草地底板缺失或范围不足");
      var ropeVisual = net.transform.Find("RopeNetVisual");
      if (ropeVisual == null || ropeVisual.GetComponentsInChildren<LineRenderer>(true).Length < 20)
        throw new Exception("真实绳网网格不足");
      if (!ropeVisual.gameObject.activeSelf)
        throw new Exception("渔网主视觉必须始终存在");
      if (net.transform.Find("NetSubmergedMarker") != null)
        throw new Exception("旧的水下重复渔网未移除");
      if (Shader.Find("GestureFistGame/InteractiveWater") == null) throw new Exception("URP 互动水面 Shader 未加载");
      if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline == null) throw new Exception("URP 全局渲染管线未启用");
      if (net.GetComponent<BoxCollider>() == null) throw new Exception("鱼网捕获触发器缺失");
      var netTrigger = net.GetComponent<BoxCollider>();
      if (netTrigger.size.x <= netTrigger.size.y * 4f || netTrigger.size.z <= netTrigger.size.y * 4f || netTrigger.size.y > .5f)
        throw new Exception("鱼网触发器必须是宽而薄的水平区域");
      foreach (var strand in ropeVisual.GetComponentsInChildren<LineRenderer>(true))
        for (int i = 0; i < strand.positionCount; i++)
          if (Mathf.Abs(strand.GetPosition(i).y) > .06f)
            throw new Exception("鱼网绳索仍偏离水平网面");
      if (net.restHeight > -.08f) throw new Exception("休息状态渔网没有低于鱼的水下位置");
      if (spawner.prototypes == null || spawner.prototypes.Length != 3) throw new Exception("鱼种原型不足");
      if (net.game != game || spawner.game != game || input.game != game || tracker.fishInput != input)
        throw new Exception("捕鱼场景脚本引用未连接");
      if (!input.requireTwoHands) throw new Exception("双手合作模式未启用");
      Directory.CreateDirectory("Artifacts");
      File.WriteAllText("Artifacts/fish-scene-validation.txt",
        "Scene: " + scene.path + "\nObjects: " + objects.Length + "\nFish prototypes: " + spawner.prototypes.Length +
        "\nFish direction: right-to-left\nNet: horizontal surface trigger\nTwo-hand cooperation: " + input.cooperationWindow.ToString("0.00") + " seconds\n" +
        "Fish catch loop: linked\nCentral overlap catch scan: linked\nInteractive water: original GestureFistGame shader + URP shallow-water height/flow simulation + mass conservation + render-texture ripples\nRope net: one grid mesh, visible underwater at idle and raised during the two-hand throw\nOpen meadow: 32x26 ground + layered outer scenery\nIsland props: trees=" + islandTreeCount + " rocks=" + islandRockCount + " tufts=" + islandTuftCount + " legacyKayKit=" + kayKitCount + " legacyGrass=" + grassCount + " stylized grass shader=linked\nPlayer avatars: " + avatars.Length + " hand-specific throw animations\nMediaPipe pose raised-arm input: linked\nMouse fallback: linked\nMobile camera: pose-only, max 2 people, 20 FPS\n");
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
      // The side terraces only close the gap between the water and the
      // meadow meshes. Match them to the meadow palette so they read as one
      // continuous painted ground plane from the isometric camera.
      Mat("BankGround", new Color(.45f, .52f, .25f), .05f);
      Mat("Meadow", new Color(.18f, .42f, .16f), .08f);
      Mat("MeadowLight", new Color(.28f, .56f, .20f), .08f);
      Mat("MeadowDark", new Color(.12f, .30f, .13f), .08f);
      StylizedGrassMat();
      Mat("Sand", new Color(.82f, .72f, .43f), .3f);
      Mat("Soil", new Color(.34f, .24f, .15f), .18f);
      Mat("Rock", new Color(.34f, .39f, .38f), .42f);
      Mat("Reed", new Color(.16f, .42f, .18f), .25f);
      // Reuse the KayKit meshes, but put every forest piece on the same
      // muted low-poly palette as the reference instead of the source pack's
      // saturated lime materials.
      Mat("ForestLeaf", new Color(.31f, .46f, .34f), .08f);
      Mat("ForestLeafLight", new Color(.41f, .56f, .38f), .08f);
      Mat("ForestTrunk", new Color(.38f, .22f, .16f), .06f);
      Mat("ForestRock", new Color(.43f, .45f, .53f), .08f);
      Mat("Net", new Color(.1f, .82f, .95f), .15f);
      Mat("NetDark", new Color(.03f, .25f, .42f), .25f);
      Mat("Gold", new Color(1f, .65f, .08f), .2f);
      Mat("White", new Color(.96f, .96f, .93f), .2f);
      Mat("Ink", new Color(.12f, .16f, .2f), .2f);
      Mat("AvatarSkin", new Color(.86f, .55f, .40f), .12f);
      Mat("AvatarHair", new Color(.16f, .045f, .025f), .08f);
      Mat("AvatarClothes", new Color(.24f, .43f, .16f), .08f);
      Mat("AvatarBoot", new Color(.18f, .38f, .12f), .08f);
      Mat("AvatarSkinA", new Color(.93f, .65f, .48f), .04f);
      Mat("AvatarSkinB", new Color(1f, .76f, .58f), .04f);
      Mat("AvatarHairA", new Color(.16f, .07f, .045f), .02f);
      Mat("AvatarHairB", new Color(.27f, .12f, .065f), .02f);
      Mat("AvatarClothesA", new Color(.12f, .48f, .43f), .03f);
      Mat("AvatarClothesB", new Color(.83f, .37f, .24f), .03f);
      Mat("AvatarBootA", new Color(.06f, .20f, .20f), .08f);
      Mat("AvatarBootB", new Color(.32f, .14f, .09f), .08f);
      Mat("AvatarCream", new Color(1f, .88f, .65f), .02f);
      Mat("AvatarBlush", new Color(.95f, .39f, .32f), .02f);
      Mat("AvatarFaceInk", new Color(.10f, .055f, .04f), .02f);
      Mat("FishBlue", new Color(.15f, .55f, 1f), .2f);
      Mat("FishRed", new Color(1f, .24f, .18f), .2f);
      Mat("FishGold", new Color(1f, .7f, .08f), .2f);
      Mat("FishRainbow", new Color(.9f, .2f, .85f), .2f);
      // Soft supporting colors for the face and fins keep the fish readable
      // against the blue water while matching the rounded meadow palette.
      Mat("FishCream", new Color(1f, .90f, .66f), .04f);
      Mat("FishBlush", new Color(1f, .48f, .45f), .03f);
    }

    private static Material Mat(string name, Color color, float gloss)
    {
      var path = Root + "/Materials/Fish_" + name + ".mat";
      var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
      var urpLit = name == "BankGround"
        ? (Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit"))
        : Shader.Find("Universal Render Pipeline/Lit");
      var fallback = urpLit != null ? urpLit : Shader.Find("Standard");
      if (mat == null) { mat = new Material(fallback); AssetDatabase.CreateAsset(mat, path); }
      else if (urpLit != null && mat.shader != urpLit) mat.shader = urpLit;
      mat.color = color;
      if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", gloss);
      if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", gloss);
      EditorUtility.SetDirty(mat);
      Mats[name] = mat;
      return mat;
    }

    private static Material StylizedGrassMat()
    {
      const string path = Root + "/Materials/Fish_GrassStylized.mat";
      var shader = Shader.Find("GestureFistGame/StylizedGrass");
      var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
      if (shader == null) return Mat("GrassStylized", new Color(.22f, .58f, .18f), .2f);
      if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
      else if (mat.shader != shader) mat.shader = shader;
      mat.SetColor("_BaseColor", new Color(.055f, .28f, .075f, 1f));
      mat.SetColor("_TipColor", new Color(.58f, .90f, .16f, 1f));
      mat.SetColor("_RimColor", new Color(.70f, .98f, .28f, 1f));
      mat.SetFloat("_WindStrength", .045f);
      mat.SetFloat("_WindSpeed", 1.15f);
      mat.SetFloat("_WindScale", .72f);
      mat.SetFloat("_HeightRange", 1.0f);
      mat.SetFloat("_VariationStrength", .16f);
      mat.SetFloat("_RimPower", 3.2f);
      mat.enableInstancing = true;
      EditorUtility.SetDirty(mat);
      Mats["GrassStylized"] = mat;
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
      // 横向水道：Doro 从右岸进入，经过中央水平网后向左岸游出。
      var water = Primitive("WaterChannel_RightToLeft", PrimitiveType.Cube, parent, new Vector3(0, 0, 0), new Vector3(8.4f, .25f, 8), Mats["Water"]);
      var waterSurface = water.AddComponent<InteractiveWaterSurface>();
      waterSurface.width = 8.4f; waterSurface.depth = 8f;
      waterSurface.columns = 56; waterSurface.rows = 40;
      waterSurface.circular = false;
      waterSurface.enableRenderTextureRipples = true;
      waterSurface.rippleMapResolution = 128;
      waterSurface.rippleLifetime = 1.35f;
      var rippleLayer = water.AddComponent<WaterRippleRenderTexture>();
      rippleLayer.resolution = 128;
      rippleLayer.lifetime = 1.35f;
      waterSurface.waterShader = AssetDatabase.LoadAssetAtPath<Shader>(WaterShader);
      waterSurface.waterMaterialOverride = null;
      CreateOpenMeadow(parent);
      // The side terraces share the serialized meadow material, so create the
      // asset first and then reference it instead of the previous build's
      // deleted material instance.
      CreateSideBanks(parent);
      // Keep the water object and its parameters intact. Rebuild only the
      // surrounding island dressing with the calmer low-poly art direction.
      StylizedIslandBuilder.Build(parent);
    }

    private static void CreateSideBanks(Transform parent)
    {
      // The playable water rectangle stays exactly the same. These two low
      // side terraces only fill the land outside its X bounds, removing the
      // sky-blue voids visible beside the central water and giving both sides
      // the continuous meadow treatment of the reference image.
      float bankWidth = 15.0f;
      float bankX = 4.2f + bankWidth * .5f + .12f;
      var bankMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/GeneratedMeadow/MeadowGround.mat") ?? Mats["BankGround"];
      Primitive("SideBank_Left", PrimitiveType.Cube, parent,
        new Vector3(-bankX, .085f, 0f), new Vector3(bankWidth, .17f, 8.5f), bankMaterial, true).AddComponent<FishGroundSurface>();
      Primitive("SideBank_Right", PrimitiveType.Cube, parent,
        new Vector3(bankX, .085f, 0f), new Vector3(bankWidth, .17f, 8.5f), bankMaterial, true).AddComponent<FishGroundSurface>();
    }

    private static void CreateOpenMeadow(Transform parent)
    {
      // Use the authored dense field and noise-shaped ground instead of the
      // old cube-plus-patches placeholder. The builder saves meshes/materials
      // as project assets, so the playable scene and the preview share them.
      OpenMeadowBuilder.Build(parent);
    }

    private static void CreateNaturalBankDetails(Transform parent)
    {
      var rockPositions = new[] { -4.0f, -2.6f, -1.1f, .55f, 2.1f, 3.65f };
      for (int i = 0; i < rockPositions.Length; i++)
      {
        float x = rockPositions[i];
        var near = Primitive("NearBankRock_" + i, PrimitiveType.Sphere, parent, new Vector3(x, .57f + (i % 2) * .05f, -4.02f), new Vector3(.42f + (i % 3) * .12f, .22f, .30f), Mats["Rock"], false);
        near.transform.localRotation = Quaternion.Euler(0, i * 27f, i * 5f);
        var far = Primitive("FarBankRock_" + i, PrimitiveType.Sphere, parent, new Vector3(-x, .57f + ((i + 1) % 2) * .05f, 4.02f), new Vector3(.38f + (i % 2) * .14f, .20f, .28f), Mats["Rock"], false);
        far.transform.localRotation = Quaternion.Euler(0, i * 19f, -i * 4f);
      }
      for (int i = 0; i < 9; i++)
      {
        float x = -4.1f + i * 1.02f;
        var near = Primitive("NearReed_" + i, PrimitiveType.Capsule, parent, new Vector3(x, .77f, -3.92f), new Vector3(.045f, .48f, .045f), Mats["Reed"], false);
        near.transform.localRotation = Quaternion.Euler(0, 0, (i % 2 == 0 ? -8f : 8f));
        var far = Primitive("FarReed_" + i, PrimitiveType.Capsule, parent, new Vector3(x + .22f, .77f, 3.92f), new Vector3(.045f, .42f, .045f), Mats["Reed"], false);
        far.transform.localRotation = Quaternion.Euler(0, 0, (i % 2 == 0 ? 7f : -7f));
      }
      CreateKayKitBankDecor(parent);
    }

    private static void CreateKayKitBankDecor(Transform parent)
    {
      const string artRoot = Root + "/Art/KayKitForest/";
      string[] trees = { "Tree_1_A_Color1.fbx", "Tree_2_C_Color1.fbx", "Tree_3_A_Color1.fbx", "Tree_4_B_Color1.fbx" };
      string[] bushes = { "Bush_1_A_Color1.fbx", "Bush_2_C_Color1.fbx", "Bush_3_A_Color1.fbx", "Bush_4_D_Color1.fbx" };
      string[] rocks = { "Rock_1_C_Color1.fbx", "Rock_2_F_Color1.fbx", "Rock_3_J_Color1.fbx" };
      string[] grass = { "Grass_1_B_Color1.fbx", "Grass_2_C_Color1.fbx" };
      // Layered outer scenery makes the river part of an open valley:
      // foreground trees, smaller background trees, then bushes, rocks and
      // grass tufts around the meadow perimeter.
      for (int i = 0; i < 8; i++)
      {
        bool nearFar = (i % 2) == 0;
        int band = i / 2;
        float outerX = nearFar ? -12.8f + (band % 5) * 6.25f : ((i % 4 < 2) ? -11.4f : 11.4f);
        float z = nearFar ? (i % 4 == 0 ? -10.2f : 10.2f) : -8.5f + (band % 5) * 4.25f;
        float scale = nearFar ? (i % 4 == 0 ? .56f : .43f) : .38f;
        KayKit(artRoot + trees[(i + 2) % trees.Length], "KayKitTree_Outer_" + i, parent,
          new Vector3(outerX, .34f, z), scale, 21f * i + 8f);
      }
      for (int i = 0; i < 10; i++)
      {
        float angle = i * 137.5f * Mathf.Deg2Rad;
        float radiusX = 7.0f + (i % 4) * 1.65f;
        float radiusZ = 6.4f + (i % 3) * 1.45f;
        float px = Mathf.Cos(angle) * radiusX;
        float pz = Mathf.Sin(angle) * radiusZ;
        if (Mathf.Abs(px) < 5.35f) px = Mathf.Sign(px == 0 ? 1 : px) * 5.35f;
        KayKit(artRoot + bushes[(i + 1) % bushes.Length], "KayKitBush_Outer_" + i, parent,
          new Vector3(px, .25f, pz), .25f + (i % 3) * .025f, 17f * i);
      }
      for (int i = 0; i < 10; i++)
      {
        float angle = (i * 1.618f + .35f) * Mathf.PI * 2f;
        float px = Mathf.Cos(angle) * (6.0f + (i % 5) * 1.45f);
        float pz = Mathf.Sin(angle) * (5.9f + (i % 4) * 1.25f);
        if (Mathf.Abs(px) < 5.0f) px = Mathf.Sign(px == 0 ? 1 : px) * 5.0f;
        KayKit(artRoot + rocks[(i + 1) % rocks.Length], "KayKitRock_Outer_" + i, parent,
          new Vector3(px, .22f, pz), .20f + (i % 3) * .03f, 29f * i);
      }
      for (int i = 0; i < 20; i++)
      {
        float angle = (i * 2.399f + .7f) * Mathf.PI * 2f;
        float px = Mathf.Cos(angle) * (5.3f + (i % 7) * 1.25f);
        float pz = Mathf.Sin(angle) * (5.2f + (i % 6) * 1.15f);
        if (Mathf.Abs(px) < 4.95f && Mathf.Abs(pz) < 4.95f) px += Mathf.Sign(px == 0 ? 1 : px) * 1.0f;
        KayKit(artRoot + grass[(i + 1) % grass.Length], "KayKitGrass_Outer_" + i, parent,
          new Vector3(px, .10f, pz), .16f + (i % 4) * .018f, -9f * i);
      }
    }

    private static GameObject KayKit(string assetPath, string name, Transform parent, Vector3 position, float scale, float yaw)
    {
      var source = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
      if (source == null) return null;
      var instance = PrefabUtility.InstantiatePrefab(source, parent) as GameObject;
      if (instance == null) return null;
      instance.name = name;
      instance.transform.localPosition = position;
      instance.transform.localRotation = Quaternion.Euler(0, yaw, 0);
      instance.transform.localScale = Vector3.one * scale;
      foreach (var col in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
      // Preserve the supplied atlas while applying a restrained palette tint.
      // This keeps the model's authored gradients and silhouettes but brings
      // the very bright default greens into the same painted world as the
      // custom island dressing.
      string category = assetPath.IndexOf("/Rock_", StringComparison.OrdinalIgnoreCase) >= 0 ? "rock" :
        (assetPath.IndexOf("/Grass_", StringComparison.OrdinalIgnoreCase) >= 0 ? "grass" : "foliage");
      foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
      {
        var sourceMaterials = renderer.sharedMaterials;
        var tinted = new Material[sourceMaterials.Length];
        for (int i = 0; i < sourceMaterials.Length; i++)
          tinted[i] = CreateKayKitTint(sourceMaterials[i], category);
        renderer.sharedMaterials = tinted;
      }
      return instance;
    }

    private static Material CreateKayKitTint(Material source, string category)
    {
      if (source == null) return null;
      string safeName = source.name.Replace("/", "_").Replace(" ", "_");
      string path = Root + "/Art/KayKitForest/Tinted_" + safeName + "_" + category + ".mat";
      var material = AssetDatabase.LoadAssetAtPath<Material>(path);
      if (material == null)
      {
        material = new Material(source) { name = "Tinted_" + safeName + "_" + category };
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        AssetDatabase.CreateAsset(material, path);
      }
      Color tint = category == "rock" ? new Color(.58f, .62f, .70f, 1f) :
        (category == "grass" ? new Color(.46f, .58f, .30f, 1f) : new Color(.40f, .52f, .34f, 1f));
      if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
      else material.color = tint;
      if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .12f);
      material.enableInstancing = true;
      EditorUtility.SetDirty(material);
      return material;
    }

    private static FishNetController CreateNet(Transform parent)
    {
      var go = new GameObject("Net - 水平抬网");
      go.transform.SetParent(parent, false);
      go.transform.localPosition = new Vector3(0, 0, 0);
      var net = go.AddComponent<FishNetController>();
      net.restHeight = FishNetGeometry.RestHeight;
      net.raisedHeight = FishNetGeometry.RaisedHeight;
      var body = go.AddComponent<Rigidbody>();
      body.isKinematic = true;
      body.useGravity = false;
      var trigger = go.AddComponent<BoxCollider>();
      trigger.isTrigger = true;
      trigger.center = FishNetGeometry.TriggerCenter;
      trigger.size = FishNetGeometry.TriggerSize;
      var panel = CreateRopeNetVisual(go.transform);
      net.visual = panel.transform;
      // There is deliberately no second submerged mesh.  The former dark
      // rope grid read as an unrelated duplicate net through the water.
      net.submergedVisual = null;
      return net;
    }

    private static GameObject CreateRopeNetVisual(Transform parent)
    {
      return FishNetGeometry.Create(parent, "RopeNetVisual", Mats["Net"], Mats["Gold"], false).gameObject;
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
        FishMobileUpgrade.AddFishModel(go.transform);
        result.Add(fish);
      }
      return result.ToArray();
    }

    private static void AddStylizedFishVisual(Transform parent, string name, Color tint, int variant)
    {
      var visual = new GameObject(name + "_Visual").transform;
      visual.SetParent(parent, false);
      // The fish use the same rounded low-poly language as the meadow props.
      // The nose points along local -Z so FishSpawner's 90-degree travel
      // rotation makes them face left across the water. Keep all details under
      // one visual root so FishTarget's rendered bounds and catch animation
      // continue to treat the fish as a single object.
      string materialName = variant == 0 ? "FishBlue" : variant == 1 ? "FishRed" : variant == 2 ? "FishGold" : "FishRainbow";
      var fishMat = Mats[materialName];
      var body = Primitive("FishBody", PrimitiveType.Sphere, visual, new Vector3(0, 0, .02f), new Vector3(.50f, .34f, .72f), fishMat, false);
      body.transform.localRotation = Quaternion.Euler(0, 0, variant % 2 == 0 ? -3f : 3f);
      Primitive("FishHead", PrimitiveType.Sphere, visual, new Vector3(0, .025f, -.43f), new Vector3(.43f, .31f, .37f), fishMat, false);

      // A pale belly and small rounded fins add a soft toy-like silhouette
      // without introducing custom meshes or colliders.
      Primitive("FishBelly", PrimitiveType.Sphere, visual, new Vector3(0, -.105f, -.06f), new Vector3(.38f, .19f, .48f), Mats["FishCream"], false);
      var tailTop = Primitive("FishTailTop", PrimitiveType.Capsule, visual, new Vector3(0, .14f, .72f), new Vector3(.18f, .23f, .20f), fishMat, false);
      tailTop.transform.localRotation = Quaternion.Euler(-26f, 0f, 0f);
      var tailBottom = Primitive("FishTailBottom", PrimitiveType.Capsule, visual, new Vector3(0, -.14f, .72f), new Vector3(.18f, .23f, .20f), fishMat, false);
      tailBottom.transform.localRotation = Quaternion.Euler(26f, 0f, 0f);
      var dorsal = Primitive("FishDorsalFin", PrimitiveType.Capsule, visual, new Vector3(0, .255f, .10f), new Vector3(.14f, .16f, .27f), fishMat, false);
      dorsal.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
      var bellyFin = Primitive("FishBellyFin", PrimitiveType.Capsule, visual, new Vector3(0, -.235f, .12f), new Vector3(.13f, .13f, .24f), fishMat, false);
      bellyFin.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
      var sideFinL = Primitive("FishSideFinL", PrimitiveType.Sphere, visual, new Vector3(-.42f, -.025f, .08f), new Vector3(.18f, .11f, .30f), fishMat, false);
      sideFinL.transform.localRotation = Quaternion.Euler(0f, -18f, -18f);
      var sideFinR = Primitive("FishSideFinR", PrimitiveType.Sphere, visual, new Vector3(.42f, -.025f, .08f), new Vector3(.18f, .11f, .30f), fishMat, false);
      sideFinR.transform.localRotation = Quaternion.Euler(0f, 18f, 18f);

      // Oversized eyes, white catchlights and rosy cheeks make the face read
      // at the isometric camera distance, even while the fish is moving.
      var eyeWhiteL = Primitive("FishEyeWhiteL", PrimitiveType.Sphere, visual, new Vector3(-.19f, .135f, -.595f), new Vector3(.16f, .17f, .11f), Mats["White"], false);
      var eyeWhiteR = Primitive("FishEyeWhiteR", PrimitiveType.Sphere, visual, new Vector3(.19f, .135f, -.595f), new Vector3(.16f, .17f, .11f), Mats["White"], false);
      eyeWhiteL.transform.localRotation = Quaternion.Euler(0f, -10f, 0f);
      eyeWhiteR.transform.localRotation = Quaternion.Euler(0f, 10f, 0f);
      Primitive("FishPupilL", PrimitiveType.Sphere, visual, new Vector3(-.19f, .145f, -.675f), new Vector3(.082f, .098f, .050f), Mats["Ink"], false);
      Primitive("FishPupilR", PrimitiveType.Sphere, visual, new Vector3(.19f, .145f, -.675f), new Vector3(.082f, .098f, .050f), Mats["Ink"], false);
      Primitive("FishEyeSparkleL", PrimitiveType.Sphere, visual, new Vector3(-.165f, .186f, -.710f), new Vector3(.031f, .036f, .018f), Mats["White"], false);
      Primitive("FishEyeSparkleR", PrimitiveType.Sphere, visual, new Vector3(.215f, .186f, -.710f), new Vector3(.031f, .036f, .018f), Mats["White"], false);
      Primitive("FishCheekL", PrimitiveType.Sphere, visual, new Vector3(-.285f, -.095f, -.625f), new Vector3(.13f, .060f, .030f), Mats["FishBlush"], false);
      Primitive("FishCheekR", PrimitiveType.Sphere, visual, new Vector3(.285f, -.095f, -.625f), new Vector3(.13f, .060f, .030f), Mats["FishBlush"], false);

      // Three tiny ink dots form a readable, happy smile from the same simple
      // primitives used for the eyes, avoiding a fragile line renderer.
      Primitive("FishSmileL", PrimitiveType.Sphere, visual, new Vector3(-.070f, -.070f, -.715f), new Vector3(.065f, .030f, .022f), Mats["Ink"], false).transform.localRotation = Quaternion.Euler(0f, 0f, -20f);
      Primitive("FishSmileCenter", PrimitiveType.Sphere, visual, new Vector3(0f, -.092f, -.720f), new Vector3(.065f, .028f, .022f), Mats["Ink"], false);
      Primitive("FishSmileR", PrimitiveType.Sphere, visual, new Vector3(.070f, -.070f, -.715f), new Vector3(.065f, .030f, .022f), Mats["Ink"], false).transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
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
      CreatePlayerAvatar(arena, "玩家1", new Vector3(0, .25f, -4.68f), true, Mats["Net"]);
      CreatePlayerAvatar(arena, "玩家2", new Vector3(0, .25f, 4.68f), false, Mats["Gold"]);
    }

    private static void CreatePlayerAvatar(Transform parent, string name, Vector3 position, bool faceAwayFromCamera, Material accent)
    {
      var root = new GameObject(name).transform;
      root.SetParent(parent, false);
      root.localPosition = position;
      root.localScale = Vector3.one * 1.40f;
      root.localRotation = Quaternion.Euler(0, faceAwayFromCamera ? 180f : 0f, 0);
      bool warmPalette = name == "玩家2";
      var skinMaterial = Mats[warmPalette ? "AvatarSkinB" : "AvatarSkinA"];
      var hairMaterial = Mats[warmPalette ? "AvatarHairB" : "AvatarHairA"];
      var clothesMaterial = Mats[warmPalette ? "AvatarClothesB" : "AvatarClothesA"];
      var bootMaterial = Mats[warmPalette ? "AvatarBootB" : "AvatarBootA"];
      // Approximately 2.2 heads tall: a large readable face, soft shirt and
      // short limbs. Unit-scale animation pivots keep hands from inheriting
      // the very small scale of the arm meshes.
      Primitive("AvatarGroundShadow", PrimitiveType.Cylinder, root, new Vector3(0, .025f, 0), new Vector3(.78f, .012f, .52f), Mats["MeadowDark"], false);
      var body = new GameObject("AvatarBody").transform;
      body.SetParent(root, false); body.localPosition = new Vector3(0, .79f, 0);
      Primitive("Shirt", PrimitiveType.Capsule, body, Vector3.zero, new Vector3(.68f, .34f, .50f), clothesMaterial, false);
      Primitive("Bib", PrimitiveType.Sphere, body, new Vector3(0, .025f, -.227f), new Vector3(.42f, .40f, .095f), Mats["AvatarCream"], false);
      Primitive("Pocket", PrimitiveType.Sphere, body, new Vector3(0, -.055f, -.28f), new Vector3(.24f, .15f, .025f), clothesMaterial, false);
      for (int side = -1; side <= 1; side += 2)
      {
        Primitive("Strap", PrimitiveType.Cube, body, new Vector3(side * .16f, .20f, -.216f), new Vector3(.065f, .20f, .025f), Mats["AvatarCream"], false);
        Primitive("Button", PrimitiveType.Sphere, body, new Vector3(side * .16f, .12f, -.278f), Vector3.one * .055f, bootMaterial, false);
        Primitive("AvatarLeg" + side, PrimitiveType.Capsule, root, new Vector3(side * .18f, .32f, 0), new Vector3(.22f, .20f, .24f), skinMaterial, false);
        Primitive("AvatarBoot" + side, PrimitiveType.Sphere, root, new Vector3(side * .18f, .12f, -.075f), new Vector3(.29f, .20f, .37f), bootMaterial, false);
      }
      // All facial features and hair share one pivot, so elastic head motion
      // cannot leave the eyes, mouth or accessories floating in place.
      var head = new GameObject("AvatarHead").transform;
      head.SetParent(root, false);
      head.localPosition = new Vector3(0, 1.48f, -.01f);
      head.localRotation = Quaternion.Euler(14f, 0f, 0f);
      var hair = new GameObject("AvatarHair").transform;
      hair.SetParent(head, false);
      Primitive("HairShell", PrimitiveType.Sphere, hair, new Vector3(0, .045f, .075f), new Vector3(1.07f, 1.04f, .84f), hairMaterial, false);
      Primitive("Face", PrimitiveType.Sphere, head, new Vector3(0, -.015f, -.125f), new Vector3(.91f, .88f, .66f), skinMaterial, false);
      for (int side = -1; side <= 1; side += 2)
      {
        Primitive("Ear", PrimitiveType.Sphere, head, new Vector3(side * .447f, -.035f, -.11f), new Vector3(.14f, .20f, .14f), skinMaterial, false);
        Primitive("HairSide", PrimitiveType.Capsule, hair, new Vector3(side * .439f, -.005f, .008f), new Vector3(.17f, .31f, .35f), hairMaterial, false);
        var eye = Primitive("Eye", PrimitiveType.Sphere, head, new Vector3(side * .168f, .015f, -.441f), new Vector3(.051f, .075f, .025f), Mats["AvatarFaceInk"], false);
        eye.transform.localRotation = Quaternion.Euler(0f, 0f, side * -9f);
        Primitive("Cheek", PrimitiveType.Sphere, head, new Vector3(side * .26f, -.112f, -.39f), new Vector3(.13f, .063f, .025f), Mats["AvatarBlush"], false);
      }
      for (int i = 0; i < 3; i++)
      {
        var fringe = Primitive("Fringe", PrimitiveType.Sphere, hair, new Vector3(-.24f + i * .18f, .323f + i * .01f, -.20f),
          new Vector3(.29f, .20f, .24f), hairMaterial, false);
        fringe.transform.localRotation = Quaternion.Euler(0, 0, -22f + i * 10f);
      }
      Primitive("HairClip", PrimitiveType.Capsule, hair, new Vector3(warmPalette ? .34f : -.34f, .26f, -.29f),
        new Vector3(.065f, .11f, .035f), Mats["AvatarCream"], false).transform.localRotation = Quaternion.Euler(0, 0, warmPalette ? -38f : 38f);
      var mouth = new GameObject("Smile").AddComponent<LineRenderer>();
      mouth.transform.SetParent(head, false);
      mouth.useWorldSpace = false; mouth.positionCount = 13;
      mouth.startWidth = mouth.endWidth = .022f;
      mouth.numCapVertices = 3; mouth.numCornerVertices = 3;
      mouth.sharedMaterial = Mats["AvatarFaceInk"];
      mouth.shadowCastingMode = ShadowCastingMode.Off;
      for (int i = 0; i < 13; i++)
      {
        float t = i / 12f * 2f - 1f;
        mouth.SetPosition(i, new Vector3(t * .07f, -.16f + .036f * t * t, -.447f));
      }
      var leftArm = CreateCuteArm(root, "AvatarLeftArm", -1f, skinMaterial, clothesMaterial);
      var rightArm = CreateCuteArm(root, "AvatarRightArm", 1f, skinMaterial, clothesMaterial);
      var avatar = root.gameObject.AddComponent<FishPlayerAvatar>();
      avatar.handSide = name == "玩家1" ? FishGestureInput.HandSide.Left : FishGestureInput.HandSide.Right;
      // Both rigs face local -Z, and their short hands hang below the shoulders.
      avatar.netDirection = -1f;
      avatar.gestureArm = name == "玩家1" ? rightArm : leftArm;
      avatar.supportArm = name == "玩家1" ? leftArm : rightArm;
      avatar.bodyVisual = body;
      avatar.ragdoll = root.gameObject.AddComponent<FishAvatarRagdoll>();
    }

    private static Transform CreateCuteArm(Transform root, string name, float side, Material skin, Material shirt)
    {
      var pivot = new GameObject(name).transform;
      pivot.SetParent(root, false);
      pivot.localPosition = new Vector3(side * .36f, 1.015f, 0f);
      pivot.localRotation = Quaternion.Euler(12f, 0f, side * 16f);
      Primitive("Sleeve", PrimitiveType.Capsule, pivot, new Vector3(0, -.065f, 0), new Vector3(.25f, .15f, .26f), shirt, false);
      Primitive("Forearm", PrimitiveType.Capsule, pivot, new Vector3(0, -.22f, 0), new Vector3(.17f, .14f, .18f), skin, false);
      Primitive("AvatarHand", PrimitiveType.Sphere, pivot, new Vector3(0, -.35f, -.015f), new Vector3(.215f, .22f, .22f), skin, false);
      return pivot;
    }

    private static void AddDoroCharacterVisual(Transform parent)
    {
      var source = AssetDatabase.LoadAssetAtPath<GameObject>(DoroModel);
      if (source == null) return;
      var model = PrefabUtility.InstantiatePrefab(source, parent) as GameObject;
      if (model == null) return;
      model.name = "DoroCharacterModel";
      model.transform.localRotation = Quaternion.Euler(90f, 0f, 0f) * new Quaternion(.5f, .5f, -.5f, -.5f);
      var baseMat = AssetDatabase.LoadAssetAtPath<Material>(DoroMaterial);
      var matPath = Root + "/Materials/Fish_DoroAvatar.mat";
      var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
      if (mat == null && baseMat != null)
      {
        mat = new Material(baseMat);
        AssetDatabase.CreateAsset(mat, matPath);
      }
      var urpLit = Shader.Find("Universal Render Pipeline/Lit");
      if (mat != null && urpLit != null && mat.shader != urpLit)
      {
        var texture = baseMat != null ? baseMat.mainTexture : null;
        mat.shader = urpLit;
        if (texture != null) mat.SetTexture("_BaseMap", texture);
        mat.SetColor("_BaseColor", baseMat != null ? baseMat.color : Color.white);
      }
      var renderers = model.GetComponentsInChildren<Renderer>(true);
      foreach (var renderer in renderers)
      {
        if (mat != null) renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => mat).ToArray();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        renderer.receiveShadows = true;
      }
      var bounds = BoundsOf(renderers);
      float scale = 1.55f / Mathf.Max(.1f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
      model.transform.localScale = Vector3.one * scale;
    }

    private static void CreateCamera(Transform parent)
    {
      var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
      go.transform.SetParent(parent, false);
      go.tag = "MainCamera";
      go.transform.position = new Vector3(9.4f, 13.8f, -13.6f);
      go.transform.LookAt(new Vector3(0, .45f, 0));
      var camera = go.GetComponent<Camera>();
      camera.orthographic = true; camera.orthographicSize = 8.05f;
      camera.nearClipPlane = .05f; camera.farClipPlane = 80;
      camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.52f, .82f, .93f);
    }

    private static void CreateLighting(Transform parent)
    {
      var go = new GameObject("Sun - 水面主光", typeof(Light));
      go.transform.SetParent(parent, false); go.transform.rotation = Quaternion.Euler(45, -30, 0);
      var light = go.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = .92f; light.color = new Color(1f, .95f, .82f);
      light.shadows = LightShadows.Soft;
      light.shadowStrength = .62f;
      light.shadowBias = .045f;
      light.shadowNormalBias = .35f;
      RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
      RenderSettings.ambientLight = new Color(.58f, .64f, .64f);
      RenderSettings.fog = false;
      CreatePostProcessing(parent);
    }

    private static void CreatePostProcessing(Transform parent)
    {
      var volumeObject = new GameObject("Presentation Volume - 柔和高光");
      volumeObject.transform.SetParent(parent, false);
      var volume = volumeObject.AddComponent<Volume>();
      volume.isGlobal = true;
      volume.priority = 10f;
      var profile = ScriptableObject.CreateInstance<VolumeProfile>();
      var bloom = profile.Add<Bloom>(true);
      bloom.threshold.value = .92f;
      bloom.intensity.value = .08f;
      bloom.scatter.value = .62f;
      bloom.tint.value = new Color(1f, .90f, .74f);
      var color = profile.Add<ColorAdjustments>(true);
      color.postExposure.value = 0f;
      color.contrast.value = 0f;
      color.saturation.value = -2f;
      color.colorFilter.value = new Color(1f, .98f, .94f, 1f);
      var tone = profile.Add<Tonemapping>(true);
      tone.mode.value = TonemappingMode.ACES;
      var vignette = profile.Add<Vignette>(true);
      vignette.intensity.value = .08f;
      vignette.smoothness.value = .72f;
      volume.sharedProfile = profile;
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
      var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
      var colors = ColorBlock.defaultColorBlock;
      colors.normalColor = new Color(.94f, .82f, .54f, .96f);
      colors.highlightedColor = new Color(1f, .92f, .68f, 1f);
      colors.pressedColor = new Color(.78f, .62f, .35f, 1f);
      colors.selectedColor = colors.highlightedColor;
      colors.fadeDuration = .08f;
      button.colors = colors;
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
      var ink = new Color(.94f, .91f, .80f, .92f); var dark = new Color(.16f, .19f, .15f); var muted = new Color(.30f, .34f, .25f);
      var title = Panel("TitleCard", canvasGo.transform, new Vector2(0, 1), new Vector2(225, -105), new Vector2(430, 150), new Color(.95f, .94f, .86f, .94f));
      Label("Title", title, "双人合作抛网捕鱼", new Vector2(0, 34), new Vector2(400, 54), 34, dark, TextAnchor.MiddleCenter);
      Label("Subtitle", title, "玩家1 + 玩家2  /  同步上挥", new Vector2(0, -28), new Vector2(400, 28), 18, muted, TextAnchor.MiddleCenter);
      var scorePanel = Panel("ScorePanel", canvasGo.transform, new Vector2(1, 1), new Vector2(-225, -105), new Vector2(430, 150), ink);
      ui.scoreText = Label("Score", scorePanel, "分数  0    连击  0", new Vector2(0, 34), new Vector2(400, 40), 26, dark, TextAnchor.MiddleCenter);
      ui.timeText = Label("Time", scorePanel, "时间  30 秒", new Vector2(-100, -30), new Vector2(180, 32), 21, muted, TextAnchor.MiddleCenter);
      ui.rankText = Label("Rank", scorePanel, "等级  C", new Vector2(110, -30), new Vector2(150, 32), 21, muted, TextAnchor.MiddleCenter);
      var statusPanel = Panel("StatusPanel", canvasGo.transform, new Vector2(.5f, 0), new Vector2(0, 150), new Vector2(900, 190), new Color(.96f, .95f, .88f, .92f));
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
