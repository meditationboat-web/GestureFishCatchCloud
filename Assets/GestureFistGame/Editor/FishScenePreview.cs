using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using System.Reflection;
using GestureFistGame;

namespace GestureFistGame.Editor
{
  /// <summary>Captures an editor-only overview for visual acceptance.</summary>
  public static class FishScenePreview
  {
    public static void CaptureOverview()
    {
      var scene = EditorSceneManager.OpenScene(BuildFishGameScene.OutputScene, OpenSceneMode.Single);
      var camera = Object.FindObjectOfType<Camera>();
      if (camera == null) throw new System.Exception("Fish preview camera missing");
      foreach (var canvas in Object.FindObjectsOfType<Canvas>()) canvas.gameObject.SetActive(false);
      PrepareRuntimeVisuals();
      camera.transform.position = new Vector3(9.4f, 13.8f, -13.6f);
      camera.transform.LookAt(new Vector3(0f, .45f, 0f));
      camera.orthographic = true;
      camera.orthographicSize = 8.05f;
      Directory.CreateDirectory("Artifacts");
      Capture(camera, "Artifacts/horizontal-net-idle.png", 1600, 900);
      var net = Object.FindObjectOfType<FishNetController>();
      if (net != null)
      {
        if (net.submergedVisual != null) net.submergedVisual.gameObject.SetActive(false);
        if (net.visual != null) net.visual.gameObject.SetActive(true);
        net.transform.localPosition = new Vector3(0, net.raisedHeight, 0);
        net.RefreshHandLinkedVisualForPreview();
      }
      Capture(camera, "Artifacts/horizontal-net-raised.png", 1600, 900);
      Capture(camera, "Artifacts/open-meadow-preview.png", 1600, 900);
      Debug.Log("FISH_OPEN_MEADOW_PREVIEW_OK scene=" + scene.path);
    }

    private static void PrepareRuntimeVisuals()
    {
      // Editor screenshots do not run Awake/Start. Initialize only the
      // existing runtime components so the preview uses the same water mesh,
      // material and idle net visibility as play mode.
      var water = Object.FindObjectOfType<InteractiveWaterSurface>();
      InvokeAwakeIfNeeded(water, "_mesh");
      var net = Object.FindObjectOfType<FishNetController>();
      InvokeAwakeIfNeeded(net, null);
      QualitySettings.shadows = ShadowQuality.All;
    }

    private static void InvokeAwakeIfNeeded(MonoBehaviour component, string initializedField)
    {
      if (component == null) return;
      if (!string.IsNullOrEmpty(initializedField))
      {
        var field = component.GetType().GetField(initializedField, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null && field.GetValue(component) != null) return;
      }
      var awake = component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
      if (awake != null) awake.Invoke(component, null);
    }

    private static void Capture(Camera camera, string path, int width, int height)
    {
      var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
      rt.Create();
      var previousTarget = camera.targetTexture;
      var previousActive = RenderTexture.active;
      camera.targetTexture = rt;
      camera.Render();
      RenderTexture.active = rt;
      var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
      tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
      tex.Apply(false, false);
      File.WriteAllBytes(path, tex.EncodeToPNG());
      camera.targetTexture = previousTarget;
      RenderTexture.active = previousActive;
      Object.DestroyImmediate(tex);
      rt.Release();
      Object.DestroyImmediate(rt);
    }
  }
}
