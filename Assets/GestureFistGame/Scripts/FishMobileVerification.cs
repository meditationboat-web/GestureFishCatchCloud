using System.Collections;
using System.IO;
using UnityEngine;
namespace GestureFistGame
{
  public sealed class FishMobileVerification : MonoBehaviour
  {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
      if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fish-mobile-check") >= 0 || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fish-spawn-check") >= 0)
        new GameObject("Fish mobile verification").AddComponent<FishMobileVerification>();
    }
    private IEnumerator CheckSpawn(FishGameController game)
    {
      Physics.SyncTransforms();
      bool passed = true;
      var fishList = new System.Collections.Generic.List<FishTarget>();
      foreach (var source in game.spawner.prototypes)
      {
        foreach (float lane in new[] { -3.2f, 0f, 3.2f })
        {
          var fish = Instantiate(source, game.spawner.transform);
          fish.transform.position = new Vector3(game.spawner.spawnX, .7f, lane);
          fish.transform.rotation = Quaternion.Euler(0, 90, 0);
          fish.speed = 1.8f;
          fish.gameObject.SetActive(true);
          fishList.Add(fish);
        }
      }
      yield return new WaitForSecondsRealtime(1f);
      foreach (var fish in fishList)
      {
        bool alive = fish != null && fish.gameObject.activeSelf;
        passed &= alive;
        Debug.Log("SPAWN_CHECK aliveAfterOneSecond=" + alive + (alive ? " x=" + fish.transform.position.x : ""));
      }
      yield return new WaitForSecondsRealtime(2f);
      foreach (var fish in fishList)
      {
        bool crossed = fish != null && fish.gameObject.activeSelf && fish.transform.position.x < 0f;
        passed &= crossed;
        Debug.Log("SPAWN_CHECK crossedWaterCenter=" + crossed);
      }
      // Record the last live position to distinguish contact removal from
      // the legacy offscreen fallback at x < -4.7.
      float deadline = Time.unscaledTime + 3f;
      var lastX = new float[fishList.Count];
      while (Time.unscaledTime < deadline)
      {
        for (int i = 0; i < fishList.Count; i++)
          if (fishList[i] != null && fishList[i].gameObject.activeSelf) lastX[i] = fishList[i].transform.position.x;
        yield return new WaitForFixedUpdate();
      }
      for (int i = 0; i < fishList.Count; i++)
      {
        bool bankRemoved = fishList[i] == null && lastX[i] < -3f && lastX[i] > -4.7f;
        passed &= bankRemoved;
        Debug.Log("SPAWN_CHECK removedAtLeftBank=" + bankRemoved + " lastX=" + lastX[i]);
      }
      // Also run the real timed spawner, rather than only manual clones.
      game.ResetRound();
      yield return new WaitForSecondsRealtime(2f);
      int active = FindObjectsOfType<FishTarget>().Length;
      passed &= active >= 2;
      Debug.Log("SPAWN_CHECK timedSpawnerActiveFish=" + active);
      Debug.Log("FISH_SPAWN_VALIDATION_" + (passed ? "PASS" : "FAIL"));
      Application.Quit(passed ? 0 : 2);
    }
    private IEnumerator Start()
    {
      Application.runInBackground = true;
      bool landscape = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fish-landscape") >= 0;
      Screen.SetResolution(landscape ? 1280 : 390, landscape ? 720 : 844, false);
      yield return new WaitForSecondsRealtime(2);
      var game = FindObjectOfType<FishGameController>();
      var welcome = FindObjectOfType<FishStartScreen>();
      bool menuIdle = welcome != null && welcome.IsMenuOpen && !game.IsRunning && FindObjectsOfType<FishTarget>().Length == 0;
      Debug.Log("START_MENU_IDLE=" + menuIdle);
      ScreenCapture.CaptureScreenshot(Path.GetFullPath("Artifacts/fish-start-screen.png"));
      yield return new WaitForSecondsRealtime(.3f);
      welcome.Begin(false);
      Debug.Log("START_MENU_BEGIN=" + (game.IsRunning && !welcome.IsMenuOpen));
      game.input.UseMouse(); game.ResetRound(); game.spawner.StopSpawning();
      if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fish-spawn-check") >= 0)
      {
        yield return CheckSpawn(game);
        yield break;
      }
      var prototype = game.spawner.prototypes[0];
      var fish = Instantiate(prototype, game.spawner.transform);
      fish.transform.position = new Vector3(-3.3f, 0f, 0f);
      fish.transform.rotation = Quaternion.Euler(0,90,0);
      fish.speed = 3f; fish.gameObject.SetActive(true);
      yield return new WaitForSecondsRealtime(1);
      bool groundRemoved = fish == null;
      Directory.CreateDirectory("Artifacts");
      File.WriteAllText("Artifacts/fish-mobile-check.txt", "Ground contact removes swimming fish: " + groundRemoved + "\n");
      bool speciesValid = game.spawner.prototypes.Length == 3;
      for (int i = 0; i < game.spawner.prototypes.Length; i++)
      {
        var preview = Instantiate(game.spawner.prototypes[i], game.spawner.transform);
        preview.transform.position = new Vector3(0,0,(i-1)*1.6f);
        preview.transform.rotation = Quaternion.Euler(0,90,0);
        preview.speed = 0; preview.gameObject.SetActive(true);
        var before = game.Score;
        game.CatchFish(preview);
        speciesValid &= game.Score - before == preview.points && game.CapturedCount(preview.displayName) == 1;
        Debug.Log("SPECIES_SCORE " + preview.displayName + " points=" + preview.points + " delta=" + (game.Score-before));
        Destroy(preview.gameObject);
        preview = Instantiate(game.spawner.prototypes[i], game.spawner.transform);
        preview.transform.position = new Vector3(0,0,(i-1)*1.6f);
        preview.transform.rotation = Quaternion.Euler(0,90,0);
        preview.speed = 0; preview.gameObject.SetActive(true);
      }
      Debug.Log("FISH_SPECIES_VALIDATION_" + (speciesValid ? "PASS" : "FAIL"));
      yield return new WaitForSecondsRealtime(.2f);
      ScreenCapture.CaptureScreenshot(Path.GetFullPath(landscape ? "Artifacts/fish-ui-landscape.png" : "Artifacts/fish-mobile-390x844.png"));
      yield return new WaitForSecondsRealtime(.4f);
      game.roundSeconds = 1; game.ResetRound();
      yield return new WaitForSecondsRealtime(1.2f);
      Debug.Log("SPECIES_COUNTERS_RESET=" + (game.CapturedCount("红眼鱼") == 0 && game.CapturedCount("金鱼") == 0 && game.CapturedCount("白斑狗鱼") == 0));
      yield return null;
      ScreenCapture.CaptureScreenshot(Path.GetFullPath(landscape ? "Artifacts/fish-ui-landscape-finish.png" : "Artifacts/fish-mobile-finish-390x844.png"));
      yield return new WaitForSecondsRealtime(.4f);
      Application.Quit(groundRemoved && speciesValid ? 0 : 2);
    }
  }
}

