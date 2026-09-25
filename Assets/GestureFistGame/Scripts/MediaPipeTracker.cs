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
    public bool CameraReady => _webcam != null && _webcam.isPlaying && _webcam.width > 32 && _webcam.height > 32 && _hasFirstFrame;
    public string Diagnostics => BuildDiagnostics();
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
    private bool _hasFirstFrame;
    private string _permissionState = "未请求";
    private string _deviceState = "未枚举";
    private string _lastCameraError = "";

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
      _hasFirstFrame = false;
      _lastCameraError = "";
      _permissionState = "等待授权";
      Status = "正在等待摄像头权限";
      if (!Application.isEditor)
      {
        yield return WaitForCameraPermission();
        if (!HasCameraPermission())
        {
          _lastCameraError = "系统权限未授予";
          Status = "摄像头权限被拒绝 · 请到系统设置允许后再点重试";
          _starting = false;
          yield break;
        }
      }
      _permissionState = "已授权";
      Status = "正在载入 MediaPipe";
      if (!LoadModels()) { _starting = false; yield break; }
      if (Application.isEditor || !IsAndroid())
      {
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
          yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
          _permissionState = "未授权";
          _lastCameraError = "Unity WebCam 权限未授予";
          Status = "摄像头权限未授予 · 请允许后点重试";
          _starting = false;
          yield break;
        }
      }

      // Android 在权限回调后的头几帧可能还拿不到设备列表，轮询而不是立即判定失败。
      WebCamDevice[] allDevices = null;
      float deviceUntil = Time.realtimeSinceStartup + 4f;
      while (Time.realtimeSinceStartup < deviceUntil)
      {
        allDevices = WebCamTexture.devices;
        if (allDevices != null && allDevices.Length > 0) break;
        yield return null;
      }
      if (allDevices == null) allDevices = new WebCamDevice[0];
      var devices = Array.FindAll(allDevices, d => !skipUnsupportedIntelVirtualCamera ||
        !d.name.Equals("Intel Virtual Camera", StringComparison.OrdinalIgnoreCase));
      _deviceState = allDevices.Length + " 个设备";
      Debug.Log("GESTURE_CAMERAS: " + string.Join(", ", Array.ConvertAll(allDevices,d=>d.name)));
      if (devices.Length == 0)
      {
        Status = allDevices.Length > 0
          ? "Intel 虚拟摄像头格式不兼容 · 请接入 USB 摄像头后重试 · F2 测试物理"
          : "未发现摄像头 · 接好后点“重试摄像头” · F2 可测试物理";
        _lastCameraError = "WebCamTexture.devices 为空";
        _starting = false;
        yield break;
      }

      // 手机默认优先前置摄像头；保留 preferredCamera 和 NextCamera 的手动选择能力。
      int preferredIndex = FindPreferredDevice(devices);
      bool started = false;
      for (int attempt = 0; attempt < devices.Length && !started; attempt++)
      {
        _cameraIndex = (preferredIndex + attempt) % devices.Length;
        var device = devices[_cameraIndex];
        Status = "正在启动摄像头 " + (_cameraIndex + 1) + "/" + devices.Length;
        _webcam = new WebCamTexture(device.name, 640, 480, 30);
        _webcam.Play();
        float until = Time.realtimeSinceStartup + 12f;
        while (_webcam != null && Time.realtimeSinceStartup < until)
        {
          if (_webcam.isPlaying && _webcam.width > 32 && _webcam.height > 32 && _webcam.didUpdateThisFrame)
          {
            _hasFirstFrame = true;
            started = true;
            break;
          }
          yield return null;
        }
        if (!started)
        {
          _lastCameraError = device.name + " 未产生首帧 (" +
            (_webcam == null ? "null" : _webcam.width + "x" + _webcam.height + ", playing=" + _webcam.isPlaying) + ")";
          StopCamera();
        }
      }

      if (!started || !CameraReady)
      {
        Status = "摄像头未能取得画面 · 请确认系统相机没有被其他应用占用，再点重试";
        _starting = false;
        yield break;
      }

      if (preview != null) { preview.texture = _webcam; preview.color=Color.white; }
      Status = devices[_cameraIndex].name + " · 画面已连接，请让一只手入镜";
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
      _hasFirstFrame = true;
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
        _lastCameraError = e.GetType().Name + ": " + e.Message;
        Debug.LogError(Status);
        StopCamera();
      }
    }

    private void StopCamera()
    {
      _hasFirstFrame = false;
      if (_webcam == null) return;
      _webcam.Stop(); Destroy(_webcam); _webcam=null;
      if(preview!=null) { preview.texture=null; preview.color=new Color(.75f,.75f,.75f); }
    }

    private IEnumerator WaitForCameraPermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
      if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
      {
        _permissionState = "已授权";
        yield break;
      }

      bool callbackFinished = false;
      bool denied = false;
      var callbacks = new UnityEngine.Android.PermissionCallbacks();
      callbacks.PermissionGranted += permission =>
      {
        if (permission == UnityEngine.Android.Permission.Camera) callbackFinished = true;
      };
      callbacks.PermissionDenied += permission =>
      {
        if (permission == UnityEngine.Android.Permission.Camera) { denied = true; callbackFinished = true; }
      };
      callbacks.PermissionDeniedAndDontAskAgain += permission =>
      {
        if (permission == UnityEngine.Android.Permission.Camera) { denied = true; callbackFinished = true; }
      };
      _permissionState = "等待系统弹窗";
      UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera, callbacks);
      float until = Time.realtimeSinceStartup + 20f;
      while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera) &&
             !callbackFinished && Time.realtimeSinceStartup < until)
        yield return null;

      if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
      {
        _permissionState = "已授权";
      }
      else
      {
        _permissionState = denied ? "已拒绝" : "授权超时";
      }
#else
      yield return null;
#endif
    }

    private bool HasCameraPermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
      return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera);
#else
      return true;
#endif
    }

    private static bool IsAndroid()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
      return true;
#else
      return false;
#endif
    }

    private int FindPreferredDevice(WebCamDevice[] devices)
    {
      if (!string.IsNullOrEmpty(preferredCamera))
        for (int i = 0; i < devices.Length; i++)
          if (devices[i].name.IndexOf(preferredCamera, StringComparison.OrdinalIgnoreCase) >= 0) return i;
      for (int i = 0; i < devices.Length; i++) if (devices[i].isFrontFacing) return i;
      if (devices.Length == 0) return 0;
      return Mathf.Clamp(_cameraIndex, 0, devices.Length - 1);
    }

    private string BuildDiagnostics()
    {
      var cam = _webcam == null ? "无" : _webcam.width + "x" + _webcam.height +
        " 播放=" + (_webcam.isPlaying ? "是" : "否") +
        " 首帧=" + (_hasFirstFrame ? "是" : "否");
      var rotation = _webcam == null ? "-" : _webcam.videoRotationAngle + "°";
      var mirror = _webcam == null ? "-" : (_webcam.videoVerticallyMirrored ? "是" : "否");
      return "权限=" + _permissionState + " | 设备=" + _deviceState + " | 画面=" + cam +
        " | 旋转=" + rotation + " | 镜像=" + mirror + " | 模型=" + (ModelsReady ? "已加载" : "未完成") +
        (string.IsNullOrEmpty(_lastCameraError) ? "" : " | 错误=" + _lastCameraError);
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

