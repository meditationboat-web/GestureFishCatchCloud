using System;
using System.Collections.Generic;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using UnityEngine;

namespace GestureFistGame
{
  // Pose wrists above an estimated head-top line drive the two sides of the fishing net.
  public sealed class FishGestureInput : MonoBehaviour
  {
    public enum HandSide { Left, Right }

    public FishGameController game;
    public MediaPipeTracker tracker;
    public FishMouseInput mouseInput;
    public bool mouseMode = true;
    public bool startCameraOnMobile = true;
    [Tooltip("启用后左右两侧都要在时间窗内举手过头顶，才会抛网")]
    public bool requireTwoHands = true;
    [Range(.1f, .9f)] public float minimumVisibility = .5f;
    [Tooltip("头顶参考线：比可见眼睛/耳朵最高点再高出此比例的肩宽")]
    public float headTopMarginRatio = .35f;
    [Tooltip("手腕低于肩膀的距离，占肩宽的比例；放下后才可再次触发")]
    public float loweredShoulderRatio = .15f;
    public float poseHoldSeconds = .1f;
    [Range(.25f, 1.5f)] public float cooperationWindow = 1.2f;
    [Range(.15f, 1f)] public float handLostTimeout = .55f;
    public string Status { get; private set; } = "鼠标调试 · 左右两手合作抛网";
    public bool HandDetected { get; private set; }
    public bool CameraReady => tracker != null && tracker.CameraReady;
    public bool BothHandsDetected { get; private set; }
    public int DetectedHands { get; private set; }
    public float LastHandY { get; private set; }
    public float LastUpwardVelocity { get; private set; }
    public event Action<float> UpwardWaveDetected;
    public event Action<HandSide> HandWaveDetected;

    private sealed class HandState
    {
      public bool seen;
      public float lastY;
      public float lastSampleTime = -1f;
      public float raisedSince = -1f;
      public float loweredSince = -1f;
      public float waveTime = -10f;
      public bool armed = true;
    }

    private readonly HandState _left = new HandState();
    private readonly HandState _right = new HandState();
    private int _peopleCount;
    private float _lastPoseTime = -1f;
    private float _leftOwnerX, _rightOwnerX;

    private void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
      if (startCameraOnMobile && game != null && game.IsRunning) UseCamera();
#endif
    }

