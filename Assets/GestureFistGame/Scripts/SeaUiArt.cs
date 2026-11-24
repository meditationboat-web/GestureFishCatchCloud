using UnityEngine;using UnityEngine.UI;using System.Collections.Generic;
namespace GestureFistGame {
 public static class SeaUiArt {
 static Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
 public static Sprite Load(string name){if(sprites.TryGetValue(name,out var s))return s;var t=Resources.Load<Texture2D>("SeaUI/"+name);if(t==null)return null;s=Sprite.Create(t,new Rect(0,0,t.width,t.height),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,new Vector4(t.width*.18f,t.height*.28f,t.width*.18f,t.height*.28f));sprites[name]=s;return s;}
 public static void Panel(Image image,string color){var s=Load("Panel"+color);if(s==null)return;image.sprite=s;image.type=Image.Type.Sliced;image.color=Color.white;}
 }
 [DefaultExecutionOrder(1100)] public class SeaUiNumbers:MonoBehaviour {
 Text text;RectTransform digits;List<Image> images=new List<Image>();string previous;Vector2 previousSize;
 void Awake(){text=GetComponent<Text>();var go=new GameObject("PictureDigits",typeof(RectTransform));digits=go.GetComponent<RectTransform>();digits.SetParent(transform,false);digits.anchorMin=Vector2.zero;digits.anchorMax=Vector2.one;digits.offsetMin=digits.offsetMax=Vector2.zero;}
 void LateUpdate()
 {
   if (text == null) return;
   string value = text.text;
   Vector2 size = digits.rect.size;
   if (size.x <= 0 || size.y <= 0 || (value == previous && size == previousSize)) return;
   previous = value;
   previousSize = size;
   foreach (var image in images) image.gameObject.SetActive(false);

   float height = Mathf.Min(size.y * .92f, text.fontSize);
   const float gap = .035f;
   float widthInUnits = Mathf.Max(0, value.Length - 1) * gap;
   var glyphs = new Sprite[value.Length];
   var widths = new float[value.Length];
   for (int i = 0; i < value.Length; i++)
   {
     char c = value[i];
     var sprite = c >= '0' && c <= '9' ? SeaUiArt.Load("Digit" + c) : null;
     glyphs[i] = sprite;
     widths[i] = sprite != null ? sprite.rect.width / sprite.rect.height : .60f;
     widthInUnits += widths[i];
   }
   if (widthInUnits > 0) height = Mathf.Min(height, (size.x - 2f) / widthInUnits);
   float x = -height * widthInUnits * .5f;
   for (int i = 0; i < value.Length; i++)
   {
     if (i == images.Count)
     {
       var go = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
       go.transform.SetParent(digits, false);
       images.Add(go.GetComponent<Image>());
     }
     var image = images[i];
     image.gameObject.SetActive(true);
     image.raycastTarget = false;
     image.sprite = glyphs[i];
     image.enabled = glyphs[i] != null;
     var rect = image.rectTransform;
     rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
     float width = height * widths[i];
     rect.sizeDelta = new Vector2(width, height);
     rect.anchoredPosition = new Vector2(x + width * .5f, 0);
     x += width + gap * height;

     var label = image.GetComponentInChildren<Text>(true);
     if (image.enabled)
     {
       if (label != null) label.gameObject.SetActive(false);
       continue;
     }
     if (label == null)
     {
       var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
       go.transform.SetParent(image.transform, false);
       label = go.GetComponent<Text>();
       label.rectTransform.anchorMin = Vector2.zero;
       label.rectTransform.anchorMax = Vector2.one;
       label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
       label.raycastTarget = false;
     }
     label.gameObject.SetActive(true);
     label.text = value[i].ToString();
     label.font = text.font;
     label.fontStyle = FontStyle.Bold;
     label.fontSize = Mathf.Max(1, Mathf.RoundToInt(height * .62f));
     label.alignment = TextAnchor.MiddleCenter;
     label.color = new Color(.12f, .28f, .42f);
   }
   text.canvasRenderer.SetAlpha(0);
 }
 }
 [DefaultExecutionOrder(1200)] public class SeaUiSkin:MonoBehaviour {
 FishGameUI ui;Image[] players=new Image[2];
 void Start(){ui=GetComponent<FishGameUI>();foreach(var image in GetComponentsInChildren<Image>(true)){if(image.name=="Settings"||image.name=="ActionTutorial"||image.name=="TeamResultCards")SeaUiArt.Panel(image,"White");else if(image.name.StartsWith("FishCounter"))SeaUiArt.Panel(image,"Yellow");else if(image.GetComponent<Button>()!=null && image.transform.parent.name!="OpeningArtwork")SeaUiArt.Panel(image,image.name.Contains("返回")||image.name=="ReturnHome"?"Pink":"Yellow");}
 foreach(var text in GetComponentsInChildren<Text>(true)){string parent=text.transform.parent.name;if(parent=="TimeLeft"||parent=="TeamScore"||parent=="PerfectCombo"||parent=="TeamSync"||parent.StartsWith("FishCounter")||text.transform.parent.parent?.name=="SpeciesCounters")text.gameObject.AddComponent<SeaUiNumbers>();}
 foreach(string name in new[]{"TimeLeft","TeamSync"}){var t=transform.Find("SafeArea/CompactCoopHUD/"+name);if(t!=null)SeaUiArt.Panel(t.GetComponent<Image>(),"Blue");}
 for(int i=0;i<2;i++){var go=new GameObject("PlayerAnimation"+i,typeof(RectTransform),typeof(Image));go.transform.SetParent(transform.Find("SafeArea"),false);players[i]=go.GetComponent<Image>();players[i].raycastTarget=false;players[i].preserveAspect=true;var r=players[i].rectTransform;r.anchorMin=r.anchorMax=new Vector2(i==0?0f:1f,0);r.pivot=new Vector2(i==0?0f:1f,0);r.anchoredPosition=Vector2.zero;r.sizeDelta=new Vector2(239,322);r.localScale=Vector3.one;}
 }
 void LateUpdate(){if(ui==null)return;for(int i=0;i<2;i++){players[i].gameObject.SetActive(ui.game.IsRunning);int frame=(int)(Time.unscaledTime*(ui.game.PlayerLifted(i)?5f:2f))%2;players[i].sprite=SeaUiArt.Load("Player"+(i+1)+"Frame"+frame);players[i].type=Image.Type.Simple;players[i].preserveAspect=true;var parent=(RectTransform)players[i].transform.parent;var sprite=players[i].sprite;if(sprite!=null){float height=322f*(parent.rect.width/1980f);players[i].rectTransform.sizeDelta=new Vector2(height*sprite.rect.width/sprite.rect.height,height);}players[i].rectTransform.localScale=Vector3.one;}}
 }
}
