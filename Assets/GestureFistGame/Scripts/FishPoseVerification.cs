using System.Collections;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using UnityEngine;

namespace GestureFistGame
{
  // Runs the actual input adapter with deterministic pose frames, without a webcam.
  public sealed class FishPoseVerification : MonoBehaviour
  {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
      if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fish-pose-check") >= 0)
        new GameObject("Pose verification").AddComponent<FishPoseVerification>();
    }
    private FishGestureInput input;
    private FishGameController game;
    private int left, right, throws;
    private IEnumerator Start()
    {
      yield return null;
      game = FindObjectOfType<FishGameController>(); input = game.input;
      game.ResetRound(); game.spawner?.StopSpawning();
      input.mouseMode = false;
      input.HandWaveDetected += side => { if (side == FishGestureInput.HandSide.Left) left++; else right++; };
      input.UpwardWaveDetected += v => throws++;
      input.tracker.fishInput = input; input.tracker.input = null;
      Check(input.tracker.LoadModels() && input.tracker.Pose != null && input.tracker.Hands == null, "pose-only native model");
      // Verify that the native two-pose graph really executes, including an empty frame.
      var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
      texture.SetPixels32(new Color32[64 * 64]); texture.Apply();
      var blank = PoseLandmarkerResult.Alloc(2);
      using (var image = new Mediapipe.Image(Mediapipe.ImageFormat.Types.Format.Srgba, texture))
        input.tracker.Pose.TryDetectForVideo(image, 1, null, ref blank);
      Destroy(texture);
      yield return Feed(true, true, raisedY: .4f);
      Check(left == 0 && right == 0, "above shoulder but below head does not trigger");
      yield return Feed(true, true, headConfidence: .1f);
      Check(left == 0 && right == 0, "head reference missing does not trigger");
      yield return Feed(false, false);
      yield return Feed(true, false);
      Check(left == 1 && right == 0 && throws == 0, "single side cannot throw");
      yield return Feed(true, false, duration: .85f);
      yield return Feed(true, true);
      Check(throws == 1, "cooperation .85 seconds accepted");
      yield return Feed(true, true);
      Check(left == 1 && right == 1 && throws == 1, "held arms do not repeat");
      yield return FeedEmpty(.65f);
      yield return Feed(true, true);
      Check(left == 1 && right == 1, "tracking loss does not rearm");
      game.net.ResetNet();
      yield return Feed(false, false);
      yield return Feed(true, true);
      Check(throws == 2 && left == 2 && right == 2, "lower then raise rearms");
      Reset();
      yield return Feed(true, false);
      yield return Feed(true, false, duration: 1.3f);
      yield return Feed(true, true);
      Check(throws == 0, "expired cooperation rejected");
      Reset();
      yield return Feed(true, true, false, .1f);
      Check(left == 0 && right == 0, "low confidence ignored");
      Reset();
      yield return Feed(false, false, true);
      yield return Feed(true, false, true);
      Check(left == 1 && right == 0 && throws == 0, "one person cannot supply both slots in two-person mode");
      yield return Feed(true, true, true);
      Check(throws == 1, "two people cooperate despite result order changes");
      Debug.Log("FISH_POSE_VALIDATION_PASS poseOnly=true maxPoses=2 overHead=true headRequired=true hold=true rearm=true loss=true confidence=true onePerson=true twoPeople=true window=1.2");
      Application.Quit(0);
    }
    private void Reset()
    {
      game.net.ResetNet(); input.ResetForRound(); left = right = throws = 0;
    }
    private IEnumerator Feed(bool l, bool r, bool two = false, float confidence = 1, float duration = .18f, float raisedY = .2f, float headConfidence = 1)
    {
      float until = Time.unscaledTime + duration; int frame = 0;
      while (Time.unscaledTime < until)
      {
        var result = PoseLandmarkerResult.Alloc(2);
        if (!two) result.poseLandmarks.Add(Person(.5f, l, r, confidence, raisedY, headConfidence));
        else
        {
          var a = Person(.25f, l, false, confidence, raisedY, headConfidence);
          var b = Person(.75f, false, r, confidence, raisedY, headConfidence);
          result.poseLandmarks.Add(frame % 2 == 0 ? a : b);
          result.poseLandmarks.Add(frame % 2 == 0 ? b : a);
        }
        input.ReceivePose(result); frame++;
        yield return null;
      }
    }
    private IEnumerator FeedEmpty(float seconds)
    {
      float until = Time.unscaledTime + seconds;
      while (Time.unscaledTime < until) { input.ReceivePose(default); yield return null; }
    }
    private static NormalizedLandmarks Person(float x, bool l, bool r, float confidence, float raisedY, float headConfidence)
    {
      var pose = NormalizedLandmarks.Alloc(33);
      for (int i = 0; i < 33; i++)
      {
        float px = x, py = .7f;
        if (i == 11 || i == 15) px -= .08f;
        if (i == 12 || i == 16) px += .08f;
        if (i < 11) py = .35f;
        if (i == 11 || i == 12) py = .5f;
        if (i == 15) py = l ? raisedY : .7f;
        if (i == 16) py = r ? raisedY : .7f;
        pose.landmarks.Add(NormalizedLandmark.CreateFrom(new Mediapipe.NormalizedLandmark
          { X = px, Y = py, Z = 0, Visibility = i < 11 ? headConfidence : confidence, Presence = confidence }));
      }
      return pose;
    }
    private static void Check(bool value, string label)
    {
      if (value) { Debug.Log("POSE_CHECK_PASS " + label); return; }
      Debug.LogError("FISH_POSE_VALIDATION_FAIL " + label);
      Application.Quit(1);
      throw new System.Exception(label);
    }
  }
}
