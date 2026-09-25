using System;
using System.Collections.Generic;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.HandLandmarker;
using UnityEngine;

namespace GestureFistGame
{
  /// <summary>
  /// Converts two detected hand waves into one cooperative net throw. Hands are
  /// assigned by their screen-space x position, so the rule is stable even when
  /// MediaPipe returns the hands in a different order on each frame.
  /// </summary>
  public sealed class FishGestureInput : MonoBehaviour
  {
    public enum HandSide { Left, Right }

    public FishGameController game;
    public MediaPipeTracker tracker;
    public FishMouseInput mouseInput;
    public bool mouseMode = true;
    public bool startCameraOnMobile = true;
    [Tooltip("启用后必须左右两只手都在时间窗内上挥，才会抛网")]
    public bool requireTwoHands = true;
    [Range(.08f, .6f)] public float upwardVelocityThreshold = .72f;
    [Range(.01f, .3f)] public float minimumDisplacement = .055f;
    [Range(.05f, .8f)] public float gestureCooldown = .22f;
    [Range(.02f, .3f)] public float rearmDistance = .075f;
    [Range(.25f, 1.5f)] public float cooperationWindow = .70f;
    [Range(.15f, 1f)] public float handLostTimeout = .55f;
    public string Status { get; private set; } = "鼠标调试 · 左右两手合作抛网";
    public bool HandDetected { get; private set; }
    public bool CameraReady => tracker != null && tracker.CameraReady;
    public bool BothHandsDetected { get; private set; }
    public int DetectedHands { get; private set; }
    public float LastHandY { get; private set; }
    public float LastUpwardVelocity { get; private set; }
    public event Action<float> UpwardWaveDetected;

    private sealed class HandState
    {
      public bool seen;
      public float lastY;
      public float lastSampleTime = -1f;
      public float lastTriggerY;
      public float waveTime = -10f;
      public bool armed = true;
    }

    private readonly HandState _left = new HandState();
    private readonly HandState _right = new HandState();
    private float _cooldownUntil;

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
      ExpirePendingWaves(Time.unscaledTime);
      if (mouseMode)
      {
        mouseInput?.Tick(false);
        Status = "鼠标调试 · 左右区域上拖，或 Q + P 合作抛网";
        return;
      }
      if (Input.GetKeyDown(KeyCode.Space))
      {
        // 手机没有键盘；此快捷键只保留作编辑器测试，同时模拟两位玩家。
        RegisterMouseWave(HandSide.Left);
        RegisterMouseWave(HandSide.Right);
      }
      if (!HandDetected || Time.unscaledTime - Mathf.Max(_left.lastSampleTime, _right.lastSampleTime) > .5f)
        Status = tracker != null && !tracker.CameraReady ? tracker.Status :
          (requireTwoHands ? "请让左右两只手入镜，并在时间窗内一起向上挥" : "请将一只手放入镜头并向上挥");
      else if (requireTwoHands && !BothHandsDetected)
        Status = "已识别 " + DetectedHands + "/2 只手 · 请让另一只手入镜";
      else if (requireTwoHands && (_left.waveTime >= 0f || _right.waveTime >= 0f))
        Status = "一只手已上挥 · 等另一只手合作";
      else
        Status = "左右手已识别 · " + (game != null && game.net != null ? game.net.PhaseName : "等待");
    }

    public void UseMouse()
    {
      mouseMode = true;
      ResetHands();
      tracker?.SuspendCamera();
      Status = "鼠标调试 · 左右区域上拖，或 Q + P 合作抛网";
    }

    public void UseCamera()
    {
      mouseMode = false;
      ResetHands();
      if (tracker != null)
      {
        tracker.fishInput = this;
        tracker.input = null;
        tracker.autoStart = false;
        tracker.usePose = false;
        tracker.RetryCamera();
      }
      Status = "正在启动摄像头 · 请让左右两只手同时入镜";
    }

    public void ReceivePose(Mediapipe.Tasks.Vision.PoseLandmarker.PoseLandmarkerResult result) { }

