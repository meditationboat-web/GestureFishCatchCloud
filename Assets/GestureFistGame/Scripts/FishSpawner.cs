using UnityEngine;

namespace GestureFistGame
{
  public sealed class FishSpawner : MonoBehaviour
  {
    public FishGameController game;
    public FishTarget[] prototypes;
    public float spawnInterval = .72f;
    [Tooltip("鱼从画面右侧进入")]
    public float spawnX = 3.55f;
    [Tooltip("水道在屏幕上下方向的半宽")]
    public float laneHalfWidth = 3.2f;
    public float minSpeed = 1.3f;
    public float maxSpeed = 2.4f;
    public string ValueWindowHint
    {
      get
      {
        return "红眼鱼 1分   金鱼 3分   狗鱼 5分";
      }
    }
    private float _nextSpawn;
    private System.Random _random = new System.Random(20260923);

    private void Start()
    {
      RemoveLegacyDoroOutlines();
    }

    private void RemoveLegacyDoroOutlines()
    {
      if (prototypes == null) return;
      foreach (var prototype in prototypes)
      {
        if (prototype == null) continue;
        var style = prototype.GetComponent<DoroFishPresentation>();
        if (style != null) Destroy(style);
        foreach (var child in prototype.GetComponentsInChildren<Transform>(true))
          if (child != prototype.transform && child.name.EndsWith("_ValueOutline", System.StringComparison.Ordinal))
            Destroy(child.gameObject);
      }
    }

    private int CurrentTier()
    {
      if (game == null || prototypes == null || prototypes.Length == 0) return 0;
      float elapsed = Mathf.Max(0f, game.roundSeconds - game.TimeRemaining);
      return Mathf.Clamp(Mathf.FloorToInt(elapsed / Mathf.Max(1f, game.roundSeconds / (float)prototypes.Length)), 0, prototypes.Length - 1);
    }

    private FishTarget ChoosePrototype()
    {
      // All three species can appear throughout the round.
      double roll = _random.NextDouble();
      int index = roll < .5 ? 0 : roll < .82 ? 1 : 2;
      return prototypes[Mathf.Min(index, prototypes.Length - 1)];
    }

    private int _pendingCount, _pendingIndex;
    private float _nextFish;
    private float[] _waveLanes;
    private float _nextWave;
    private int _waveId;
    public int WaveId => _waveId;
    private void Update()
    {
      if (game == null || !game.SimulationActive || prototypes == null || prototypes.Length == 0) return;
      if (_pendingIndex < _pendingCount && game.Elapsed >= _nextFish) {
        SpawnOne(_pendingIndex++, _pendingCount);
        _nextFish = game.Elapsed + .18f + (float)_random.NextDouble() * .14f;
      }
      if (game.Elapsed < _nextWave || game.TimeRemaining < 6f) return;
      if (_waveId > 0 && !game.WaveResolved(_waveId)) game.RecordUncaughtWave(_waveId);
      int tier = Mathf.Min(2, Mathf.FloorToInt(game.Elapsed / 30f));
      _waveId++; game.WaveStarted();
      _pendingCount=tier+2;_pendingIndex=0;_nextFish=game.Elapsed;
      _waveLanes=new float[_pendingCount];
      for(int i=0;i<_pendingCount;i++) _waveLanes[i]=Mathf.Lerp(-1.2f,1.2f,(i+.25f+(float)_random.NextDouble()*.5f)/_pendingCount);
      for(int i=_pendingCount-1;i>0;i--){int j=_random.Next(i+1);float lane=_waveLanes[i];_waveLanes[i]=_waveLanes[j];_waveLanes[j]=lane;}
      _nextWave = game.Elapsed + (tier == 0 ? 8 : tier == 1 ? 7 : 6);
    }

    public void ResetSpawner()
    {
      foreach (var fish in FindObjectsOfType<FishTarget>())
      {
        if (!fish.transform.IsChildOf(transform)) continue;
        // Stop the old fish immediately so a reset cannot leave one active for
        // the rest of the frame while the new round accepts keyboard input.
        fish.gameObject.SetActive(false);
        Destroy(fish.gameObject);
      }
      _nextWave = 0; _waveId = 0; _pendingCount=_pendingIndex=0;
    }

    public void StopSpawning() { _nextWave = float.PositiveInfinity; _pendingCount=_pendingIndex=0; }

    private void SpawnOne(int index, int count)
    {
      var source = ChoosePrototype();
      var fish = Instantiate(source, transform);
      fish.game = game;
      fish.transform.position = new Vector3(12.5f+(float)_random.NextDouble()*.6f, .7f, _waveLanes[index]);
      fish.transform.localScale *= 1.65f;
      fish.speed = game.Elapsed < 30 ? 2.4f : game.Elapsed < 60 ? 2.7f : 3f;
      fish.WaveId = _waveId; game.RegisterSpawnedFish();
      // The Doro asset's forward axis is opposite to its travel axis. Rotate
      // it so the face is visibly left while the fish crosses right-to-left.
      fish.transform.rotation = Quaternion.Euler(0, 90, 0);
      fish.gameObject.SetActive(true);
    }
  }
}
