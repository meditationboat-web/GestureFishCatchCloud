using UnityEngine;

namespace GestureFistGame
{
  /// <summary>Reuse the authored kit for compact bank gardens and layered river scenery.</summary>
  public static class FishRiverScenery
  {
    public static void Ensure(Transform island)
    {
      if (island == null || island.Find("RiverBankScenery") != null) return;
      var sources = island.GetComponentsInChildren<Transform>(true);
      var tree = Find(sources, "IslandTree_Blender_");
      var bush = Find(sources, "IslandBush_Blender_");
      var rock = Find(sources, "IslandRock_Blender_");
      var flower = Find(sources, "IslandTuft_");
      if (bush == null && tree == null) return;
      var root = new GameObject("RiverBankScenery").transform;
      root.SetParent(island, false);
      // Small plants near the water; taller silhouettes sit behind the fishing pocket.
      for (int side = -1; side <= 1; side += 2)
      {
        for (int group = 0; group < 10; group++)
        {
          float x = -24f + group * 5.2f;
          float z = side * (5.8f + (group % 3) * .35f);
          Copy(bush, root, new Vector3(x, .23f, z), .85f + (group % 3) * .12f, group * 47f);
          Copy(bush, root, new Vector3(x + .8f, .23f, z + side * .5f), .6f, group * 31f);
          Copy(rock, root, new Vector3(x - .65f, .2f, side * 4.7f), .5f, group * 73f);
          for (int j = 0; j < 3; j++)
            Copy(flower, root, new Vector3(x + j * .48f - .5f, .24f, side * (4.65f + j * .2f)), .7f, j * 113f);
          // Leave the center free for the avatars and their throwing animations.
          if (Mathf.Abs(x) > 5f)
            Copy(tree, root, new Vector3(x + .4f, .2f, side * (8.1f + group % 2)), .85f + (group % 3) * .12f, group * 67f);
        }
        // Visible near-bank accents on both sides of the central character.
        foreach (float x in new[] { -3.8f, 3.8f })
        {
          Copy(bush, root, new Vector3(x, .23f, side * 5.7f), .65f, x * 23f);
          for (int j = 0; j < 4; j++)
            Copy(flower, root, new Vector3(x + (j - 1.5f) * .35f, .24f, side * (4.65f + j % 2 * .35f)), .8f, j * 71f);
        }
      }
    }

    private static Transform Find(Transform[] sources, string prefix)
    {
      foreach (var t in sources)
        if (t.name.StartsWith(prefix) && t.gameObject.activeSelf) return t;
      return null;
    }

    private static void Copy(Transform source, Transform parent, Vector3 position, float scale, float yaw)
    {
      if (source == null) return;
      var copy = Object.Instantiate(source.gameObject, parent).transform;
      copy.name = "RiverScenery_" + source.name;
      copy.position = position;
      copy.rotation = Quaternion.Euler(0, yaw, 0);
      copy.localScale = source.localScale * scale;
      // Decorative copies share existing meshes/materials and never obstruct fish physics.
      foreach (var c in copy.GetComponentsInChildren<Collider>()) c.enabled = false;
    }
  }
}
