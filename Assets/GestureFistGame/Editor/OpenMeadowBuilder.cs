using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GestureFistGame.Editor
{
  /// <summary>
  /// Creates the serialized, low-poly meadow surrounding the fish river.
  /// The water lane is deliberately left empty (|z| &lt; 4.15) so the river,
  /// its banks, and its interactive surface remain the only geometry there.
  /// </summary>
  public static class OpenMeadowBuilder
  {
    private const string RootPath = "Assets/GestureFistGame/Art/GeneratedMeadow";
    private const string ShaderPath = "Assets/GestureFistGame/Shaders/Meadow.shader";

    private const float HalfWidth = 32f;
    private const float InnerZ = 4.0f;
    private const float OuterZ = 24f;
    private const int GroundXSegments = 32;
    private const int GroundZSegments = 24;
    private const int GrassChunksPerSide = 4;
    // A restrained cluster field gives the ground a readable stylized texture
    // while keeping the large color blocks calm. Four chunks keep each
    // serialized mesh comfortably below Unity's 16-bit vertex limit.
    // Broad, quiet ground is the primary shape. Small clusters provide
    // texture without turning the island into a wall of noisy spikes.
    private const int GrassCellsX = 72;
    private const int GrassCellsZ = 42;

    /// <summary>
    /// Builds both meadow banks below <paramref name="parent"/>.
    /// All meshes and materials are saved as project assets and referenced by
    /// scene MeshFilter/MeshRenderer components before this method returns.
    /// </summary>
    public static void Build(Transform parent)
    {
      if (parent == null) return;
      EnsureFolders();
      RemoveOldRoot(parent);

      var shader = LoadMeadowShader();
      if (shader == null)
      {
        Debug.LogError("OpenMeadowBuilder: GestureFistGame/Meadow shader could not be loaded.");
        return;
      }

      var groundMaterial = CreateGroundMaterial(shader);
      var grassMaterial = CreateGrassMaterial(shader);
      var root = new GameObject("GeneratedMeadow").transform;
      root.SetParent(parent, false);

      BuildGroundBand(root, "MeadowGround_Near", -1f, groundMaterial);
      BuildGroundBand(root, "MeadowGround_Far", 1f, groundMaterial);
      // The reference ground is a quiet painted surface. Flowers, shrubs and
      // tree shadows provide the texture; a dense blade field reads as noise
      // in the overview and breaks the target art direction.
      // Keep the grass material asset available for compatibility, but do not
      // instantiate the old procedural blade meshes.

      AssetDatabase.SaveAssets();
      EditorUtility.SetDirty(root.gameObject);
    }

    private static void EnsureFolders()
    {
      Directory.CreateDirectory("Assets/GestureFistGame/Art");
      Directory.CreateDirectory(RootPath);
      AssetDatabase.Refresh();
    }

    private static void RemoveOldRoot(Transform parent)
    {
      var old = parent.Find("GeneratedMeadow");
      if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
    }

    private static Shader LoadMeadowShader()
    {
      var shader = Shader.Find("GestureFistGame/Meadow");
      if (shader == null) shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
      return shader;
    }

    private static Material CreateGroundMaterial(Shader shader)
    {
      const string path = RootPath + "/MeadowGround.mat";
      DeleteAssetIfPresent(path);
      var material = new Material(shader) { name = "MeadowGround" };
      material.SetFloat("_IsGrass", 0f);
      // Reference palette: warm, low-saturation yellow-green meadow with a
      // slightly darker contact band beside the water.
      material.SetColor("_BaseColor", new Color(.49f, .56f, .27f, 1f));
      material.SetColor("_MeadowColor", new Color(.61f, .66f, .34f, 1f));
      material.SetColor("_WetColor", new Color(.42f, .46f, .23f, 1f));
      material.SetFloat("_NoiseScale", .24f);
      material.SetFloat("_NoiseStrength", .075f);
      material.SetFloat("_Smoothness", .12f);
      AssetDatabase.CreateAsset(material, path);
      return material;
    }

    private static Material CreateGrassMaterial(Shader shader)
    {
      const string path = RootPath + "/MeadowGrass.mat";
      DeleteAssetIfPresent(path);
      var material = new Material(shader) { name = "MeadowGrass" };
      material.enableInstancing = true;
      material.SetFloat("_IsGrass", 1f);
      material.SetColor("_RootColor", new Color(.30f, .39f, .14f, 1f));
      material.SetColor("_TipColor", new Color(.68f, .76f, .27f, 1f));
      material.SetColor("_WetColor", new Color(.32f, .35f, .12f, 1f));
      material.SetColor("_BaseColor", new Color(.40f, .50f, .16f, 1f));
      material.SetColor("_MeadowColor", new Color(.70f, .76f, .30f, 1f));
      material.SetFloat("_NoiseScale", .18f);
      material.SetFloat("_NoiseStrength", .10f);
      material.SetFloat("_WindStrength", .032f);
      material.SetFloat("_WindSpeed", .78f);
      material.SetFloat("_WindScale", .38f);
      material.SetVector("_WindDirection", new Vector4(.82f, 0f, .57f, 0f));
      material.SetFloat("_Smoothness", .08f);
      AssetDatabase.CreateAsset(material, path);
      return material;
    }

    private static void BuildGroundBand(Transform root, string name, float side, Material material)
    {
      var mesh = new Mesh { name = name + "Mesh" };
      var vertices = new Vector3[(GroundXSegments + 1) * (GroundZSegments + 1)];
      var colors = new Color[vertices.Length];
      var uvs = new Vector2[vertices.Length];
      var triangles = new int[GroundXSegments * GroundZSegments * 6];
      for (int z = 0; z <= GroundZSegments; z++)
      {
        float t = z / (float)GroundZSegments;
        float worldZ = side * Mathf.Lerp(InnerZ, OuterZ, t);
        for (int x = 0; x <= GroundXSegments; x++)
        {
          float u = x / (float)GroundXSegments;
          float worldX = Mathf.Lerp(-HalfWidth, HalfWidth, u);
          int index = z * (GroundXSegments + 1) + x;
          float noise = SmoothNoise(worldX * .075f + side * 13.4f, worldZ * .075f);
          float edgeSlope = Mathf.SmoothStep(0f, 1f, t);
          // Keep the terrain almost planar at the camera distance. A small
          // lift at the outer edge is enough for contact shadows; larger
          // per-vertex changes create distracting diagonal facets.
          float height = .18f + edgeSlope * .10f + (noise - .5f) * .018f;
          height += Mathf.Sin(worldX * .13f + side * .7f) * .004f;
          vertices[index] = new Vector3(worldX, height, worldZ);
          // Keep the serialized mesh colors stable. The old per-vertex noise
          // was interpolated across each large quad and showed up as diagonal
          // wedges in the overview. Per-pixel shader noise supplies enough
          // painterly variation without exposing the grid.
          float wet = 1f - Mathf.SmoothStep(0f, .22f, t);
          colors[index] = new Color(wet, 1f, edgeSlope, 1f);
          uvs[index] = new Vector2(worldX * .055f, worldZ * .055f);
        }
      }
      FillGridTriangles(triangles, GroundXSegments, GroundZSegments);
      // The near bank is generated in the reverse Z direction. Flip its
      // winding so both banks keep upward-facing normals and the same soft
      // low-poly lighting instead of one side becoming a dark slab.
      if (side < 0f)
      {
        for (int i = 0; i < triangles.Length; i += 3)
        {
          int swap = triangles[i + 1];
          triangles[i + 1] = triangles[i + 2];
          triangles[i + 2] = swap;
        }
      }
      mesh.vertices = vertices;
      mesh.colors = colors;
      mesh.uv = uvs;
      mesh.triangles = triangles;
      mesh.RecalculateNormals();
      mesh.RecalculateBounds();
      var path = RootPath + "/" + name + ".asset";
      DeleteAssetIfPresent(path);
      AssetDatabase.CreateAsset(mesh, path);
      var ground = CreateMeshObject(name, root, mesh, material, false);
      ground.AddComponent<MeshCollider>().sharedMesh = mesh;
      ground.AddComponent<FishGroundSurface>();
    }

    private static void BuildGrassBands(Transform root, float side, Material material)
    {
      for (int chunk = 0; chunk < GrassChunksPerSide; chunk++)
      {
        var mesh = BuildGrassMesh(side, chunk);
        if (mesh == null) continue;
        string sideName = side < 0f ? "Near" : "Far";
        string name = "MeadowGrass_" + sideName + "_" + chunk.ToString("00");
        var path = RootPath + "/" + name + ".asset";
        DeleteAssetIfPresent(path);
        AssetDatabase.CreateAsset(mesh, path);
        CreateMeshObject(name, root, mesh, material, true);
      }
    }

    private static Mesh BuildGrassMesh(float side, int chunk)
    {
      // Four x chunks keep each serialized mesh below the 16-bit vertex limit.
      int startX = Mathf.FloorToInt(chunk * GrassCellsX / (float)GrassChunksPerSide);
      int endX = Mathf.FloorToInt((chunk + 1) * GrassCellsX / (float)GrassChunksPerSide);
      int cells = Mathf.Max(1, endX - startX) * GrassCellsZ;
      var vertices = new List<Vector3>(cells * 8);
      var colors = new List<Color>(cells * 8);
      var uvs = new List<Vector2>(cells * 8);
      var triangles = new List<int>(cells * 12);
      for (int zCell = 0; zCell < GrassCellsZ; zCell++)
      {
        float zT = (zCell + .37f) / GrassCellsZ;
        float z = side * Mathf.Lerp(InnerZ + .06f, OuterZ - .18f, zT);
        for (int xCell = startX; xCell < endX; xCell++)
        {
          float xT = (xCell + .43f) / GrassCellsX;
          float x = Mathf.Lerp(-HalfWidth, HalfWidth, xT);
          int seed = (int)(xCell * 928371 + zCell * 364479 + (side < 0f ? 17 : 31));
          if (Hash01(seed + 61) > .78f) continue;
          float jitterX = Hash01(seed) * .34f - .17f;
          float jitterZ = Hash01(seed + 7) * .30f - .15f;
          x += jitterX;
          z += side * jitterZ;
          float playerZ = side * 4.68f;
          if ((new Vector2(x, z - playerZ)).sqrMagnitude < .9f * .9f) continue;

          float terrainY = TerrainHeight(x, z, side);
          float height = Mathf.Lerp(.07f, .22f, Hash01(seed + 11));
          float width = Mathf.Lerp(.022f, .052f, Hash01(seed + 19));
          float leanAmount = Mathf.Lerp(.018f, .065f, Hash01(seed + 23));
          float leanX = (Hash01(seed + 29) * 2f - 1f) * leanAmount;
          float leanZ = (Hash01(seed + 37) * 2f - 1f) * leanAmount;
          float angle = Hash01(seed + 43) * Mathf.PI * 2f;
          Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
          Vector3 perp = new Vector3(-dir.z, 0f, dir.x) * width;
          Vector3 root = new Vector3(x, terrainY + .003f, z);
          Vector3 top = root + new Vector3(leanX, height, leanZ);
          AddGrassQuad(vertices, colors, uvs, triangles, root, top, perp, Hash01(seed + 47));
          Vector3 crossDir = new Vector3(-dir.z, 0f, dir.x);
          Vector3 crossPerp = new Vector3(-crossDir.z, 0f, crossDir.x) * width;
          AddGrassQuad(vertices, colors, uvs, triangles, root, top, crossPerp, Hash01(seed + 53));
        }
      }
      if (vertices.Count == 0) return null;
      var mesh = new Mesh { name = "MeadowGrassMesh" };
      mesh.SetVertices(vertices);
      mesh.SetColors(colors);
      mesh.SetUVs(0, uvs);
      mesh.SetTriangles(triangles, 0, true);
      mesh.RecalculateNormals();
      mesh.RecalculateBounds();
      return mesh;
    }

    private static void AddGrassQuad(List<Vector3> vertices, List<Color> colors, List<Vector2> uvs, List<int> triangles,
      Vector3 root, Vector3 top, Vector3 halfWidth, float variation)
    {
      int first = vertices.Count;
      var rootL = root - halfWidth;
      var rootR = root + halfWidth;
      var topL = top - halfWidth * .28f;
      var topR = top + halfWidth * .28f;
      float tint = Mathf.Lerp(.78f, 1.12f, variation);
      // Store a root-to-tip gradient in the mesh, following the open-source
      // generator's useful pattern: the shader gets both stylized color depth
      // and baked contact darkening without a texture lookup.
      Color rootColor = new Color(tint * .58f, tint * .66f, tint * .48f, 1f);
      Color tipColor = new Color(tint, tint * .98f, tint * .86f, 1f);
      vertices.Add(rootL); vertices.Add(rootR); vertices.Add(topL); vertices.Add(topR);
      colors.Add(rootColor); colors.Add(rootColor); colors.Add(tipColor); colors.Add(tipColor);
      uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(0f, 1f)); uvs.Add(new Vector2(1f, 1f));
      triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 1);
      triangles.Add(first + 1); triangles.Add(first + 2); triangles.Add(first + 3);
    }

    private static float TerrainHeight(float x, float z, float side)
    {
      float t = Mathf.InverseLerp(InnerZ, OuterZ, Mathf.Abs(z));
      float noise = SmoothNoise(x * .075f + side * 13.4f, z * .075f);
      return .18f + Mathf.SmoothStep(0f, 1f, t) * .10f + (noise - .5f) * .018f + Mathf.Sin(x * .13f + side * .7f) * .004f;
    }

    private static void FillGridTriangles(int[] triangles, int xSegments, int zSegments)
    {
      int t = 0;
      int row = xSegments + 1;
      for (int z = 0; z < zSegments; z++) for (int x = 0; x < xSegments; x++)
      {
        int a = z * row + x, b = a + 1, c = a + row, d = c + 1;
        triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
        triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
      }
    }

    private static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, Material material, bool castShadows)
    {
      var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
      go.transform.SetParent(parent, false);
      go.GetComponent<MeshFilter>().sharedMesh = mesh;
      var renderer = go.GetComponent<MeshRenderer>();
      renderer.sharedMaterial = material;
      renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
      renderer.receiveShadows = true;
      return go;
    }

    private static float SmoothNoise(float x, float y)
    {
      // Unity's PerlinNoise is deterministic and smooth enough for broad
      // meadow variation; this helper keeps geometry and shader variation in
      // the same low-frequency range without per-vertex random popping.
      return Mathf.PerlinNoise(x, y);
    }

    private static float Hash01(int value)
    {
      unchecked
      {
        uint x = (uint)value;
        x ^= x >> 16;
        x *= 0x7feb352d;
        x ^= x >> 15;
        x *= 0x846ca68b;
        x ^= x >> 16;
        return (x & 0x00ffffff) / 16777215f;
      }
    }

    private static void DeleteAssetIfPresent(string path)
    {
      if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null) AssetDatabase.DeleteAsset(path);
    }
  }
}
