using System;
using UnityEngine;

namespace GestureFistGame
{
  public sealed class FishGameController : MonoBehaviour
  {
    public FishNetController net;
    public FishSpawner spawner;
    public FishGestureInput input;
    public int roundSeconds = 30;
    public bool autoStart = true;
    [Header("捕鱼音效")]
    public AudioSource catchAudioSource;
    public AudioClip catchSound;
    [Range(0f, 1f)] public float catchVolume = .72f;
    public int Score { get; private set; }
    public int Catches { get; private set; }
    public int Combo { get; private set; }
    public float TimeRemaining { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsFinished { get; private set; }
    public string LastEvent { get; private set; } = "准备开始 · 左右双手合作";
    public string Rank
    {
      get
      {
        if (Score >= 80) return "S";
        if (Score >= 45) return "A";
        if (Score >= 20) return "B";
        return "C";
      }
    }

    public event Action<FishTarget> FishCaught;

    private void Awake()
    {
      if (catchAudioSource == null) catchAudioSource = gameObject.AddComponent<AudioSource>();
      catchAudioSource.playOnAwake = false;
      catchAudioSource.loop = false;
      catchAudioSource.spatialBlend = 0f;
      catchAudioSource.volume = catchVolume;
      if (catchSound == null) catchSound = CreateCatchSound();
    }

    private void Start()
    {
#if UNITY_ANDROID || UNITY_IOS
      Screen.orientation = ScreenOrientation.Portrait;
      Screen.autorotateToPortrait = true;
      Screen.autorotateToPortraitUpsideDown = true;
      Screen.autorotateToLandscapeLeft = false;
      Screen.autorotateToLandscapeRight = false;
#endif
      if (autoStart) ResetRound();
    }

    private void Update()
    {
      if (Input.GetKeyDown(KeyCode.R)) ResetRound();
      if (!IsRunning) return;
      // 手机首次授权和 MediaPipe 模型加载期间不消耗捕鱼回合时间。
      // 摄像头失败时也保留画面和诊断文字，方便用户点“重新连接摄像头”。
      if (input != null && !input.mouseMode && !input.CameraReady) return;
      TimeRemaining -= Time.unscaledDeltaTime;
      if (TimeRemaining <= 0)
      {
        TimeRemaining = 0;
        IsRunning = false;
        IsFinished = true;
        LastEvent = "回合结束 · 点击重新开始";
        spawner?.StopSpawning();
      }
    }

    public void ResetRound()
    {
      Score = 0;
      Catches = 0;
      Combo = 0;
      TimeRemaining = roundSeconds;
      IsFinished = false;
      IsRunning = true;
      LastEvent = "左右双手一起向上挥，把鱼网抛起来";
      net?.ResetNet();
      spawner?.ResetSpawner();
    }

    public bool TryThrow(string source)
    {
      if (!IsRunning || IsFinished || net == null) return false;
      if (!net.TryThrow())
      {
        LastEvent = "网还在下落或冷却中";
        return false;
      }
      LastEvent = source + " · 网已抛起";
      return true;
    }

    public void CatchFish(FishTarget fish)
    {
      if (!IsRunning || IsFinished || fish == null || fish.Caught) return;
      fish.MarkCaught();
      Score += fish.points;
      Catches++;
      Combo++;
      LastEvent = "捕获 " + fish.displayName + "  +" + fish.points;
      if (catchAudioSource != null && catchSound != null)
        catchAudioSource.PlayOneShot(catchSound, catchVolume);
      FishCaught?.Invoke(fish);
    }

    public void MissedFish()
    {
      Combo = 0;
      if (IsRunning) LastEvent = "鱼游过去了 · 再挥一次";
    }

    private static AudioClip CreateCatchSound()
    {
      const int sampleRate = 44100;
      const float duration = .28f;
      int samples = Mathf.CeilToInt(sampleRate * duration);
      var data = new float[samples];
      for (int i = 0; i < samples; i++)
      {
        float t = i / (float)sampleRate;
        float frequency = t < .12f ? 880f : 1320f;
        float envelope = Mathf.Exp(-5.5f * t) * Mathf.Clamp01(Mathf.Min(t * 45f, (duration - t) * 35f));
        data[i] = (Mathf.Sin(2f * Mathf.PI * frequency * t) * .62f +
          Mathf.Sin(2f * Mathf.PI * frequency * 2f * t) * .18f) * envelope;
      }
      var clip = AudioClip.Create("Procedural_FishCatch", samples, 1, sampleRate, false);
      clip.SetData(data, 0);
      return clip;
    }
  }
}
