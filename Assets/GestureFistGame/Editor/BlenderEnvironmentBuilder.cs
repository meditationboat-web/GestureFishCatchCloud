using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GestureFistGame.Editor
{
  /// <summary>
  /// Places the original low-poly kit generated in Blender. The imported
  /// meshes provide consistent silhouettes and flat-shaded facets while this
  /// class keeps all scene materials in the project's URP palette.
  /// </summary>
  public static class BlenderEnvironmentBuilder
  {
    private const string Root = "Assets/GestureFistGame/Art/BlenderKit/";
    private const string SceneRootName = "BlenderEnvironment";
    private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

    public static bool Build(Transform parent, Mesh islandBlob, Dictionary<string, Material> islandMaterials)
    {
      if (parent == null) return false;
      var treeA = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Tree_Stylized_A.fbx");
      var bush = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Bush_Stylized.fbx");
      var rock = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Rock_Stylized.fbx");
      var pumpkin = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Pumpkin_Stylized.fbx");
      var cabbage = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Cabbage_Stylized.fbx");
      var fence = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Fence_Stylized.fbx");
      var flower = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Flower_Stylized.fbx");
      var crate = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Crate_Stylized.fbx");
      var chest = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Chest_Stylized.fbx");
      if (treeA == null || bush == null || rock == null || pumpkin == null || fence == null || flower == null)
      {
        Debug.LogWarning("BlenderEnvironmentBuilder: imported kit not ready; keeping procedural environment fallback.");
        return false;
      }

      var old = parent.Find(SceneRootName);
      if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
      var root = new GameObject(SceneRootName).transform;
      root.SetParent(parent, false);
      CreatePalette();

      var treeB = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Tree_Stylized_B.fbx");
      var trees = new[]
      {
        new Vector3(-6.6f, .20f, -2.2f), new Vector3(-7.5f, .20f, 2.4f),
        new Vector3(-6.8f, .20f, 7.0f), new Vector3(6.7f, .20f, -2.8f),
        new Vector3(7.6f, .20f, 2.0f), new Vector3(6.9f, .20f, 7.2f),
        new Vector3(-10.0f, .20f, -6.7f), new Vector3(10.0f, .20f, -6.8f),
        new Vector3(9.8f, .20f, 7.4f), new Vector3(-11.8f, .20f, 5.1f),
        new Vector3(12.1f, .20f, 4.2f)
      };
      for (int i = 0; i < trees.Length; i++)
        Place(treeB != null && i % 3 == 1 ? treeB : treeA, root, "IslandTree_Blender_" + i,
          trees[i], .72f + (i % 3) * .08f, i * 23f, "tree");

      var bushes = new[]
      {
        new Vector3(-8.8f, .23f, -6.0f), new Vector3(-6.8f, .23f, -7.4f),
        new Vector3(7.5f, .23f, -6.5f), new Vector3(10.2f, .23f, -5.4f),
        new Vector3(-9.4f, .23f, 5.5f), new Vector3(-7.0f, .23f, 7.0f),
        new Vector3(7.5f, .23f, 5.8f), new Vector3(10.0f, .23f, 7.0f),
        new Vector3(-12.4f, .22f, -2.8f), new Vector3(12.4f, .22f, 1.0f)
      };
      for (int i = 0; i < bushes.Length; i++)
        Place(bush, root, "IslandBush_Blender_" + i, bushes[i], .75f + (i % 3) * .09f, i * 31f, "bush");

      var rocks = new[]
      {
        new Vector3(-6.3f, .22f, -1.0f), new Vector3(-7.2f, .20f, 3.4f),
        new Vector3(6.2f, .18f, -1.7f), new Vector3(7.4f, .22f, 4.2f),
        new Vector3(-8.4f, .20f, -5.0f), new Vector3(-8.0f, .18f, 6.0f),
        new Vector3(6.8f, .20f, 6.3f), new Vector3(8.6f, .22f, -5.4f),
        new Vector3(10.2f, .18f, 2.8f), new Vector3(-10.0f, .18f, 2.6f),
        new Vector3(-4.9f, .20f, -6.3f), new Vector3(4.8f, .20f, 6.4f)
      };
      for (int i = 0; i < rocks.Length; i++)
        Place(rock, root, "IslandRock_Blender_" + i, rocks[i], .52f + (i % 3) * .08f, i * 19f, "rock");

      BuildGarden(root, islandBlob, islandMaterials, pumpkin, cabbage, fence, flower, -7.7f, -6.0f, false);
      BuildGarden(root, islandBlob, islandMaterials, pumpkin, cabbage, fence, flower, 7.5f, 6.1f, true);

      // Small authored props give the banks a readable human scale without
      // crowding the central fishing lane. They are optional so an older kit
      // can still build with the procedural fallback.
      if (crate != null)
        Place(crate, root, "IslandCrate_Blender", new Vector3(-4.9f, .31f, -7.15f), .54f, -12f, "crate");
      if (chest != null)
        Place(chest, root, "IslandChest_Blender", new Vector3(4.9f, .31f, 7.10f), .48f, 168f, "chest");

      for (int i = 0; i < 26; i++)
      {
        float a = (i * 2.399f + .4f) * Mathf.Deg2Rad;
        float rx = 7.1f + (i % 4) * 1.45f;
        float rz = 6.5f + (i % 5) * 1.12f;
        float x = Mathf.Cos(a) * rx;
        float z = Mathf.Sin(a) * rz;
        if (Mathf.Abs(z) < 4.9f) z = Mathf.Sign(z == 0f ? 1f : z) * 5.15f;
        Place(flower, root, "IslandTuft_" + i, new Vector3(x, .24f, z), .62f + (i % 3) * .06f, i * 17f, "flower");
      }

      AssetDatabase.SaveAssets();
      return true;
    }

    private static void BuildGarden(Transform root, Mesh blob, Dictionary<string, Material> islandMaterials,
      GameObject pumpkin, GameObject cabbage, GameObject fence, GameObject flower, float x, float z, bool far)
    {
      if (blob != null && islandMaterials != null && islandMaterials.TryGetValue("Soil", out var soil))
      {
        var bed = new GameObject("PumpkinGardenSoil_Blender", typeof(MeshFilter), typeof(MeshRenderer));
        bed.transform.SetParent(root, false);
        bed.transform.localPosition = new Vector3(x, .26f, z);
        bed.transform.localRotation = Quaternion.Euler(0f, far ? 9f : -7f, 0f);
        bed.transform.localScale = new Vector3(2.65f, .024f, 1.28f);
        bed.GetComponent<MeshFilter>().sharedMesh = blob;
        bed.GetComponent<MeshRenderer>().sharedMaterial = soil;
      }
      Place(fence, root, "IslandFence_Blender_" + (far ? "Far" : "Near"),
        new Vector3(x, .30f, z + (far ? .98f : -.98f)), 1f, far ? 9f : -7f, "fence");
      for (int row = 0; row < 2; row++)
        for (int col = 0; col < 4; col++)
        {
          float px = x - 1.65f + col * 1.08f + (row % 2) * .25f;
          float pz = z - .42f + row * .82f;
          bool useCabbage = cabbage != null && ((row + col) % 3 == 0 || (far && row == 1 && col == 2));
          Place(useCabbage ? cabbage : pumpkin, root,
            (useCabbage ? "IslandCabbage_Blender_" : "IslandPumpkin_Blender_") + (far ? "F" : "N") + row + col,
            new Vector3(px, useCabbage ? .31f : .40f, pz),
            (useCabbage ? .72f : .70f) + ((row + col) % 3) * .07f, (row * 4 + col) * 29f,
            useCabbage ? "cabbage" : "pumpkin");
        }
      for (int i = 0; i < 3; i++)
        Place(flower, root, "IslandTuft_Garden_" + (far ? "F" : "N") + i,
          new Vector3(x - 1.2f + i * 1.15f, .24f, z + (far ? .48f : -.48f)), .48f, i * 32f, "flower");
    }

    private static void CreatePalette()
    {
      Materials.Clear();
      Materials["leafDark"] = MaterialAsset("BlenderKit_LeafDark", new Color(.16f, .31f, .22f));
      Materials["leafMid"] = MaterialAsset("BlenderKit_LeafMid", new Color(.24f, .46f, .28f));
      Materials["leafLight"] = MaterialAsset("BlenderKit_LeafLight", new Color(.38f, .58f, .32f));
      Materials["trunk"] = MaterialAsset("BlenderKit_Trunk", new Color(.34f, .18f, .12f));
      Materials["rock"] = MaterialAsset("BlenderKit_Rock", new Color(.38f, .40f, .47f));
      Materials["rockLight"] = MaterialAsset("BlenderKit_RockLight", new Color(.54f, .55f, .62f));
      Materials["pumpkin"] = MaterialAsset("BlenderKit_Pumpkin", new Color(.85f, .30f, .055f));
      Materials["pumpkinLight"] = MaterialAsset("BlenderKit_PumpkinLight", new Color(1f, .48f, .08f));
      Materials["cabbage"] = MaterialAsset("BlenderKit_Cabbage", new Color(.90f, .84f, .62f));
      Materials["cabbageLeaf"] = MaterialAsset("BlenderKit_CabbageLeaf", new Color(.34f, .52f, .35f));
      Materials["wood"] = MaterialAsset("BlenderKit_Wood", new Color(.52f, .28f, .16f));
      Materials["woodLight"] = MaterialAsset("BlenderKit_WoodLight", new Color(.72f, .45f, .25f));
      Materials["flower"] = MaterialAsset("BlenderKit_Flower", new Color(.96f, .91f, .70f));
      Materials["flowerCenter"] = MaterialAsset("BlenderKit_FlowerCenter", new Color(.95f, .54f, .10f));
    }

    private static Material MaterialAsset(string name, Color color)
    {
      string path = "Assets/GestureFistGame/Materials/" + name + ".mat";
      var material = AssetDatabase.LoadAssetAtPath<Material>(path);
      if (material == null)
      {
        var shader = Shader.Find("GestureFistGame/IslandToon") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        material = new Material(shader) { name = name };
        AssetDatabase.CreateAsset(material, path);
      }
      else
      {
        var toon = Shader.Find("GestureFistGame/IslandToon");
        if (toon != null && material.shader != toon) material.shader = toon;
      }
      if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
      else material.color = color;
      if (material.HasProperty("_Color")) material.SetColor("_Color", color);
      if (material.HasProperty("_PaintColor")) material.SetColor("_PaintColor", color);
      if (material.HasProperty("_ShadeColor"))
      {
        Color shade = Color.Lerp(color, new Color(.075f, .10f, .075f, 1f), .20f);
        material.SetColor("_ShadeColor", shade);
        material.SetColor("_ShadowTint", new Color(.88f, .92f, .84f, 1f));
        material.SetFloat("_Threshold", .18f);
        material.SetFloat("_Softness", .075f);
        material.SetFloat("_Flat", .65f);
      }
      if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .08f);
      material.enableInstancing = false;
      EditorUtility.SetDirty(material);
      return material;
    }

    private static GameObject Place(GameObject source, Transform parent, string name, Vector3 position,
      float scale, float yaw, string category)
    {
      var instance = PrefabUtility.InstantiatePrefab(source, parent) as GameObject;
      if (instance == null) return null;
      instance.name = name;
      instance.transform.localPosition = position;
      instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
      instance.transform.localScale = Vector3.one * scale;
      foreach (var col in instance.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(col);
      foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
      {
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        ApplyRendererMaterials(renderer, category);
      }
      return instance;
    }

    private static void ApplyRendererMaterials(Renderer renderer, string category)
    {
      var materials = renderer.sharedMaterials;
      for (int i = 0; i < materials.Length; i++)
      {
        string n = renderer.gameObject.name.ToLowerInvariant();
        if (category == "tree") materials[i] = n.Contains("trunk") ? Materials["trunk"] : (n.Contains("light") ? Materials["leafLight"] : (n.Contains("left") ? Materials["leafDark"] : Materials["leafMid"]));
        else if (category == "bush") materials[i] = n.Contains("left") ? Materials["leafDark"] : (n.Contains("right") ? Materials["leafLight"] : Materials["leafMid"]);
        else if (category == "rock") materials[i] = n.Contains("face") ? Materials["rockLight"] : Materials["rock"];
        else if (category == "pumpkin") materials[i] = n.Contains("stem") ? Materials["trunk"] : (n.Contains("leaf") ? Materials["leafMid"] : (n.Contains("rib_0") ? Materials["pumpkinLight"] : Materials["pumpkin"]));
        else if (category == "cabbage") materials[i] = n.Contains("leaf") ? Materials["cabbageLeaf"] : Materials["cabbage"];
        else if (category == "fence") materials[i] = n.Contains("endgrain") ? Materials["woodLight"] : Materials["wood"];
        else if (category == "crate" || category == "chest") materials[i] = (n.Contains("end") || n.Contains("lid") || n.Contains("brass")) ? Materials["woodLight"] : Materials["wood"];
        else if (category == "flower") materials[i] = n.Contains("center") ? Materials["flowerCenter"] : (n.Contains("stem") ? Materials["leafMid"] : Materials["flower"]);
      }
      renderer.sharedMaterials = materials;
    }
  }
}
