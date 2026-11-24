using UnityEngine;using UnityEngine.UI;using UnityEngine.Video;
namespace GestureFistGame {
 [DefaultExecutionOrder(1300)]public sealed class SeaRiverPresentation:MonoBehaviour {
 FishGameUI ui;RawImage border,water;VideoPlayer video;RenderTexture texture;
 void Start(){ui=GetComponent<FishGameUI>();var river=transform.Find("SafeArea/CentralRiver");
 var go=new GameObject("VideoRiverWater",typeof(RectTransform),typeof(RawImage));go.transform.SetParent(transform,false);water=go.GetComponent<RawImage>();water.raycastTarget=false;Stretch(water.rectTransform);go.transform.SetAsFirstSibling();
 texture=new RenderTexture(512,1024,0);water.texture=texture;water.color=Color.white;water.uvRect=new Rect(.002f,.078f,.996f,.844f);
 video=go.AddComponent<VideoPlayer>();video.clip=Resources.Load<VideoClip>("SeaUI/RiverWater");video.renderMode=VideoRenderMode.RenderTexture;video.targetTexture=texture;video.aspectRatio=VideoAspectRatio.Stretch;video.isLooping=true;video.audioOutputMode=VideoAudioOutputMode.None;video.errorReceived+=(v,e)=>{water.enabled=false;Debug.LogWarning("River video: "+e);};video.prepareCompleted+=v=>v.Play();video.Prepare();
 var frame=new GameObject("SeaGameFrame",typeof(RectTransform),typeof(RawImage));frame.transform.SetParent(transform,false);border=frame.GetComponent<RawImage>();border.texture=Resources.Load<Texture2D>("SeaUI/GameFrame");border.raycastTarget=false;Stretch(border.rectTransform);
 }
 void LateUpdate(){if(ui==null)return;bool active=ui.game.IsRunning||ui.game.IsFinished;border.gameObject.SetActive(active);water.gameObject.SetActive(active);if(header!=null)header.gameObject.SetActive(active);
 if(active){
 var river=transform.Find("SafeArea/CentralRiver") as RectTransform;var wr=water.rectTransform;wr.anchorMin=new Vector2(670f/1980f,0);wr.anchorMax=new Vector2(1275f/1980f,1);wr.offsetMin=wr.offsetMax=Vector2.zero;water.transform.SetSiblingIndex(1);river.SetSiblingIndex(1);
 foreach(var surface in FindObjectsOfType<InteractiveWaterSurface>())foreach(var renderer in surface.GetComponentsInChildren<Renderer>())renderer.enabled=false;
 LayoutHud();border.transform.SetAsLastSibling();var safe=transform.Find("SafeArea");safe.SetAsLastSibling();}if(video!=null&&video.isPrepared){if(ui.game.Phase==FishGameController.RoundPhase.Paused)video.Pause();else if(!video.isPlaying)video.Play();}}
 RectTransform header;Text timeValue,syncValue;
 RectTransform Item(string name,float x,float y,float w,float h){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(header,false);var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
 void Picture(string name,float x,float y,float w,float h,string asset){var r=Item(name,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.sprite=SeaUiArt.Load(asset);image.color=Color.white;image.raycastTarget=false;}
 Text Number(string name,float x,float y,float w,float h){var r=Item(name,x,y,w,h);var t=r.gameObject.AddComponent<Text>();t.font=ui.scoreText.font;t.fontSize=70;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;r.gameObject.AddComponent<SeaUiNumbers>();return t;}
 void LayoutHud(){
 if(header==null){var go=new GameObject("PSDHeader",typeof(RectTransform));go.transform.SetParent(transform.Find("SafeArea"),false);header=go.GetComponent<RectTransform>();header.anchorMin=header.anchorMax=new Vector2(.5f,1);header.pivot=new Vector2(.5f,1);header.sizeDelta=new Vector2(1980,190);header.anchoredPosition=Vector2.zero;
 const float cardTop = 9f;
 const float cardHeight = 89f;
 float[] cardLeft = {593f, 849f, 1098f};
 string[] fishArt = {"HeaderGoldfish", "HeaderPike", "HeaderRedeye"};
 for (int j = 0; j < fishArt.Length; j++)
 {
   float x = cardLeft[j];
   Picture("FishCard" + j, x, cardTop, 223f, cardHeight, "CounterCard");
   var sprite = SeaUiArt.Load(fishArt[j]);
   if (sprite != null)
   {
     float scale = Mathf.Min(115f / sprite.rect.width, 63f / sprite.rect.height);
     float width = sprite.rect.width * scale;
     float height = sprite.rect.height * scale;
     float top = cardTop + (cardHeight - height) * .5f;
     Picture("FishIcon" + j, x + 16f + (115f - width) * .5f, top, width, height, fishArt[j]);
   }
   Number("FishCount" + j, x + 136f, 25f, 74f, 57f);
 }
 Picture("TimeCaption",650,113,131,86,"TimeLabel");Picture("ScoreCard",1005,113,139,86,"PanelBlue");var scoreCaption=Item("ScoreCaption",1012,125,125,62).gameObject.AddComponent<Text>();scoreCaption.font=ui.scoreText.font;scoreCaption.fontSize=42;scoreCaption.alignment=TextAnchor.MiddleCenter;scoreCaption.color=new Color(.12f,.28f,.42f);scoreCaption.text="分数";scoreCaption.raycastTarget=false;timeValue=Number("TimeDigits",790,116,196,79);syncValue=Number("SyncDigits",1152,116,174,79);
 }
 var safe=(RectTransform)transform.Find("SafeArea");header.localScale=Vector3.one*(safe.rect.width/1980f);
 var old=transform.Find("SafeArea/SpeciesCounters");if(old!=null)old.gameObject.SetActive(false);
 foreach(string name in new[]{"TimeLeft","TeamSync","TeamScore","PerfectCombo","PlayerOneState","PlayerTwoState"}){var t=transform.Find("SafeArea/CompactCoopHUD/"+name);if(t!=null)t.gameObject.SetActive(false);}
 int[] indices={1,2,0};string[] species={"红眼鱼","金鱼","白斑狗鱼"};for(int j=0;j<3;j++)header.Find("FishCount"+j).GetComponent<Text>().text=ui.game.CapturedCount(species[indices[j]]).ToString();
 timeValue.text=ui.game.Stage == 1 ? "—" : Mathf.CeilToInt(ui.game.TimeRemaining).ToString("00");syncValue.text=ui.game.Score.ToString("00");header.gameObject.SetActive(ui.game.IsRunning||ui.game.IsFinished);
 }
 static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
 void OnDestroy(){if(texture!=null){texture.Release();Destroy(texture);}}
 }
}
