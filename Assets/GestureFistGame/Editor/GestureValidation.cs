using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;
using UnityEditor.Build.Reporting;
namespace GestureFistGame.Editor
{
  [InitializeOnLoad]
  public static class GestureValidation
  {
    static GestureValidation(){EditorApplication.update+=Poll;}
    public static void Run(){Begin(false);}
    public static void Preview(){Begin(true);}
    private static void Begin(bool preview)
    {
      EditorSceneManager.OpenScene(BuildGestureFistScene.OutputScene,OpenSceneMode.Single);
      var tracker=Object.FindObjectOfType<MediaPipeTracker>();tracker.autoStart=preview;
      Object.FindObjectOfType<FistGestureInput>().externalTestInput=!preview;
      var test=new GameObject("Runtime validation ONLY").AddComponent<GestureRuntimeChecks>();test.previewOnly=preview;
      SessionState.SetBool("GF.Validate",true);SessionState.SetBool("GF.Exiting",false);
      SessionState.SetFloat("GF.Start",(float)EditorApplication.timeSinceStartup);
      EditorApplication.isPlaying=true;
    }
    private static void Poll()
    {
      if(!SessionState.GetBool("GF.Validate",false)) return;
      if(SessionState.GetBool("GF.Exiting",false))
      {
        if(!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
          SessionState.SetBool("GF.Validate",false);
          EditorApplication.Exit(SessionState.GetInt("GF.Exit",1));
        }
        return;
      }
      var probe=Object.FindObjectOfType<GestureRuntimeChecks>();
      if(EditorApplication.isPlaying && probe!=null && probe.Finished)
      {
        SessionState.SetInt("GF.Exit",probe.Failed?1:0);
        SessionState.SetBool("GF.Exiting",true);EditorApplication.isPlaying=false;
      }
      if(EditorApplication.timeSinceStartup-SessionState.GetFloat("GF.Start",0)>100)
      {
        File.WriteAllText("Artifacts/validation-timeout.txt","Runtime validation did not finish within 100 seconds.");
        SessionState.SetBool("GF.Validate",false);EditorApplication.Exit(2);
      }
    }
    public static void BuildWindows()
    {
      var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
        scenes=new[]{BuildGestureFistScene.OutputScene},
        target=BuildTarget.StandaloneWindows64,
        locationPathName="Builds/Windows/GestureFistGame.exe",
        options=BuildOptions.None
      });
      if(report.summary.result!=BuildResult.Succeeded) throw new System.Exception("Windows build failed");
      Debug.Log("GESTURE_WINDOWS_BUILD_OK");
    }
  }
}

