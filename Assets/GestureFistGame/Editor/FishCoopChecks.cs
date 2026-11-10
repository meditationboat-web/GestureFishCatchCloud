using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace GestureFistGame.Editor
{
  public static class FishCoopChecks
  {
    static void Set(object o, string n, object v) => o.GetType().GetField("<"+n+">k__BackingField", BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
    static void Check(bool ok,string name) { if(!ok) throw new Exception("COOP CHECK FAILED: "+name); Debug.Log("COOP CHECK OK: "+name); }
    static Mediapipe.Tasks.Components.Containers.NormalizedLandmarks Person(float x,bool l,bool r)
    {
      var p=Mediapipe.Tasks.Components.Containers.NormalizedLandmarks.Alloc(33);
      for(int i=0;i<33;i++) {
        float px=x, y=i<11?.35f:.5f;
        if(i==11 || i==15)px-=.08f;if(i==12 || i==16)px+=.08f;
        if(i==15)y=l?.2f:.7f;if(i==16)y=r?.2f:.7f;
        p.landmarks.Add(Mediapipe.Tasks.Components.Containers.NormalizedLandmark.CreateFrom(new Mediapipe.NormalizedLandmark{X=px,Y=y,Visibility=1,Presence=1}));
      }return p;
    }
    public static void Run()
    {
      var root=new GameObject("Coop verification");
      try {
        var game=root.AddComponent<FishGameController>(); game.autoStart=false;
        var net=root.AddComponent<FishNetController>(); net.game=game; game.net=net;
        net.visual=new GameObject("Net visual").transform; net.visual.SetParent(root.transform); net.linkVisualToHands=false;
        game.ResetRound();
        Check(game.TimeRemaining==90 && game.Phase==FishGameController.RoundPhase.Confirming,"90 seconds and confirmation gate");
        game.RegisterLift(0); Check(game.CompletedWaves==0,"cannot lift before countdown");
        Set(game,"Phase",FishGameController.RoundPhase.Playing);
        FishTarget[] fish=new FishTarget[4];
        for(int i=0;i<4;i++) {var obj=new GameObject("Fish"+i); obj.transform.SetParent(root.transform); obj.transform.position=new Vector3(i*.1f,0,0); fish[i]=obj.AddComponent<FishTarget>(); fish[i].game=game; fish[i].WaveId=1; fish[i].points=3;game.RegisterSpawnedFish();}
        game.RegisterLift(0); Set(game,"TimeRemaining",89.5f); game.RegisterLift(1);
        Check(game.Catches==4 && game.Score==12 && game.Combo==1,"0.5 second perfect captures all");
        net.ResetNet();
        for(int i=0;i<4;i++){UnityEngine.Object.DestroyImmediate(fish[i].gameObject);var obj=new GameObject("Fish"+i);obj.transform.SetParent(root.transform);obj.transform.position=new Vector3(i*.1f,0,0);fish[i]=obj.AddComponent<FishTarget>();fish[i].game=game;fish[i].WaveId=2;fish[i].points=1;game.RegisterSpawnedFish();}
        game.RegisterLift(0);Set(game,"TimeRemaining",88f);game.RegisterLift(1);
        Check(game.Catches==6 && game.Score==14 && game.Combo==0,"1.5 second normal keeps nearest half and breaks combo");
        Check(fish[0].Caught && fish[1].Caught && !fish[2].Caught && !fish[3].Caught,"normal selects fish nearest center");
        net.ResetNet(); net.ResolveCooperation(2);Check(game.CompletedWaves==2,"resolved wave cannot score twice");
        net.ResetNet();
        for(int i=0;i<4;i++){if(fish[i]!=null)UnityEngine.Object.DestroyImmediate(fish[i].gameObject);var obj=new GameObject("FailFish"+i);obj.transform.SetParent(root.transform);obj.transform.position=Vector3.zero;fish[i]=obj.AddComponent<FishTarget>();fish[i].game=game;fish[i].WaveId=3;}
        game.RegisterLift(0);Set(game,"TimeRemaining",86.4f);game.RegisterLift(1);
        Check(game.Catches==6 && fish[0].Escaping && game.Combo==0,"over 1.5 seconds all fish escape");
        game.RecordWave(2,2);game.RecordWave(2,2);game.RecordWave(1,2);Check(game.LongestCombo==2 && game.Combo==0,"longest streak retained");
        game.PauseRound();Check(!game.SimulationActive,"tracking pause freezes simulation");
        game.ConfirmResume();Check(game.Phase==FishGameController.RoundPhase.Paused,"cannot resume without players");
        typeof(FishGameController).GetMethod("FinishRound",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{false});
        Check(game.Score==24,"longest combo bonus awarded once");
        typeof(FishGameController).GetMethod("FinishRound",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{false});Check(game.Score==24,"finish idempotent");
        game.ResetRound(); Set(game,"Phase",FishGameController.RoundPhase.Playing);
        var input=root.AddComponent<FishGestureInput>();game.input=input;input.game=game;input.mouseMode=false;
        int lifts=0;input.HandWaveDetected+=side=>lifts++;
        var result=Mediapipe.Tasks.Vision.PoseLandmarker.PoseLandmarkerResult.Alloc(2);
        result.poseLandmarks.Add(Person(.25f,false,false));input.ReceivePose(result);
        Check(!input.TwoPlayersReady,"one person cannot confirm");
        result.poseLandmarks.Add(Person(.75f,false,false));input.ReceivePose(result);
        Check(input.TwoPlayersReady,"two people with wrists and head detected");
        var sample=typeof(FishGestureInput).GetMethod("SamplePerson",BindingFlags.Instance|BindingFlags.NonPublic);
        var state=typeof(FishGestureInput).GetField("_left",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(input);
        sample.Invoke(input,new object[]{state,FishGestureInput.HandSide.Left,Person(.25f,false,false).landmarks,1f});
        sample.Invoke(input,new object[]{state,FishGestureInput.HandSide.Left,Person(.25f,false,false).landmarks,1.2f});
        sample.Invoke(input,new object[]{state,FishGestureInput.HandSide.Left,Person(.25f,true,false).landmarks,1.3f});
        sample.Invoke(input,new object[]{state,FishGestureInput.HandSide.Left,Person(.25f,true,false).landmarks,1.5f});
        Check(lifts==0,"single wrist above head is invalid");
        sample.Invoke(input,new object[]{state,FishGestureInput.HandSide.Left,Person(.25f,true,true).landmarks,1.6f});
        sample.Invoke(input,new object[]{state,FishGestureInput.HandSide.Left,Person(.25f,true,true).landmarks,1.8f});
        Check(lifts==1,"both wrists above estimated head trigger");
        sample.Invoke(input,new object[]{state,FishGestureInput.HandSide.Left,Person(.25f,true,true).landmarks,2f});
        Check(lifts==1,"held hands do not repeat");
        Debug.Log("FISH_COOP_CHECKS_PASS");
      } finally {UnityEngine.Object.DestroyImmediate(root);}
    }
  }
}
