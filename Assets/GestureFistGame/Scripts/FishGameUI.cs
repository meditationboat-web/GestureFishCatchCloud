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
    public Button gestureButton;
    public Button mouseButton;
    public Button resetButton;
    public Button calibrateButton;
    private float _nextRefresh;

    private void Update()
    {
      if (game == null || input == null || Time.unscaledTime < _nextRefresh) return;
      _nextRefresh = Time.unscaledTime + .08f;
      scoreText.text = "分数  " + game.Score + "    连击  " + game.Combo;
      timeText.text = "时间  " + Mathf.CeilToInt(game.TimeRemaining).ToString("00") + " 秒";
      rankText.text = "等级  " + game.Rank;
      statusText.text = game.LastEvent + "\n" + input.Status;
      netText.text = "网状态  " + (game.net != null ? game.net.PhaseName : "-");
      if (tracker != null && !input.mouseMode && tracker.CameraReady)
        statusText.text += "\n" + tracker.Status;
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
  }
}
