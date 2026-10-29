using UnityEngine;
using UnityEngine.UI;
namespace GestureFistGame
{
  /// <summary>Reserves separate safe-area regions for gameplay and camera preview.</summary>
  public sealed class FishCameraSplitLayout : MonoBehaviour
  {
    private FishGameUI ui;
    private Camera sceneCamera;
    private RectTransform previewFrame;
    private Text placeholder;
    private void Awake()
    {
      ui=GetComponent<FishGameUI>(); sceneCamera=Camera.main;
      var backdrop = new GameObject("SplitScreenBackground").AddComponent<Camera>();
      backdrop.depth=-100; backdrop.cullingMask=0; backdrop.clearFlags=CameraClearFlags.SolidColor;
      backdrop.backgroundColor=new Color(.85f,.92f,.70f); backdrop.useOcclusionCulling=false;
      var frame=new GameObject("CameraViewport",typeof(RectTransform),typeof(Image),typeof(Mask));
      previewFrame=frame.GetComponent<RectTransform>();previewFrame.SetParent(ui.cameraPanel.transform,false);
      FishUiTheme.Card(frame.GetComponent<Image>(),new Color(.87f,.93f,.76f));
      frame.GetComponent<Mask>().showMaskGraphic=true;
      ui.cameraPreview.transform.SetParent(previewFrame,false);
      ui.cameraPreview.raycastTarget=false;
      var label=new GameObject("CameraPlaceholder",typeof(RectTransform),typeof(Text));
      label.transform.SetParent(previewFrame,false);
      placeholder=label.GetComponent<Text>();placeholder.font=ui.scoreText.font;placeholder.fontSize=32;
      placeholder.color=new Color(.35f,.47f,.24f);placeholder.alignment=TextAnchor.MiddleCenter;
      placeholder.raycastTarget=false;
      Stretch(label.GetComponent<RectTransform>(),Vector2.zero,Vector2.one);
    }
    private static void Stretch(RectTransform rect,Vector2 min,Vector2 max)
    {
      rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;
      rect.pivot=Vector2.one*.5f;
    }
    private void LateUpdate()
    {
      var safe=Screen.safeArea;
      bool wide=Screen.width>=Screen.height;
      var welcome=GetComponent<FishStartScreen>(); bool menu=welcome!=null && welcome.IsMenuOpen;
      // Portrait: game above, camera below. Wide screens keep both views side by side.
      var play=wide?new Rect(0,.22f,.64f,.62f):new Rect(0,.40f,1,.43f);
      if(menu)play=new Rect(0,0,1,1);
      if(sceneCamera!=null)
      {
        sceneCamera.rect=new Rect((safe.x+safe.width*play.x)/Screen.width,(safe.y+safe.height*play.y)/Screen.height,
          safe.width*play.width/Screen.width,safe.height*play.height/Screen.height);
        float aspect=sceneCamera.pixelRect.width/Mathf.Max(1,sceneCamera.pixelRect.height);
        sceneCamera.orthographicSize=menu?8.05f:Mathf.Max(4.5f,5.6f/aspect);
      }
      var card=ui.cameraPanel.transform as RectTransform;
      Stretch(card,wide?new Vector2(.66f,.25f):new Vector2(.025f,.145f),wide?new Vector2(.98f,.82f):new Vector2(.975f,.39f));
      Stretch(previewFrame,new Vector2(.025f,.15f),new Vector2(.975f,.96f));
      var hint=ui.cameraDebugText.rectTransform;
      Stretch(hint,new Vector2(.03f,.01f),new Vector2(.97f,.14f));
      ui.cameraDebugText.fontSize=28;
      var tex=ui.cameraPreview.texture;
      bool hasImage=tex!=null;
      ui.cameraPreview.enabled=hasImage;
      placeholder.gameObject.SetActive(!hasImage);
      placeholder.text=ui.input.mouseMode?"摄像头预览":"等待摄像头";
      if(hasImage)
      {
        // Fit the rotated image without stretching or cropping the hands.
        var rect=ui.cameraPreview.rectTransform;
        rect.anchorMin=rect.anchorMax=Vector2.one*.5f;rect.anchoredPosition=Vector2.zero;
        bool rotated=Mathf.Abs(Mathf.Sin(rect.localEulerAngles.z*Mathf.Deg2Rad))>.5f;
        float w=tex.width,h=tex.height;
        float scale=Mathf.Min(previewFrame.rect.width/(rotated?h:w),previewFrame.rect.height/(rotated?w:h));
        rect.sizeDelta=new Vector2(w,h)*scale;
      }
      var status=transform.Find("SafeArea/StatusPanel") as RectTransform;
      Stretch(status,new Vector2(.025f,.01f),new Vector2(.975f,.135f));
      var text=ui.statusText.rectTransform;
      Stretch(text,new Vector2(.02f,.69f),new Vector2(.98f,.99f));
      ui.statusText.fontSize=28;
      Button[] buttons={ui.resetButton,ui.calibrateButton,ui.gestureButton,ui.mouseButton};
      for(int i=0;i<buttons.Length;i++)
      {
        int col=i%2;int row=i/2;
        Stretch(buttons[i].transform as RectTransform,new Vector2(.03f+col*.49f,.37f-row*.34f),new Vector2(.48f+col*.49f,.67f-row*.34f));
        buttons[i].GetComponentInChildren<Text>().fontSize=30;
      }
      if(ui.finishPanel!=null)
      {
        var rect=ui.finishPanel.transform as RectTransform;
        rect.anchorMin=rect.anchorMax=new Vector2(wide?.32f:.5f,.615f);
        rect.sizeDelta=new Vector2(wide?750:850,300);rect.anchoredPosition=Vector2.zero;
      }
    }
  }
}


