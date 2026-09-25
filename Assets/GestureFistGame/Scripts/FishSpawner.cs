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
    private float _nextSpawn;
    private System.Random _random = new System.Random(20260923);

    private void Start()
    {
      PrepareDoroStyles();
    }

    private void PrepareDoroStyles()
    {
      if (prototypes == null) return;
      foreach (var prototype in prototypes)
      {
        if (prototype == null) continue;
        var style = prototype.GetComponent<DoroFishPresentation>();
        if (style == null) style = prototype.gameObject.AddComponent<DoroFishPresentation>();
        style.Configure(OutlineColor(prototype.points));
      }
    }

    private static Color OutlineColor(int points)
    {
      if (points <= 1) return new Color(.12f, .75f, 1f, 1f);
      if (points <= 3) return new Color(1f, .22f, .18f, 1f);
      if (points <= 5) return new Color(1f, .72f, .08f, 1f);
      return new Color(1f, .20f, .80f, 1f);
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
      var source = prototypes[_random.Next(prototypes.Length)];
      var fish = Instantiate(source, transform);
      fish.game = game;
      fish.transform.position = new Vector3(spawnX, .7f, UnityEngine.Random.Range(-laneHalfWidth, laneHalfWidth));
      fish.speed = UnityEngine.Random.Range(minSpeed, maxSpeed) * (1f + game.Catches * .006f);
      fish.transform.rotation = Quaternion.Euler(0, 180, 0);
      fish.gameObject.SetActive(true);
    }
  }
}
