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
      Screen.orientation = ScreenOrientation.Portrait;
      Screen.autorotateToPortrait = true;
      Screen.autorotateToPortraitUpsideDown = true;
      Screen.autorotateToLandscapeLeft = false;
      Screen.autorotateToLandscapeRight = false;
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
    public int BestScore => PlayerPrefs.GetInt("FishTeamBest", 0);
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
    public string FlowHint => Phase == RoundPhase.Confirming ? "两位玩家请入镜，各露出双手" : Phase == RoundPhase.Countdown ? Mathf.CeilToInt(Countdown).ToString() : Phase == RoundPhase.Paused ? (input != null && input.TwoPlayersReady ? "两人已就位，点击确认继续" : "追踪暂停，请两人重新入镜") : LastEvent;

    private void Update()
    {
      if (Phase == RoundPhase.Menu || IsFinished) return;
      bool ready = input != null && (input.mouseMode || input.TwoPlayersReady);
      if (Phase == RoundPhase.Confirming)
      {
        _stable = ready ? _stable + Time.unscaledDeltaTime : 0;
        if (_stable >= 2) { Phase = RoundPhase.Countdown; Countdown = 3; }
        return;
      }
      if (Phase == RoundPhase.Paused) { _pauseTime += Time.unscaledDeltaTime; if (_pauseTime >= 60) FinishRound(false); return; }
      _lost = ready ? 0 : input != null ? input.TrackingLostSeconds : _lost + Time.unscaledDeltaTime;
      if (_lost > .5f) { PauseRound(); return; }
      if (Phase == RoundPhase.Countdown)
      {
        if (!ready) return;
        Countdown -= Time.unscaledDeltaTime;
        if (Countdown <= 0) { Phase = RoundPhase.Playing; input?.ResetForPlay(); LastEvent = "观察鱼群，两人双手举过头顶"; }
        return;
      }
      TimeRemaining = Mathf.Max(0, TimeRemaining - Time.unscaledDeltaTime);
      if (_firstLift >= 0 && Elapsed - _firstLift > 1.5f) ResolveLift(2f);
      if (TimeRemaining <= 0) FinishRound(true);
    }
    public float Elapsed => roundSeconds - TimeRemaining;
    public void ResetRound()
    {
      _liftFeedbackUntil = 0;
      _resolvedWaves.Clear(); roundSeconds = 90; _speciesCounts.Clear(); Score = Catches = Combo = LongestCombo = PerfectWaves = CompletedWaves = PassedFish = 0;
      System.Array.Clear(_responseSum, 0, 2); System.Array.Clear(_responses, 0, 2);
      TimeRemaining = 90; IsFinished = false; IsRunning = true;
      Phase = RoundPhase.Confirming; _stable = _lost = _pauseTime = 0; _firstLift = -1;
      LastEvent = "等待两位玩家";
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
      if (Phase != RoundPhase.Paused || input == null || (!input.mouseMode && !input.TwoPlayersReady)) return;
      Phase = RoundPhase.Countdown; Countdown = 3; _lost = 0; input.ResetForPlay();
    }
    private void FinishRound(bool complete)
    {
      if (IsFinished) return;
      Score += LongestCombo * 5; IsFinished = true; IsRunning = false; Phase = RoundPhase.Finished; _firstLift = -1;
      if (spawner != null && spawner.WaveId > 0 && !WaveResolved(spawner.WaveId)) RecordUncaughtWave(spawner.WaveId);
      spawner?.StopSpawning();
      if (complete && Score > BestScore) { PlayerPrefs.SetInt("FishTeamBest", Score); PlayerPrefs.Save(); }
      LastEvent = "回合结束";
    }
    public bool CanAcceptLift => SimulationActive && net != null && net.CanThrow;
    public void CancelPendingLift() { _firstLift = -1; _liftFeedbackUntil = 0; }
    public void RegisterLift(int side)
    {
      if (!SimulationActive || net == null || !net.CanThrow) return;
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
      net.ResolveCooperation(gap <= .5f ? 2 : gap <= 1.5f ? 1 : 0);
    }
    public void RecordWave(int quality, int candidates)
    {
      if (candidates == 0) { LastEvent = "网内暂无鱼"; return; }
      GetComponent<FishAudio>()?.Result(quality);
      CompletedWaves++;
      if (quality == 2) { PerfectWaves++; Combo++; LongestCombo = Mathf.Max(LongestCombo, Combo); }
      else Combo = 0;
      LastEvent = quality == 2 ? "完美协作！ 连击 × " + Combo : quality == 1 ? "差一点，鱼跑了一半" : "鱼跑啦，下波再来";
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
