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
      {
        if (!fish.transform.IsChildOf(transform)) continue;
        // Stop the old fish immediately so a reset cannot leave one active for
        // the rest of the frame while the new round accepts keyboard input.
        fish.gameObject.SetActive(false);
        Destroy(fish.gameObject);
      }
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
      // The Doro asset's forward axis is opposite to its travel axis. Rotate
      // it so the face is visibly left while the fish crosses right-to-left.
      fish.transform.rotation = Quaternion.Euler(0, 90, 0);
      fish.gameObject.SetActive(true);
    }
  }
}
