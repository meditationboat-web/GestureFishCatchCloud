using UnityEngine;
using UnityEngine.UI;
namespace GestureFistGame
{
  public sealed class FistGameUI : MonoBehaviour
  {
    public FistGameManager game;
    public FistGestureInput input;
    public MediaPipeTracker tracker;
    public Text stageText,statusText,trackerText,leftText,rightText,scoreText,finishText;
    public GameObject helpPanel,finishPanel;
    public GameObject cameraPanel;
    public Text modeText;
    public Button mouseButton,cameraButton,keyboardButton,calibrateButton;
    private float _next;
    private void Update()
    {
      if(Input.GetKeyDown(KeyCode.H)) ToggleHelp();
      if(Time.unscaledTime<_next || game==null) return;
      _next=Time.unscaledTime+.1f;
      stageText.text=game.Stage;
      statusText.text=input.Status;
      if(!tracker.CameraReady && input.UsesCamera && !input.externalTestInput)
        statusText.text="摄像头未就绪 · 点击左下角“鼠标游玩”即可开始 · H 查看操作说明";
      if(modeText!=null) modeText.text="当前："+(input.mouseMode?"鼠标游玩":input.keyboardMode?"键盘控制":"手势控制");
      if(mouseButton!=null) mouseButton.interactable=!input.mouseMode;
      if(cameraButton!=null) cameraButton.interactable=!input.UsesCamera;
      if(keyboardButton!=null) keyboardButton.interactable=!input.keyboardMode;
      if(calibrateButton!=null) calibrateButton.interactable=input.UsesCamera;
      if(cameraPanel!=null) cameraPanel.SetActive(input.UsesCamera);
      trackerText.text=tracker.Status;
      leftText.text="左拳  /  "+(game.player.leftFist.IsPlanted?"已支撑":"自由");
      rightText.text="右拳  /  "+(game.player.rightFist.IsPlanted?"已支撑":"自由");
      scoreText.text=game.player.Hits+" 次命中    "+game.Falls+" 次掉落    "+Mathf.FloorToInt(game.Elapsed/60).ToString("00")+":"+Mathf.FloorToInt(game.Elapsed%60).ToString("00");
      finishPanel.SetActive(game.Completed);
      if(game.Completed) finishText.text=game.finishMessage+"\n"+game.player.Hits+" 次命中 · 用时 "+game.Elapsed.ToString("0")+" 秒\n点击重新开始，再挑战一次";
    }
    public void ToggleHelp() { helpPanel.SetActive(!helpPanel.activeSelf);input.mouseControl?.ResetControl(); }
    public void ResetPlayer() { input.mouseControl?.ResetControl();game.Respawn(); }
  }
}

