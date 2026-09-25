using UnityEngine;

namespace GestureFistGame
{
  public sealed class FishSpawner : MonoBehaviour
  {
    public FishGameController game;
    public FishTarget[] prototypes;
    public float spawnInterval = .72f;
    [Tooltip("鱼从画面右侧进入")]
    public float spawnX = 4.1f;
    [Tooltip("水道在屏幕上下方向的半宽")]
    public float laneHalfWidth = 3.2f;
    public float minSpeed = 1.3f;
    public float maxSpeed = 2.4f;
    public string ValueWindowHint
    {
      get
      {
        int tier = CurrentTier();
        int value = prototypes == null || prototypes.Length == 0 ? 1 : prototypes[Mathf.Clamp(tier, 0, prototypes.Length - 1)].points;
        return "当前时段鱼值：" + value + " 分 · 双手合作抓准时机";
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
      int tier = CurrentTier();
      // The current time band determines the main value. A small chance of
      // the previous band keeps the river lively without recoloring Doro.
      if (tier > 0 && _random.NextDouble() < .25) tier--;
      return prototypes[Mathf.Clamp(tier, 0, prototypes.Length - 1)];
    }

    private void Update()
    {
      if (game == null || !game.IsRunning || prototypes == null || prototypes.Length == 0) return;
      if (Time.unscaledTime < _nextSpawn) return;
      SpawnOne();
      _nextSpawn = Time.unscaledTime + spawnInterval * UnityEngine.Random.Range(.72f, 1.18f);
    }

    public void ResetSpawner()
    {
      foreach (var fish in FindObjectsOfType<FishTarget>())
        if (fish.transform.IsChildOf(transform)) Destroy(fish.gameObject);
      _nextSpawn = Time.unscaledTime + .4f;
    }

    public void StopSpawning() { _nextSpawn = float.PositiveInfinity; }

    private void SpawnOne()
    {
      var source = ChoosePrototype();
      var fish = Instantiate(source, transform);
      fish.game = game;
      fish.transform.position = new Vector3(spawnX, .7f, UnityEngine.Random.Range(-laneHalfWidth, laneHalfWidth));
      fish.speed = UnityEngine.Random.Range(minSpeed, maxSpeed) * (1f + game.Catches * .006f);
      // Doro's face points in the same direction as its travel (right to left)
      // instead of toward the near/bottom bank.
      fish.transform.rotation = Quaternion.Euler(0, 270, 0);
      fish.gameObject.SetActive(true);
    }
  }
}
