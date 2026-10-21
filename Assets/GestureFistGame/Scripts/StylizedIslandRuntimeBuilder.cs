using System;
using System.Collections.Generic;
using UnityEngine;

namespace GestureFistGame
{
  /// <summary>
  /// Runtime fallback for older serialized scenes. It replaces only the
  /// surrounding island dressing; the water, fish, net and avatars remain
  /// owned by their existing scene objects.
  /// </summary>
  public static class StylizedIslandRuntimeBuilder
  {
    private const string RootName = "StylizedIslandEnvironment";

    public static void Ensure(Transform arena)
    {
      if (arena == null || arena.Find(RootName) != null) return;
      RemoveLegacyDressing(arena);
      var root = new GameObject(RootName).transform;
      root.SetParent(arena, false);
      var mats = CreateMaterials();
      CreateGround(root, mats["Ground"]);
      CreatePatches(root, mats);
      CreateTrees(root, mats);
      CreateRocks(root, mats);
      CreateTufts(root, mats);
      CreateFences(root, mats);
    }

    private static Dictionary<string, Material> CreateMaterials()
    {
      var result = new Dictionary<string, Material>();
      result["Ground"] = MakeMaterial("Runtime Island Ground", new Color(.66f, .72f, .38f, 1f));
      result["GroundDark"] = MakeMaterial("Runtime Island Ground Dark", new Color(.42f, .53f, .27f, 1f));
      result["GroundWarm"] = MakeMaterial("Runtime Island Ground Warm", new Color(.78f, .68f, .36f, 1f));
      result["Trunk"] = MakeMaterial("Runtime Island Trunk", new Color(.36f, .20f, .12f, 1f));
      result["LeafOlive"] = MakeMaterial("Runtime Island Leaf Olive", new Color(.28f, .45f, .20f, 1f));
      result["LeafGreen"] = MakeMaterial("Runtime Island Leaf Green", new Color(.39f, .57f, .25f, 1f));
      result["LeafCoral"] = MakeMaterial("Runtime Island Leaf Coral", new Color(.86f, .35f, .24f, 1f));
      result["LeafGold"] = MakeMaterial("Runtime Island Leaf Gold", new Color(.94f, .62f, .12f, 1f));
      result["Rock"] = MakeMaterial("Runtime Island Rock", new Color(.37f, .42f, .48f, 1f));
      result["RockLight"] = MakeMaterial("Runtime Island Rock Light", new Color(.53f, .57f, .61f, 1f));
      result["Tuft"] = MakeMaterial("Runtime Island Tuft", new Color(.30f, .47f, .16f, 1f));
      result["Flower"] = MakeMaterial("Runtime Island Flower", new Color(.98f, .93f, .68f, 1f));
      result["Wood"] = MakeMaterial("Runtime Island Fence Wood", new Color(.62f, .36f, .20f, 1f));
      return result;
    }

    private static Material MakeMaterial(string name, Color color)
    {
      var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
      var material = new Material(shader) { name = name };
      if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
      else material.color = color;
      if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .08f);
      if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
      return material;
    }

    private static void RemoveLegacyDressing(Transform arena)
    {
      var children = new List<Transform>();
      for (int i = 0; i < arena.childCount; i++) children.Add(arena.GetChild(i));
      foreach (var child in children)
      {
        string n = child.name;
        bool remove = n == "GeneratedMeadow" || n == "LeftGrassBank" || n == "RightGrassBank" ||
          n == "LeftWaterEdge" || n == "RightWaterEdge" || n == "FarBank" || n == "NearBank" ||
          n == "RiverBed_RealDepth" || n.StartsWith("NaturalShore_Left", StringComparison.Ordinal) || n.StartsWith("NaturalShore_Right", StringComparison.Ordinal) ||
          n.StartsWith("NaturalBank", StringComparison.Ordinal) || n.StartsWith("KayKit", StringComparison.Ordinal) ||
          n.StartsWith("NearBankRock", StringComparison.Ordinal) || n.StartsWith("FarBankRock", StringComparison.Ordinal) ||
          n.StartsWith("NearReed", StringComparison.Ordinal) || n.StartsWith("FarReed", StringComparison.Ordinal) ||
          n.StartsWith("MeadowGround", StringComparison.Ordinal);
        if (remove) UnityEngine.Object.Destroy(child.gameObject);
      }
    }

    private static void CreateGround(Transform root, Material material)
    {
      CreatePrimitive("IslandGround", PrimitiveType.Cube, root, new Vector3(0, -.18f, 0), new Vector3(32f, .22f, 26f), material);
    }

    private static void CreatePatches(Transform root, Dictionary<string, Material> mats)
    {
      var positions = new[]
      {
        new Vector3(-12.5f, .02f, -8.4f), new Vector3(-7.4f, .02f, -13f), new Vector3(4.8f, .02f, -9.6f),
        new Vector3(12.4f, .02f, -13.3f), new Vector3(-13.2f, .02f, 9.2f), new Vector3(-5.2f, .02f, 13.5f),
        new Vector3(6.6f, .02f, 9.3f), new Vector3(13.8f, .02f, 14f)
      };
      for (int i = 0; i < positions.Length; i++)
      {
        var patch = CreatePrimitive("IslandPatch_" + i, PrimitiveType.Sphere, root, positions[i],
          new Vector3(2.8f + (i % 3) * .9f, .035f, 1.25f + (i % 2) * .45f),
          i % 3 == 0 ? mats["GroundWarm"] : (i % 2 == 0 ? mats["GroundDark"] : mats["Ground"]));
        patch.transform.localRotation = Quaternion.Euler(0, i * 23f, 0);
      }
    }

