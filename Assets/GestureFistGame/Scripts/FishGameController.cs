using System;
using UnityEngine;

namespace GestureFistGame
{
  public sealed class FishGameController : MonoBehaviour
  {
    public FishNetController net;
    public FishSpawner spawner;
    public FishGestureInput input;
    public int roundSeconds = 90;
    public int Stage { get; private set; } = 2;
    public string StageName => Stage == 1 ? "第一关 · 教程" : Stage == 2 ? "第二关 · 90秒" : "第三关 · 180秒";
    public bool CanAdvance => IsFinished && _stageComplete && Stage < 3;
    private bool _stageComplete, _tutorialCaught;
    private float _tutorialElapsed;
    public void StartCampaign() { Stage = 1; ResetRound(); }
    public void AdvanceStage() { if (!CanAdvance) return; Stage++; ResetRound(); }
    public void ReplayCampaign() { StartCampaign(); }
    public bool autoStart = true;
    [Header("捕鱼音效")]
    public AudioSource catchAudioSource;
    public AudioClip catchSound;
    [Range(0f, 1f)] public float catchVolume = .72f;
    private readonly System.Collections.Generic.Dictionary<string, int> _speciesCounts = new System.Collections.Generic.Dictionary<string, int>();
    public int CapturedCount(string species) => _speciesCounts.TryGetValue(species, out var count) ? count : 0;
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
      if(GetComponent<FishAudio>()==null)gameObject.AddComponent<FishAudio>();
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
      Screen.autorotateToPortrait = false;
      Screen.autorotateToPortraitUpsideDown = false;
      Screen.autorotateToLandscapeLeft = true;
      Screen.autorotateToLandscapeRight = true;
      Screen.orientation = ScreenOrientation.AutoRotation;
#endif
      if (autoStart) ResetRound();
    }

    public enum RoundPhase { Menu, Confirming, Countdown, Playing, Paused, Finished }
    public RoundPhase Phase { get; private set; }
    public bool SimulationActive => Phase == RoundPhase.Playing;
    public int LongestCombo { get; private set; }
    public int PerfectWaves { get; private set; }
    public int CompletedWaves { get; private set; }
    public int PassedFish { get; private set; }
    public int BestScore => PlayerPrefs.GetInt("FishSoloBest", 0);
    public float SyncRate => CompletedWaves == 0 ? 0 : (float)PerfectWaves / CompletedWaves;
    public int Stars => PassedFish == 0 ? 0 : Catches >= PassedFish * .8f ? 3 : Catches >= PassedFish * .6f ? 2 : Catches >= PassedFish * .4f ? 1 : 0;
    public float Countdown { get; private set; }
    public string PlayerResponse(int side) => _responses[side] == 0 ? "—" : (_responseSum[side] / _responses[side]).ToString("0.00") + " 秒";
    public string PlayerReport(int side) => "P" + (side + 1) + " 响应 " + (_responses[side] == 0 ? "—" : (_responseSum[side] / _responses[side]).ToString("0.00") + "秒") + " · 完美 " + PerfectWaves;
    private readonly float[] _responseSum = new float[2];
    private readonly int[] _responses = new int[2];
    private float _stable, _lost, _pauseTime, _firstLift = -1;
    private int _firstSide;
    private float _liftFeedbackUntil;
    public bool PlayerLifted(int side) => SimulationActive && ((_firstLift >= 0 && _firstSide == side) || Elapsed < _liftFeedbackUntil);
    private float _waveStarted;
    public void WaveStarted() { _waveStarted = Elapsed; }
    public const bool SoloMode = true;
    public string FlowHint => Phase == RoundPhase.Confirming ? "请站在摄像头前，露出头部和双手" : Phase == RoundPhase.Countdown ? Mathf.CeilToInt(Countdown).ToString() : Phase == RoundPhase.Paused ? (input != null && input.PlayersReady ? "已就位，稳定后自动继续" : "追踪暂停，请重新入镜") : Stage == 1 && SimulationActive ? "教程：等鱼进入网内，双手腕举过肩膀" : LastEvent;

