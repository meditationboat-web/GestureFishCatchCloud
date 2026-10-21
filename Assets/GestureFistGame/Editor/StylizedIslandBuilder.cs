using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GestureFistGame.Editor
{
  /// <summary>
  /// Builds the non-interactive island dressing around the existing river.
  /// The river mesh/material, fish, and player avatars are deliberately owned
  /// by other systems and are not touched here.
  /// </summary>
  public static class StylizedIslandBuilder
  {
    private const string RootPath = "Assets/GestureFistGame/Art/GeneratedIsland";
    private const string RootName = "StylizedIslandEnvironment";
    private static readonly Color GroundLight = new Color(.66f, .72f, .35f, 1f);
    private static readonly Color GroundDark = new Color(.57f, .64f, .31f, 1f);

    public static void Build(Transform parent)
    {
      if (parent == null) return;
      EnsureFolder();
      var old = parent.Find(RootName);
      if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
      var root = new GameObject(RootName).transform;
      root.SetParent(parent, false);

      var materials = CreateMaterials();
      var blob = CreateBlobAsset("IslandBlob");
      // The meadow now meets the water directly; separate long shore slabs
      // created the artificial parallel bands visible in the overview.
      // Keep the meadow open around the water.  The old side ribbons and
      // diagonal soil path read as several artificial roads crossing the
      // whole composition, so the bank itself now supplies the boundary.
      BuildWaterGrassEdge(root, materials["GroundDark"]);
      // The scene now uses the authored Blender kit for the readable prop
      // silhouettes. Keep the older procedural builders below as a fallback
      // inside BlenderEnvironmentBuilder when the imported kit is unavailable.
      bool importedKit = BlenderEnvironmentBuilder.Build(root, blob, materials);
      if (!importedKit)
      {
        BuildTrees(root, blob, materials);
        BuildBushClusters(root, blob, materials);
        BuildRocks(root, blob, materials);
        BuildTuftsAndFlowers(root, materials);
        BuildFences(root, materials);
        BuildShadowPatches(root, blob, materials);
        BuildPumpkinGardens(root, blob, materials);
      }
      BuildReferenceProps(root, materials);
      AssetDatabase.SaveAssets();
      EditorUtility.SetDirty(root.gameObject);
    }

    private static void EnsureFolder()
    {
      Directory.CreateDirectory("Assets/GestureFistGame/Art");
      Directory.CreateDirectory(RootPath);
      AssetDatabase.Refresh();
    }

    private static Dictionary<string, Material> CreateMaterials()
    {
      var result = new Dictionary<string, Material>();
      result["GroundLight"] = MaterialAsset("IslandGroundLight", new Color(.66f, .72f, .35f, 1f));
      result["GroundDark"] = MaterialAsset("IslandGroundDark", new Color(.57f, .64f, .31f, 1f));
      result["GroundWarm"] = MaterialAsset("IslandGroundWarm", new Color(.62f, .68f, .34f, 1f));
      result["Shadow"] = MaterialAsset("IslandShadow", new Color(.30f, .38f, .21f, 1f));
      result["BankEdge"] = CreateBankEdgeMaterial();
      result["Soil"] = MaterialAsset("IslandSoil", new Color(.55f, .30f, .14f, 1f));
      result["SoilLight"] = MaterialAsset("IslandSoilLight", new Color(.64f, .39f, .18f, 1f));
      result["Trunk"] = MaterialAsset("IslandTreeTrunk", new Color(.38f, .19f, .14f, 1f));
      result["LeafOlive"] = MaterialAsset("IslandLeafOlive", new Color(.20f, .33f, .25f, 1f));
      result["LeafGreen"] = MaterialAsset("IslandLeafGreen", new Color(.28f, .42f, .29f, 1f));
      result["LeafCoral"] = MaterialAsset("IslandLeafCoral", new Color(.64f, .35f, .29f, 1f));
      result["LeafGold"] = MaterialAsset("IslandLeafGold", new Color(.74f, .60f, .28f, 1f));
      result["Pumpkin"] = MaterialAsset("IslandPumpkin", new Color(.88f, .38f, .075f, 1f));
      result["PumpkinLight"] = MaterialAsset("IslandPumpkinLight", new Color(1.0f, .56f, .12f, 1f));
      result["Rock"] = MaterialAsset("IslandRock", new Color(.42f, .43f, .52f, 1f));
      result["RockLight"] = MaterialAsset("IslandRockLight", new Color(.60f, .60f, .68f, 1f));
      result["Tuft"] = MaterialAsset("IslandTuft", new Color(.32f, .48f, .24f, 1f));
      result["Flower"] = MaterialAsset("IslandFlower", new Color(.98f, .95f, .78f, 1f));
      result["Wood"] = MaterialAsset("IslandFenceWood", new Color(.55f, .31f, .19f, 1f));
      result["Chest"] = MaterialAsset("IslandChest", new Color(.68f, .27f, .08f, 1f));
      result["ChestInner"] = MaterialAsset("IslandChestInner", new Color(.25f, .09f, .035f, 1f));
      result["ChestGold"] = MaterialAsset("IslandChestGold", new Color(.96f, .60f, .08f, 1f));
      result["Sign"] = MaterialAsset("IslandSign", new Color(.95f, .92f, .77f, 1f));
      result["SignInk"] = MaterialAsset("IslandSignInk", new Color(.15f, .14f, .12f, 1f));
      return result;
    }

    private static Material MaterialAsset(string name, Color color)
    {
      string path = RootPath + "/" + name + ".mat";
      DeleteAsset(path);
      bool flatSurface = name.StartsWith("IslandGround", StringComparison.Ordinal) ||
        name.StartsWith("IslandSoil", StringComparison.Ordinal) || name == "IslandShadow";
      var shader = flatSurface
        ? (Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"))
        : (Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
      var material = new Material(shader) { name = name };
      if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
      else material.color = color;
      if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .08f);
      if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
      AssetDatabase.CreateAsset(material, path);
      return material;
    }

    private static Mesh CreateBlobAsset(string name)
    {
      string path = RootPath + "/" + name + ".asset";
      DeleteAsset(path);
      const int sides = 8;
      float[] heights = { -.95f, -.56f, .06f, .58f, .92f };
      float[] radii = { .42f, .86f, 1.00f, .72f, .24f };
      var vertices = new List<Vector3>(sides * heights.Length + 2);
      for (int ring = 0; ring < heights.Length; ring++)
        for (int side = 0; side < sides; side++)
        {
          float angle = side * Mathf.PI * 2f / sides;
          float jitter = 1f + Mathf.Sin(side * 17.3f + ring * 3.7f) * .07f;
          vertices.Add(new Vector3(Mathf.Cos(angle) * radii[ring] * jitter, heights[ring], Mathf.Sin(angle) * radii[ring] * jitter));
        }
      int bottom = vertices.Count; vertices.Add(new Vector3(0, -1.02f, 0));
      int top = vertices.Count; vertices.Add(new Vector3(0, 1.0f, 0));
      var triangles = new List<int>(sides * (heights.Length - 1) * 6 + sides * 6);
      for (int ring = 0; ring < heights.Length - 1; ring++)
        for (int side = 0; side < sides; side++)
        {
          int next = (side + 1) % sides;
          int a = ring * sides + side, b = ring * sides + next;
          int c = (ring + 1) * sides + side, d = (ring + 1) * sides + next;
          triangles.Add(a); triangles.Add(c); triangles.Add(b);
          triangles.Add(b); triangles.Add(c); triangles.Add(d);
        }
      for (int side = 0; side < sides; side++)
      {
        int next = (side + 1) % sides;
        triangles.Add(bottom); triangles.Add(next); triangles.Add(side);
        int a = (heights.Length - 1) * sides + side;
        int b = (heights.Length - 1) * sides + next;
        // Viewed from above the cap must wind clockwise around the top
        // vertex so its normal points outward. The old order left a visible
        // hole in flattened bushes and tree crowns with back-face culling.
        triangles.Add(top); triangles.Add(b); triangles.Add(a);
      }
      var mesh = new Mesh { name = name };
      mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0, true);
      mesh.RecalculateNormals(); mesh.RecalculateBounds();
      AssetDatabase.CreateAsset(mesh, path);
      return mesh;
    }

    private static void BuildShorePatches(Transform root, Mesh blob, Dictionary<string, Material> mats)
    {
      for (int side = 0; side < 2; side++)
      {
        var shore = MeshObject("IslandShore_" + side, root, blob, mats["GroundDark"]);
        shore.transform.localPosition = new Vector3(0, .17f, side == 0 ? -4.46f : 4.46f);
        shore.transform.localRotation = Quaternion.Euler(0, side == 0 ? 4f : -5f, 0);
        shore.transform.localScale = new Vector3(14.5f, .055f, .48f);
      }
      // Keep the wide meadow readable from the overview camera. Large
      // floating color blobs were visually dominant and read as accidental
      // triangles; the authored soil path and garden beds supply the warm
      // accents instead.
    }

    private static void BuildSideShore(Transform root, Mesh blob, Dictionary<string, Material> mats)
    {
      // A continuous irregular ribbon reads as a natural shoreline. Tiled
      // identical blobs looked like a row of lily pads in the overview.
      for (int side = -1; side <= 1; side += 2)
      {
        var shore = MeshObject("IslandSideShore_" + side, root, CreateShoreRibbon(side), mats["BankEdge"]);
        shore.transform.localPosition = Vector3.zero;
      }
    }

    private static Mesh CreateShoreRibbon(int side)
    {
      const int segments = 14;
      var vertices = new List<Vector3>((segments + 1) * 2);
      var triangles = new List<int>(segments * 6);
      for (int i = 0; i <= segments; i++)
      {
        float t = i / (float)segments;
        float z = Mathf.Lerp(-4.02f, 4.02f, t);
        float wave = Mathf.Sin(i * 1.73f + side * .6f) * .10f + Mathf.Sin(i * .63f) * .06f;
        float x0 = side * (4.20f + wave);
        float x1 = side * (4.56f + wave);
        vertices.Add(new Vector3(x0, .205f, z));
        vertices.Add(new Vector3(x1, .205f, z));
        if (i >= segments) continue;
        int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
        if (side < 0)
        {
          triangles.Add(a); triangles.Add(c); triangles.Add(b);
          triangles.Add(b); triangles.Add(c); triangles.Add(d);
        }
        else
        {
          triangles.Add(a); triangles.Add(b); triangles.Add(c);
          triangles.Add(b); triangles.Add(d); triangles.Add(c);
        }
      }
      var mesh = new Mesh { name = "IslandShoreRibbon" };
      mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0, true);
      mesh.RecalculateNormals(); mesh.RecalculateBounds();
      return mesh;
    }

    private static Material CreateBankEdgeMaterial()
    {
      string path = RootPath + "/IslandBankEdge.mat";
      DeleteAsset(path);
      var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
      var material = new Material(shader) { name = "IslandBankEdge" };
      if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(.56f, .62f, .28f, 1f));
      if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .08f);
      AssetDatabase.CreateAsset(material, path);
      return material;
    }

    private static void BuildReferencePaths(Transform root, Mesh blob, Dictionary<string, Material> mats)
    {
      // Intentionally empty.  The former wide diagonal path dominated the
      // overview and was mistaken for a second river bank.
    }

    private static void BuildWaterGrassEdge(Transform root, Material material)
    {
      // A narrow, slightly irregular contact strip hides the hard water cube
      // edge without becoming another road across the meadow.
      const float halfX = 4.20f;
      const float halfZ = 4.00f;
      const float width = .18f;
      const int segments = 10;
      var vertices = new List<Vector3>(segments * 8 + 8);
      var triangles = new List<int>(segments * 24 + 24);
      AddEdge(vertices, triangles, new Vector3(-halfX, .185f, halfZ), new Vector3(halfX, .185f, halfZ), width, segments, true);
      AddEdge(vertices, triangles, new Vector3(halfX, .185f, -halfZ), new Vector3(-halfX, .185f, -halfZ), width, segments, true);
      AddEdge(vertices, triangles, new Vector3(-halfX, .185f, -halfZ), new Vector3(-halfX, .185f, halfZ), width, segments, false);
      AddEdge(vertices, triangles, new Vector3(halfX, .185f, halfZ), new Vector3(halfX, .185f, -halfZ), width, segments, false);
      var mesh = new Mesh { name = "WaterGrassContactEdge" };
      mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0, true);
      mesh.RecalculateNormals(); mesh.RecalculateBounds();
      MeshObject("WaterGrassContactEdge", root, mesh, material);
    }

    private static void AddEdge(List<Vector3> vertices, List<int> triangles, Vector3 start, Vector3 end, float width, int segments, bool horizontal)
    {
      int first = vertices.Count;
      Vector3 direction = (end - start).normalized;
      Vector3 outward = horizontal ? new Vector3(0, 0, Mathf.Sign(start.z)) : new Vector3(Mathf.Sign(start.x), 0, 0);
      for (int i = 0; i <= segments; i++)
      {
        float t = i / (float)segments;
        Vector3 center = Vector3.Lerp(start, end, t);
        float wobble = Mathf.Sin((i + 1) * 1.71f + (horizontal ? start.z : start.x)) * .045f;
        center += outward * wobble;
        vertices.Add(center - outward * width * .5f);
        vertices.Add(center + outward * width * .5f);
      }
      for (int i = 0; i < segments; i++)
      {
        int a = first + i * 2, b = a + 1, c = a + 2, d = a + 3;
        triangles.Add(a); triangles.Add(b); triangles.Add(c);
        triangles.Add(b); triangles.Add(d); triangles.Add(c);
      }
    }

    private static void BuildTrees(Transform root, Mesh blob, Dictionary<string, Material> mats)
    {
      var positions = new[]
      {
        new Vector3(-6.6f, 0, -2.2f), new Vector3(-7.5f, 0, 2.4f), new Vector3(-6.8f, 0, 7.0f),
        new Vector3(6.7f, 0, -2.8f), new Vector3(7.6f, 0, 2.0f), new Vector3(6.9f, 0, 7.2f),
        new Vector3(-10.0f, 0, -6.7f), new Vector3(10.0f, 0, -6.8f), new Vector3(9.8f, 0, 7.4f)
      };
      var leafMats = new[] { mats["LeafOlive"], mats["LeafGreen"] };
      for (int i = 0; i < positions.Length; i++)
      {
        var tree = new GameObject("IslandTree_" + i).transform;
        tree.SetParent(root, false); tree.localPosition = positions[i];
        // The reference uses small props around a human-height focal point.
        float scale = .76f + (i % 3) * .10f;
        var trunk = Primitive("Trunk", PrimitiveType.Cylinder, tree, new Vector3(0, .9f * scale, 0), new Vector3(.26f * scale, .9f * scale, .26f * scale), mats["Trunk"]);
        trunk.transform.localRotation = Quaternion.Euler(0, i * 19f, (i % 2 == 0 ? -4f : 4f));
        var crown = MeshObject("Crown", tree, blob, leafMats[i % leafMats.Length]);
        crown.transform.localPosition = new Vector3(0, 2.20f * scale, 0);
        crown.transform.localRotation = Quaternion.Euler(0, i * 31f, 0);
        crown.transform.localScale = new Vector3(1.25f * scale, .93f * scale, 1.08f * scale);
        var crown2 = MeshObject("CrownAccent", tree, blob, leafMats[(i + 1) % leafMats.Length]);
        crown2.transform.localPosition = new Vector3(.48f * scale, 2.46f * scale, .10f * scale);
        crown2.transform.localScale = new Vector3(.68f * scale, .56f * scale, .62f * scale);
        if (i % 2 == 0)
        {
          var crown3 = MeshObject("CrownAccentBack", tree, blob, mats["LeafOlive"]);
          crown3.transform.localPosition = new Vector3(-.40f * scale, 2.34f * scale, -.12f * scale);
          crown3.transform.localScale = new Vector3(.58f * scale, .48f * scale, .55f * scale);
        }
      }
    }

    private static void BuildBushClusters(Transform root, Mesh blob, Dictionary<string, Material> mats)
    {
      // Broad, low bushes carry the rounded silhouette seen in the target
      // scenes. They sit outside the water lane and replace the noisy blade
      // texture with a few readable masses.
      var positions = new[]
      {
        new Vector3(-8.8f, .36f, -6.0f), new Vector3(-6.8f, .34f, -7.4f),
        new Vector3(7.5f, .35f, -6.5f), new Vector3(10.2f, .36f, -5.4f),
        new Vector3(-9.4f, .35f, 5.5f), new Vector3(-7.0f, .34f, 7.0f),
        new Vector3(7.5f, .35f, 5.8f), new Vector3(10.0f, .35f, 7.0f)
      };
      for (int i = 0; i < positions.Length; i++)
      {
        var bush = MeshObject("IslandBush_" + i, root, blob, mats[i % 2 == 0 ? "LeafOlive" : "LeafGreen"]);
        bush.transform.localPosition = positions[i];
        bush.transform.localRotation = Quaternion.Euler(0f, i * 23f, 0f);
        float scale = .82f + (i % 3) * .12f;
        bush.transform.localScale = new Vector3(1.15f * scale, .52f * scale, .92f * scale);
      }
    }

    private static void BuildRocks(Transform root, Mesh blob, Dictionary<string, Material> mats)
    {
      var positions = new[]
      {
        new Vector3(-6.3f, .22f, -1.0f), new Vector3(-7.2f, .20f, 3.4f), new Vector3(6.2f, .18f, -1.7f),
        new Vector3(7.4f, .22f, 4.2f), new Vector3(-8.4f, .20f, -5.0f), new Vector3(-8.0f, .18f, 6.0f),
        new Vector3(6.8f, .20f, 6.3f), new Vector3(8.6f, .22f, -5.4f), new Vector3(10.2f, .18f, 2.8f),
        new Vector3(-10.0f, .18f, 2.6f)
      };
      for (int i = 0; i < positions.Length; i++)
      {
        var rock = MeshObject("IslandRock_" + i, root, blob, i % 3 == 0 ? mats["RockLight"] : mats["Rock"]);
        rock.transform.localPosition = positions[i];
        rock.transform.localRotation = Quaternion.Euler(i * 13f, i * 27f, i * 5f);
        rock.transform.localScale = new Vector3(.65f + (i % 3) * .22f, .34f + (i % 2) * .14f, .48f + (i % 4) * .12f);
      }
    }

    private static void BuildTuftsAndFlowers(Transform root, Dictionary<string, Material> mats)
    {
      int index = 0;
      for (int row = 0; row < 5; row++)
        for (int col = 0; col < 8; col++)
        {
          float x = -15.5f + col * 4.35f + (row % 2) * 1.2f;
          float z = (row % 2 == 0 ? -6.0f - row * 2.1f : 6.0f + row * 2.0f);
          var tuft = Primitive("IslandTuft_" + index++, PrimitiveType.Capsule, root, new Vector3(x, .24f, z), new Vector3(.06f, .24f, .06f), mats["Tuft"]);
          tuft.transform.localRotation = Quaternion.Euler(0, col * 27f, (col % 2 == 0 ? -12f : 12f));
          if (col % 3 == 0)
            CreateDaisy(root, "IslandFlower_" + row + "_" + col, new Vector3(x + .16f, .38f, z + .10f), mats);
        }
    }

    private static void BuildFences(Transform root, Dictionary<string, Material> mats)
    {
      CreateFence(root, "IslandFence_Near", new Vector3(-7.0f, .25f, -2.6f), 3.8f, mats["Wood"]);
      CreateFence(root, "IslandFence_Far", new Vector3(7.0f, .25f, 4.8f), 3.6f, mats["Wood"]);
    }

    private static void BuildShadowPatches(Transform root, Mesh blob, Dictionary<string, Material> mats)
    {
      // Graphic contact shadows are part of the reference language. They
      // make the low-poly props feel planted even when the overview camera is
      // high and the terrain is intentionally almost flat.
      var trees = new[]
      {
        new Vector3(-6.6f, .218f, -2.2f), new Vector3(-7.5f, .218f, 2.4f),
        new Vector3(-6.8f, .218f, 7.0f), new Vector3(6.7f, .218f, -2.8f),
        new Vector3(7.6f, .218f, 2.0f), new Vector3(6.9f, .218f, 7.2f),
        new Vector3(-10.0f, .218f, -6.7f), new Vector3(10.0f, .218f, -6.8f),
        new Vector3(9.8f, .218f, 7.4f)
      };
      for (int i = 0; i < trees.Length; i++)
      {
        var shadow = MeshObject("IslandTreeShadow_" + i, root, blob, mats["Shadow"]);
        shadow.transform.localPosition = trees[i];
        shadow.transform.localRotation = Quaternion.Euler(0f, 12f * i, 0f);
        shadow.transform.localScale = new Vector3(.92f + (i % 3) * .18f, .012f, .48f + (i % 2) * .12f);
      }
      var bushes = new[]
      {
        new Vector3(-8.8f, .224f, -6.0f), new Vector3(-6.8f, .224f, -7.4f),
        new Vector3(7.5f, .224f, -6.5f), new Vector3(10.2f, .224f, -5.4f),
        new Vector3(-9.4f, .224f, 5.5f), new Vector3(-7.0f, .224f, 7.0f),
        new Vector3(7.5f, .224f, 5.8f), new Vector3(10.0f, .224f, 7.0f)
      };
      for (int i = 0; i < bushes.Length; i++)
      {
        var shadow = MeshObject("IslandBushShadow_" + i, root, blob, mats["Shadow"]);
        shadow.transform.localPosition = bushes[i];
        shadow.transform.localRotation = Quaternion.Euler(0f, 17f * i, 0f);
        shadow.transform.localScale = new Vector3(.72f + (i % 3) * .12f, .01f, .34f + (i % 2) * .08f);
      }
    }

    private static void BuildPumpkinGardens(Transform root, Mesh blob, Dictionary<string, Material> mats)
    {
      var gardens = new[]
      {
        new Vector3(-7.7f, .265f, -6.0f),
        new Vector3(7.5f, .275f, 6.1f)
      };
      for (int g = 0; g < gardens.Length; g++)
      {
        var garden = MeshObject("PumpkinGardenSoil_" + g, root, blob, mats["Soil"]);
        garden.transform.localPosition = gardens[g];
        garden.transform.localRotation = Quaternion.Euler(0f, g == 0 ? -7f : 9f, 0f);
        garden.transform.localScale = new Vector3(2.65f, .024f, 1.28f);
        CreateFence(root, "PumpkinGardenFence_" + g,
          gardens[g] + new Vector3(0f, .04f, g == 0 ? -.98f : .98f), 4.6f, mats["Wood"]);
        for (int row = 0; row < 2; row++)
          for (int col = 0; col < 4; col++)
          {
            float x = gardens[g].x - 1.65f + col * 1.08f + (row % 2) * .25f;
            float z = gardens[g].z - .42f + row * .82f;
            CreatePumpkin(root, blob, new Vector3(x, .45f, z), .72f + ((row + col) % 3) * .08f, mats, row + col + g);
          }
        // Small companion leaves break up the repeated round silhouettes.
        for (int i = 0; i < 4; i++)
        {
          var leaf = MeshObject("PumpkinLeaf_" + g + "_" + i, root, blob, mats[i % 2 == 0 ? "LeafGreen" : "LeafOlive"]);
          leaf.transform.localPosition = gardens[g] + new Vector3(-1.3f + i * .82f, .43f, .18f + (i % 2) * .42f);
          leaf.transform.localRotation = Quaternion.Euler(0f, 23f * i, 0f);
          leaf.transform.localScale = new Vector3(.28f, .06f, .16f);
        }
      }
    }

    private static void CreatePumpkin(Transform root, Mesh blob, Vector3 position, float scale,
      Dictionary<string, Material> mats, int variant)
    {
      var pumpkin = MeshObject("Pumpkin_" + variant, root, blob, mats[variant % 3 == 0 ? "PumpkinLight" : "Pumpkin"]);
      pumpkin.transform.localPosition = position;
      pumpkin.transform.localRotation = Quaternion.Euler(0f, variant * 31f, 0f);
      pumpkin.transform.localScale = new Vector3(.56f * scale, .42f * scale, .56f * scale);
      var stem = Primitive("PumpkinStem", PrimitiveType.Cylinder, pumpkin.transform,
        new Vector3(0f, .47f, 0f), new Vector3(.065f, .13f, .065f), mats["Trunk"]);
      stem.transform.localRotation = Quaternion.Euler(0f, 0f, variant % 2 == 0 ? -8f : 8f);
    }

    private static void BuildReferenceProps(Transform root, Dictionary<string, Material> mats)
    {
      CreateChest(root, new Vector3(7.1f, .30f, 5.8f), mats);
      CreateSign(root, new Vector3(-7.0f, .28f, -1.8f), mats);
      for (int i = 0; i < 18; i++)
      {
        float angle = i * 2.399f + .4f;
        float radiusX = 7.4f + (i % 3) * 1.6f;
        float radiusZ = 7.0f + (i % 4) * 1.25f;
        float x = Mathf.Cos(angle) * radiusX;
        float z = Mathf.Sin(angle) * radiusZ;
        if (Mathf.Abs(z) < 4.9f) z = Mathf.Sign(z == 0f ? 1f : z) * 5.15f;
        CreateDaisy(root, "ReferenceFlower_" + i, new Vector3(x, .43f, z), mats);
      }
    }

    private static void CreateDaisy(Transform root, string name, Vector3 position, Dictionary<string, Material> mats)
    {
      var flower = new GameObject(name).transform;
      flower.SetParent(root, false);
      flower.localPosition = position;
      for (int i = 0; i < 5; i++)
      {
        float angle = i * Mathf.PI * 2f / 5f;
        Primitive("Petal_" + i, PrimitiveType.Sphere, flower,
          new Vector3(Mathf.Cos(angle) * .095f, 0f, Mathf.Sin(angle) * .095f),
          new Vector3(.085f, .028f, .085f), mats["Flower"]);
      }
      Primitive("Center", PrimitiveType.Sphere, flower, new Vector3(0f, .018f, 0f),
        new Vector3(.055f, .035f, .055f), mats["ChestGold"]);
    }

    private static void CreateChest(Transform root, Vector3 position, Dictionary<string, Material> mats)
    {
      var chest = new GameObject("ReferenceChest").transform;
      chest.SetParent(root, false); chest.localPosition = position; chest.localRotation = Quaternion.Euler(0f, -12f, 0f);
      chest.localScale = Vector3.one * 1.18f;
      Primitive("ChestBody", PrimitiveType.Cube, chest, new Vector3(0f, .30f, 0f), new Vector3(.90f, .42f, .62f), mats["Chest"]);
      var lid = Primitive("ChestLid", PrimitiveType.Cube, chest, new Vector3(0f, .66f, .18f), new Vector3(.94f, .10f, .58f), mats["Chest"]);
      lid.transform.localRotation = Quaternion.Euler(-62f, 0f, 0f);
      Primitive("ChestInner", PrimitiveType.Cube, chest, new Vector3(0f, .64f, .12f), new Vector3(.78f, .22f, .08f), mats["ChestInner"]);
      Primitive("ChestBand", PrimitiveType.Cube, chest, new Vector3(0f, .51f, -.33f), new Vector3(.09f, .46f, .045f), mats["ChestGold"]);
      Primitive("ChestLock", PrimitiveType.Cube, chest, new Vector3(0f, .49f, -.37f), new Vector3(.16f, .16f, .06f), mats["ChestGold"]);
    }

    private static void CreateSign(Transform root, Vector3 position, Dictionary<string, Material> mats)
    {
      var sign = new GameObject("ReferenceSign").transform;
      sign.SetParent(root, false); sign.localPosition = position; sign.localRotation = Quaternion.Euler(0f, 14f, 0f);
      Primitive("SignPost", PrimitiveType.Cylinder, sign, new Vector3(0f, .54f, 0f), new Vector3(.07f, .54f, .07f), mats["Wood"]);
      Primitive("SignBoard", PrimitiveType.Cube, sign, new Vector3(0f, 1.08f, 0f), new Vector3(1.05f, .44f, .08f), mats["Sign"]);
      Primitive("SignTop", PrimitiveType.Cube, sign, new Vector3(0f, 1.33f, 0f), new Vector3(1.12f, .07f, .10f), mats["Wood"]);
      for (int i = 0; i < 3; i++)
      {
        var line = Primitive("SignInk_" + i, PrimitiveType.Cube, sign,
          new Vector3(-.27f + i * .05f, 1.08f + (i % 2) * .07f, -.055f),
          new Vector3(.48f, .025f, .012f), mats["SignInk"]);
        line.transform.localRotation = Quaternion.Euler(0f, 0f, i == 1 ? -8f : 6f);
      }
    }

    private static void CreateFence(Transform root, string name, Vector3 position, float length, Material material)
    {
      var fence = new GameObject(name).transform; fence.SetParent(root, false); fence.localPosition = position;
      int posts = Mathf.Max(2, Mathf.RoundToInt(length / 1.3f) + 1);
      for (int i = 0; i < posts; i++)
      {
        float x = Mathf.Lerp(-length * .5f, length * .5f, i / (float)(posts - 1));
        Primitive("Post_" + i, PrimitiveType.Cylinder, fence, new Vector3(x, .35f, 0), new Vector3(.07f, .36f, .07f), material);
      }
      Primitive("Rail_Low", PrimitiveType.Cube, fence, new Vector3(0, .28f, 0), new Vector3(length, .08f, .08f), material);
      Primitive("Rail_High", PrimitiveType.Cube, fence, new Vector3(0, .67f, 0), new Vector3(length, .08f, .08f), material);
    }

    private static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material material)
    {
      var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
      go.transform.SetParent(parent, false);
      go.GetComponent<MeshFilter>().sharedMesh = mesh;
      go.GetComponent<MeshRenderer>().sharedMaterial = material;
      return go;
    }

    private static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
      var go = GameObject.CreatePrimitive(type);
      go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
      var renderer = go.GetComponent<Renderer>(); if (renderer != null) renderer.sharedMaterial = material;
      var collider = go.GetComponent<Collider>(); if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
      return go;
    }

    private static void DeleteAsset(string path)
    {
      if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null) AssetDatabase.DeleteAsset(path);
    }
  }
}
