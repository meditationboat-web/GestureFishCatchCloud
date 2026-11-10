using UnityEngine;
using UnityEngine.UI;
namespace GestureFistGame
{
  public sealed class FishCoopOverlay : MonoBehaviour
  {
    private FishGameUI ui;
    private Text title;
    private Button resume;
    private void Awake()
    {
      ui = GetComponent<FishGameUI>();
      var go = new GameObject("CooperationPrompt", typeof(RectTransform), typeof(Text));
      go.transform.SetParent(transform.Find("SafeArea"), false);
      var rect = go.GetComponent<RectTransform>();
      rect.anchorMin = new Vector2(.08f, .52f); rect.anchorMax = new Vector2(.92f, .77f); rect.offsetMin = rect.offsetMax = Vector2.zero;
      title = go.GetComponent<Text>(); title.font = ui.scoreText.font; title.fontSize = 42;
      title.alignment = TextAnchor.MiddleCenter; title.color = new Color(.14f,.25f,.08f); title.raycastTarget = false;
      var outline = go.AddComponent<Outline>(); outline.effectColor = new Color(1,1,.9f,.95f); outline.effectDistance = new Vector2(2,-2);
      var button = new GameObject("ConfirmTeamResume", typeof(RectTransform), typeof(Image), typeof(Button));
      button.transform.SetParent(transform.Find("SafeArea"), false);
      var br = button.GetComponent<RectTransform>(); br.anchorMin = new Vector2(.25f,.44f); br.anchorMax = new Vector2(.75f,.51f); br.offsetMin = br.offsetMax = Vector2.zero;
      FishUiTheme.Card(button.GetComponent<Image>(), new Color(.75f,.88f,.45f));
      resume = button.GetComponent<Button>(); resume.onClick.AddListener(()=>ui.game.ConfirmResume());
      var label = new GameObject("Label", typeof(RectTransform), typeof(Text)); label.transform.SetParent(button.transform,false);
      var lr = label.GetComponent<RectTransform>(); lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.offsetMin=lr.offsetMax=Vector2.zero;
      var text=label.GetComponent<Text>();text.font=title.font;text.fontSize=32;text.alignment=TextAnchor.MiddleCenter;text.color=title.color;text.text="确认继续";
    }
    private void Update()
    {
      var game=ui.game;
      bool waiting=game.Phase==FishGameController.RoundPhase.Confirming || game.Phase==FishGameController.RoundPhase.Countdown || game.Phase==FishGameController.RoundPhase.Paused;
      title.gameObject.SetActive(waiting);
      title.text=game.Phase==FishGameController.RoundPhase.Countdown ? game.FlowHint+" · 准备起网" : game.FlowHint;
      resume.gameObject.SetActive(game.Phase==FishGameController.RoundPhase.Paused);
      resume.interactable=ui.input.mouseMode || ui.input.TwoPlayersReady;
    }
  }
}
