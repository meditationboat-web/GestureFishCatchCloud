using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace GestureFistGame.Editor
{
  public static class FishCounterIcons
  {
    public static void Build()
    {
      EditorSceneManager.OpenScene(BuildFishGameScene.OutputScene, OpenSceneMode.Single);
      var spawner = Object.FindObjectOfType<FishSpawner>();
      const string folder = "Assets/GestureFistGame/Resources/FishCounterIcons";
      Directory.CreateDirectory(folder);
      for (int i=0;i<spawner.prototypes.Length;i++)
      {
        var preview = new PreviewRenderUtility();
        var model = Object.Instantiate(spawner.prototypes[i].transform.GetChild(0).gameObject);
        model.transform.position=Vector3.zero; model.transform.rotation=Quaternion.Euler(0,90,0);
        preview.AddSingleGO(model);
        var renderers=model.GetComponentsInChildren<Renderer>();
        var bounds=renderers[0].bounds;
        foreach(var r in renderers) bounds.Encapsulate(r.bounds);
        preview.camera.orthographic=true;
        preview.camera.orthographicSize=Mathf.Max(bounds.size.y*.65f,bounds.size.x/2.8f)*1.15f;
        preview.camera.transform.position=bounds.center+new Vector3(0,.25f,-5);
        preview.camera.transform.LookAt(bounds.center);
        preview.camera.clearFlags=CameraClearFlags.SolidColor;
        preview.camera.backgroundColor=Color.clear;
        preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=20;
        preview.lights[0].intensity=1.2f;preview.lights[0].transform.rotation=Quaternion.Euler(40,25,0);
        preview.lights[1].intensity=.8f;
        preview.ambientColor=new Color(.65f,.65f,.65f);
        preview.BeginStaticPreview(new Rect(0,0,384,192));
        preview.Render(true);
        var texture=preview.EndStaticPreview();
        var pixels = texture.GetPixels32();
        for (int pixel=0;pixel<pixels.Length;pixel++)
          if (pixels[pixel].r < 8 && pixels[pixel].g < 8 && pixels[pixel].b < 8) pixels[pixel].a=0;
        var rgba = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        rgba.SetPixels32(pixels); rgba.Apply();
        File.WriteAllBytes(folder+"/Fish"+i+".png",rgba.EncodeToPNG());
        Object.DestroyImmediate(rgba);Object.DestroyImmediate(texture);preview.Cleanup();
      }
      AssetDatabase.Refresh();
      for(int i=0;i<3;i++)
      {
        var importer=(TextureImporter)AssetImporter.GetAtPath(folder+"/Fish"+i+".png");
        importer.textureType=TextureImporterType.Sprite;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();
      }
      BuildFishGameScene.BuildWindows();
    }
  }
}