    private static void CreateTrees(Transform root, Dictionary<string, Material> mats)
    {
      var positions = new[]
      {
        new Vector3(-14f, 0, -9.7f), new Vector3(-8.7f, 0, -14.2f), new Vector3(1.6f, 0, -11.3f),
        new Vector3(9f, 0, -15.1f), new Vector3(14.6f, 0, -8f), new Vector3(-14f, 0, 10.6f),
        new Vector3(-6.8f, 0, 15f), new Vector3(3.4f, 0, 10.7f), new Vector3(12.9f, 0, 14.4f)
      };
      var leafMats = new[] { mats["LeafOlive"], mats["LeafGreen"], mats["LeafCoral"], mats["LeafGold"] };
      for (int i = 0; i < positions.Length; i++)
      {
        var tree = new GameObject("IslandTree_" + i).transform;
        tree.SetParent(root, false); tree.localPosition = positions[i];
        float scale = .82f + (i % 3) * .14f;
        var trunk = CreatePrimitive("Trunk", PrimitiveType.Cylinder, tree, new Vector3(0, .9f * scale, 0), new Vector3(.26f * scale, .9f * scale, .26f * scale), mats["Trunk"]);
        trunk.transform.localRotation = Quaternion.Euler(0, i * 19f, i % 2 == 0 ? -4f : 4f);
        var crown = CreatePrimitive("Crown", PrimitiveType.Sphere, tree, new Vector3(0, 2.35f * scale, 0), new Vector3(1.25f * scale, .93f * scale, 1.08f * scale), leafMats[i % leafMats.Length]);
        crown.transform.localRotation = Quaternion.Euler(0, i * 31f, 0);
        if (i % 2 == 0)
          CreatePrimitive("CrownAccent", PrimitiveType.Sphere, tree, new Vector3(.42f * scale, 2.58f * scale, .12f * scale), new Vector3(.62f * scale, .52f * scale, .58f * scale), leafMats[(i + 1) % leafMats.Length]);
      }
    }

    private static void CreateRocks(Transform root, Dictionary<string, Material> mats)
    {
      var positions = new[]
      {
        new Vector3(-11f, .22f, -6.2f), new Vector3(-4.8f, .20f, -8f), new Vector3(6.4f, .18f, -7.2f),
        new Vector3(11.6f, .22f, -11.6f), new Vector3(-12.1f, .20f, 7.1f), new Vector3(-7.1f, .18f, 11.4f),
        new Vector3(5.3f, .20f, 7f), new Vector3(11.6f, .22f, 10.8f), new Vector3(15.2f, .18f, 5.4f),
        new Vector3(-15f, .18f, -5.6f)
      };
      for (int i = 0; i < positions.Length; i++)
      {
        var rock = CreatePrimitive("IslandRock_" + i, PrimitiveType.Sphere, root, positions[i],
          new Vector3(.65f + (i % 3) * .22f, .34f + (i % 2) * .14f, .48f + (i % 4) * .12f), i % 3 == 0 ? mats["RockLight"] : mats["Rock"]);
        rock.transform.localRotation = Quaternion.Euler(i * 13f, i * 27f, i * 5f);
      }
    }

    private static void CreateTufts(Transform root, Dictionary<string, Material> mats)
    {
      int index = 0;
      for (int row = 0; row < 5; row++) for (int col = 0; col < 8; col++)
      {
        float x = -15.5f + col * 4.35f + (row % 2) * 1.2f;
        float z = row % 2 == 0 ? -6f - row * 2.1f : 6f + row * 2f;
        var tuft = CreatePrimitive("IslandTuft_" + index++, PrimitiveType.Capsule, root, new Vector3(x, .24f, z), new Vector3(.06f, .24f, .06f), mats["Tuft"]);
        tuft.transform.localRotation = Quaternion.Euler(0, col * 27f, col % 2 == 0 ? -12f : 12f);
        if (col % 3 == 0) CreatePrimitive("IslandFlower_" + row + "_" + col, PrimitiveType.Sphere, root, new Vector3(x + .16f, .38f, z + .10f), new Vector3(.10f, .05f, .10f), mats["Flower"]);
      }
    }

    private static void CreateFences(Transform root, Dictionary<string, Material> mats)
    {
      CreateFence(root, "IslandFence_Near", new Vector3(-8f, .25f, -7f), 4f, mats["Wood"]);
      CreateFence(root, "IslandFence_Far", new Vector3(6f, .25f, 7.3f), 3.3f, mats["Wood"]);
    }

    private static void CreateFence(Transform root, string name, Vector3 position, float length, Material material)
    {
      var fence = new GameObject(name).transform; fence.SetParent(root, false); fence.localPosition = position;
      int posts = Mathf.Max(2, Mathf.RoundToInt(length / 1.3f) + 1);
      for (int i = 0; i < posts; i++)
      {
        float x = Mathf.Lerp(-length * .5f, length * .5f, i / (float)(posts - 1));
        CreatePrimitive("Post_" + i, PrimitiveType.Cylinder, fence, new Vector3(x, .35f, 0), new Vector3(.07f, .36f, .07f), material);
      }
      CreatePrimitive("Rail_Low", PrimitiveType.Cube, fence, new Vector3(0, .28f, 0), new Vector3(length, .08f, .08f), material);
      CreatePrimitive("Rail_High", PrimitiveType.Cube, fence, new Vector3(0, .67f, 0), new Vector3(length, .08f, .08f), material);
    }

    private static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
      var go = GameObject.CreatePrimitive(type);
      go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
      var renderer = go.GetComponent<Renderer>(); if (renderer != null) renderer.sharedMaterial = material;
      var collider = go.GetComponent<Collider>(); if (collider != null) UnityEngine.Object.Destroy(collider);
      return go;
    }
  }
}
