using UnityEngine;
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
    private float _nextRefresh;

    private void Awake()
    {
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
      MakeUiReadable();
    }

    private void Update()
    {
      if (game == null || input == null || Time.unscaledTime < _nextRefresh) return;
      _nextRefresh = Time.unscaledTime + .08f;
      scoreText.text = "分数  " + game.Score + "    连击  " + game.Combo;
      timeText.text = "时间  " + Mathf.CeilToInt(game.TimeRemaining).ToString("00") + " 秒";
      rankText.text = "等级  " + game.Rank;
      statusText.text = game.LastEvent + "\n" + input.Status;
      if (game.spawner != null) statusText.text += "\n" + game.spawner.ValueWindowHint;
      netText.text = "网状态  " + (game.net != null ? game.net.PhaseName : "-");
      if (tracker != null && !input.mouseMode)
      {
        statusText.text += "\n" + tracker.Status;
        if (cameraDebugText != null)
          cameraDebugText.text = tracker.Diagnostics + " | 手=" + input.DetectedHands + "/2" + (input.BothHandsDetected ? " 合作就绪" : " 等待另一只");
      }
      else if (cameraDebugText != null)
      {
        cameraDebugText.text = "切换到手势控制后显示摄像头诊断";
      }
      if (finishPanel != null)
      {
        finishPanel.SetActive(game.IsFinished);
        if (game.IsFinished && finishText != null)
          finishText.text = "回合结束\n" + game.Score + " 分 · 等级 " + game.Rank + "\n点击“重新开始”再来一次";
      }
      if (cameraPanel != null) cameraPanel.SetActive(!input.mouseMode);
      if (gestureButton != null) gestureButton.interactable = input.mouseMode;
      if (mouseButton != null) mouseButton.interactable = !input.mouseMode;
      if (calibrateButton != null) calibrateButton.interactable = !input.mouseMode;
    }

    public void UseGesture() { input?.UseCamera(); }
    public void UseMouse() { input?.UseMouse(); }
    public void ResetRound() { game?.ResetRound(); }
    public void Calibrate() { input?.UseCamera(); }

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
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
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

    private void ResizePanel(string objectName, Vector2 size, Vector2 position)
    {
      if (transform == null) return;
      var child = transform.Find(objectName);
      if (child == null) return;
      var rect = child as RectTransform;
      if (rect == null) return;
      rect.sizeDelta = size;
      rect.anchoredPosition = position;
    }

    private void ResizeChild(string parentName, string childName, Vector2 size, Vector2 position)
    {
      var parent = transform.Find(parentName);
      if (parent == null) return;
      var child = parent.Find(childName) as RectTransform;
      if (child == null) return;
      child.sizeDelta = size;
      child.anchoredPosition = position;
    }
  }
}
