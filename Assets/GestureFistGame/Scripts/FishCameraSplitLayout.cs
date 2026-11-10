using UnityEngine;
using UnityEngine.UI;
namespace GestureFistGame
{
  /// <summary>Full-screen live camera with a central vertical river and pose overlay.</summary>
  public sealed class FishCameraSplitLayout : MonoBehaviour
  {
    private FishGameUI ui;
    private Camera sceneCamera;
    private RectTransform previewFrame;
    private Text placeholder;
    private RenderTexture riverTexture;
    private RectTransform riverRect;
    private Camera displayCamera;
    private RawImage meadowBackground;
    private Camera meadowCamera;
    private RenderTexture meadowTexture;
    private const int RiverLayer = 29;
    private static void RiverOnly(Transform root) { if(root==null)return;foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=RiverLayer; }
    private void OnDestroy(){if(sceneCamera!=null)sceneCamera.targetTexture=null;if(riverTexture!=null){riverTexture.Release();Destroy(riverTexture);}if(meadowTexture!=null){meadowTexture.Release();Destroy(meadowTexture);}}
    private void Awake()
    {
      ui=GetComponent<FishGameUI>(); sceneCamera=Camera.main;
      var display=new GameObject("CameraBackgroundDisplay");display.transform.SetParent(transform,false);
      displayCamera=display.AddComponent<Camera>();displayCamera.cullingMask=0;displayCamera.depth=-100;
      displayCamera.clearFlags=CameraClearFlags.SolidColor;displayCamera.backgroundColor=new Color(.16f,.23f,.19f);
      meadowTexture=new RenderTexture(1600,900,24);
      var meadowView=new GameObject("HomepageMeadowCamera");meadowView.transform.SetParent(transform,false);
      meadowCamera=meadowView.AddComponent<Camera>();if(sceneCamera!=null)meadowCamera.CopyFrom(sceneCamera);
      meadowCamera.targetTexture=meadowTexture;meadowCamera.cullingMask=~(1<<RiverLayer);meadowCamera.orthographic=true;meadowCamera.orthographicSize=8.05f;
      meadowCamera.transform.position=new Vector3(9.4f,13.8f,-13.6f);meadowCamera.transform.LookAt(new Vector3(0,.45f,0));
      var meadow=new GameObject("HomepageMeadowBackground",typeof(RectTransform),typeof(RawImage));meadow.transform.SetParent(transform,false);meadow.transform.SetAsFirstSibling();
      meadowBackground=meadow.GetComponent<RawImage>();meadowBackground.texture=meadowTexture;meadowBackground.raycastTarget=false;Stretch(meadow.GetComponent<RectTransform>(),Vector2.zero,Vector2.one);
      var frame=new GameObject("CameraBackground",typeof(RectTransform),typeof(Image),typeof(RectMask2D));
      previewFrame=frame.GetComponent<RectTransform>();previewFrame.SetParent(transform,false);previewFrame.SetAsFirstSibling();
      frame.GetComponent<Image>().color=new Color(.16f,.23f,.19f);
      Stretch(previewFrame,Vector2.zero,Vector2.one);
      ui.cameraPreview.transform.SetParent(previewFrame,false);ui.cameraPreview.raycastTarget=false;
      var lines=new GameObject("LivePoseSkeleton",typeof(RectTransform),typeof(CanvasRenderer));lines.SetActive(false);
      lines.transform.SetParent(ui.cameraPreview.transform,false);Stretch(lines.GetComponent<RectTransform>(),Vector2.zero,Vector2.one);
      var skeleton=lines.AddComponent<FishPoseLines>();skeleton.input=ui.input;skeleton.preview=ui.cameraPreview;skeleton.raycastTarget=false;lines.SetActive(true);
      riverTexture=new RenderTexture(512,1536,24);riverTexture.name="VerticalRiverPreview";
      var river=new GameObject("CentralRiver",typeof(RectTransform),typeof(RawImage));
      river.transform.SetParent(transform.Find("SafeArea"),false);river.transform.SetAsFirstSibling();
      riverRect=river.GetComponent<RectTransform>();river.GetComponent<RawImage>().texture=riverTexture;river.GetComponent<RawImage>().raycastTarget=false;
      river.GetComponent<RawImage>().color=new Color(1,1,1,.88f);
      if(sceneCamera!=null){sceneCamera.targetTexture=riverTexture;sceneCamera.rect=new Rect(0,0,1,1);
        sceneCamera.transform.position=new Vector3(0,24,0);sceneCamera.transform.rotation=Quaternion.LookRotation(Vector3.down,Vector3.right);
        sceneCamera.orthographic=true;sceneCamera.orthographicSize=12;
      }
      foreach(var avatar in FindObjectsOfType<FishPlayerAvatar>())foreach(var renderer in avatar.GetComponentsInChildren<Renderer>())renderer.enabled=false;
      var net=FindObjectOfType<FishNetController>();if(net!=null)net.linkVisualToHands=false;
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
      bool wide=Screen.width>=Screen.height;
      // Legacy arena initialization runs after UI Awake and used to overwrite this view.
      if(sceneCamera!=null) {
        sceneCamera.transform.SetPositionAndRotation(new Vector3(0,24,0),Quaternion.LookRotation(Vector3.down,Vector3.right));
        sceneCamera.orthographic=true;sceneCamera.orthographicSize=12;sceneCamera.cullingMask=1<<RiverLayer;
        sceneCamera.clearFlags=CameraClearFlags.SolidColor;sceneCamera.backgroundColor=new Color(.08f,.4f,.48f,1);
      }
      foreach(var water in FindObjectsOfType<InteractiveWaterSurface>())RiverOnly(water.transform);
      foreach(var fish in FindObjectsOfType<FishTarget>())RiverOnly(fish.transform);
      var net=FindObjectOfType<FishNetController>();if(net!=null){RiverOnly(net.transform);net.linkVisualToHands=false;net.transform.localScale=new Vector3(1.4f,1.4f,2.15f);}
      foreach(var avatar in FindObjectsOfType<FishPlayerAvatar>())foreach(var renderer in avatar.GetComponentsInChildren<Renderer>())renderer.enabled=false;
      var welcome=GetComponent<FishStartScreen>(); bool menu=welcome!=null && welcome.IsMenuOpen;
      Stretch(previewFrame,Vector2.zero,Vector2.one);
      Stretch(riverRect,new Vector2(wide?.36f:.32f,0),new Vector2(wide?.64f:.68f,1));
      riverRect.gameObject.SetActive(!menu);
      meadowBackground.gameObject.SetActive(menu);meadowCamera.enabled=menu;
      if(menu)meadowBackground.transform.SetAsFirstSibling();
      previewFrame.gameObject.SetActive(!menu);
      var card=ui.cameraPanel.transform as RectTransform;
      Stretch(card,new Vector2(.02f,.14f),new Vector2(.98f,.20f));
      var panelImage=ui.cameraPanel.GetComponent<Image>();if(panelImage!=null)panelImage.enabled=false;
      Stretch(ui.cameraDebugText.rectTransform,Vector2.zero,Vector2.one);ui.cameraDebugText.fontSize=26;
      var tex=ui.cameraPreview.texture;
      bool hasImage=tex!=null;
      ui.cameraPreview.enabled=hasImage;
      placeholder.gameObject.SetActive(!hasImage);
      placeholder.text=ui.input.mouseMode?"摄像头预览":"等待摄像头";
      if(hasImage)
      {
        // Preserve the entire camera frame so portrait screens do not crop out either player.
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
        rect.sizeDelta=new Vector2(wide?750:850,460);rect.anchoredPosition=Vector2.zero;
        if(ui.finishText!=null) {
          Stretch(ui.finishText.rectTransform,new Vector2(.04f,.06f),new Vector2(.96f,.94f));
          ui.finishText.fontSize=wide?32:36;
        }
      }
    }
  }
}


