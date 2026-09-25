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
      if (trigger != null) { trigger.center = new Vector3(0, .45f, 0); trigger.size = new Vector3(.75f, 2.2f, 3.8f); }
      SetTransform(FindChild(net.transform, "NetPanel_Vertical", "NetPanel"), new Vector3(0, .70f, 0), new Vector3(.10f, 2.0f, 3.5f));
      SetTransform(FindChild(net.transform, "NetFrontPost", "NetLeftPost"), new Vector3(0, .70f, -1.8f), new Vector3(.10f, 1.1f, .10f));
      SetTransform(FindChild(net.transform, "NetBackPost", "NetRightPost"), new Vector3(0, .70f, 1.8f), new Vector3(.10f, 1.1f, .10f));
      SetTransform(FindChild(net.transform, "NetTop_Vertical", "NetTop"), new Vector3(0, 1.75f, 0), new Vector3(.14f, .10f, 3.7f));
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
      CreateAvatar(arena, "左侧玩家小人", new Vector3(-4.25f, .42f, 0), new Color(.12f, .8f, 1f), true);
      CreateAvatar(arena, "右侧玩家小人", new Vector3(4.25f, .42f, 0), new Color(1f, .7f, .1f), false);
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
      CreatePrimitive("AvatarBody", PrimitiveType.Capsule, root, new Vector3(0, .65f, 0), new Vector3(.45f, .7f, .45f), bodyMaterial);
      CreatePrimitive("AvatarHead", PrimitiveType.Sphere, root, new Vector3(0, 1.55f, 0), new Vector3(.55f, .55f, .55f), material);
      CreatePrimitive("AvatarArmA", PrimitiveType.Capsule, root, new Vector3(.38f, 1f, -.22f), new Vector3(.14f, .55f, .14f), material).transform.localRotation = Quaternion.Euler(0, 0, -42f);
      CreatePrimitive("AvatarArmB", PrimitiveType.Capsule, root, new Vector3(.38f, 1f, .22f), new Vector3(.14f, .55f, .14f), material).transform.localRotation = Quaternion.Euler(0, 0, -42f);
      CreatePrimitive("AvatarBase", PrimitiveType.Cylinder, root, new Vector3(0, .12f, 0), new Vector3(.7f, .12f, .7f), bodyMaterial);
      var label = new GameObject("AvatarLabel", typeof(TextMesh));
      label.transform.SetParent(root, false); label.transform.localPosition = new Vector3(0, 2.15f, 0); label.transform.localRotation = Quaternion.Euler(58f, 0, 0);
      var text = label.GetComponent<TextMesh>(); text.text = name.Replace("小人", ""); text.characterSize = .12f; text.fontSize = 36; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.color = new Color(.1f, .15f, .2f);
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
