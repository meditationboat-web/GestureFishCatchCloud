using System;
using System.Collections;
using System.IO;
using Mediapipe;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Vision.HandLandmarker;
using UnityEngine;
using UnityEngine.UI;
using MPImage = Mediapipe.Image;
using Color = UnityEngine.Color;

namespace GestureFistGame
{
  // One camera feeds both CPU tasks. Model bytes avoid native fopen on Unicode paths.
  public sealed class MediaPipeTracker : MonoBehaviour
  {
    public FistGestureInput input;
    public FishGestureInput fishInput;
    public RawImage preview;
    public string preferredCamera = "";
    [Range(8,30)] public int inferenceFps = 20;
    public string poseModel = "pose_landmarker_lite.bytes";
    public string handModel = "hand_landmarker.bytes";
    public bool autoStart = true;
    public bool usePose = true;
    public bool mirrorPreview = true;
    [Tooltip("Skip the Intel virtual driver whose RGB24 subtype can block Unity WebCamTexture.Play on Windows.")]
    public bool skipUnsupportedIntelVirtualCamera = true;
    public string Status { get; private set; } = "摄像头准备中";
    public bool ModelsReady => (usePose ? _pose != null : true) && _hands != null;
    public bool CameraReady => _webcam != null && _webcam.isPlaying && _webcam.width > 32;
    public float InferenceMs { get; private set; }
    public PoseLandmarker Pose => _pose;
    public HandLandmarker Hands => _hands;
    private PoseLandmarker _pose;
    private HandLandmarker _hands;
    private WebCamTexture _webcam;
    private Texture2D _frame;
    private Color32[] _source, _ordered;
    private PoseLandmarkerResult _poseResult;
    private HandLandmarkerResult _handResult;
    private long _timestamp;
    private float _nextFrame;
    private int _counter, _cameraIndex;
    private bool _starting;

    private IEnumerator Start()
    {
      if (!autoStart) yield break;
      yield return StartCamera();
    }

