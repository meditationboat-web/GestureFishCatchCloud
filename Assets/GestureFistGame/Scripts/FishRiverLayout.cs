using UnityEngine;

namespace GestureFistGame
{
  // Keep the fishing pocket intact; open its upstream/downstream ends into a valley.
  public static class FishRiverLayout
  {
    public const float Length = 64f;
    public static void Apply()
    {
      foreach (var t in Object.FindObjectsOfType<Transform>(true))
      {
        if (t.name == "SideBank_Left" || t.name == "SideBank_Right")
          t.position = new Vector3(t.name.EndsWith("Left") ? -39.62f : 39.62f, t.position.y, t.position.z);
        if (t.name == "LeftGrassBank" || t.name == "RightGrassBank" ||
            t.name == "LeftWaterEdge" || t.name == "RightWaterEdge" ||
            t.name.StartsWith("IslandSideShore_") || t.name == "WaterGrassContactEdge")
          t.gameObject.SetActive(false);
      }
      var island = GameObject.Find("StylizedIslandEnvironment");
      if (island == null) return;
      // Move whole prop groups, never individual leaves/branches or character parts.
      var dressing = new System.Collections.Generic.List<Transform>();
      foreach (Transform child in island.transform)
      {
        if (child.name == "BlenderEnvironment")
          foreach (Transform prop in child) dressing.Add(prop);
        else dressing.Add(child);
      }
      foreach (var t in dressing)
      {
        if (t.name.StartsWith("IslandShore_"))
        {
          var scale = t.localScale; scale.x = 64f; t.localScale = scale;
          t.localRotation = Quaternion.identity;
          continue;
        }
        var p = t.position;
        var renderers = t.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0 || Mathf.Abs(p.z) >= 6.3f || Mathf.Abs(p.x) < 4.2f) continue;
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        if (bounds.size.x > 10f) continue;
        float side = p.z < 0 ? -1f : 1f;
        p.z = side * (6.4f + Mathf.Abs(p.x) % 3f);
        t.position = p;
      }
      FishRiverScenery.Ensure(island.transform);
    }
  }
}
