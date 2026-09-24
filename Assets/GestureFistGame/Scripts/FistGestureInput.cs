using System.Collections.Generic;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Vision.HandLandmarker;
using UnityEngine;

namespace GestureFistGame
{
  public sealed class FistGestureInput : MonoBehaviour
  {
    public FistPlayerController player;
    public MediaPipeTracker tracker;
    public bool mirrorInput = true;
    [Range(.2f,.9f)] public float minimumVisibility=.5f;
    public float armScale=1.85f;
    public float depthScale=1.65f;
    public bool requireConfirmedFist=false;
    public bool keyboardMode;
    public bool mouseMode;
    public MouseFistInput mouseControl;
    public GameObject helpPanel;
    public bool UsesCamera=>!mouseMode && !keyboardMode;
    public bool externalTestInput;
    public bool Calibrated {get;private set;}
    public string Status {get;private set;}="请后退，让肩膀和双手完整入镜";
    public readonly Vector3[] PreviewPoints=new Vector3[33];
    public float LastPoseTime {get;private set;}=-100;
    private readonly bool[] _visible=new bool[33];
    private readonly bool[] _closed=new bool[2];
    private readonly float[] _handTime={-100,-100};
    private float _width=.28f, _sumWidth, _calibrationTime;
    private int _samples;
    private Vector3[] _smoothed={new Vector3(-1,-.5f,.7f),new Vector3(1,-.5f,.7f)};
    private bool[] _wasValid={false,false};
    private Vector3[] _debug={new Vector3(-1,-.4f,1),new Vector3(1,-.4f,1)};
    private readonly bool[] _debugGrip={true,true};
    private float _releaseUntil;
    private bool _poseFresh;
    public void Recalibrate()
    {
      Calibrated=false; _samples=0; _sumWidth=0; _calibrationTime=0;
      player?.ReleaseAll();
      _wasValid[0]=_wasValid[1]=false;
    }
    public void ToggleKeyboard()
    {
      if(keyboardMode) UseMouse(); else UseKeyboard();
    }
    public void UseMouse() { SetMode(true,false); }
    public void UseKeyboard() { SetMode(false,true); }
    public void UseCamera() { SetMode(false,false);Recalibrate();if(tracker!=null && !tracker.CameraReady) tracker.RetryCamera(); }
    private void SetMode(bool mouse,bool keyboard)
    {
      mouseMode=mouse;keyboardMode=keyboard;
      mouseControl?.ResetControl();player?.InvalidateHands();
      if(!UsesCamera) tracker?.SuspendCamera();
    }

    // Invoked synchronously on Unity's main thread, after the task's result is ready.
    public void ReceivePose(PoseLandmarkerResult result)
    {
      if(result.poseLandmarks==null || result.poseLandmarks.Count==0) { _poseFresh=false; return; }
      var p=result.poseLandmarks[0].landmarks;
      if(p==null || p.Count<33) { _poseFresh=false; return; }
      for(int i=0;i<33;i++)
      {
        PreviewPoints[i]=new Vector3(p[i].x,p[i].y,p[i].z);
        _visible[i]=(p[i].visibility??1)>=minimumVisibility;
      }
      _poseFresh=_visible[11] && _visible[12];
      if(!_poseFresh) return;
      LastPoseTime=Time.unscaledTime;
      var shoulderWidth=Vector2.Distance(PreviewPoints[11],PreviewPoints[12]);
      if(shoulderWidth<.08f) { _poseFresh=false; return; }
      if(!Calibrated)
      {
        if(!_visible[15] || !_visible[16]) return;
        if(_samples==0) _calibrationTime=Time.unscaledTime;
        _sumWidth+=shoulderWidth; _samples++;
        if(_samples>=10 && Time.unscaledTime-_calibrationTime>=1)
        {
          _width=Mathf.Clamp(_sumWidth/_samples,.08f,.65f);
          Calibrated=true;
        }
      }
    }

    public void ReceiveHands(HandLandmarkerResult result)
    {
      if(result.handLandmarks==null) return;
      for(int i=0;i<result.handLandmarks.Count;i++)
      {
        var landmarks=result.handLandmarks[i].landmarks;
        if(landmarks==null || landmarks.Count<21) continue;
        // Match wrists spatially; handedness labels depend on whether an image was mirrored.
        var wrist=new Vector3(landmarks[0].x,landmarks[0].y,0);
        float dl=Vector2.Distance(wrist,PreviewPoints[15]), dr=Vector2.Distance(wrist,PreviewPoints[16]);
        int side=dl<dr ? 0:1;
        if(Mathf.Min(dl,dr)>.2f) continue;
        _closed[side]=IsClosedFist(landmarks); _handTime[side]=Time.unscaledTime;
      }
    }

