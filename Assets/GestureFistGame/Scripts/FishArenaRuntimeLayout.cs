using UnityEngine;

namespace GestureFistGame
{
  /// <summary>
  /// Applies the right-to-left water layout to older saved scenes as they load.
  /// This keeps cloud builds using an already serialized scene in sync with the
  /// editor scene builder without requiring a manual scene rebuild first.
  /// </summary>
  public sealed class FishArenaRuntimeLayout : MonoBehaviour
  {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForFishScene()
    {
      if (FindObjectOfType<FishGameController>() == null || FindObjectOfType<FishNetController>() == null) return;
      var go = new GameObject("FishArenaRuntimeLayout - right to left");
      go.AddComponent<FishArenaRuntimeLayout>();
    }

    private void Awake()
    {
      ApplyArena();
      ApplyNet();
      ApplyCamera();
      ApplyWorldGuide();
      ApplySpawner();
      ApplyInputLinks();
      CreateAvatarsIfMissing();
    }

    private static void ApplyArena()
    {
      var water = FindNamed("WaterChannel_RightToLeft", "WaterChannel");
      SetTransform(water, new Vector3(0, 0, 0), new Vector3(8.4f, .25f, 8));
      ApplyWaterSurface(water);
      var arena = FindNamed("Arena - 水道与草岸");
      StylizedIslandRuntimeBuilder.Ensure(arena);
      EnsureOpenMeadow(arena);
      // Newly rebuilt scenes own their island dressing. Keep the legacy
      // runtime enhancer only for older scenes that have no authored island.
      if (arena != null && arena.Find("StylizedIslandEnvironment") == null)
        RiverEnvironmentEnhancer.Ensure(arena);
      var left = FindNamed("LeftGrassBank");
      SetTransform(left, new Vector3(-4.8f, .25f, 0), new Vector3(.8f, .5f, 8));
      var right = FindNamed("RightGrassBank");
      SetTransform(right, new Vector3(4.8f, .25f, 0), new Vector3(.8f, .5f, 8));
      SetTransform(FindNamed("LeftWaterEdge"), new Vector3(-4.3f, .18f, 0), new Vector3(.18f, .18f, 8));
      SetTransform(FindNamed("RightWaterEdge"), new Vector3(4.3f, .18f, 0), new Vector3(.18f, .18f, 8));
      SetTransform(FindNamed("StartBank", "NearBank"), new Vector3(0, .28f, -4.3f), new Vector3(10, .55f, 1.0f));
      SetTransform(FindNamed("EndBank", "FarBank"), new Vector3(0, .28f, 4.3f), new Vector3(10, .55f, 1.0f));
      for (int i = 0; i < 10; i++)
      {
        var line = FindNamed("WaterLine_" + i);
        if (line != null) line.gameObject.SetActive(false);
      }
    }

    private static void EnsureOpenMeadow(Transform arena)
    {
      if (arena == null || arena.Find("StylizedIslandEnvironment") != null || arena.Find("MeadowGround") != null || arena.Find("GeneratedMeadow") != null) return;
      var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
      if (shader == null) return;
      var meadow = new Material(shader) { name = "Runtime Open Meadow" };
      meadow.color = new Color(.18f, .42f, .16f, 1f);
      if (meadow.HasProperty("_Smoothness")) meadow.SetFloat("_Smoothness", .08f);
      CreatePrimitive("MeadowGround", PrimitiveType.Cube, arena, new Vector3(0, -.12f, 0), new Vector3(32f, .24f, 26f), meadow);
      var light = new Material(shader) { name = "Runtime Meadow Light" };
      light.color = new Color(.28f, .56f, .20f, 1f);
      var dark = new Material(shader) { name = "Runtime Meadow Dark" };
      dark.color = new Color(.12f, .30f, .13f, 1f);
      var positions = new[]
      {
        new Vector3(-10.8f, .025f, -7.4f), new Vector3(10.6f, .025f, -7.8f),
        new Vector3(-11.8f, .025f, 6.8f), new Vector3(11.7f, .025f, 7.2f),
        new Vector3(-7.4f, .025f, -10.2f), new Vector3(7.8f, .025f, 10.0f),
        new Vector3(-13.4f, .025f, .4f), new Vector3(13.1f, .025f, -.8f)
      };
      for (int i = 0; i < positions.Length; i++)
      {
        var patch = CreatePrimitive("MeadowPatch_" + i, PrimitiveType.Sphere, arena, positions[i],
          new Vector3(3.8f + (i % 3) * .7f, .045f, 2.2f + (i % 2) * .55f), i % 3 == 0 ? dark : light);
        patch.transform.localRotation = Quaternion.Euler(0, i * 23f, 0);
      }
    }

