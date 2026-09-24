using System;
using System.Collections.Generic;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.HandLandmarker;
using UnityEngine;

namespace GestureFistGame
{
  public sealed class FishGestureInput : MonoBehaviour
  {
    public FishGameController game;
    public MediaPipeTracker tracker;
    public FishMouseInput mouseInput;
    public bool mouseMode = true;
    public bool startCameraOnMobile = true;
    [Range(.08f, .6f)] public float upwardVelocityThreshold = .72f;
    [Range(.01f, .3f)] public float minimumDisplacement = .055f;
    [Range(.05f, .8f)] public float gestureCooldown = .22f;
    [Range(.02f, .3f)] public float rearmDistance = .075f;
    public string Status { get; private set; } = "鼠标调试模式 · 向上拖动或按空格抛网";
    public bool HandDetected { get; private set; }
    public float LastHandY { get; private set; }
    public float LastUpwardVelocity { get; private set; }
    public event Action<float> UpwardWaveDetected;

    private float _lastY;
    private float _lastSampleTime = -1;
    private float _lastTriggerY;
    private float _cooldownUntil;
    private bool _armed = true;

    private void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
      if (startCameraOnMobile) UseCamera();
#endif
    }

    private void Update()
    {
      if (Input.GetKeyDown(KeyCode.F1)) UseCamera();
      if (Input.GetKeyDown(KeyCode.F3)) UseMouse();
      if (Input.GetKeyDown(KeyCode.R)) game?.ResetRound();
      if (mouseMode)
      {
        mouseInput?.Tick(false);
        Status = "鼠标调试 · 向上拖动或按空格抛网";
        return;
      }
      if (Input.GetKeyDown(KeyCode.Space)) TriggerWave("键盘测试");
      if (!HandDetected || Time.unscaledTime - _lastSampleTime > .5f)
        Status = tracker != null && !tracker.CameraReady ? "摄像头启动中 · 请允许摄像头权限" : "请将一只手放入镜头并向上挥";
      else
        Status = "手部已识别 · " + (game != null && game.net != null ? game.net.PhaseName : "等待");
    }

    public void UseMouse()
    {
      mouseMode = true;
      tracker?.SuspendCamera();
      Status = "鼠标调试 · 向上拖动或按空格抛网";
    }

    public void UseCamera()
    {
      mouseMode = false;
      HandDetected = false;
      _armed = true;
      _lastSampleTime = -1;
#if UNITY_ANDROID && !UNITY_EDITOR
      if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
        UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
#endif
      if (tracker != null)
      {
        tracker.fishInput = this;
        tracker.input = null;
        tracker.autoStart = false;
        tracker.usePose = false;
        tracker.RetryCamera();
      }
      Status = "正在启动摄像头 · 请正对手机";
    }

    public void ReceivePose(Mediapipe.Tasks.Vision.PoseLandmarker.PoseLandmarkerResult result) { }

    public void ReceiveHands(HandLandmarkerResult result)
    {
      if (mouseMode || result.handLandmarks == null || result.handLandmarks.Count == 0) return;
      List<NormalizedLandmark> chosen = null;
      for (int i = 0; i < result.handLandmarks.Count; i++)
      {
        var candidate = result.handLandmarks[i].landmarks;
        if (candidate != null && candidate.Count >= 21) { chosen = candidate; break; }
      }
      if (chosen == null)
      {
        HandDetected = false;
        return;
      }
      var palm = PalmCenter(chosen);
      float now = Time.unscaledTime;
      HandDetected = true;
      LastHandY = palm.y;
      if (_lastSampleTime > 0)
      {
        float dt = Mathf.Max(.001f, now - _lastSampleTime);
        float displacement = _lastY - palm.y;
        LastUpwardVelocity = displacement / dt;
        bool rising = displacement >= minimumDisplacement && LastUpwardVelocity >= upwardVelocityThreshold;
        if (rising && _armed && now >= _cooldownUntil)
        {
          _armed = false;
          _lastTriggerY = palm.y;
          _cooldownUntil = now + gestureCooldown;
          TriggerWave("手势上挥");
          UpwardWaveDetected?.Invoke(LastUpwardVelocity);
        }
        if (!_armed && palm.y > _lastTriggerY + rearmDistance && now >= _cooldownUntil)
          _armed = true;
      }
      _lastY = palm.y;
      _lastSampleTime = now;
    }

    private static Vector2 PalmCenter(List<NormalizedLandmark> p)
    {
      var sum = Vector2.zero;
      foreach (var index in new[] { 0, 5, 9, 13, 17 }) sum += new Vector2(p[index].x, p[index].y);
      return sum / 5f;
    }

    public bool TriggerWave(string source)
    {
      return game != null && game.TryThrow(source);
    }
  }
}
