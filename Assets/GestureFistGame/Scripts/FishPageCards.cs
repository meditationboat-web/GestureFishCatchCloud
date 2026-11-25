using UnityEngine;
using UnityEngine.UI;
namespace GestureFistGame {
 [DefaultExecutionOrder(300)]
 public sealed class FishPageCards:MonoBehaviour {
  Text resultTitle, advanceText;
  FishGameUI ui;RectTransform tutorial,results;Text[] values=new Text[6];Text[] responses=new Text[2],perfects=new Text[2];bool gesture,teaching;
  RectTransform Card(string name,Transform parent,Vector2 min,Vector2 max,Color color){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;FishUiTheme.Card(go.GetComponent<Image>(),color);return r;}
  Text Label(Transform parent,string text,Vector2 min,Vector2 max,int size){var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;var t=go.GetComponent<Text>();t.font=ui.scoreText.font;t.text=text;t.fontSize=size;t.alignment=TextAnchor.MiddleCenter;t.color=new Color(.14f,.22f,.28f);t.raycastTarget=false;return t;}
  void Button(Transform parent, string text, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
  {
   var rect = Card(text, parent, min, max, new Color(.16f,.55f,.61f));
   var button = rect.gameObject.AddComponent<Button>();
   button.targetGraphic = rect.GetComponent<Image>();
   button.onClick.AddListener(action);
   var label = Label(rect, text, Vector2.zero, Vector2.one, 34);
   label.color = Color.white;
   label.resizeTextForBestFit = true;
   label.resizeTextMinSize = 18;
   label.resizeTextMaxSize = 34;
  }
  void Awake(){ui=GetComponent<FishGameUI>();var safe=transform.Find("SafeArea");
   tutorial=Card("ActionTutorial",safe,new Vector2(.08f,.16f),new Vector2(.92f,.86f),new Color(.95f,.97f,.99f));
   Label(tutorial,"动作教学 · 三步",new Vector2(.05f,.85f),new Vector2(.95f,.98f),44);
   string[] steps={"第 1 步  观察鱼群\n等鱼游进渔网范围","第 2 步  举起渔网\n先放下双手，再将两只手腕举过肩膀","第 3 步  捕获鱼群\n举网成功时，收下渔网范围内的鱼\n抓到一条鱼即可完成教程"};
   var recognition=new GameObject("RecognitionCharacter",typeof(RectTransform),typeof(RawImage));recognition.transform.SetParent(tutorial,false);
   var rr=recognition.GetComponent<RectTransform>();rr.anchorMin=new Vector2(.015f,.16f);rr.anchorMax=new Vector2(.2f,.82f);rr.offsetMin=rr.offsetMax=Vector2.zero;
   recognition.GetComponent<RawImage>().texture=Resources.Load<Texture2D>("Opening/Recognition");recognition.GetComponent<RawImage>().raycastTarget=false;
   recognition.GetComponent<RawImage>().uvRect=new Rect(0,0,1,1);
   var raised=new GameObject("RaiseHandsCharacter",typeof(RectTransform),typeof(RawImage));raised.transform.SetParent(tutorial,false);
   var ar=raised.GetComponent<RectTransform>();ar.anchorMin=new Vector2(.8f,.16f);ar.anchorMax=new Vector2(.985f,.82f);ar.offsetMin=ar.offsetMax=Vector2.zero;
   raised.GetComponent<RawImage>().texture=Resources.Load<Texture2D>("Opening/RaiseHands");raised.GetComponent<RawImage>().raycastTarget=false;
   raised.GetComponent<RawImage>().uvRect=new Rect(0,0,1,1);
   float[] bottoms={.69f,.49f,.14f},tops={.83f,.67f,.47f};
   for(int i=0;i<3;i++){
    var r=Card("Step"+(i+1),tutorial,new Vector2(.21f,bottoms[i]),new Vector2(.79f,tops[i]),Color.white);
    var text=Label(r,steps[i],new Vector2(.02f,.03f),new Vector2(.98f,.97f),i==2?28:30);
    text.resizeTextForBestFit=true;text.resizeTextMinSize=22;text.resizeTextMaxSize=i==2?28:30;
   }
   Button(tutorial,"返回首页",new Vector2(.38f,.025f),new Vector2(.62f,.135f),()=>{ConfirmTutorial();});
   results=Card("TeamResultCards",safe,new Vector2(.06f,.08f),new Vector2(.94f,.92f),new Color(.95f,.97f,.99f));
   resultTitle=Label(results,"单人挑战成绩",new Vector2(.05f,.85f),new Vector2(.95f,.98f),44);
   string[] names={"捕获鱼数","总分","捕获率","最长连击","星级","历史最佳"};
   for(int i=0;i<6;i++){int col=i%3,row=i/3;float x=.04f+col*.31f,y=.61f-row*.23f;var r=Card(names[i],results,new Vector2(x,y),new Vector2(x+.29f,y+.21f),Color.white);Label(r,names[i],new Vector2(.02f,.6f),new Vector2(.98f,.94f),34);values[i]=Label(r,"—",new Vector2(.02f,.05f),new Vector2(.98f,.6f),48);}
   for(int i=0;i<1;i++) {
    float x=.16f;
    var player=Card("PlayerReport"+(i+1),results,new Vector2(x,.18f),new Vector2(x+.68f,.36f),Color.white);
    Label(player,"玩家表现",new Vector2(.02f,.7f),new Vector2(.98f,.98f),34);
    Label(player,"平均响应",new Vector2(.02f,.4f),new Vector2(.49f,.7f),30);
    Label(player,"成功捕获波数",new Vector2(.51f,.4f),new Vector2(.98f,.7f),30);
    responses[i]=Label(player,"—",new Vector2(.02f,.03f),new Vector2(.49f,.4f),38);
    perfects[i]=Label(player,"0",new Vector2(.51f,.03f),new Vector2(.98f,.4f),38);
   }
   Button(results,"再来一局",new Vector2(.21f,.025f),new Vector2(.47f,.15f),()=>{if(ui.game.CanAdvance)ui.game.AdvanceStage();else if(ui.game.Stage==3 && ui.game.IsFinished)ui.game.ReplayCampaign();else ui.game.ResetRound();});
   advanceText=results.Find("再来一局/Label").GetComponent<Text>();
   Button(results,"返回首页",new Vector2(.53f,.025f),new Vector2(.79f,.15f),()=>GetComponent<FishStartScreen>().ReturnHome());
   tutorial.gameObject.SetActive(false);results.gameObject.SetActive(false);
  }
  void FitCharacter(string name,float left,float right){
   var rect=tutorial.Find(name) as RectTransform;if(rect==null)return;
   var image=rect.GetComponent<RawImage>();if(image.texture==null)return;
   rect.anchorMin=rect.anchorMax=new Vector2((left+right)*.5f,.48f);rect.pivot=Vector2.one*.5f;rect.anchoredPosition=Vector2.zero;
   float width=tutorial.rect.width*(right-left),height=tutorial.rect.height*.62f;
   float ratio=(float)image.texture.width/image.texture.height;
   rect.sizeDelta=width/height>ratio?new Vector2(height*ratio,height):new Vector2(width,width/ratio);
  }
  public bool Teaching => teaching;
  public void ConfirmTutorial(){teaching=false;tutorial.gameObject.SetActive(false);}
  public void ShowTutorial(bool camera){gesture=camera;teaching=true;tutorial.gameObject.SetActive(true);tutorial.SetAsLastSibling();}
  void LateUpdate(){FitCharacter("RecognitionCharacter",.02f,.2f);FitCharacter("RaiseHandsCharacter",.8f,.98f);tutorial.gameObject.SetActive(teaching);if(teaching)tutorial.SetAsLastSibling();var g=ui.game;results.gameObject.SetActive(g.IsFinished);if(!g.IsFinished)return;if(ui.finishPanel!=null)ui.finishPanel.SetActive(false);results.SetAsLastSibling();resultTitle.text=g.StageName+" · "+(g.CanAdvance?"完成":"成绩");advanceText.text=g.CanAdvance?"下一关":g.Stage==3?"重新挑战":"重试本关";values[0].text=g.Catches+" 条";values[1].text=g.Score.ToString();values[2].text=(g.PassedFish == 0 ? 0f : (float)g.Catches / g.PassedFish).ToString("P0");values[3].text=g.LongestCombo.ToString();values[4].text=new string('★',g.Stars)+new string('☆',3-g.Stars);values[5].text=g.BestScore+" 分";for(int i=0;i<1;i++){responses[i].text=g.PlayerResponse(i);perfects[i].text=g.PerfectWaves.ToString();}}
 }
}