    private void Update()
    {
      if (Phase == RoundPhase.Menu || IsFinished) return;
      bool ready = input != null && (input.mouseMode || input.PlayersReady);
      if (Phase == RoundPhase.Confirming)
      {
        _stable = ready ? _stable + Time.unscaledDeltaTime : 0;
        if (_stable >= 2) { Phase = RoundPhase.Countdown; Countdown = 3; }
        return;
      }
      if (Phase == RoundPhase.Paused)
      {
        _pauseTime += Time.unscaledDeltaTime;
        _stable = ready ? _stable + Time.unscaledDeltaTime : 0;
        if (_stable >= .8f) ConfirmResume();
        else if (_pauseTime >= 60) FinishRound(false);
        return;
      }
      _lost = ready ? 0 : input != null ? input.TrackingLostSeconds : _lost + Time.unscaledDeltaTime;
      if (_lost > .5f) { PauseRound(); return; }
      if (Phase == RoundPhase.Countdown)
      {
        if (!ready) return;
        Countdown -= Time.unscaledDeltaTime;
        if (Countdown <= 0) { Phase = RoundPhase.Playing; input?.ResetForPlay(); LastEvent = "观察鱼群，双手举过肩膀"; }
        return;
      }
      if (Stage == 1 && _tutorialCaught) { FinishRound(true); return; }
      if (Stage == 1) _tutorialElapsed += Time.unscaledDeltaTime;
      if (Stage != 1) TimeRemaining = Mathf.Max(0, TimeRemaining - Time.unscaledDeltaTime);
      if (_firstLift >= 0 && Elapsed - _firstLift > 1.5f) ResolveLift(2f);
      if (TimeRemaining <= 0) FinishRound(true);
    }
    public float Elapsed => Stage == 1 ? _tutorialElapsed : roundSeconds - TimeRemaining;
    public void ResetRound()
    {
      LiftDebugResult = "";
      _liftFeedbackUntil = 0;
      _stageComplete = _tutorialCaught = false; _tutorialElapsed = 0;
      _resolvedWaves.Clear(); roundSeconds = Stage == 3 ? 180 : 90; _speciesCounts.Clear(); Score = Catches = Combo = LongestCombo = PerfectWaves = CompletedWaves = PassedFish = 0;
      System.Array.Clear(_responseSum, 0, 2); System.Array.Clear(_responses, 0, 2);
      TimeRemaining = roundSeconds; IsFinished = false; IsRunning = true;
      Phase = RoundPhase.Confirming; _stable = _lost = _pauseTime = 0; _firstLift = -1;
      LastEvent = "等待玩家入镜";
      input?.ResetForRound(); net?.ResetNet(); spawner?.ResetSpawner();
    }
    public void ReturnToMenu() { IsRunning=false; IsFinished=false; Phase=RoundPhase.Menu; _firstLift=-1; spawner?.StopSpawning(); net?.ResetNet(); }
    public void PauseRound()
    {
      if (Phase != RoundPhase.Playing && Phase != RoundPhase.Countdown) return;
      Phase = RoundPhase.Paused; _pauseTime = _stable = 0; _liftFeedbackUntil = 0;
      _firstLift = -1; input?.ResetForPlay(); net?.ResetNet();
    }
    public void ConfirmResume()
    {
      if (Phase != RoundPhase.Paused || input == null || (!input.mouseMode && !input.PlayersReady)) return;
      Phase = RoundPhase.Countdown; Countdown = 3; _lost = 0; input.ResetForPlay();
    }
    private void FinishRound(bool complete)
    {
      if (IsFinished) return;
      _stageComplete = complete;
      Score += LongestCombo * 5; IsFinished = true; IsRunning = false; Phase = RoundPhase.Finished; _firstLift = -1;
      if (spawner != null && spawner.WaveId > 0 && !WaveResolved(spawner.WaveId)) RecordUncaughtWave(spawner.WaveId);
      spawner?.StopSpawning();
      if (complete && Stage != 1 && Score > BestScore) { PlayerPrefs.SetInt("FishSoloBest", Score); PlayerPrefs.Save(); }
      LastEvent = complete ? (Stage == 3 ? "三关挑战完成！" : StageName + "完成") : "本关中断，请重试";
    }
    public string LiftDebugResult { get; private set; } = "";
    public string PendingLiftStatus => _firstLift < 0 ? "无" : $"P{_firstSide + 1} 等待队友，已过 {Elapsed - _firstLift:0.00} 秒";
    public bool CanAcceptLift => SimulationActive && net != null && net.CanThrow;
    public void CancelPendingLift() { if (_firstLift >= 0) LiftDebugResult = "待配对的举网因追踪丢失取消"; _firstLift = -1; _liftFeedbackUntil = 0; }
    public void RegisterLift(int side)
    {
      if (!SimulationActive || net == null || !net.CanThrow) return;
      if (SoloMode) { _responses[0]++; _responseSum[0] += Mathf.Max(0, Elapsed - _waveStarted); ResolveLift(0f); return; }
      if (_firstLift < 0) { _responses[side]++; _responseSum[side] += Mathf.Max(0, Elapsed - _waveStarted); _firstLift = Elapsed; _firstSide = side; LastEvent = "P" + (side + 1) + " 已举网，等待队友"; return; }
      if (_firstSide == side) return;
      float gap = Elapsed - _firstLift;
      // Response is measured from the current fish wave appearing, not against the other player.
      _responses[side]++;
      _responseSum[side] += Mathf.Max(0, Elapsed - _waveStarted);
      ResolveLift(gap);
    }
    private void ResolveLift(float gap)
    {
      if (gap <= 1.5f) _liftFeedbackUntil = Elapsed + .7f;
      _firstLift = -1;
      int quality = gap <= .5f ? 2 : gap <= 1.5f ? 1 : 0;
      net.ResolveCooperation(quality);
      LiftDebugResult = quality == 0
        ? "配对超时（超过 1.5 秒），本次不起网"
        : $"合作通过，时差 {gap:0.00} 秒。起网指令后网状态：{net.PhaseName}";

    }
    public void RecordWave(int quality, int candidates)
    {
      if (candidates == 0) { LastEvent = "网内暂无鱼"; return; }
      GetComponent<FishAudio>()?.Result(quality);
      CompletedWaves++;
      if (quality == 2) { if (Stage == 1) _tutorialCaught = true; PerfectWaves++; Combo++; LongestCombo = Mathf.Max(LongestCombo, Combo); }
      else Combo = 0;
      LastEvent = quality == 2 ? "起网成功！ 连击 × " + Combo : quality == 1 ? "差一点，鱼跑了一半" : "鱼跑啦，下波再来";
    }
    public void RegisterSpawnedFish() { PassedFish++; }
    private readonly System.Collections.Generic.HashSet<int> _resolvedWaves = new System.Collections.Generic.HashSet<int>();
    public bool WaveResolved(int id) => _resolvedWaves.Contains(id);
    public void MarkWaveResolved(int id) { _resolvedWaves.Add(id); }
    public void RecordUncaughtWave(int id) { if (_resolvedWaves.Add(id)) RecordWave(0, 1); }
    public bool TryThrow(string source) => SimulationActive && net != null && net.TryThrow();

    public void CatchFish(FishTarget fish)
    {
      if (!SimulationActive || fish == null || fish.Caught) return;
      fish.MarkCaught();
      Score += fish.points;
      Catches++;
      _speciesCounts[fish.displayName] = CapturedCount(fish.displayName) + 1;
      // Cooperative result text remains visible after awarding individual fish points.
      if (catchAudioSource != null && catchSound != null)
        catchAudioSource.PlayOneShot(catchSound, catchVolume);
      FishCaught?.Invoke(fish);
    }

    public void MissedFish()
    {
      // Wave resolution, rather than each individual missed fish, owns the combo.
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
