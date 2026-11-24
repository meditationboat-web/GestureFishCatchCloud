using UnityEngine;

namespace GestureFistGame
{
  /// <summary>Builds a shallow riverbed and sloped, irregular wet banks at runtime.</summary>
  public static class RiverEnvironmentEnhancer
  {
    public static void Ensure(Transform arena)
    {
      if (arena == null || arena.Find("RiverBed_RealDepth") != null) return;
      var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
      if (lit == null) return;
      var bed = new Material(lit) { name = "Riverbed Dark Silt" };
      bed.color = new Color(.025f, .11f, .14f, 1f);
      if (bed.HasProperty("_Smoothness")) bed.SetFloat("_Smoothness", .48f);
      var wet = new Material(lit) { name = "Wet Mud Shore" };
      wet.color = new Color(.20f, .25f, .16f, 1f);
      if (wet.HasProperty("_Smoothness")) wet.SetFloat("_Smoothness", .18f);
      var grass = new Material(lit) { name = "Natural Grass Shore" };
      grass.color = new Color(.24f, .40f, .19f, 1f);
      if (grass.HasProperty("_Smoothness")) grass.SetFloat("_Smoothness", .08f);
      CreateBed(arena, bed);
      CreateShore(arena, "NaturalShore_Left", -1f, wet, grass);
      CreateShore(arena, "NaturalShore_Right", 1f, wet, grass);
      CreateBankStones(arena, lit);
    }

    private static void CreateBed(Transform parent, Material material)
    {
      const int nx = 28, nz = 24;
      var mesh = new Mesh { name = "RiverbedDepthMesh" };
      var vertices = new Vector3[(nx + 1) * (nz + 1)];
      for (int z = 0; z <= nz; z++) for (int x = 0; x <= nx; x++)
      {
        float u = x / (float)nx, v = z / (float)nz;
        float px = Mathf.Lerp(-4.2f, 4.2f, u);
        float pz = Mathf.Lerp(-4.0f, 4.0f, v);
        float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
        float uneven = Mathf.Sin(px * 1.7f + pz * .8f) * .025f + Mathf.Cos(pz * 1.2f - px * .45f) * .018f;
        vertices[z * (nx + 1) + x] = new Vector3(px, -.38f + Mathf.Clamp01(edge * 8f) * .10f + uneven, pz);
      }
      BuildGrid(mesh, vertices, nx, nz);
      CreateMeshObject("RiverBed_RealDepth", parent, mesh, material);
    }

    private static void CreateShore(Transform parent, string name, float side, Material wet, Material grass)
    {
      const int samples = 32;
      var wetMesh = new Mesh { name = name + "WetSlope" };
      var grassMesh = new Mesh { name = name + "GrassSlope" };
      var wetVertices = new Vector3[(samples + 1) * 2];
      var grassVertices = new Vector3[(samples + 1) * 2];
      for (int i = 0; i <= samples; i++)
      {
        float z = Mathf.Lerp(-4.05f, 4.05f, i / (float)samples);
        float waviness = Mathf.Sin(z * 1.7f + side * .8f) * .08f + Mathf.Sin(z * 3.9f) * .035f;
        float inner = 4.12f + waviness;
        float outer = 4.78f + waviness * .45f;
        wetVertices[i * 2] = new Vector3(side * inner, .055f + Mathf.Abs(waviness) * .03f, z);
        wetVertices[i * 2 + 1] = new Vector3(side * (inner + .34f), .17f + Mathf.Abs(waviness) * .05f, z);
        grassVertices[i * 2] = wetVertices[i * 2 + 1];
        grassVertices[i * 2 + 1] = new Vector3(side * outer, .34f + Mathf.Abs(waviness) * .10f, z);
      }
      // The left bank is authored from the inner edge toward the outside
      // with decreasing X. Reverse its winding so both banks keep an upward
      // facing normal under the URP Lit material's default back-face culling.
      bool flipWinding = side < 0f;
      BuildStrip(wetMesh, wetVertices, samples, flipWinding);
      BuildStrip(grassMesh, grassVertices, samples, flipWinding);
      CreateMeshObject(name + "_Wet", parent, wetMesh, wet);
      CreateMeshObject(name + "_Grass", parent, grassMesh, grass);
    }

    private static void CreateBankStones(Transform parent, Shader shader)
    {
      var stone = new Material(shader) { name = "Riverbank Stone" };
      stone.color = new Color(.25f, .29f, .27f, 1f);
      if (stone.HasProperty("_Smoothness")) stone.SetFloat("_Smoothness", .22f);
      for (int sideIndex = 0; sideIndex < 2; sideIndex++)
      {
        float side = sideIndex == 0 ? -1f : 1f;
        for (int i = 0; i < 7; i++)
        {
          float z = Mathf.Lerp(-3.7f, 3.7f, i / 6f) + Mathf.Sin(i * 2.1f + side) * .10f;
          var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
          go.name = "NaturalBankStone_" + sideIndex + "_" + i;
          go.transform.SetParent(parent, false);
          go.transform.localPosition = new Vector3(side * (4.35f + (i % 3) * .09f), .18f + (i % 2) * .04f, z);
          go.transform.localScale = new Vector3(.24f + (i % 3) * .08f, .12f + (i % 2) * .05f, .20f + (i % 2) * .06f);
          go.transform.localRotation = Quaternion.Euler(i * 13f, i * 27f, i * 9f);
          go.GetComponent<Renderer>().sharedMaterial = stone;
          var collider = go.GetComponent<Collider>();
          // This helper is called by the editor scene builder as well as at
          // runtime. DestroyImmediate keeps the editor build free of the
          // repeated "Destroy may not be called from edit mode" warnings.
          if (collider != null) Object.DestroyImmediate(collider);
        }
      }
    }

    private static void BuildGrid(Mesh mesh, Vector3[] vertices, int nx, int nz)
    {
      var triangles = new int[nx * nz * 6]; int t = 0;
      for (int z = 0; z < nz; z++) for (int x = 0; x < nx; x++)
      {
        int a = z * (nx + 1) + x, b = a + 1, c = a + nx + 1, d = c + 1;
        triangles[t++] = a; triangles[t++] = c; triangles[t++] = b; triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
      }
      mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
    }

    private static void BuildStrip(Mesh mesh, Vector3[] vertices, int samples, bool flipWinding = false)
    {
      var triangles = new int[samples * 6]; int t = 0;
      for (int i = 0; i < samples; i++)
      {
        int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
        if (!flipWinding)
        {
          triangles[t++] = a; triangles[t++] = c; triangles[t++] = b; triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
        }
        else
        {
          triangles[t++] = a; triangles[t++] = b; triangles[t++] = c; triangles[t++] = b; triangles[t++] = d; triangles[t++] = c;
        }
      }
      mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
    }

    private static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, Material material)
    {
      var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
      go.transform.SetParent(parent, false);
      go.GetComponent<MeshFilter>().sharedMesh = mesh;
      go.GetComponent<MeshRenderer>().sharedMaterial = material;
      return go;
    }
  }
}
