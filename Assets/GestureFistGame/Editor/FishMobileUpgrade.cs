using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
namespace GestureFistGame.Editor
{
  public static class FishMobileUpgrade
  {
    private const string Folder = "Assets/GestureFistGame/Art/Scardinius/";
    public static void AddFishModel(Transform parent)
    {
      var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Scardinius.fbx");
      if (asset == null) throw new Exception("Scardinius model missing");
      var model = Object.Instantiate(asset, parent, false);
      model.name = "ScardiniusFishModel";
      // Blender's Y axis is the fish's length; FBX converts it to Unity Z.
      model.transform.localPosition = Vector3.zero;
      model.transform.localRotation = Quaternion.Euler(0, 180, 0);
      var renderers = model.GetComponentsInChildren<Renderer>(true);
      var bounds = renderers[0].bounds;
      foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
      model.transform.localScale *= 1.35f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
      bounds = renderers[0].bounds;
      foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
      model.transform.position += parent.position - bounds.center;
      var path = Folder + "Scardinius.mat";
      var material = AssetDatabase.LoadAssetAtPath<Material>(path);
      if (material == null)
      {
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        AssetDatabase.CreateAsset(material, path);
      }
      material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Fish.png"));
      material.SetColor("_BaseColor", Color.white);
      material.SetFloat("_Smoothness", .22f);
      material.SetFloat("_Cull", 0f);
      foreach (var renderer in renderers)
      {
        renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
        var skinned = renderer as SkinnedMeshRenderer;
        if (skinned != null) skinned.updateWhenOffscreen = true;
      }
      var box = parent.GetComponent<BoxCollider>();
      if (box != null) { box.center = Vector3.zero; box.size = parent.InverseTransformVector(bounds.size); }
      EditorUtility.SetDirty(material);
    }

    [MenuItem("Gesture Fist Game/更新鱼模型和手机界面")]
    public static void Apply()
    {
      EditorSceneManager.OpenScene(BuildFishGameScene.OutputScene, OpenSceneMode.Single);
      var spawner = Object.FindObjectOfType<FishSpawner>();
      spawner.spawnX = 3.55f;
      FishSpeciesCatalog.Replace(spawner);
      foreach (var mesh in Object.FindObjectsOfType<MeshFilter>())
      {
        if (!mesh.name.StartsWith("MeadowGround", StringComparison.Ordinal)) continue;
        var collider = mesh.GetComponent<MeshCollider>();
        if (collider == null) collider = mesh.gameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh.sharedMesh;
        if (mesh.GetComponent<FishGroundSurface>() == null) mesh.gameObject.AddComponent<FishGroundSurface>();
      }
      foreach (var renderer in Object.FindObjectsOfType<MeshRenderer>())
      {
        if (!renderer.name.StartsWith("SideBank_", StringComparison.Ordinal)) continue;
        if (renderer.GetComponent<BoxCollider>() == null) renderer.gameObject.AddComponent<BoxCollider>();
        if (renderer.GetComponent<FishGroundSurface>() == null) renderer.gameObject.AddComponent<FishGroundSurface>();
      }
      EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
      AssetDatabase.SaveAssets();
      BuildFishGameScene.ValidateScene();
      Debug.Log("FISH_MOBILE_UPGRADE_OK");
    }
    public static void ApplyAndBuild()
    {
      Apply();
      BuildFishGameScene.BuildWindows();
    }
  }
}

