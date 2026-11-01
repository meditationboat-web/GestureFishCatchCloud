using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GestureFistGame.Editor
{
  public static class FishRiverUpgrade
  {
    [MenuItem("Gesture Fist Game/更新长河与两岸景观（不构建）")]
    public static void ApplyAndPreview()
    {
      var scene = EditorSceneManager.OpenScene(BuildFishGameScene.OutputScene);
      var water = UnityEngine.Object.FindObjectOfType<InteractiveWaterSurface>();
      if (water == null) throw new Exception("River surface missing");
      water.width = FishRiverLayout.Length;
      water.columns = 128;
      water.transform.localScale = new Vector3(FishRiverLayout.Length, .25f, 8f);
      FishRiverLayout.Apply();
      EditorSceneManager.MarkSceneDirty(scene);
      EditorSceneManager.SaveScene(scene);
      AssetDatabase.SaveAssets();
      if (Mathf.Abs(water.depth - 8f) > .01f) throw new Exception("Fishing river width changed");
      Debug.Log("FISH_RIVER_UPGRADE_PASS length=" + water.width + " depth=" + water.depth);
      FishScenePreview.CaptureOverview();
    }
  }
}