    private static void ApplyNet()
    {
      var net = FindObjectOfType<FishNetController>();
      if (net == null) return;
      net.transform.localPosition = Vector3.zero;
      net.transform.localRotation = Quaternion.identity;
      net.transform.localScale = Vector3.one;
      net.name = "Net - 水平抬网";
      var trigger = net.GetComponent<BoxCollider>();
      if (trigger != null) { trigger.center = FishNetGeometry.TriggerCenter; trigger.size = FishNetGeometry.TriggerSize; }
      var panel = FindChild(net.transform, "RopeNetVisual", "NetPanel_Vertical", "NetPanel");
      var marker = FindChild(net.transform, "NetSubmergedMarker");
      // Remove the legacy second net even when an older serialized scene is
      // loaded.  The active rope grid is the only net shown in this scene.
      if (marker != null)
      {
        marker.gameObject.SetActive(false);
        Object.Destroy(marker.gameObject);
        marker = null;
      }
      // Migrate old wall-shaped nets to the same geometry used by the editor.
      if (panel == null || panel.Find("RopeNet_X_0") == null)
      {
        var source = panel != null ? panel.GetComponentInChildren<Renderer>(true) : null;
        var rope = source != null ? source.sharedMaterial : null;
        if (rope == null)
        {
          var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
          rope = new Material(shader) { color = new Color(.1f, .82f, .95f) };
        }
        var edge = new Material(rope) { color = new Color(1f, .7f, .08f) };
        if (panel != null) { panel.gameObject.SetActive(false); Destroy(panel.gameObject); }
        panel = FishNetGeometry.Create(net.transform, "RopeNetVisual", rope, edge, false);
      }
      foreach (var oldName in new[] { "NetFrontPost", "NetBackPost", "NetLeftPost", "NetRightPost", "NetTop_Vertical", "NetTop" })
      {
        var old = net.transform.Find(oldName);
        if (old != null) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
      }
      SetTransform(panel, Vector3.zero, Vector3.one);
      panel.localRotation = Quaternion.identity;
      net.visual = panel;
      net.SetSubmergedVisual(null);
      net.ConfigureLayout(FishNetGeometry.RestHeight, FishNetGeometry.RaisedHeight);
    }

    private static void ApplyWaterSurface(Transform water)
    {
      if (water == null) return;
      var surface = water.GetComponent<InteractiveWaterSurface>();
      if (surface == null) surface = water.gameObject.AddComponent<InteractiveWaterSurface>();
      surface.width = 8.4f;
      surface.depth = 8f;
      surface.columns = 56;
      surface.rows = 40;
      surface.circular = false;
      surface.enableRenderTextureRipples = true;
      surface.rippleMapResolution = 128;
      surface.rippleLifetime = 1.35f;
      // Keep the previously accepted water look. The E package remains in the
      // project for reference, but this game scene uses its original custom
      // InteractiveWater shader after the material comparison.
      surface.waterMaterialOverride = null;
      surface.waterShader = Shader.Find("GestureFistGame/InteractiveWater");
      var ripple = water.GetComponent<WaterRippleRenderTexture>();
      if (ripple == null) ripple = water.gameObject.AddComponent<WaterRippleRenderTexture>();
      ripple.resolution = surface.rippleMapResolution;
      ripple.lifetime = surface.rippleLifetime;
    }

    private static void ApplyCamera()
    {
      var camera = FindNamed("Main Camera");
      if (camera == null) return;
      camera.position = new Vector3(9.4f, 13.8f, -13.6f);
      camera.LookAt(new Vector3(0, .45f, 0));
      var component = camera.GetComponent<Camera>();
      if (component != null) { component.orthographic = true; component.orthographicSize = 8.05f; }
    }

    private static void ApplyWorldGuide()
    {
      var board = FindNamed("WorldSpaceGuide - 3D提示牌");
      if (board == null) return;
      // The old blue gate looked like a fence and obscured the river. The
      // instruction is now shown in the readable status bar instead.
      board.gameObject.SetActive(false);
    }

    private static void ApplySpawner()
    {
      var spawner = FindObjectOfType<FishSpawner>();
      if (spawner == null) return;
      spawner.spawnX = 4.1f;
      spawner.laneHalfWidth = 3.2f;
    }

    private static void ApplyInputLinks()
    {
      var input = FindObjectOfType<FishGestureInput>();
      var mouse = FindObjectOfType<FishMouseInput>();
      if (input != null && mouse != null) { input.mouseInput = mouse; mouse.gestureInput = input; }
    }

    private static void CreateAvatarsIfMissing()
    {
      var arena = FindNamed("Arena - 水道与草岸");
      if (arena == null) return;
      var lower = FindNamed("玩家1", "左侧玩家小人", "下方玩家小人");
      var upper = FindNamed("玩家2", "右侧玩家小人", "上方玩家小人");
      if (lower == null) lower = CreateAvatar(arena, "玩家1", new Vector3(0, .25f, -4.68f), new Color(.12f, .8f, 1f), true);
      if (upper == null) upper = CreateAvatar(arena, "玩家2", new Vector3(0, .25f, 4.68f), new Color(1f, .7f, .1f), false);
      ConfigureAvatar(lower, "玩家1", new Vector3(0, .25f, -4.68f), true);
      ConfigureAvatar(upper, "玩家2", new Vector3(0, .25f, 4.68f), false);
    }