    private void Update()
    {
      if (game != null && !game.IsRunning && !game.IsFinished) return;
      if (Input.GetKeyDown(KeyCode.F1)) UseCamera();
      if (Input.GetKeyDown(KeyCode.F3)) UseMouse();
      if (Input.GetKeyDown(KeyCode.R)) game?.ResetRound();
      ExpirePendingWaves(Time.unscaledTime);
      if (!mouseMode && Time.unscaledTime - _lastPoseTime > handLostTimeout)
      {
        MarkMissing(_left, Time.unscaledTime); MarkMissing(_right, Time.unscaledTime);
        HandDetected = BothHandsDetected = false; DetectedHands = 0;
      }
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
          (requireTwoHands ? "请让头部、肩膀和手腕入镜，举手过头顶" : "请让头部、肩膀和手腕入镜，举手过头顶");
      else if (requireTwoHands && !BothHandsDetected)
        Status = "已识别 " + DetectedHands + "/2 只手腕 · 请让头部、肩膀和手腕入镜";
      else if (requireTwoHands && (_left.waveTime >= 0f || _right.waveTime >= 0f))
        Status = "一侧已举手 · 等另一侧合作";
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
        tracker.usePose = true;
        tracker.RetryCamera();
      }
      Status = "正在启动摄像头 · 请让头部和上半身入镜";
    }

    public void ReceivePose(PoseLandmarkerResult result)
    {
      if (mouseMode) return;
      float now = Time.unscaledTime;
      ExpirePendingWaves(now);
      var people = new List<List<NormalizedLandmark>>();
      if (result.poseLandmarks != null)
        foreach (var pose in result.poseLandmarks)
        {
          var p = pose.landmarks;
          if (p != null && p.Count >= 17 && Visible(p[11]) && Visible(p[12]) &&
              Vector2.Distance(Point(p[11]), Point(p[12])) >= .04f) people.Add(p);
        }
      people.Sort((a, b) => CenterX(a).CompareTo(CenterX(b)));
      if (people.Count > 2) people.RemoveRange(2, people.Count - 2);
      DetectedHands = 0;
      // A changed player arrangement cancels pending gestures; lower arms to rearm.
      if (people.Count > 0)
      {
        float lx = CenterX(people[0]), rx = CenterX(people[people.Count - 1]);
        if (_peopleCount > 0 && (people.Count != _peopleCount ||
            Mathf.Abs(lx - _leftOwnerX) > .25f || Mathf.Abs(rx - _rightOwnerX) > .25f))
        {
          ResetState(_left); ResetState(_right);
          _left.armed = _right.armed = false;
        }
        _peopleCount = people.Count; _leftOwnerX = lx; _rightOwnerX = rx; _lastPoseTime = now;
        if (people.Count == 1)
        {
          var p = people[0];
          // Shoulder order is stable even when the wrists cross.
          int l = p[11].x < p[12].x ? 11 : 12;
          SampleArm(_left, HandSide.Left, p, l, now);
          SampleArm(_right, HandSide.Right, p, l == 11 ? 12 : 11, now);
        }
        else
        {
          SamplePerson(_left, HandSide.Left, people[0], now);
          SamplePerson(_right, HandSide.Right, people[1], now);
        }
      }
      else { MarkMissing(_left, now); MarkMissing(_right, now); }
      HandDetected = DetectedHands > 0;
      BothHandsDetected = DetectedHands >= 2;
    }

    private bool Visible(NormalizedLandmark p) =>
      !float.IsNaN(p.x) && !float.IsNaN(p.y) && p.x >= 0 && p.x <= 1 && p.y >= 0 && p.y <= 1 &&
      (p.visibility ?? 1f) >= minimumVisibility && (p.presence ?? 1f) >= minimumVisibility;
    private static Vector2 Point(NormalizedLandmark p) => new Vector2(p.x, p.y);
    private static float CenterX(List<NormalizedLandmark> p) => (p[11].x + p[12].x) * .5f;
    private static float ArmHeight(List<NormalizedLandmark> p, int shoulder) =>
      (p[shoulder].y - p[shoulder + 4].y) / Mathf.Max(.06f, Vector2.Distance(Point(p[11]), Point(p[12])));

    private bool TryHeadTop(List<NormalizedLandmark> p, out float headTop)
    {
      // Pose has no crown landmark. Estimate it above the highest visible eye/ear.
      headTop = 1f;
      int count = 0;
      foreach (int index in new[] { 2, 5, 7, 8 })
        if (Visible(p[index])) { headTop = Mathf.Min(headTop, p[index].y); count++; }
      if (count < 2) return false;
      headTop -= headTopMarginRatio * Vector2.Distance(Point(p[11]), Point(p[12]));
      return headTop >= 0f;
    }

    private void SamplePerson(HandState state, HandSide side, List<NormalizedLandmark> p, float now)
    {
      bool a = Visible(p[15]), b = Visible(p[16]);
      if (!a && !b) { MarkMissing(state, now); return; }
      int shoulder = a && (!b || ArmHeight(p, 11) >= ArmHeight(p, 12)) ? 11 : 12;
      SampleArm(state, side, p, shoulder, now);
    }

    private void SampleArm(HandState state, HandSide side, List<NormalizedLandmark> p, int shoulder, float now)
    {
      if (!Visible(p[shoulder + 4]) || !TryHeadTop(p, out float headTop))
      { MarkMissing(state, now); return; }
      DetectedHands++;
      float y = p[shoulder + 4].y;
      LastHandY = y;
      LastUpwardVelocity = state.lastSampleTime >= 0 ? Mathf.Max(0, (state.lastY - y) / Mathf.Max(.001f, now - state.lastSampleTime)) : 0;
      if (state.lastSampleTime >= 0 && now - state.lastSampleTime > handLostTimeout)
      { state.waveTime = -10; state.raisedSince = state.loweredSince = -1; }
      state.seen = true; state.lastSampleTime = now; state.lastY = y;
      float height = ArmHeight(p, shoulder);
      if (height <= -loweredShoulderRatio)
      {
        state.raisedSince = -1;
        if (state.loweredSince < 0) state.loweredSince = now;
        if (now - state.loweredSince >= poseHoldSeconds) state.armed = true;
      }
      else
      {
        state.loweredSince = -1;
        if (y > headTop) { state.raisedSince = -1; return; }
        if (state.raisedSince < 0) state.raisedSince = now;
        if (state.armed && now - state.raisedSince >= poseHoldSeconds)
        {
          state.armed = false; state.waveTime = now;
          HandWaveDetected?.Invoke(side);
          if (!requireTwoHands) CompleteWave("姿态举手", 1f);
          else TryCompleteCooperativeWave(1f);
        }
      }
    }

    private void MarkMissing(HandState state, float now)
    {
      state.raisedSince = state.loweredSince = -1;
      if (state.lastSampleTime >= 0 && now - state.lastSampleTime > handLostTimeout)
      {
        state.seen = false; state.waveTime = -10f;
        // Tracking loss must never rearm a hand that is still raised.
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
      CompleteWave("左右合作举手", velocity);
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
      state.waveTime = now;
      state.armed = false;
      HandWaveDetected?.Invoke(side);
      if (!requireTwoHands) CompleteWave("鼠标上挥", 1f);
      else TryCompleteCooperativeWave(1f);
    }

    public bool TriggerWave(string source)
    {
      return game != null && game.TryThrow(source);
    }

    public void ResetForRound()
    {
      ResetHands();
    }

    private void ResetHands()
    {
      ResetState(_left);
      ResetState(_right);
      HandDetected = false;
      BothHandsDetected = false;
      DetectedHands = 0;
      _peopleCount = 0; _lastPoseTime = -1f;
    }

    private static void ResetState(HandState state)
    {
      state.seen = false;
      state.lastY = 0f;
      state.lastSampleTime = -1f;
      state.raisedSince = state.loweredSince = -1f;
      state.waveTime = -10f;
      state.armed = true;
    }

  }
}
