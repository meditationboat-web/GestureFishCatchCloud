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
      var left = FindNamed("LeftGrassBank");
      SetTransform(left, new Vector3(-4.8f, .25f, 0), new Vector3(.8f, .5f, 8));
      var right = FindNamed("RightGrassBank");
      SetTransform(right, new Vector3(4.8f, .25f, 0), new Vector3(.8f, .5f, 8));
      SetTransform(FindNamed("LeftWaterEdge"), new Vector3(-4.3f, .18f, 0), new Vector3(.18f, .18f, 8));
      SetTransform(FindNamed("RightWaterEdge"), new Vector3(4.3f, .18f, 0), new Vector3(.18f, .18f, 8));
      SetTransform(FindNamed("StartBank", "NearBank"), new Vector3(0, .28f, -4.3f), new Vector3(10, .55f, 1.0f));
      SetTransform(FindNamed("EndBank", "FarBank"), new Vector3(0, .28f, 4.3f), new Vector3(10, .55f, 1.0f));
      for (int i = 0; i < 10; i++)
        SetTransform(FindNamed("WaterLine_" + i), new Vector3(0, .14f, -3.6f + i * .8f), new Vector3(8.1f, .018f, .025f));
    }

    private static void ApplyNet()
    {
      var net = FindObjectOfType<FishNetController>();
      if (net == null) return;
      net.transform.localPosition = new Vector3(0, .65f, 0);
      var trigger = net.GetComponent<BoxCollider>();
      if (trigger != null) { trigger.center = new Vector3(0, .45f, 0); trigger.size = new Vector3(.75f, 2.2f, 8.0f); }
      SetTransform(FindChild(net.transform, "NetPanel_Vertical", "NetPanel"), new Vector3(0, .70f, 0), new Vector3(.10f, 2.0f, 7.7f));
      SetTransform(FindChild(net.transform, "NetFrontPost", "NetLeftPost"), new Vector3(0, .70f, -3.9f), new Vector3(.10f, 1.1f, .10f));
      SetTransform(FindChild(net.transform, "NetBackPost", "NetRightPost"), new Vector3(0, .70f, 3.9f), new Vector3(.10f, 1.1f, .10f));
      SetTransform(FindChild(net.transform, "NetTop_Vertical", "NetTop"), new Vector3(0, 1.75f, 0), new Vector3(.14f, .10f, 8.0f));
    }

    private static void ApplyCamera()
    {
      var camera = FindNamed("Main Camera");
      if (camera == null) return;
      camera.position = new Vector3(0, 12.8f, -12.8f);
      camera.LookAt(new Vector3(0, .45f, 0));
      var component = camera.GetComponent<Camera>();
      if (component != null) { component.orthographic = true; component.orthographicSize = 7.6f; }
    }

    private static void ApplyWorldGuide()
    {
      var board = FindNamed("WorldSpaceGuide - 3D提示牌");
      if (board == null) return;
      board.localPosition = new Vector3(0, 1.0f, 3.65f);
      board.localScale = Vector3.one * 1.35f;
      var label = board.GetComponentInChildren<TextMesh>(true);
      if (label != null)
      {
        label.text = "双手合作\n抛网捕鱼";
        label.fontSize = 76;
        label.characterSize = .045f;
      }
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
      if (FindNamed("左侧玩家小人") != null && FindNamed("右侧玩家小人") != null) return;
      var arena = FindNamed("Arena - 水道与草岸");
      if (arena == null) return;
      CreateAvatar(arena, "左侧玩家小人", new Vector3(-3.9f, .25f, 0), new Color(.12f, .8f, 1f), true);
      CreateAvatar(arena, "右侧玩家小人", new Vector3(3.9f, .25f, 0), new Color(1f, .7f, .1f), false);
    }

    private static void CreateAvatar(Transform parent, string name, Vector3 position, Color accent, bool faceRight)
    {
      var root = new GameObject(name).transform;
      root.SetParent(parent, false); root.localPosition = position;
      root.localRotation = Quaternion.Euler(0, faceRight ? 90f : -90f, 0);
      var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
      if (shader == null) return;
      var material = new Material(shader); material.color = accent; material.name = name + " Accent";
      var bodyMaterial = new Material(shader); bodyMaterial.color = new Color(.96f, .96f, .93f); bodyMaterial.name = name + " Body";
      CreatePrimitive("AvatarBody", PrimitiveType.Capsule, root, new Vector3(0, .9f, 0), new Vector3(.58f, .82f, .58f), material);
      CreatePrimitive("AvatarHead", PrimitiveType.Sphere, root, new Vector3(0, 1.9f, 0), new Vector3(.62f, .62f, .62f), bodyMaterial);
      CreatePrimitive("AvatarArmA", PrimitiveType.Capsule, root, new Vector3(.48f, 1.25f, -.28f), new Vector3(.18f, .68f, .18f), bodyMaterial).transform.localRotation = Quaternion.Euler(0, 0, -48f);
      CreatePrimitive("AvatarArmB", PrimitiveType.Capsule, root, new Vector3(.48f, 1.25f, .28f), new Vector3(.18f, .68f, .18f), bodyMaterial).transform.localRotation = Quaternion.Euler(0, 0, -48f);
      CreatePrimitive("AvatarHandA", PrimitiveType.Sphere, root, new Vector3(.95f, 1.55f, -.28f), new Vector3(.24f, .24f, .24f), material);
      CreatePrimitive("AvatarHandB", PrimitiveType.Sphere, root, new Vector3(.95f, 1.55f, .28f), new Vector3(.24f, .24f, .24f), material);
      CreatePrimitive("AvatarLegA", PrimitiveType.Capsule, root, new Vector3(0, .35f, -.2f), new Vector3(.20f, .45f, .20f), bodyMaterial);
      CreatePrimitive("AvatarLegB", PrimitiveType.Capsule, root, new Vector3(0, .35f, .2f), new Vector3(.20f, .45f, .20f), bodyMaterial);
      CreatePrimitive("AvatarBase", PrimitiveType.Cylinder, root, new Vector3(0, .08f, 0), new Vector3(.85f, .10f, .85f), bodyMaterial);
      var label = new GameObject("AvatarLabel", typeof(TextMesh));
      label.transform.SetParent(root, false); label.transform.localPosition = new Vector3(0, 2.15f, 0); label.transform.localRotation = Quaternion.Euler(58f, 0, 0);
      var text = label.GetComponent<TextMesh>(); text.text = name.Replace("小人", ""); text.characterSize = .12f; text.fontSize = 36; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.color = new Color(.1f, .15f, .2f);
      label.AddComponent<FishWorldLabel>();
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
