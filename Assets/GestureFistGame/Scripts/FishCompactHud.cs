using UnityEngine;
using UnityEngine.UI;
namespace GestureFistGame {
  // Minimal camera-first HUD; each value reads the live round state.
  [DefaultExecutionOrder(200)]
  public sealed class FishCompactHud:MonoBehaviour {
    FishGameUI ui;RectTransform root;Text score,time,combo,sync,p1,p2,result;float resultUntil;string last;Button replay,home,reconnect;
    Text Pill(string name,Vector2 min,Vector2 max,Color bg,Color fg,int size){
      var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(root,false);
      var r=go.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;
      FishUiTheme.Card(go.GetComponent<Image>(),bg);go.GetComponent<Image>().raycastTarget=false;
      var label=new GameObject("Label",typeof(RectTransform),typeof(Text));label.transform.SetParent(go.transform,false);
      var lr=label.GetComponent<RectTransform>();lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.offsetMin=new Vector2(8,2);lr.offsetMax=new Vector2(-8,-2);
      var t=label.GetComponent<Text>();t.font=ui.scoreText.font;t.fontSize=size;t.fontStyle=FontStyle.Bold;t.alignment=TextAnchor.MiddleCenter;t.color=fg;t.raycastTarget=false;return t;
    }
    void Awake(){
      ui=GetComponent<FishGameUI>();root=new GameObject("CompactCoopHUD",typeof(RectTransform)).GetComponent<RectTransform>();root.SetParent(transform.Find("SafeArea"),false);root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
      Color ink=new Color(.13f,.2f,.25f),white=new Color(1,1,1,.95f);
      score=Pill("TeamScore",new Vector2(.02f,.92f),new Vector2(.22f,.98f),white,ink,38);
      time=Pill("TimeLeft",new Vector2(.76f,.92f),new Vector2(.98f,.98f),white,ink,36);
      combo=Pill("PerfectCombo",new Vector2(.02f,.865f),new Vector2(.23f,.91f),new Color(1,.96f,.75f,.95f),new Color(.53f,.38f,.07f),30);
      p1=Pill("PlayerOneState",new Vector2(.02f,.025f),new Vector2(.28f,.08f),new Color(.91f,.28f,.29f,.95f),Color.white,30);
      p2=Pill("PlayerTwoState",new Vector2(.72f,.025f),new Vector2(.98f,.08f),new Color(.23f,.5f,.8f,.95f),Color.white,30);
      sync=Pill("TeamSync",new Vector2(.34f,.025f),new Vector2(.66f,.08f),white,ink,30);
      var restart=Pill("ReplayRound",new Vector2(.19f,.12f),new Vector2(.49f,.18f),new Color(.16f,.55f,.61f),Color.white,32);
      restart.text="再来一局";var image=restart.transform.parent.GetComponent<Image>();image.raycastTarget=true;
      replay=image.gameObject.AddComponent<Button>();replay.targetGraphic=image;replay.onClick.AddListener(()=>ui.game.ResetRound());
      var back=Pill("ReturnHome",new Vector2(.51f,.12f),new Vector2(.81f,.18f),new Color(.16f,.55f,.61f),Color.white,32);
      back.text="返回首页";var backImage=back.transform.parent.GetComponent<Image>();backImage.raycastTarget=true;
      home=backImage.gameObject.AddComponent<Button>();home.targetGraphic=backImage;home.onClick.AddListener(()=>GetComponent<FishStartScreen>().ReturnHome());
      var camera=Pill("ReconnectCamera",new Vector2(.34f,.32f),new Vector2(.66f,.38f),new Color(.16f,.55f,.61f),Color.white,30);
      camera.text="重连摄像头";var cameraImage=camera.transform.parent.GetComponent<Image>();cameraImage.raycastTarget=true;
      reconnect=cameraImage.gameObject.AddComponent<Button>();reconnect.targetGraphic=cameraImage;reconnect.onClick.AddListener(()=>ui.Calibrate());
      result=Pill("CooperationResult",new Vector2(.2f,.22f),new Vector2(.8f,.3f),new Color(.1f,.2f,.15f,.65f),Color.white,38);
    }
    void LateUpdate(){
      var g=ui.game;bool active=g.IsRunning||g.IsFinished;root.gameObject.SetActive(active);
      if(!active)return;
      foreach(string name in new[]{"TitleCard","ScorePanel","StatusPanel"}){var old=transform.Find("SafeArea/"+name);if(old!=null)old.gameObject.SetActive(false);}
      reconnect.gameObject.SetActive(g.Phase==FishGameController.RoundPhase.Paused || g.Phase==FishGameController.RoundPhase.Confirming);
      replay.gameObject.SetActive(false);home.gameObject.SetActive(false);
      score.text=g.Score+" 分";time.text="剩 "+Mathf.CeilToInt(g.TimeRemaining)+" 秒";combo.text="连击 × "+g.Combo;sync.text="同步率 "+g.SyncRate.ToString("P0");
      if(ui.input.mouseMode && g.IsRunning)sync.text="Q：P1  P：P2";
      p1.text="P1 "+(g.PlayerLifted(0)?"已抬网":ui.input.TwoPlayersReady?"待抬网":"待入镜");
      p2.text="P2 "+(g.PlayerLifted(1)?"已抬网":ui.input.TwoPlayersReady?"待抬网":"待入镜");
      if(!g.SimulationActive) { resultUntil=0; last=null; }
      if(last!=g.LastEvent){last=g.LastEvent;if(last.StartsWith("完美")||last.StartsWith("差一点")||last.StartsWith("鱼跑")){result.text=last;resultUntil=Time.unscaledTime+2;}}
      result.transform.parent.gameObject.SetActive(g.SimulationActive&&Time.unscaledTime<resultUntil);
      if(ui.finishPanel!=null&&g.IsFinished)ui.finishPanel.transform.SetAsLastSibling();
    }
  }
}
