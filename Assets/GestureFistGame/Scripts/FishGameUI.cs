using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GestureFistGame
{
  public sealed class FishGameUI : MonoBehaviour
  {
    public FishGameController game;
    public FishGestureInput input;
    public MediaPipeTracker tracker;
    public Text scoreText;
    public Text timeText;
    public Text rankText;
    public Text statusText;
    public Text netText;
    public Text finishText;
    public GameObject finishPanel;
    public GameObject cameraPanel;
    public RawImage cameraPreview;
    [Tooltip("手机端相机诊断文字；为空时自动使用 CameraHint 文本")]
    public Text cameraDebugText;
    public Button gestureButton;
    public Button mouseButton;
    public Button resetButton;
    public Button calibrateButton;
    private Transform _layoutRoot;
    private Rect _lastSafeArea;
    private Vector2Int _lastScreen;
    private float _nextRefresh;
    private bool _lastLandscape;
    private bool _layoutInitialized;

    private void Awake()
    {
      // Older serialized scenes may not have kept every script reference.
      // Resolve the core controllers locally so a missing optional UI binding
      // cannot prevent the fishing loop from starting.
      if (game == null) game = FindObjectOfType<FishGameController>();
      if (input == null) input = FindObjectOfType<FishGestureInput>();
      if (tracker == null) tracker = FindObjectOfType<MediaPipeTracker>();
      // 兼容已经保存的旧场景：无需重新手动拖引用，就能把诊断写入相机卡片。
      if (cameraDebugText == null && cameraPanel != null)
      {
        var hint = cameraPanel.transform.Find("CameraHint");
        if (hint != null) cameraDebugText = hint.GetComponent<Text>();
        if (cameraDebugText == null) cameraDebugText = cameraPanel.GetComponentInChildren<Text>(true);
      }
      if (cameraDebugText != null)
      {
        cameraDebugText.fontSize = 11;
        cameraDebugText.horizontalOverflow = HorizontalWrapMode.Wrap;
        cameraDebugText.verticalOverflow = VerticalWrapMode.Overflow;
      }
      var safe = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
      safe.SetParent(transform, false);
      foreach (var child in new System.Collections.Generic.List<Transform>(GetTopLevelChildren())) child.SetParent(safe, false);
      _layoutRoot = safe;
      MakeUiReadable();
      FishUiTheme.Apply(transform);
      SetTextByName("Title", "绿叶小渔场");
      SetTextByName("Subtitle", "");
      DisableKeyboardSubmit(resetButton);
      DisableKeyboardSubmit(gestureButton);
      DisableKeyboardSubmit(mouseButton);
      DisableKeyboardSubmit(calibrateButton);
      SetButtonLabel(resetButton, "再玩一回");
      SetButtonLabel(calibrateButton, "重连相机");
      SetButtonLabel(gestureButton, "手势游玩");
      SetButtonLabel(mouseButton, "鼠标游玩");
      ApplyResponsiveLayout(true);
      gameObject.AddComponent<FishStartScreen>();
      gameObject.AddComponent<FishCameraSplitLayout>();
    }

    private void Update()
    {
      ApplyResponsiveLayout(false);
      if (game == null || input == null || Time.unscaledTime < _nextRefresh) return;
      _nextRefresh = Time.unscaledTime + .08f;
      if (scoreText != null) scoreText.text = "收获  " + game.Score + " 分";
      if (timeText != null) timeText.text = "时间  " + Mathf.CeilToInt(game.TimeRemaining).ToString("00") + " 秒";
      if (rankText != null) rankText.text = "连击 ×" + game.Combo;
      if (statusText != null)
      {
        statusText.text = game.IsFinished ? "回合结束" : input.mouseMode ? "左右上拖抛网" :
          !input.CameraReady ? "连接摄像头…" : input.DetectedHands < 2 ? "请露出头部和手腕" : "举手过头顶抛网";
      }
      if (netText != null) netText.gameObject.SetActive(false);
      if (tracker != null && !input.mouseMode)
      {

        if (cameraDebugText != null)
          cameraDebugText.text = input.CameraReady ? "已识别 " + input.DetectedHands + "/2 只手" : "相机连接中，可点击重连";
      }
      else if (cameraDebugText != null)
      {
        cameraDebugText.text = "摄像头 · 手势模式可开启";
      }
      if (finishPanel != null)
      {
        finishPanel.SetActive(game.IsFinished);
        if (game.IsFinished && finishText != null)
          finishText.text = "回合结束\n\n" + game.Score + " 分 · " + game.Catches + " 条";
      }
      if (cameraPanel != null) cameraPanel.SetActive(game.IsRunning || game.IsFinished);
      if (gestureButton != null) gestureButton.interactable = input.mouseMode;
      if (mouseButton != null) mouseButton.interactable = !input.mouseMode;
      if (calibrateButton != null) calibrateButton.interactable = !input.mouseMode;
    }

    public void UseGesture() { input?.UseCamera(); ClearUiSelection(); }
    public void UseMouse() { input?.UseMouse(); ClearUiSelection(); }
    public void ResetRound() { game?.ResetRound(); ClearUiSelection(); }
    public void Calibrate() { input?.UseCamera(); ClearUiSelection(); }

    private static void DisableKeyboardSubmit(Button button)
    {
      if (button == null) return;
      var navigation = button.navigation;
      navigation.mode = Navigation.Mode.None;
      button.navigation = navigation;
    }

    private static void ClearUiSelection()
    {
      if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void MakeUiReadable()
    {
      var canvas = GetComponent<Canvas>();
      if (canvas != null)
      {
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null) scaler.matchWidthOrHeight = 0f;
      }
      foreach (var text in GetComponentsInChildren<Text>(true))
      {
        text.fontStyle = FontStyle.Bold;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.resizeTextForBestFit = false;
        text.color = new Color(.08f, .12f, .17f, 1f);
        var outline = text.GetComponent<Outline>();
        if (outline == null) outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, .92f);
        outline.effectDistance = new Vector2(2f, -2f);
        var name = text.gameObject.name;
        if (name == "Title") text.fontSize = Mathf.Max(text.fontSize, 34);
        else if (name == "Subtitle") text.fontSize = Mathf.Max(text.fontSize, 18);
        else if (name == "Score") text.fontSize = Mathf.Max(text.fontSize, 26);
        else if (name == "Time" || name == "Rank") text.fontSize = Mathf.Max(text.fontSize, 21);
        else if (name == "Status") text.fontSize = Mathf.Max(text.fontSize, 22);
        else if (name == "NetState") text.fontSize = Mathf.Max(text.fontSize, 19);
        else if (name == "CameraHint") text.fontSize = Mathf.Max(text.fontSize, 14);
        else if (text.transform.parent != null && text.transform.parent.GetComponent<Button>() != null) text.fontSize = Mathf.Max(text.fontSize, 18);
      }
      ResizePanel("StatusPanel", new Vector2(900, 190), new Vector2(0, 150));
      ResizePanel("TitleCard", new Vector2(430, 150), new Vector2(225, -105));
      ResizePanel("ScorePanel", new Vector2(430, 150), new Vector2(-225, -105));
      ResizeChild("StatusPanel", "Status", new Vector2(860, 88), new Vector2(0, 48));
      ResizeChild("StatusPanel", "NetState", new Vector2(300, 28), new Vector2(0, -12));
      ResizeChild("StatusPanel", "重新开始", new Vector2(145, 36), new Vector2(-330, -65));
      ResizeChild("StatusPanel", "重新连接摄像头", new Vector2(145, 36), new Vector2(-165, -65));
      ResizeChild("StatusPanel", "手势控制", new Vector2(145, 36), new Vector2(165, -65));
      ResizeChild("StatusPanel", "鼠标测试", new Vector2(145, 36), new Vector2(330, -65));
    }

    private void ApplyResponsiveLayout(bool force)
    {
      var safeRect = Screen.safeArea;
      var screen = new Vector2Int(Screen.width, Screen.height);
      var landscape = Screen.width >= Screen.height;
      if (_layoutRoot is RectTransform safe)
      {
        safe.anchorMin = new Vector2(safeRect.xMin / Screen.width, safeRect.yMin / Screen.height);
        safe.anchorMax = new Vector2(safeRect.xMax / Screen.width, safeRect.yMax / Screen.height);
        safe.offsetMin = safe.offsetMax = Vector2.zero;
      }
      if (!force && _layoutInitialized && landscape == _lastLandscape && _lastSafeArea == safeRect && _lastScreen == screen) return;
      _lastSafeArea = safeRect; _lastScreen = screen;
      _layoutInitialized = true;
      _lastLandscape = landscape;
      var canvas = GetComponent<Canvas>();
      var scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
      if (scaler != null)
      {
        scaler.referenceResolution = landscape ? new Vector2(1920, 1080) : new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = landscape ? .5f : 0f;
      }

      var status = (_layoutRoot ?? transform).Find("StatusPanel") as RectTransform;
      var title = (_layoutRoot ?? transform).Find("TitleCard") as RectTransform;
      var score = (_layoutRoot ?? transform).Find("ScorePanel") as RectTransform;
      var cameraCard = (_layoutRoot ?? transform).Find("CameraPanel") as RectTransform;
      SetRect(title, landscape ? new Vector2(420, 140) : new Vector2(500, 166), landscape ? new Vector2(235, -95) : new Vector2(270, -110), new Vector2(0,1));
      SetRect(score, landscape ? new Vector2(350, 140) : new Vector2(500, 166), landscape ? new Vector2(-200,-95) : new Vector2(-270,-110), new Vector2(1,1));
      ResizeChild("TitleCard", "Title", new Vector2(460,68), new Vector2(0,0));
      ResizeChild("TitleCard", "Subtitle", new Vector2(460,44), new Vector2(0,-38));
      ResizeChild("ScorePanel", "Score", new Vector2(440,64), new Vector2(0,32));
      ResizeChild("ScorePanel", "Time", new Vector2(220,42), new Vector2(-110,-40));
      ResizeChild("ScorePanel", "Rank", new Vector2(210,42), new Vector2(115,-40));
      SetRect(status, new Vector2(1000,290), new Vector2(0,165), new Vector2(.5f,0));
      ResizeChild("StatusPanel", "Status", new Vector2(940,60), new Vector2(0,94));
      ResizeChild("StatusPanel", "重新开始", new Vector2(450,78), new Vector2(-240,8));
      ResizeChild("StatusPanel", "重新连接摄像头", new Vector2(450,78), new Vector2(240,8));
      ResizeChild("StatusPanel", "手势控制", new Vector2(450,78), new Vector2(-240,-82));
      ResizeChild("StatusPanel", "鼠标测试", new Vector2(450,78), new Vector2(240,-82));
      SetRect(cameraCard, new Vector2(320,280), new Vector2(180,-370), new Vector2(0,1));
      ResizeChild("CameraPanel", "CameraPreview", new Vector2(290,200), new Vector2(0,25));
      ResizeChild("CameraPanel", "CameraHint", new Vector2(290,48), new Vector2(0,-105));
      SetRect(finishPanel != null ? finishPanel.transform as RectTransform : null, new Vector2(880,440), Vector2.zero, new Vector2(.5f,.5f));
      ResizeChild("FinishPanel", "FinishText", new Vector2(810,380), Vector2.zero);
      SetFont("Title", landscape ? 38 : 48); SetFont("Subtitle", 30); SetFont("Score", 48);
      SetFont("Time", 34); SetFont("Rank", 32); SetFont("Status", 32); SetFont("NetState",28); SetFont("CameraHint",26); SetFont("FinishText",42);
      foreach (var button in GetComponentsInChildren<Button>(true))
        foreach (var label in button.GetComponentsInChildren<Text>(true))
        {
          label.fontSize = 36;
          label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
          label.rectTransform.offsetMin = new Vector2(12,4); label.rectTransform.offsetMax = new Vector2(-12,-4);
        }
    }

    private static void SetButtonLabel(Button button, string value)
    {
      if (button != null) button.GetComponentInChildren<Text>().text = value;
    }

    private System.Collections.Generic.IEnumerable<Transform> GetTopLevelChildren()
    {
      foreach (Transform child in transform) if (child.name != "SafeArea") yield return child;
    }

    private void SetRect(RectTransform rect, Vector2 size, Vector2 position, Vector2 anchor)
    {
      if (rect == null) return;
      rect.anchorMin = rect.anchorMax = anchor;
      rect.pivot = new Vector2(.5f, .5f);
      rect.sizeDelta = size;
      rect.anchoredPosition = position;
    }

    private void SetFont(string objectName, int size)
    {
      foreach (var text in GetComponentsInChildren<Text>(true))
        if (text.gameObject.name == objectName) text.fontSize = size;
    }

    private void SetTextByName(string objectName, string value)
    {
      foreach (var text in GetComponentsInChildren<Text>(true))
        if (text.gameObject.name == objectName) text.text = value;
    }

    private void ResizePanel(string objectName, Vector2 size, Vector2 position)
    {
      if (transform == null) return;
      var child = (_layoutRoot ?? transform).Find(objectName);
      if (child == null) return;
      var rect = child as RectTransform;
      if (rect == null) return;
      rect.sizeDelta = size;
      rect.anchoredPosition = position;
    }

    private void ResizeChild(string parentName, string childName, Vector2 size, Vector2 position)
    {
      var parent = (_layoutRoot ?? transform).Find(parentName);
      if (parent == null) return;
      var child = parent.Find(childName) as RectTransform;
      if (child == null) return;
      child.sizeDelta = size;
      child.anchoredPosition = position;
    }
  }
}
