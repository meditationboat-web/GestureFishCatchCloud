using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GestureFistGame.Editor
{
  /// <summary>Creates the small URP setup used by the fish scene.</summary>
  public static class ConfigureGestureFishURP
  {
    private const string Root = "Assets/GestureFistGame/Rendering";
    private const string PipelinePath = Root + "/GestureFishURP.asset";

    [MenuItem("Gesture Fist Game/配置 URP 渔场渲染")]
    public static void Run()
    {
      Directory.CreateDirectory(Root);
      AssetDatabase.Refresh();
      var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
      if (pipeline == null)
      {
        pipeline = UniversalRenderPipelineAsset.Create();
        pipeline.LoadBuiltinRendererData();
        AssetDatabase.CreateAsset(pipeline, PipelinePath);
      }
      pipeline.supportsCameraDepthTexture = true;
      pipeline.supportsCameraOpaqueTexture = true;
      pipeline.msaaSampleCount = 2;
      pipeline.renderScale = 1f;
      pipeline.supportsHDR = true;
      pipeline.shadowDistance = 45f;
      // URP 14 exposes these as read-only capabilities. The scene light's
      // LightShadows.Soft setting and the renderer asset control the actual
      // shadow mode; assigning the old mutable properties breaks compilation.
      GraphicsSettings.defaultRenderPipeline = pipeline;
      QualitySettings.renderPipeline = pipeline;
      EditorUtility.SetDirty(pipeline);
      AssetDatabase.SaveAssets();
      AssetDatabase.Refresh();
      Debug.Log("GESTURE_FISH_URP_CONFIGURED " + PipelinePath);
    }
  }
}