    public static bool IsClosedFist(List<NormalizedLandmark> points)
    {
      if(points==null || points.Count<21) return false;
      Vector3 palm=new Vector3(points[0].x,points[0].y,points[0].z);
      int curled=0;
      for(int i=8;i<=20;i+=4)
      {
        Vector3 tip=new Vector3(points[i].x,points[i].y,points[i].z);
        Vector3 pip=new Vector3(points[i-2].x,points[i-2].y,points[i-2].z);
        if(Vector3.Distance(tip,palm)<Vector3.Distance(pip,palm)*1.15f) curled++;
      }
      return curled>=3;
    }

    private void Update()
    {
      if(player==null || externalTestInput) return;
      if(Input.GetKeyDown(KeyCode.F2)) ToggleKeyboard();
      if(Input.GetKeyDown(KeyCode.F1)) UseCamera();
      if(Input.GetKeyDown(KeyCode.F3)) UseMouse();
      if(Input.GetKeyDown(KeyCode.C)) Recalibrate();
      if(mouseMode)
      {
        mouseControl?.Tick(helpPanel!=null && helpPanel.activeSelf);
        Status="按住左 / 右键选拳，可双键同时按 · 左右调整拳头\n向下拖动=下压撑地并后划 · 滚轮微调高度 · 向上抬拳换支点";
        return;
      }
      if(keyboardMode) { KeyboardFrame(); return; }
      bool fresh=_poseFresh && Time.unscaledTime-LastPoseTime<.35f && Calibrated;
      var center=(PreviewPoints[11]+PreviewPoints[12])*.5f;
      for(int i=0;i<2;i++)
      {
        int index=i==0?15:16;
        bool valid=fresh && _visible[index];
        var wrist=PreviewPoints[index];
        var offset=new Vector3(
          (wrist.x-center.x)/_width*armScale*(mirrorInput?-1:1),
          .65f+(center.y-wrist.y)/_width*armScale,
          .5f+Mathf.Clamp((center.z-wrist.z)/_width*depthScale,-1.5f,2.7f));
        offset=Vector3.ClampMagnitude(offset,3.4f);
        if(valid && !_wasValid[i]) _smoothed[i]=offset;
        if(valid) _smoothed[i]=Vector3.Lerp(_smoothed[i],offset,1-Mathf.Exp(-14*Time.unscaledDeltaTime));
        bool handFresh=Time.unscaledTime-_handTime[i]<.35f;
        bool grip=handFresh?_closed[i]:!requireConfirmedFist;
        player.SubmitHand(i,_smoothed[i],grip,valid);
        _wasValid[i]=valid;
      }
      Status=!fresh ? (Calibrated?"识别丢失：停止施力 · 请让双手回到镜头内":"校准中：正对摄像头，肩膀和双手入镜约 1 秒")
        : "体感控制 · 下探接地 → 向后划 / 下压 → 抬手换支点";
      if(fresh && Time.unscaledTime-Mathf.Max(_handTime[0],_handTime[1])>.35f)
        Status+="\n手部细节不可见：使用腕部支撑模式";
    }

    private void KeyboardFrame()
    {
      if(Input.GetKeyDown(KeyCode.Space)) { _releaseUntil=Time.unscaledTime+.35f; player.ReleaseAll(); }
      Vector3 delta=new Vector3((Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0),(Input.GetKey(KeyCode.UpArrow)?1:0)-(Input.GetKey(KeyCode.DownArrow)?1:0),(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0));
      for(int i=0;i<2;i++)
      {
        bool select=Input.GetKey(i==0?KeyCode.Q:KeyCode.E);
        if(select) _debug[i]=Vector3.ClampMagnitude(_debug[i]+delta*2.3f*Time.unscaledDeltaTime,3.4f);
        if(Input.GetKeyDown(i==0?KeyCode.Z:KeyCode.X))
          _debug[i]=new Vector3(i==0?-0.65f:.65f,.2f,2.9f);
        if(Input.GetKeyUp(i==0?KeyCode.Z:KeyCode.X))
          _debug[i]=new Vector3(i==0?-1:1,-.4f,1);
        player.SubmitHand(i,_debug[i],Time.unscaledTime>_releaseUntil,true);
      }
      Status="键盘模式 · 按住 Q / E 选择拳头 · WASD 前后左右 · ↑↓ 上下\nZ / X 出拳 · 空格松开支点 · F1 手势 / F3 鼠标 · R 复位";
    }
  }
}

