using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace GestureFistGame
{
  public sealed class FishStartScreen : MonoBehaviour
  {
    private FishGameUI ui;
    private FishGameController game;
    private RectTransform menu, card, counters;
    private Text[] counts = new Text[3];
    private string[] species = { "红眼鱼", "金鱼", "白斑狗鱼" };
    private bool started;
    private Font font;
    public bool IsMenuOpen => !started;
    private void Awake()
    {
      ui = GetComponent<FishGameUI>(); game = ui.game;
      game.autoStart = false;
      font = ui.scoreText.font;
      var safe = transform.Find("SafeArea");
      counters = Panel("SpeciesCounters", safe, new Vector2(1000,150), Vector2.zero, false);
      for (int i=0;i<3;i++)
      {
        var tile = Panel("FishCounter"+i, counters, new Vector2(316,150), new Vector2((i-1)*338,0), true);
        var iconRect = Panel("FishIcon",tile,new Vector2(190,112),new Vector2(-47,0),false);
        var icon = iconRect.gameObject.AddComponent<Image>();
        icon.sprite = Resources.Load<Sprite>("FishCounterIcons/Fish"+i);
        icon.preserveAspect = true; icon.raycastTarget=false;
        counts[i] = Label(tile,"×0",new Vector2(100,0),new Vector2(105,90),48);
      }
      menu = Panel("StartScreen", safe, Vector2.zero, Vector2.zero, false);
      menu.anchorMin=Vector2.zero;menu.anchorMax=Vector2.one;menu.offsetMin=menu.offsetMax=Vector2.zero;
      var background = menu.gameObject.AddComponent<Image>(); background.color=new Color(.24f,.36f,.12f,.58f);
      card=Panel("WelcomeCard",menu,new Vector2(940,1000),Vector2.zero,true);
      Label(card,"绿叶小渔场",new Vector2(0,215),new Vector2(850,140),64);
      Label(card,"举手过头顶，一起抛网",new Vector2(0,90),new Vector2(850,70),36);
      MakeButton(card,"手势开始",new Vector2(0,-60),()=>Begin(true));
      MakeButton(card,"鼠标开始",new Vector2(0,-180),()=>Begin(false));
      SetHud(false);
      Layout();
    }
    public void Begin(bool gesture)
    {
      started=true; menu.gameObject.SetActive(false); SetHud(true);
      if(gesture) ui.UseGesture(); else ui.UseMouse();
      game.ResetRound(); EventSystem.current?.SetSelectedGameObject(null);
    }
    private void SetHud(bool visible)
    {
      foreach(Transform child in transform.Find("SafeArea"))
        if(child != menu) child.gameObject.SetActive(visible && (child.gameObject != ui.finishPanel) && (child.gameObject != ui.cameraPanel));
    }
    private void Update()
    {
      Layout();
      for(int i=0;i<3;i++) counts[i].text="×" + game.CapturedCount(species[i]);
      if(!started)
      {
        // Existing UI refreshes optional panels; keep the welcome screen exclusive.
        if(ui.cameraPanel!=null)ui.cameraPanel.SetActive(false);
        if(ui.finishPanel!=null)ui.finishPanel.SetActive(false);
      }
    }
    private void Layout()
    {
      bool wide=Screen.width>=Screen.height;
      counters.anchorMin=counters.anchorMax=new Vector2(.5f,1);
      counters.anchoredPosition=new Vector2(0,wide?-220:-285);
      card.sizeDelta=wide?new Vector2(1100,660):new Vector2(940,660);
      card.localScale=Vector3.one*(wide?.85f:1f);
    }
    private RectTransform Panel(string name,Transform parent,Vector2 size,Vector2 position,bool rounded)
    {
      var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
      rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;rect.sizeDelta=size;rect.anchoredPosition=position;
      if(rounded)FishUiTheme.Card(rect.gameObject.AddComponent<Image>(),new Color(.97f,1f,.88f,.98f));
      return rect;
    }
    private Text Label(Transform parent,string value,Vector2 position,Vector2 size,int fontSize)
    {
      var rect=Panel("Label",parent,size,position,false);var text=rect.gameObject.AddComponent<Text>();
      text.font=font;text.fontSize=fontSize;text.text=value;text.color=new Color(.23f,.36f,.14f);text.alignment=TextAnchor.MiddleCenter;
      text.raycastTarget=false;return text;
    }
    private void MakeButton(Transform parent,string value,Vector2 position,UnityEngine.Events.UnityAction action)
    {
      var rect=Panel(value,parent,new Vector2(720,96),position,false);
      FishUiTheme.Card(rect.gameObject.AddComponent<Image>(),new Color(.70f,.86f,.34f));
      var button=rect.gameObject.AddComponent<Button>();button.onClick.AddListener(action);
      Label(rect,value,Vector2.zero,new Vector2(680,90),42);
    }
  }
}

