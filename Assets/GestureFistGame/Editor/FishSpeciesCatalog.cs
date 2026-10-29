using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
namespace GestureFistGame.Editor
{
  public static class FishSpeciesCatalog
  {
    private const string Folder = "Assets/GestureFistGame/Art/FishSpecies/";
    public static void Replace(FishSpawner spawner)
    {
      var old = spawner.prototypes;
      var parent = old[0].transform.parent;
      var result = new FishTarget[3];
      string[] names = { "红眼鱼", "金鱼", "白斑狗鱼" };
      int[] points = { 1, 3, 5 };
      for (int i = 0; i < 3; i++)
      {
        var fish = Object.Instantiate(old[0], parent);
        fish.gameObject.SetActive(false);
        fish.name = names[i] + "_" + points[i] + "分";
        foreach (var child in fish.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
        fish.points = points[i]; fish.displayName = names[i];
        if (i == 0) FishMobileUpgrade.AddFishModel(fish.transform);
        else AddModel(fish.transform, i == 1 ? "Goldfish" : "Esox", i == 1 ? "fish.png" : "EsoxAtlas.png");
        result[i] = fish;
      }
      spawner.prototypes = result;
      foreach (var fish in old) if (fish != null) Object.DestroyImmediate(fish.gameObject);
    }
    private static void AddModel(Transform parent, string name, string texture)
    {
      var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".fbx");
      if (source == null) throw new Exception("Missing fish model " + name);
      var model = Object.Instantiate(source, parent, false);
      model.name = name + "Model";
      model.transform.localPosition = Vector3.zero;
      model.transform.localRotation = Quaternion.Euler(0, name == "Esox" ? 0 : 180, 0);
      var renderers = model.GetComponentsInChildren<Renderer>(true);
      var bounds = renderers[0].bounds;
      foreach (var r in renderers) bounds.Encapsulate(r.bounds);
      model.transform.localScale *= 1.35f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
      bounds = renderers[0].bounds;
      foreach (var r in renderers) bounds.Encapsulate(r.bounds);
      model.transform.position += parent.position - bounds.center;
      var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + name + ".mat");
      if (material == null)
      {
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        AssetDatabase.CreateAsset(material, Folder + name + ".mat");
      }
      material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + texture));
      material.SetColor("_BaseColor", Color.white); material.SetFloat("_Cull", 0f); material.SetFloat("_Smoothness", .2f);
      foreach (var r in renderers) r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
      var box = parent.GetComponent<BoxCollider>();
      box.center = Vector3.zero; box.size = parent.InverseTransformVector(bounds.size);
      EditorUtility.SetDirty(material);
    }
  }
}

