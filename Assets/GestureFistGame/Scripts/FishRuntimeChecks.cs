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
      // Keep the validation deterministic; only the fish created by this
      // test should be available during the catch assertion.
      game.spawner?.StopSpawning();
      var trigger = game.net.GetComponent<BoxCollider>();
      if (trigger == null || trigger.size.x <= trigger.size.y * 4f || trigger.size.z <= trigger.size.y * 4f || trigger.size.y > .5f)
        Fail("horizontal net trigger dimensions failed");
      if (!game.net.HasHandAnchors)
        Fail("hand-linked net anchors missing");
      var strands = game.net.visual.GetComponentsInChildren<LineRenderer>(true);
      if (strands.Length < 20) Fail("horizontal rope grid missing");
      foreach (var strand in strands)
        for (int i = 0; i < strand.positionCount; i++)
          if (Mathf.Abs(strand.GetPosition(i).y) > .06f) Fail("net surface is not horizontal");
      var avatars = FindObjectsOfType<FishPlayerAvatar>();
      if (avatars.Length < 2 || avatars[0].gestureArm == null || avatars[1].gestureArm == null)
        Fail("throwing avatar rig links missing");
      var ragdolls = FindObjectsOfType<FishAvatarRagdoll>();
      if (ragdolls.Length < 2) Fail("soft ragdoll components missing");
      foreach (var ragdoll in ragdolls) ragdoll.Kick(1f);
      yield return new WaitForSecondsRealtime(.04f);
      if (ragdolls[0].MotionAmount < .02f && ragdolls[1].MotionAmount < .02f)
        Fail("soft ragdoll spring did not respond");
      var beforeLeft = avatars[0].gestureArm.localRotation;
      var beforeRight = avatars[1].gestureArm.localRotation;
      game.input.RegisterMouseWave(FishGestureInput.HandSide.Left);
      game.input.RegisterMouseWave(FishGestureInput.HandSide.Right);
      yield return new WaitForSecondsRealtime(.12f);
      if (Quaternion.Angle(beforeLeft, avatars[0].gestureArm.localRotation) < 3f ||
          Quaternion.Angle(beforeRight, avatars[1].gestureArm.localRotation) < 3f)
        Fail("two-handed throwing animation did not move");
      game.net.ResetNet();
      game.input.ResetForRound();
      game.input.RegisterMouseWave(FishGestureInput.HandSide.Left);
      yield return new WaitForSecondsRealtime(.85f);
      game.input.RegisterMouseWave(FishGestureInput.HandSide.Right);
      if (game.net.CanThrow) Fail("expanded cooperation window rejected .85 second hand gap");
      Debug.Log("COOPERATION_WINDOW_PASS seconds=" + game.input.cooperationWindow + " gap=.85");
      game.net.ResetNet();
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
      var fish = Instantiate(game.spawner.prototypes[0], game.spawner.transform);
      fish.gameObject.SetActive(true);
      fish.game = game;
      fish.speed = 0;
      // Away from the old narrow wall: this must be caught by the wide surface.
      var insideNet = game.net.visual.TransformPoint(new Vector3(FishNetGeometry.HalfWidth * .65f, 0f, FishNetGeometry.HalfDepth * .65f));
      insideNet.y = 0f;
      fish.transform.position = insideNet;
      var outside = Instantiate(game.spawner.prototypes[0], game.spawner.transform);
      outside.gameObject.SetActive(true);
      outside.game = game;
      outside.speed = 0;
      outside.transform.position = new Vector3(FishNetGeometry.HalfWidth + 2f, 0f, 0f);
      int points = fish.points;
      yield return new WaitForSecondsRealtime(.05f);
      if (game.Catches != 0) Fail("idle net caught fish");
      if (!game.TryThrow("自动化捕获测试")) Fail("second throw failed");
      yield return new WaitForSecondsRealtime(.12f);
      if (fish == null || !fish.Caught || (outside != null && outside.Caught) || game.Score != points || game.Catches != 1)
        Fail("horizontal overlap catch or outside rejection failed score=" + game.Score + " catches=" + game.Catches + " fish=" + (fish != null ? fish.Caught.ToString() : "removed") + " bounds=" + (fish != null ? fish.VisualBounds.ToString() : "none"));
      yield return new WaitForSecondsRealtime(.8f);
      if (!game.net.CanThrow || !game.net.visual.gameObject.activeSelf ||
          (game.net.submergedVisual != null && game.net.submergedVisual.gameObject.activeSelf))
        Fail("horizontal net did not remain visible underwater at idle");
      Debug.Log("FISH_RUNTIME_VALIDATION_PASS score=" + game.Score + " catches=" + game.Catches + " throwBlocked=true mouseStroke=true horizontalNet=true overlapCatch=true outsideRejected=true idleRestored=true");
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


