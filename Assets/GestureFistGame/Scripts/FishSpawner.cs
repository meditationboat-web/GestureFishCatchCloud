using UnityEngine;

namespace GestureFistGame
{
  public sealed class FishSpawner : MonoBehaviour
  {
    public FishGameController game;
    public FishTarget[] prototypes;
    public float spawnInterval = .72f;
    public float spawnZ = 8.2f;
    public float laneWidth = 4.2f;
    public float minSpeed = 1.3f;
    public float maxSpeed = 2.4f;
    private float _nextSpawn;
    private System.Random _random = new System.Random(20260923);

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
      fish.transform.position = new Vector3(UnityEngine.Random.Range(-laneWidth, laneWidth), .7f, spawnZ);
      fish.speed = UnityEngine.Random.Range(minSpeed, maxSpeed) * (1f + game.Catches * .006f);
      fish.transform.rotation = Quaternion.Euler(0, 180, 0);
      fish.gameObject.SetActive(true);
    }
  }
}