    public bool LoadModels()
    {
      if (ModelsReady) return true;
      try
      {
        var handBytes = File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath, handModel));
        if (usePose)
        {
          var poseBytes = File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath, poseModel));
          _pose = PoseLandmarker.CreateFromOptions(new PoseLandmarkerOptions(
            new BaseOptions(BaseOptions.Delegate.CPU, modelAssetBuffer: poseBytes),
            RunningMode.VIDEO, 1, .5f, .5f, .5f, false));
        }
        _hands = HandLandmarker.CreateFromOptions(new HandLandmarkerOptions(
          new BaseOptions(BaseOptions.Delegate.CPU, modelAssetBuffer: handBytes),
          RunningMode.VIDEO, 2, .5f, .5f, .5f));
        _poseResult = usePose ? PoseLandmarkerResult.Alloc(1) : default(PoseLandmarkerResult);
        _handResult = HandLandmarkerResult.Alloc(2);
        Debug.Log("GESTURE_MODELS_READY: Pose + Hand, CPU, in-memory models.");
        return true;
      }
      catch (Exception e)
      {
        Status = "模型加载失败：" + e.Message;
        Debug.LogError(Status);
        DisposeTasks();
        return false;
      }
    }

    public IEnumerator StartCamera()
    {
      if (_starting) yield break;
      _starting = true;
      StopCamera();
      Status = "正在载入 MediaPipe";
      if (!LoadModels()) { _starting = false; yield break; }
      yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
      var allDevices = WebCamTexture.devices;
      var devices = Array.FindAll(allDevices, d => !skipUnsupportedIntelVirtualCamera ||
        !d.name.Equals("Intel Virtual Camera", StringComparison.OrdinalIgnoreCase));
      Debug.Log("GESTURE_CAMERAS: " + string.Join(", ", Array.ConvertAll(allDevices,d=>d.name)));
      if (devices.Length == 0)
      {
        Status = allDevices.Length > 0
          ? "Intel 虚拟摄像头格式不兼容 · 请接入 USB 摄像头后重试 · F2 测试物理"
          : "未发现摄像头 · 接好后点“重试摄像头” · F2 可测试物理";
        _starting = false;
        yield break;
      }
      if (!string.IsNullOrEmpty(preferredCamera))
        for (int i=0; i<devices.Length; i++) if (devices[i].name.Contains(preferredCamera)) _cameraIndex=i;
      _cameraIndex %= devices.Length;
      _webcam = new WebCamTexture(devices[_cameraIndex].name, 640, 480, 30);
      _webcam.Play();
      float until = Time.realtimeSinceStartup + 10;
      while (_webcam != null && _webcam.width <= 32 && Time.realtimeSinceStartup < until) yield return null;
      if (!CameraReady)
      {
        Status = "摄像头未能启动 · 检查 Windows 相机权限/占用，再点重试";
        StopCamera();
      }
      else
      {
        if (preview != null) { preview.texture = _webcam; preview.color=Color.white; }
        Status = devices[_cameraIndex].name + " · 请让肩膀和双手入镜";
      }
      _starting = false;
    }

    public void RetryCamera() { if (!_starting) StartCoroutine(StartCamera()); }
    public void SuspendCamera()
    {
      StopAllCoroutines();_starting=false;StopCamera();Status="点击“手势控制”启动摄像头";
    }
    public void NextCamera() { if (_starting) return; _cameraIndex++; preferredCamera=""; RetryCamera(); }

    private void Update()
    {
      if (!CameraReady || !_webcam.didUpdateThisFrame || !ModelsReady || Time.unscaledTime < _nextFrame) return;
      _nextFrame = Time.unscaledTime + 1f / inferenceFps;
      int width=_webcam.width, height=_webcam.height;
      if (_source == null || _source.Length != width*height)
      {
        _source=new Color32[width*height]; _ordered=new Color32[width*height];
        if (_frame != null) Destroy(_frame);
        _frame=new Texture2D(width,height,TextureFormat.RGBA32,false);
      }
      _webcam.GetPixels32(_source);
      // MediaPipe expects top-left row ordering. Unity stores bottom-left rows.
      for (int y=0;y<height;y++)
        Array.Copy(_source, (_webcam.videoVerticallyMirrored ? y : height-1-y)*width, _ordered,y*width,width);
      _frame.SetPixels32(_ordered);
      if (preview != null)
      {
        preview.uvRect = new UnityEngine.Rect(mirrorPreview ? 1:0, _webcam.videoVerticallyMirrored ? 1:0, mirrorPreview ? -1:1, _webcam.videoVerticallyMirrored ? -1:1);
        preview.rectTransform.localEulerAngles = new Vector3(0,0,-_webcam.videoRotationAngle);
      }
      var timer=System.Diagnostics.Stopwatch.StartNew();
      try
      {
        _timestamp=Math.Max(_timestamp+1,(long)(Time.realtimeSinceStartupAsDouble*1000));
        using(var image=new MPImage(ImageFormat.Types.Format.Srgba,_frame))
        {
          var options=new ImageProcessingOptions(rotationDegrees:_webcam.videoRotationAngle);
          if (_pose != null)
          {
            _pose.TryDetectForVideo(image,_timestamp,options,ref _poseResult);
            input?.ReceivePose(_poseResult);
            fishInput?.ReceivePose(_poseResult);
          }
          if ((_counter++ & 1)==0)
          {
            using(var handImage=new MPImage(ImageFormat.Types.Format.Srgba,_frame))
              _hands.TryDetectForVideo(handImage,_timestamp,options,ref _handResult);
            input?.ReceiveHands(_handResult);
            fishInput?.ReceiveHands(_handResult);
          }
        }
        InferenceMs=(float)timer.Elapsed.TotalMilliseconds;
        Status = _webcam.deviceName + " · CPU " + InferenceMs.ToString("0") + " ms";
      }
      catch(Exception e)
      {
        Status="识别中断：" + e.Message;
        Debug.LogError(Status);
        StopCamera();
      }
    }

    private void StopCamera()
    {
      if (_webcam == null) return;
      _webcam.Stop(); Destroy(_webcam); _webcam=null;
      if(preview!=null) { preview.texture=null; preview.color=new Color(.75f,.75f,.75f); }
    }
    private void DisposeTasks()
    {
      _pose?.Close(); _pose=null;
      _hands?.Close(); _hands=null;
    }
    private void OnDestroy()
    {
      StopCamera(); DisposeTasks();
      if(_frame!=null) Destroy(_frame);
    }
  }
}

