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
    private Text bestLabel,menuHint;
    private Button[] options=new Button[4];
    private int selected;
    private RectTransform art;
    private UnityEngine.Video.VideoPlayer openingVideo;
    private RenderTexture openingTexture;
    private RectTransform settings;
    private int settingSelected;
    private bool leftArmed,rightArmed;
    private float leftWave=-10,rightWave=-10,cooldown;
    private void ApplyOpeningArt(){
      var bg=Resources.Load<Texture2D>("Opening/Background");if(bg==null)return;
      var backdrop = Panel("OpeningBackdrop", menu, Vector2.zero, Vector2.zero, false);
      backdrop.anchorMin = Vector2.zero;
      backdrop.anchorMax = Vector2.one;
      backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
      var backdropImage = backdrop.gameObject.AddComponent<RawImage>();
      backdropImage.texture = Resources.Load<Texture2D>("Opening/EdgeBackdrop");
      backdropImage.raycastTarget = false;
      art=Panel("OpeningArtwork",menu,new Vector2(2048,1152),Vector2.zero,false);art.SetSiblingIndex(1);
      var image=art.gameObject.AddComponent<RawImage>();image.texture=bg;image.raycastTarget=false;
      foreach(Transform child in card)if(child.name=="Label")child.gameObject.SetActive(false);
      card.GetComponent<Image>().enabled=false;
      string[] assets={"Start","Tutorial","Settings","Exit"};
      float[] x={828,828,841,837},y={497,661,826,976},w={394,387,361,372},h={165,160,152,156};
      for(int i=0;i<4;i++){
        var button=options[i];button.transform.SetParent(art,false);var r=(RectTransform)button.transform;
        r.anchorMin=r.anchorMax=Vector2.one*.5f;r.sizeDelta=new Vector2(w[i],h[i]);r.anchoredPosition=new Vector2(x[i]+w[i]*.5f-1024,576-y[i]-h[i]*.5f);
        foreach(Transform child in r)child.gameObject.SetActive(false);
        var texture=Resources.Load<Texture2D>("Opening/"+assets[i]);var graphic=button.GetComponent<Image>();
        graphic.sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),Vector2.one*.5f);graphic.type=Image.Type.Simple;graphic.color=Color.white;
      }
    }
    private void Start(){
      var clip=Resources.Load<UnityEngine.Video.VideoClip>("Opening/Opening");
      if(clip!=null){
       var overlay=Panel("OpeningVideo",transform,new Vector2(2048,1152),Vector2.zero,false);overlay.anchorMin=Vector2.zero;overlay.anchorMax=Vector2.one;overlay.offsetMin=overlay.offsetMax=Vector2.zero;
       openingTexture=new RenderTexture(1280,720,0);overlay.gameObject.AddComponent<RawImage>().texture=openingTexture;
       openingVideo=overlay.gameObject.AddComponent<UnityEngine.Video.VideoPlayer>();openingVideo.clip=clip;openingVideo.renderMode=UnityEngine.Video.VideoRenderMode.RenderTexture;openingVideo.targetTexture=openingTexture;openingVideo.audioOutputMode=UnityEngine.Video.VideoAudioOutputMode.None;
       var skip=overlay.gameObject.AddComponent<Button>();skip.onClick.AddListener(()=>{openingVideo.Stop();overlay.gameObject.SetActive(false);});
       openingVideo.loopPointReached+=v=>overlay.gameObject.SetActive(false);openingVideo.errorReceived+=(v,e)=>overlay.gameObject.SetActive(false);openingVideo.Play();
      }
      ui.input.PoseUpdated+=MenuPose;if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-batchmode")<0)ui.UseGesture();}
    private void OnDestroy(){if(openingTexture!=null){openingTexture.Release();Destroy(openingTexture);}if(ui!=null&&ui.input!=null)ui.input.PoseUpdated-=MenuPose;}
    public void MenuAction(int direction){
      if(openingVideo!=null&&openingVideo.gameObject.activeSelf){if(direction==0){openingVideo.Stop();openingVideo.gameObject.SetActive(false);}return;}
      if(started)return;
      var pages=GetComponent<FishPageCards>();
      if(pages!=null&&pages.Teaching){if(direction==0)pages.ConfirmTutorial();return;}
      if(settings!=null&&settings.gameObject.activeSelf){
        if(direction!=0)settingSelected=(settingSelected+(direction>0?1:2))%3;
        else if(settingSelected==2)settings.gameObject.SetActive(false);
        else {string key=settingSelected==0?"FishMusic":"FishEffects";PlayerPrefs.SetInt(key,1-PlayerPrefs.GetInt(key,1));PlayerPrefs.Save();}
        RefreshSettings();return;
      }
      if(direction!=0)selected=(selected+(direction>0?1:3))%4;
      else options[selected].onClick.Invoke();
      for(int i=0;i<4;i++)options[i].GetComponent<Image>().color=art!=null?(i==selected?Color.white:new Color(.78f,.78f,.78f)):i==selected?new Color(.4f,.7f,.2f):new Color(.7f,.86f,.34f);
    }
    void MenuPose(Mediapipe.Tasks.Vision.PoseLandmarker.PoseLandmarkerResult result){
      if(started||result.poseLandmarks==null||result.poseLandmarks.Count!=1){leftArmed=rightArmed=false;leftWave=rightWave=-10;return;}
      var p=result.poseLandmarks[0].landmarks;if(p==null||p.Count<17)return;
      foreach(int i in new[]{11,12,15,16})if((p[i].visibility??1)<.5f||(p[i].presence??1)<.5f){leftArmed=rightArmed=false;leftWave=rightWave=-10;return;}
      float now=Time.unscaledTime;if(now<cooldown)return;
      float width=Mathf.Max(.05f,Mathf.Abs(p[11].x-p[12].x));
      if(p[15].y>p[11].y+width*.1f)leftArmed=true;
      if(p[16].y>p[12].y+width*.1f)rightArmed=true;
      if(leftArmed&&p[15].y<p[11].y-width*.3f){leftWave=now;leftArmed=false;}
      if(rightArmed&&p[16].y<p[12].y-width*.3f){rightWave=now;rightArmed=false;}
      if(leftWave>0&&rightWave>0&&Mathf.Abs(leftWave-rightWave)<=.4f){MenuAction(0);leftWave=rightWave=-10;cooldown=now+.8f;}
      else if(leftWave>0&&now-leftWave>.4f){MenuAction(-1);leftWave=-10;cooldown=now+.4f;}
      else if(rightWave>0&&now-rightWave>.4f){MenuAction(1);rightWave=-10;cooldown=now+.4f;}
    }
    void RefreshSettings(){
      var labels=settings.GetComponentsInChildren<Text>();
      labels[0].text="设置";
      for(int i=0;i<3;i++)labels[i+1].text=(i==settingSelected?"▶ ":"")+(i==0?"音乐："+(PlayerPrefs.GetInt("FishMusic",1)==1?"开":"关"):i==1?"音效："+(PlayerPrefs.GetInt("FishEffects",1)==1?"开":"关"):"返回");
    }
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
      var background = menu.gameObject.AddComponent<Image>(); background.color=new Color(.12f,.22f,.08f,.18f);
      card=Panel("WelcomeCard",menu,new Vector2(940,1000),Vector2.zero,true);
      Label(card,"体感抓鱼",new Vector2(0,320),new Vector2(850,110),70);
      string[] names={"开始游戏","教程","设置","退出游戏"};
      for(int i=0;i<4;i++){int index=i;MakeButton(card,names[i],new Vector2(0,170-i*115),()=>{
        selected=index;
        if(index==0)Begin(true);
        else if(index==1)GetComponent<FishPageCards>().ShowTutorial(true);
        else if(index==2){settings.gameObject.SetActive(true);settings.SetAsLastSibling();RefreshSettings();}
        else Application.Quit();
      });options[i]=card.Find(names[i]).GetComponent<Button>();}
      bestLabel=Label(card,"",new Vector2(0,-285),new Vector2(850,55),30);
      menuHint=Label(card,"右手：下一项    左手：上一项    双手：确认",new Vector2(0,-350),new Vector2(1200,55),28);
      settings=Panel("Settings",menu,new Vector2(1100,700),Vector2.zero,true);
      Label(settings,"设置",new Vector2(0,230),new Vector2(850,90),54);
      for(int i=0;i<3;i++){int index=i;MakeButton(settings,"Setting"+i,new Vector2(0,90-i*140),()=>{settingSelected=index;MenuAction(0);});}
      settings.GetComponent<Image>().color=new Color(.95f,.97f,.99f,1);
      foreach(var image in settings.GetComponentsInChildren<Image>(true))if(image.gameObject!=settings.gameObject)image.color=new Color(.16f,.55f,.61f,1);
      foreach(var text in settings.GetComponentsInChildren<Text>(true))text.color=text.transform.parent==settings?new Color(.14f,.22f,.28f):Color.white;
      settings.gameObject.SetActive(false);
      MenuAction(1);MenuAction(-1);
#if UNITY_EDITOR
      MakeButton(menu,"键盘测试 Q / P",new Vector2(700,-450),()=>Begin(false));
#endif
      ApplyOpeningArt();
      for(int i=0;i<4;i++)if(art!=null)options[i].GetComponent<Image>().color=i==selected?Color.white:new Color(.78f,.78f,.78f);
      SetHud(false);
      Layout();
    }
    public void ReturnHome() { started=false; game.ReturnToMenu(); SetHud(false); menu.gameObject.SetActive(true); menu.SetAsLastSibling(); }
    public void Begin(bool gesture)
    {
      started=true; menu.gameObject.SetActive(false); SetHud(true);
      if(gesture) ui.UseGesture(); else ui.UseMouse();
      game.StartCampaign(); EventSystem.current?.SetSelectedGameObject(null);
    }
    private void SetHud(bool visible)
    {
      foreach(Transform child in transform.Find("SafeArea"))
        if(child != menu) child.gameObject.SetActive(visible && (child.gameObject != ui.finishPanel) && (child.gameObject != ui.cameraPanel));
    }
    private void Update()
    {
#if UNITY_EDITOR
      if(!started){if(Input.GetKeyDown(KeyCode.DownArrow))MenuAction(1);if(Input.GetKeyDown(KeyCode.UpArrow))MenuAction(-1);if(Input.GetKeyDown(KeyCode.Return))MenuAction(0);}
#endif
      Layout();
      bestLabel.text="历史最佳 " + game.BestScore + " 分";
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
      counters.anchoredPosition=new Vector2(0,wide?-155:-190);
      counters.localScale=Vector3.one*(wide?.72f:.86f);
      card.sizeDelta=wide?new Vector2(1300,860):new Vector2(1000,960);
      card.localScale=Vector3.one;
      if(art!=null){var r=(RectTransform)menu;art.localScale=Vector3.one*Mathf.Min(r.rect.width/2048f,r.rect.height/1152f);settings.SetAsLastSibling();}
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

