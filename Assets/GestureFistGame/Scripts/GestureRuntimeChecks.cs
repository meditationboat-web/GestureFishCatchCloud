using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Vision.HandLandmarker;
using MPImage=Mediapipe.Image;
namespace GestureFistGame
{
  // Only attached to an unsaved validation scene by the Editor runner.
  public sealed class GestureRuntimeChecks : MonoBehaviour
  {
    public bool Finished {get;private set;}
    public bool Failed {get;private set;}
    public readonly List<string> Results=new List<string>();
    public bool previewOnly;
    private bool _standaloneValidation;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartStandaloneValidation()
    {
      var args=Environment.GetCommandLineArgs();
      bool preview=Array.IndexOf(args,"--gesture-preview-player")>=0;
      if(Application.isEditor || (!preview && Array.IndexOf(args,"--gesture-validate-player")<0)) return;
      Application.runInBackground=true;
      var tracker=FindObjectOfType<MediaPipeTracker>();
      if(tracker!=null) tracker.autoStart=preview;
      var input=FindObjectOfType<FistGestureInput>();
      if(input!=null) input.externalTestInput=!preview;
      var test=new GameObject("Standalone validation ONLY").AddComponent<GestureRuntimeChecks>();
      test._standaloneValidation=true;test.previewOnly=preview;
    }
    private FistPlayerController _p;
    private FistGestureInput _input;
    private FistGameManager _game;
    private MediaPipeTracker _tracker;
    private Vector3 _l,_r;
    private bool _grip=true,_active;
    private void Log(string s){Results.Add(s);Debug.Log("GESTURE_TEST "+s);}
    private bool Check(bool pass,string s)
    {
      Log((pass?"PASS ":"FAIL ")+s);
      if(!pass)Failed=true;
      return pass;
    }
    private void OnEnable(){Application.logMessageReceived+=OnLog;}
    private void OnDisable(){Application.logMessageReceived-=OnLog;}
    private void OnLog(string text,string stack,LogType type)
    {
      if(type==LogType.Exception || type==LogType.Error)
      { Failed=true; Results.Add("ERROR "+text); }
    }
    private void Update()
    {
      if(_active && _p!=null)
      {
        _p.SubmitHand(0,_l,_grip,true);_p.SubmitHand(1,_r,_grip,true);
      }
    }
    private IEnumerator Start()
    {
      _p=FindObjectOfType<FistPlayerController>();_input=FindObjectOfType<FistGestureInput>();
      _game=FindObjectOfType<FistGameManager>();_tracker=FindObjectOfType<MediaPipeTracker>();
      Directory.CreateDirectory("Artifacts");
      if(previewOnly)
      {
        yield return new WaitForSeconds(13);
        Log("CAMERA "+_tracker.Status);
        Capture("Artifacts/game-preview.png");
        yield return new WaitForSeconds(1);
        Finish();yield break;
      }
      _input.externalTestInput=true;
      yield return new WaitForSeconds(.2f);
      bool loaded=_tracker.LoadModels();
      Check(loaded,"CPU Pose + Hand models created from buffers in Chinese project path");
      if(loaded)
      {
        var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);
        texture.SetPixels32(new Color32[64*64]);
        var pose=PoseLandmarkerResult.Alloc(1);var hands=HandLandmarkerResult.Alloc(2);
        using(var image=new MPImage(Mediapipe.ImageFormat.Types.Format.Srgba,texture))
        {
          _tracker.Pose.TryDetectForVideo(image,1,null,ref pose);
          using(var handImage=new MPImage(Mediapipe.ImageFormat.Types.Format.Srgba,texture))
            _tracker.Hands.TryDetectForVideo(handImage,1,null,ref hands);
        }
        Check((pose.poseLandmarks==null || pose.poseLandmarks.Count==0) && (hands.handLandmarks==null || hands.handLandmarks.Count==0),"real native inference on blank image returns no false gesture");
        Destroy(texture);
      }
      yield return new WaitForSeconds(.6f);
      _p.ResetAt(new Vector3(0,1.3f,0));
      _l=new Vector3(-1,-1.1f,1.2f);_r=new Vector3(1,-1.1f,1.2f);_active=true;
      yield return new WaitForSeconds(.65f);
      Check(_p.SupportCount==2,"both fists acquire actual ground contact; support count="+_p.SupportCount);
      var still=_p.transform.position;
      yield return new WaitForSeconds(.5f);
      Check(Vector3.Distance(still,_p.transform.position)<.3f,"holding an anchor stays stable (no automatic towing)");
      var before=_p.transform.position;
      yield return Stroke(new Vector3(-1,-1.7f,-.7f),new Vector3(1,-1.7f,-.7f),.7f);
      Check(_p.transform.position.z-before.z>.75f,"backward hand stroke advances body; dz="+(_p.transform.position.z-before.z).ToString("0.00"));
      Check(_p.transform.position.y-before.y>.25f,"downward hand stroke raises body; dy="+(_p.transform.position.y-before.y).ToString("0.00"));
      _grip=false;
      yield return new WaitForFixedUpdate();
      yield return new WaitForFixedUpdate();
      Check(_p.SupportCount==0 && _p.Body.velocity.z>.2f,"release preserves forward momentum without an air jump");
      _active=false;
      yield return new WaitForSeconds(.45f);
      Check(_p.SupportCount==0,"stale hand input releases all support");
      _game.Respawn();
      Check(Vector3.Distance(_p.transform.position,_game.checkpoint.position)<.02f,"checkpoint reset clears velocity and constraints");
      // Combat uses the same swept fist path, not a direct TakeHit call.
      var dummy=_game.enemies[0];dummy.ResetDummy();dummy.mobile=false;
      var drb=dummy.GetComponent<Rigidbody>();drb.position=new Vector3(0,1,5.4f);drb.velocity=Vector3.zero;
      _p.ResetAt(new Vector3(0,1,3));
      _l=new Vector3(-1.5f,.2f,.5f);_r=new Vector3(0,.2f,.55f);_grip=true;_active=true;
      yield return new WaitForSeconds(.3f);
      float health=dummy.Health; int hits=_p.Hits;
      yield return Stroke(_l,new Vector3(0,.2f,2.65f),.16f);
      yield return new WaitForSeconds(.12f);
      Check(_p.Hits>hits && dummy.Health==health-1,"swept fist hits target exactly once");
      _active=false;
      yield return new WaitForSeconds(.4f);
      Check(dummy.GetComponent<Rigidbody>().velocity.magnitude>.1f,"hit imparts physical knockback");
      // Fall/finish branches are explicitly positioned test fixtures.
      _p.ResetAt(new Vector3(0,-7,10));
      yield return new WaitForSeconds(.2f);
      Check(_p.transform.position.y>0,"fall below level returns to checkpoint");
      _p.ResetAt(_game.finish.position);
      yield return new WaitForSeconds(.15f);
      Check(_game.Completed && !_p.allowControl,"finish enters completed state");
      var finishPosition=_p.Body.position;
      yield return new WaitForSeconds(.5f);
      Check(Vector3.Distance(finishPosition,_p.Body.position)<.02f,"completed player stays on the finish platform");
      _game.Restart();
      Check(!_game.Completed && _p.allowControl && _game.CheckpointIndex==0 && _p.Body.constraints==RigidbodyConstraints.FreezeRotation,"restart restores player, movement and enemies");
      yield return TraverseLevel();
      yield return CheckMouseControls();
      _game.Restart();
      _active=false;
      _input.externalTestInput=false;
      yield return new WaitForSeconds(1.5f);
      Capture("Artifacts/physics-check-preview.png");
      Capture("Artifacts/mouse-preview.png");
      var ui=FindObjectOfType<FistGameUI>();ui.ToggleHelp();
      yield return new WaitForSeconds(.15f);
      Capture("Artifacts/mouse-help-preview.png");ui.ToggleHelp();
      yield return new WaitForSeconds(.6f);
      Finish();
    }
    private IEnumerator CheckMouseControls()
    {
      _active=false;_game.Restart();
      var mouse=_input.mouseControl;
      var ui=FindObjectOfType<FistGameUI>();
      ui.keyboardButton.onClick.Invoke();yield return new WaitForSeconds(.12f);
      Check(_input.keyboardMode && !_input.mouseMode,"saved keyboard button switches input mode");
      ui.cameraButton.onClick.Invoke();yield return new WaitForSeconds(.12f);
      Check(_input.UsesCamera && ui.cameraPanel.activeSelf,"saved gesture button restores camera mode and preview panel");
      ui.mouseButton.onClick.Invoke();yield return new WaitForSeconds(.12f);
      Check(_input.mouseMode && !_tracker.CameraReady && !ui.cameraPanel.activeSelf,"saved mouse button enables play without the camera");

      var eventSystem=UnityEngine.EventSystems.EventSystem.current;
      var pointer=new UnityEngine.EventSystems.PointerEventData(eventSystem);
      pointer.position=RectTransformUtility.WorldToScreenPoint(null,ui.mouseButton.transform.position);
      var results=new List<UnityEngine.EventSystems.RaycastResult>();eventSystem.RaycastAll(pointer,results);
      Check(results.Exists(r=>r.gameObject.GetComponentInParent<UnityEngine.UI.Button>()==ui.mouseButton),"mode button receives UI pointer raycasts");
      mouse.ResetControl(false);
      mouse.ProcessFrame(Vector2.zero,0,true,false,true,false,.02f);
      mouse.ProcessFrame(new Vector2(.1f,.1f),-1,true,false,false,false,.02f);
      Check(!mouse.LeftHeld && !mouse.RightHeld,"a press started over UI cannot capture a fist after dragging out");
      mouse.ProcessFrame(Vector2.zero,0,false,false,false,false,.02f);
      mouse.ProcessFrame(Vector2.zero,0,true,false,false,false,.02f);
      mouse.ProcessFrame(new Vector2(.1f,0),0,true,false,false,false,.02f);
      Check(mouse.LeftHeld && !mouse.RightHeld && mouse.LeftOffset.x>-.5f && Mathf.Abs(mouse.RightOffset.x-1)<.01f,"left mouse drag moves only the left fist");

      _p.ResetAt(new Vector3(0,1.3f,0));mouse.ResetControl(false);
      mouse.ProcessFrame(Vector2.zero,0,true,true,false,false,.02f);
      mouse.ProcessFrame(Vector2.zero,-2,true,true,false,false,.02f);
      yield return MouseFrames(mouse,.65f,Vector2.zero,0,true,true);
      Check(_p.SupportCount==2,"mouse buttons and wheel acquire two real ground supports");
      var before=_p.Body.position;
      yield return MouseFrames(mouse,.7f,new Vector2(0,-1.0f/mouse.heightDragScale),0,true,true);
      Check(_p.Body.position.z-before.z>.4f && _p.Body.position.y-before.y>.15f,"direct downward mouse drag lowers both fists and pushes the body forward/up");
      var wheelBefore=_p.Body.position;
      yield return MouseFrames(mouse,.25f,Vector2.zero,-.55f/mouse.wheelHeight,true,true);
      Check(_p.Body.position.y-wheelBefore.y>.05f,"mouse wheel remains a fine height adjustment while supporting");
      mouse.ProcessFrame(Vector2.zero,0,false,false,false,false,.02f);
      yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
      Log("mouse release velocity="+_p.Body.velocity.ToString("F2")+" position="+_p.Body.position.ToString("F2"));
      Check(_p.SupportCount==0 && _p.Body.velocity.magnitude>.2f,"releasing mouse buttons frees supports and keeps momentum");

      var dummy=_game.enemies[0];dummy.ResetDummy();dummy.mobile=false;
      dummy.GetComponent<Rigidbody>().position=new Vector3(0,1,5.4f);
      _p.ResetAt(new Vector3(0,1,3));mouse.ResetControl(false);
      mouse.ProcessFrame(Vector2.zero,0,false,true,false,false,.02f);
      mouse.ProcessFrame(new Vector2(-1/mouse.dragScale,0),.6f/mouse.wheelHeight,false,true,false,false,.02f);
      yield return MouseFrames(mouse,.3f,Vector2.zero,0,false,true);
      float health=dummy.Health;
      yield return MouseFrames(mouse,.16f,new Vector2(0,1.5f/mouse.dragScale),0,false,true);
      Check(dummy.Health==health-1,"fast right mouse drag punches a guard through swept collision");
      ui.ToggleHelp();
      Check(!mouse.LeftHeld && !mouse.RightHeld && _p.SupportCount==0,"opening help cancels captured fists and releases supports");
      ui.ToggleHelp();
      ui.ResetPlayer();
      Check(Vector3.Distance(_p.Body.position,_game.checkpoint.position)<.02f,"saved reset button returns player to checkpoint");
      _input.UseMouse();mouse.ResetControl();
    }
    private IEnumerator MouseFrames(MouseFistInput mouse,float duration,Vector2 totalDrag,float totalWheel,bool left,bool right)
    {
      float elapsed=0;
      while(elapsed<duration)
      {
        float dt=Mathf.Min(Time.deltaTime,duration-elapsed);
        mouse.ProcessFrame(totalDrag*(dt/duration),totalWheel*(dt/duration),left,right,false,false,dt);
        elapsed+=dt;yield return null;
      }
      yield return new WaitForFixedUpdate();
    }
    private IEnumerator Stroke(Vector3 l,Vector3 r,float duration)
    {
      var a=_l;var b=_r;float start=Time.time;
      while(Time.time-start<duration)
      {
        float t=(Time.time-start)/duration;_l=Vector3.Lerp(a,l,t);_r=Vector3.Lerp(b,r,t);yield return null;
      }
      _l=l;_r=r;yield return new WaitForFixedUpdate();
    }
    private IEnumerator TraverseLevel()
    {
      _active=true;
      int strokes=0;
      while(!_game.Completed && strokes<32)
      {
        strokes++;
        _grip=false;
        _p.ReleaseAll();
        var origin=_p.Body.position;
        Vector3 landing=origin;
        bool found=false;
        for(float reach=1.8f;reach<=3.1f;reach+=.3f)
        {
          if(Physics.Raycast(new Vector3(0,10,origin.z+reach),Vector3.down,out var hit,15,1<<8,QueryTriggerInteraction.Ignore))
          {
            var candidate=hit.point+Vector3.up*.44f;
            // A point just below a step edge fits a ray, but not a whole fist.
            // Choose a clear landing for both gloves before attempting a stroke.
            if(Physics.CheckSphere(candidate+Vector3.left*.85f,.43f,1<<8,QueryTriggerInteraction.Ignore) ||
               Physics.CheckSphere(candidate+Vector3.right*.85f,.43f,1<<8,QueryTriggerInteraction.Ignore)) continue;
            landing=candidate; found=true; break;
          }
        }
        if(!found) { Log("No reachable platform at z="+origin.z);break; }
        var startL=_l;var startR=_r;float start=Time.time;
        while(Time.time-start<.3f)
        {
          var offset=landing-_p.Body.position;
          _l=Vector3.Lerp(startL,offset+Vector3.left*.85f,(Time.time-start)/.3f);
          _r=Vector3.Lerp(startR,offset+Vector3.right*.85f,(Time.time-start)/.3f);
          yield return null;
        }
        _grip=true;
        start=Time.time;
        while(Time.time-start<.2f)
        {
          _l=landing-_p.Body.position+Vector3.left*.85f;
          _r=landing-_p.Body.position+Vector3.right*.85f;
          yield return null;
        }
        yield return Stroke(new Vector3(-.85f,-1.3f,-.5f),new Vector3(.85f,-1.3f,-.5f),.5f);
        _grip=false;
        yield return new WaitForSeconds(.14f);
        Log("ROUTE stroke="+strokes+" position="+_p.Body.position.ToString("F2")+" landing="+landing.ToString("F2"));
      }
      Check(_game.Completed,"complete course using fist support strokes only; strokes="+strokes+", z="+_p.Body.position.z.ToString("0.0"));
      _active=false;
    }
    private void Capture(string path)
    {
      var camera=Camera.main;
      var canvases=FindObjectsOfType<Canvas>();
      foreach(var c in canvases){ c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=.5f; }
      var target=new RenderTexture(1280,720,24);target.Create();camera.targetTexture=target;
      Canvas.ForceUpdateCanvases();camera.Render();
      var previous=RenderTexture.active;RenderTexture.active=target;
      var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
      texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
      File.WriteAllBytes(path,texture.EncodeToPNG());
      RenderTexture.active=previous;camera.targetTexture=null;
      foreach(var c in canvases)c.renderMode=RenderMode.ScreenSpaceOverlay;
      Destroy(texture);target.Release();Destroy(target);
    }
    private void Finish()
    {
      Finished=true;
      File.WriteAllLines("Artifacts/"+(_standaloneValidation?(previewOnly?"player-camera-validation.txt":"player-validation.txt"):previewOnly?"camera-validation.txt":"physics-validation.txt"),Results);
      Debug.Log("GESTURE_TESTS_DONE "+(Failed?"FAIL":"PASS"));
      if(_standaloneValidation) Application.Quit(Failed?1:0);
    }
  }
}