    public void ReceiveHands(HandLandmarkerResult result)
    {
      if (mouseMode) return;
      var samples = new List<Vector2>();
      if (result.handLandmarks != null)
      {
        for (int i = 0; i < result.handLandmarks.Count; i++)
        {
          var landmarks = result.handLandmarks[i].landmarks;
          if (landmarks != null && landmarks.Count >= 21) samples.Add(PalmCenter(landmarks));
        }
      }
      samples.Sort((a, b) => a.x.CompareTo(b.x));
      float now = Time.unscaledTime;
      DetectedHands = Mathf.Min(2, samples.Count);
      HandDetected = DetectedHands > 0;
      BothHandsDetected = DetectedHands >= 2;
      if (samples.Count > 0) ProcessHand(_left, samples[0], now);
      else MarkMissing(_left, now);
      if (samples.Count > 1) ProcessHand(_right, samples[samples.Count - 1], now);
      else MarkMissing(_right, now);
      LastHandY = samples.Count == 0 ? LastHandY : samples[0].y;
    }

    private void ProcessHand(HandState state, Vector2 palm, float now)
    {
      state.seen = true;
      if (state.lastSampleTime > 0f)
      {
        float dt = Mathf.Max(.001f, now - state.lastSampleTime);
        float displacement = state.lastY - palm.y;
        float velocity = displacement / dt;
        LastUpwardVelocity = Mathf.Max(0f, velocity);
        bool rising = displacement >= minimumDisplacement && velocity >= upwardVelocityThreshold;
        // Allow the other hand to arrive during the short cooldown; only a
        // repeated wave from the same armed state is blocked.
        if (rising && state.armed && (now >= _cooldownUntil || state.waveTime < 0f))
        {
          state.armed = false;
          state.lastTriggerY = palm.y;
          state.waveTime = now;
          _cooldownUntil = now + gestureCooldown;
          if (!requireTwoHands) CompleteWave("手势上挥", velocity);
          else TryCompleteCooperativeWave(velocity);
        }
        if (!state.armed && palm.y > state.lastTriggerY + rearmDistance && now >= _cooldownUntil)
          state.armed = true;
      }
      state.lastY = palm.y;
      state.lastSampleTime = now;
    }

    private void MarkMissing(HandState state, float now)
    {
      if (state.seen && state.lastSampleTime > 0f && now - state.lastSampleTime > handLostTimeout)
      {
        state.seen = false;
        state.armed = true;
        state.waveTime = -10f;
        state.lastSampleTime = -1f;
      }
    }

    private void TryCompleteCooperativeWave(float velocity)
    {
      if (_left.waveTime < 0f || _right.waveTime < 0f) return;
      if (Mathf.Abs(_left.waveTime - _right.waveTime) > cooperationWindow)
      {
        if (_left.waveTime < _right.waveTime) _left.waveTime = -10f;
        else _right.waveTime = -10f;
        return;
      }
      _left.waveTime = -10f;
      _right.waveTime = -10f;
      CompleteWave("左右双手合作上挥", velocity);
    }

    private void CompleteWave(string source, float velocity)
    {
      if (game != null && game.TryThrow(source)) UpwardWaveDetected?.Invoke(velocity);
    }

    private void ExpirePendingWaves(float now)
    {
      if (_left.waveTime >= 0f && now - _left.waveTime > cooperationWindow) _left.waveTime = -10f;
      if (_right.waveTime >= 0f && now - _right.waveTime > cooperationWindow) _right.waveTime = -10f;
    }

    public void RegisterMouseWave(HandSide side)
    {
      var state = side == HandSide.Left ? _left : _right;
      float now = Time.unscaledTime;
      if (now < _cooldownUntil && state.waveTime >= 0f) return;
      state.waveTime = now;
      state.armed = false;
      if (!requireTwoHands) CompleteWave("鼠标上挥", 1f);
      else TryCompleteCooperativeWave(1f);
    }

    public bool TriggerWave(string source)
    {
      return game != null && game.TryThrow(source);
    }

    private void ResetHands()
    {
      ResetState(_left);
      ResetState(_right);
      HandDetected = false;
      BothHandsDetected = false;
      DetectedHands = 0;
      _cooldownUntil = 0f;
    }

    private static void ResetState(HandState state)
    {
      state.seen = false;
      state.lastY = 0f;
      state.lastSampleTime = -1f;
      state.lastTriggerY = 0f;
      state.waveTime = -10f;
      state.armed = true;
    }

    private static Vector2 PalmCenter(List<NormalizedLandmark> p)
    {
      var sum = Vector2.zero;
      foreach (var index in new[] { 0, 5, 9, 13, 17 }) sum += new Vector2(p[index].x, p[index].y);
      return sum / 5f;
    }
  }
}