    private static Transform CreateAvatar(Transform parent, string name, Vector3 position, Color accent, bool faceAwayFromCamera)
    {
      var root = new GameObject(name).transform;
      root.SetParent(parent, false); root.localPosition = position;
      root.localRotation = Quaternion.Euler(0, faceAwayFromCamera ? 180f : 0f, 0);
      var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
      if (shader == null) return null;
      var material = new Material(shader); material.color = accent; material.name = name + " Accent";
      var bodyMaterial = new Material(shader); bodyMaterial.color = new Color(.96f, .96f, .93f); bodyMaterial.name = name + " Body";
      CreatePrimitive("AvatarBody", PrimitiveType.Capsule, root, new Vector3(0, .9f, 0), new Vector3(.58f, .82f, .58f), material);
      CreatePrimitive("AvatarHead", PrimitiveType.Sphere, root, new Vector3(0, 1.9f, 0), new Vector3(.62f, .62f, .62f), bodyMaterial);
      var armA = CreatePrimitive("AvatarArmA", PrimitiveType.Capsule, root, new Vector3(-.28f, 1.24f, 0), new Vector3(.18f, .68f, .18f), bodyMaterial);
      var armB = CreatePrimitive("AvatarArmB", PrimitiveType.Capsule, root, new Vector3(.28f, 1.24f, 0), new Vector3(.18f, .68f, .18f), bodyMaterial);
      armA.transform.localRotation = Quaternion.Euler(position.z > 0 ? 24f : -24f, 0, 0);
      armB.transform.localRotation = Quaternion.Euler(position.z > 0 ? 24f : -24f, 0, 0);
      var handA = CreatePrimitive("AvatarHandA", PrimitiveType.Sphere, root, Vector3.zero, new Vector3(.24f, .24f, .24f), material);
      handA.transform.SetParent(armA.transform, false); handA.transform.localPosition = new Vector3(0, .72f, 0);
      var handB = CreatePrimitive("AvatarHandB", PrimitiveType.Sphere, root, Vector3.zero, new Vector3(.24f, .24f, .24f), material);
      handB.transform.SetParent(armB.transform, false); handB.transform.localPosition = new Vector3(0, .72f, 0);
      CreatePrimitive("AvatarLegA", PrimitiveType.Capsule, root, new Vector3(0, .35f, -.2f), new Vector3(.20f, .45f, .20f), bodyMaterial);
      CreatePrimitive("AvatarLegB", PrimitiveType.Capsule, root, new Vector3(0, .35f, .2f), new Vector3(.20f, .45f, .20f), bodyMaterial);
      CreatePrimitive("AvatarBase", PrimitiveType.Cylinder, root, new Vector3(0, .08f, 0), new Vector3(.85f, .10f, .85f), bodyMaterial);
      var avatar = root.gameObject.AddComponent<FishPlayerAvatar>();
      avatar.handSide = name == "玩家1" ? FishGestureInput.HandSide.Left : FishGestureInput.HandSide.Right;
      avatar.netDirection = position.z < 0 ? 1f : -1f;
      avatar.gestureArm = name == "玩家1" ? armB.transform : armA.transform;
      avatar.supportArm = name == "玩家1" ? armA.transform : armB.transform;
      avatar.bodyVisual = root.Find("AvatarBody");
      avatar.ragdoll = root.gameObject.AddComponent<FishAvatarRagdoll>();
      return root;
    }

    private static void ConfigureAvatar(Transform avatar, string displayName, Vector3 position, bool faceAwayFromCamera)
    {
      if (avatar == null) return;
      avatar.name = displayName;
      avatar.localPosition = position;
      avatar.localRotation = Quaternion.Euler(0, faceAwayFromCamera ? 180f : 0f, 0);
      var label = avatar.Find("AvatarLabel");
      if (label != null) Destroy(label.gameObject);
    }

    private static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
      var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
      var renderer = go.GetComponent<Renderer>(); if (renderer != null) renderer.sharedMaterial = material;
      var collider = go.GetComponent<Collider>(); if (collider != null) Destroy(collider);
      return go;
    }

    private static Transform FindNamed(params string[] names)
    {
      var all = FindObjectsOfType<Transform>(true);
      foreach (var name in names) foreach (var item in all) if (item.name == name) return item;
      return null;
    }

    private static Transform FindChild(Transform root, params string[] names)
    {
      if (root == null) return null;
      foreach (var name in names)
      {
        var child = root.Find(name);
        if (child != null) return child;
      }
      return null;
    }

    private static void SetTransform(Transform target, Vector3 position, Vector3 scale)
    {
      if (target == null) return;
      target.localPosition = position; target.localScale = scale;
    }
  }
}
