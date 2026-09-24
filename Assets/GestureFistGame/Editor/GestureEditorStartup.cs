using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GestureFistGame.Editor
{
  [InitializeOnLoad]
  public static class GestureEditorStartup
  {
    private const string StageKey="GF.StartupCheck.Stage";
    private const string Report="Artifacts/editor-startup-recovery.txt";
    private static readonly BindingFlags StaticFlags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
    private static Type LogEntries=>typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntries",true);
    private static int ConsoleFlags=>(int)LogEntries.GetProperty("consoleFlags",StaticFlags).GetValue(null);
    private static int ErrorPauseMask
    {
      get
      {
        var type=typeof(EditorWindow).Assembly.GetTypes().First(t=>t.IsEnum && t.Name=="ConsoleFlags" && Enum.GetNames(t).Contains("ErrorPause"));
        return Convert.ToInt32(Enum.Parse(type,"ErrorPause"));
      }
    }
    static GestureEditorStartup() { EditorApplication.update+=Poll; }
    private static void Write(string value) { Directory.CreateDirectory("Artifacts");File.AppendAllText(Report,value+Environment.NewLine); }
    private static void DisableErrorPause()
    {
      int before=ConsoleFlags;
      LogEntries.GetMethod("SetConsoleFlag",StaticFlags).Invoke(null,new object[]{ErrorPauseMask,false});
      Write("Console flags "+before+" -> "+ConsoleFlags+"; Error Pause disabled; other flags preserved="+((before & ~ErrorPauseMask)==ConsoleFlags));
    }
    private static void FocusGame()
    {
      var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView",true);
      EditorWindow.GetWindow(type).Focus();
    }
    private static void ClosePackageBrowser()
    {
      foreach(var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
        if(window.GetType().Name=="PackageManagerWindow") window.Close();
    }

    [MenuItem("Gesture Fist Game/打开主场景并恢复鼠标游玩")]
    public static void Play()
    {
      // This explicit recovery action changes Error Pause only; errors stay visible in Console.
      DisableErrorPause();ClosePackageBrowser();
      if(!EditorApplication.isPlaying)
      {
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(BuildGestureFistScene.OutputScene,OpenSceneMode.Single);
        SessionState.SetBool("GF.Startup.FocusAfterPlay",true);
        EditorApplication.isPlaying=true;
      }
      else ResumeMouse();
    }
    private static void ResumeMouse()
    {
      EditorApplication.isPaused=false;Time.timeScale=1;
      var input=UnityEngine.Object.FindObjectOfType<FistGestureInput>();
      if(input!=null) { input.externalTestInput=false;input.UseMouse(); }
      FocusGame();
    }

    // This diagnostic reproduces the editor pause itself. Mouse gameplay is separately
    // covered by the runtime input replay; queued GUI events are not hardware mouse input.
    public static void VerifyAndRepair()
    {
      Directory.CreateDirectory("Artifacts");File.WriteAllText(Report,"Editor startup diagnosis\n");
      Write("Initial flags="+ConsoleFlags+"; Error Pause="+((ConsoleFlags & ErrorPauseMask)!=0));
      LogEntries.GetMethod("SetConsoleFlag",StaticFlags).Invoke(null,new object[]{ErrorPauseMask,true});
      Write("Temporarily enable Error Pause to reproduce the reported freeze; the repair will disable it.");
      ClosePackageBrowser();
      EditorSceneManager.OpenScene(BuildGestureFistScene.OutputScene,OpenSceneMode.Single);
      SessionState.SetBool("GF.StartupCheck.Failed",false);
      SessionState.SetFloat("GF.StartupCheck.Deadline",(float)EditorApplication.timeSinceStartup+50);
      SessionState.SetInt(StageKey,1);EditorApplication.isPlaying=true;
    }
    private static void Next(int stage,float delay)
    {
      SessionState.SetInt(StageKey,stage);
      SessionState.SetFloat("GF.StartupCheck.Next",(float)EditorApplication.timeSinceStartup+delay);
    }
    private static void Check(bool pass,string message)
    {
      Write((pass?"PASS ":"FAIL ")+message);
      if(!pass) SessionState.SetBool("GF.StartupCheck.Failed",true);
    }
    private static void Poll()
    {
      if(SessionState.GetBool("GF.Startup.FocusAfterPlay",false) && EditorApplication.isPlaying)
      {
        SessionState.SetBool("GF.Startup.FocusAfterPlay",false);ResumeMouse();
        SessionState.SetFloat("GF.Startup.LiveTime",Time.time);
        SessionState.SetFloat("GF.Startup.LiveCheck",(float)EditorApplication.timeSinceStartup+2);
      }
      float liveCheck=SessionState.GetFloat("GF.Startup.LiveCheck",0);
      if(liveCheck>0 && EditorApplication.timeSinceStartup>liveCheck)
      {
        SessionState.SetFloat("GF.Startup.LiveCheck",0);
        Write("LIVE_PLAY_READY playing="+EditorApplication.isPlaying+" paused="+EditorApplication.isPaused+" timeAdvanced="+(Time.time>SessionState.GetFloat("GF.Startup.LiveTime",0)+.5f));
      }
      int stage=SessionState.GetInt(StageKey,0);if(stage==0) return;
      if(EditorApplication.timeSinceStartup>SessionState.GetFloat("GF.StartupCheck.Deadline",0))
      { Write("FAIL startup diagnosis timed out");SessionState.SetInt(StageKey,0);EditorApplication.Exit(2);return; }
      if(stage==9)
      {
        if(!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
        { SessionState.SetInt(StageKey,0);EditorApplication.Exit(SessionState.GetBool("GF.StartupCheck.Failed",true)?1:0); }
        return;
      }
      if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup<SessionState.GetFloat("GF.StartupCheck.Next",0)) return;
      var input=UnityEngine.Object.FindObjectOfType<FistGestureInput>();
      switch(stage)
      {
        case 1:
          Next(2,.3f);
          Debug.LogError("STARTUP DIAGNOSTIC: intentional editor error to reproduce Console Error Pause. This is not a game exception.");
          break;
        case 2:
          Check(EditorApplication.isPaused,"editor error reproduces the pause while Error Pause is enabled");
          DisableErrorPause();ResumeMouse();
          SessionState.SetFloat("GF.StartupCheck.GameTime",Time.time);
          Next(3,1);break;
        case 3:
          Check(!EditorApplication.isPaused && Time.time-SessionState.GetFloat("GF.StartupCheck.GameTime",0)>.4f,"Play advances after recovery");
          Write("Game view resolution="+Screen.width+"x"+Screen.height+"; mode="+input.mouseMode+"; focus="+EditorWindow.focusedWindow?.GetType().Name);
          Check(input.mouseMode && !input.externalTestInput && input.Status.StartsWith("按住"),"normal mouse update loop is active in Game view");
          Next(4,.3f);
          Debug.LogError("STARTUP DIAGNOSTIC: intentional editor error after recovery; Console should log it without pausing.");
          break;
        case 4:
          Check(!EditorApplication.isPaused,"editor errors remain logged without freezing mouse play");
          Write("STARTUP_CHECK_DONE "+(SessionState.GetBool("GF.StartupCheck.Failed",false)?"FAIL":"PASS"));
          Next(9,0);EditorApplication.isPlaying=false;break;
      }
    }
  }
}
