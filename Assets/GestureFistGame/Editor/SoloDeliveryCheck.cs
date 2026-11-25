using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GestureFistGame;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.PoseLandmarker;

public static class SoloDeliveryCheck
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/GestureFistGame/Scenes/GestureFishCatchGame.unity");
        var game = UnityEngine.Object.FindObjectOfType<FishGameController>();
        var input = UnityEngine.Object.FindObjectOfType<FishGestureInput>();
        input.mouseMode = false;
        game.StartCampaign();
        SendPose(input, false);
        if (!input.PlayersReady) throw new Exception("One player did not qualify for the round");
        typeof(FishGameController).GetProperty("Phase").SetValue(game, FishGameController.RoundPhase.Playing);
        foreach (var water in UnityEngine.Object.FindObjectsOfType<InteractiveWaterSurface>())
            water.gameObject.SetActive(false);
        input.poseHoldSeconds = 10f;
        SendPose(input, false);
        SendPose(input, true, false);
        if (game.net.PhaseName != "Ready") throw new Exception("One raised wrist triggered the net");
        SendPose(input, true, true);
        if (game.net.PhaseName != "Raising") throw new Exception("Solo lift did not raise the net");
        if (!UnityEngine.Object.FindObjectOfType<MediaPipeTracker>().LoadModels())
            throw new Exception("Pose model failed to load");
        Debug.Log("SOLO_DELIVERY_PASS");
    }

    private static void SendPose(FishGestureInput input, bool leftRaised, bool rightRaised = false)
    {
        var result = PoseLandmarkerResult.Alloc(1);
        var pose = NormalizedLandmarks.Alloc(33);
        for (int i = 0; i < 33; i++)
        {
            float x = i == 11 || i == 15 ? .4f : i == 12 || i == 16 ? .6f : .5f;
            float y = i < 11 ? .35f : i == 15 ? (leftRaised ? .45f : .7f) : i == 16 ? (rightRaised ? .45f : .7f) : .5f;
            pose.landmarks.Add(NormalizedLandmark.CreateFrom(new Mediapipe.NormalizedLandmark { X=x, Y=y, Visibility=1, Presence=1 }));
        }
        result.poseLandmarks.Add(pose);
        input.ReceivePose(result);
    }
}
