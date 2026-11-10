using UnityEngine;
using UnityEngine.UI;
namespace GestureFistGame {
 [DefaultExecutionOrder(300)]
 public sealed class FishPageCards:MonoBehaviour {
  FishGameUI ui;RectTransform tutorial,results;Text[] values=new Text[6];Text[] responses=new Text[2],perfects=new Text[2];bool gesture,teaching;
  RectTransform Card(string name,Transform parent,Vector2 min,Vector2 max,Color color){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;FishUiTheme.Card(go.GetComponent<Image>(),color);return r;}
  Text Label(Transform parent,string text,Vector2 min,Vector2 max,int size){var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;var t=go.GetComponent<Text>();t.font=ui.scoreText.font;t.text=text;t.fontSize=size;t.alignment=TextAnchor.MiddleCenter;t.color=new Color(.14f,.22f,.28f);t.raycastTarget=false;return t;}
  void Button(Transform parent,string text,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action){var r=Card(text,parent,min,max,new Color(.16f,.55f,.61f));var b=r.gameObject.AddComponent<Button>();b.onClick.AddListener(action);Label(r,text,Vector2.zero,Vector2.one,34).color=Color.white;}
  void Awake(){ui=GetComponent<FishGameUI>();var safe=transform.Find("SafeArea");
   tutorial=Card("ActionTutorial",safe,new Vector2(.08f,.16f),new Vector2(.92f,.86f),new Color(.95f,.97f,.99f));
   Label(tutorial,"动作教学 · 三步",new Vector2(.05f,.85f),new Vector2(.95f,.98f),44);
   string[] steps={"第 1 步  观察鱼群\n等鱼游进渔网范围","第 2 步  双人举网\n每人双手腕过头顶，单手无效\n两人完成动作的时间差决定结果","第 3 步  合作判定\n完美：时间差 ≤0.5 秒，网内全收\n普通：时间差 >0.5 且 ≤1.5 秒\n保留靠近网中心的一半，连击归零\n失败：超过1.5秒或只有一人举网\n鱼全部逃走，连击归零"};
   float[] bottoms={.69f,.49f,.14f},tops={.83f,.67f,.47f};
   for(int i=0;i<3;i++){
    var r=Card("Step"+(i+1),tutorial,new Vector2(.04f,bottoms[i]),new Vector2(.96f,tops[i]),Color.white);
    var text=Label(r,steps[i],new Vector2(.02f,.03f),new Vector2(.98f,.97f),i==2?28:30);
    text.resizeTextForBestFit=true;text.resizeTextMinSize=22;text.resizeTextMaxSize=i==2?28:30;
   }
   Button(tutorial,"知道了，开始",new Vector2(.15f,.025f),new Vector2(.85f,.115f),()=>{teaching=false;tutorial.gameObject.SetActive(false);GetComponent<FishStartScreen>().Begin(gesture);tutorial.gameObject.SetActive(false);});
   results=Card("TeamResultCards",safe,new Vector2(.06f,.08f),new Vector2(.94f,.92f),new Color(.95f,.97f,.99f));
   Label(results,"团队成绩",new Vector2(.05f,.85f),new Vector2(.95f,.98f),44);
   string[] names={"捕获鱼数","总分","同步率","最长连击","星级","历史最佳"};
   for(int i=0;i<6;i++){int col=i%3,row=i/3;float x=.04f+col*.31f,y=.61f-row*.23f;var r=Card(names[i],results,new Vector2(x,y),new Vector2(x+.29f,y+.21f),Color.white);Label(r,names[i],new Vector2(.02f,.6f),new Vector2(.98f,.94f),34);values[i]=Label(r,"—",new Vector2(.02f,.05f),new Vector2(.98f,.6f),48);}
   for(int i=0;i<2;i++) {
    float x=i==0?.04f:.515f;
    var player=Card("PlayerReport"+(i+1),results,new Vector2(x,.18f),new Vector2(x+.445f,.36f),Color.white);
    Label(player,"P"+(i+1),new Vector2(.02f,.7f),new Vector2(.98f,.98f),34);
    Label(player,"平均响应",new Vector2(.02f,.4f),new Vector2(.49f,.7f),30);
    Label(player,"完美次数",new Vector2(.51f,.4f),new Vector2(.98f,.7f),30);
    responses[i]=Label(player,"—",new Vector2(.02f,.03f),new Vector2(.49f,.4f),38);
    perfects[i]=Label(player,"0",new Vector2(.51f,.03f),new Vector2(.98f,.4f),38);
   }
   Button(results,"再来一局",new Vector2(.08f,.04f),new Vector2(.48f,.135f),()=>ui.game.ResetRound());
   Button(results,"返回首页",new Vector2(.52f,.04f),new Vector2(.92f,.135f),()=>GetComponent<FishStartScreen>().ReturnHome());
   tutorial.gameObject.SetActive(false);results.gameObject.SetActive(false);
  }
  public void ShowTutorial(bool camera){gesture=camera;teaching=true;tutorial.gameObject.SetActive(true);tutorial.SetAsLastSibling();}
  void LateUpdate(){tutorial.gameObject.SetActive(teaching);if(teaching)tutorial.SetAsLastSibling();var g=ui.game;results.gameObject.SetActive(g.IsFinished);if(!g.IsFinished)return;if(ui.finishPanel!=null)ui.finishPanel.SetActive(false);results.SetAsLastSibling();values[0].text=g.Catches+" 条";values[1].text=g.Score.ToString();values[2].text=g.SyncRate.ToString("P0");values[3].text=g.LongestCombo.ToString();values[4].text=new string('★',g.Stars)+new string('☆',3-g.Stars);values[5].text=g.BestScore+" 分";for(int i=0;i<2;i++){responses[i].text=g.PlayerResponse(i);perfects[i].text=g.PerfectWaves.ToString();}}
 }
}
