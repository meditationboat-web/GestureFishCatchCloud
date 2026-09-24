using System.Collections;
using UnityEngine;

namespace GestureFistGame
{
  public sealed class FishRuntimeChecks : MonoBehaviour
  {
    public FishGameController game;
    private bool _started;

    private void Start()
    {
      if (!System.Environment.GetCommandLineArgs().SystemContains("-fish-validation")) return;
      if (!_started) StartCoroutine(RunChecks());
    }

    private IEnumerator RunChecks()
    {
      _started = true;
      yield return null;
      if (game == null) Fail("game reference missing");
      game.ResetRound();
      bool first = game.TryThrow("自动化测试");
      bool blocked = !game.TryThrow("重复测试");
      if (!first || !blocked) Fail("throw state machine failed");
      yield return new WaitForSecondsRealtime(.8f);
      game.net.ResetNet();
      var mouse = FindObjectOfType<FishMouseInput>();
      bool mouseStarted = mouse != null && !mouse.ProcessFrame(Vector2.zero, true, false) && mouse.ProcessFrame(new Vector2(0, .2f), true, false);
      if (!mouseStarted) Fail("mouse upward stroke failed");
      yield return new WaitForSecondsRealtime(.8f);
      game.net.ResetNet();
      if (!game.TryThrow("自动化捕获测试")) Fail("second throw failed");
      yield return new WaitForSecondsRealtime(game.net.raiseDuration + .03f);
      var fish = Instantiate(game.spawner.prototypes[0], game.spawner.transform);
      fish.gameObject.SetActive(true);
      fish.game = game;
      fish.speed = 0;
      fish.transform.position = new Vector3(0, .7f, .05f);
      game.net.TryCatch(fish);
      yield return new WaitForSecondsRealtime(.05f);
      if (game.Score != fish.points || game.Catches != 1) Fail("fish catch score failed");
      Debug.Log("FISH_RUNTIME_VALIDATION_PASS score=" + game.Score + " catches=" + game.Catches + " throwBlocked=true mouseStroke=true");
      Application.Quit(0);
    }

    private void Fail(string message)
    {
      Debug.LogError("FISH_RUNTIME_VALIDATION_FAIL " + message);
      Application.Quit(1);
    }
  }

  internal static class FishValidationArgs
  {
    public static bool SystemContains(this string[] args, string value)
    {
      foreach (var arg in args) if (arg == value) return true;
      return false;
    }
  }
}
