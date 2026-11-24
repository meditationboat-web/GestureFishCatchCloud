#if FISH_LIFT_DIAGNOSTICS
using UnityEngine;

namespace GestureFistGame
{
    public sealed class FishLiftDiagnostics : MonoBehaviour
    {
        private FishGameUI ui;
        private bool visible = true;
        private GUIStyle style;

        private void Awake() => ui = GetComponent<FishGameUI>();
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3)) visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible || ui.game == null || ui.input == null || ui.tracker == null) return;
            var game = ui.game;
            var input = ui.input;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label);
                style.font = ui.scoreText.font;
                style.wordWrap = true;
                style.padding = new RectOffset(12, 12, 6, 6);
            }
            style.fontSize = Mathf.Clamp(Screen.height / 45, 13, 20);
            float width = Mathf.Min(580, Screen.width - 24);
            var area = new Rect(12, Screen.height * .24f, width, Mathf.Min(Screen.height * .73f, 360));
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(area);
            GUILayout.Label($"起网诊断  F3 隐藏或显示\n摄像头：{ui.tracker.Status}\n对局：{game.Phase}  入镜人数：{input.DetectedPlayers}  双人就绪：{input.TwoPlayersReady}\nP1：{input.LiftPoseStatus(0)}\nP2：{input.LiftPoseStatus(1)}\n合作：{game.PendingLiftStatus}\n网当前状态：{game.net?.PhaseName ?? "未绑定"}\n最近判定：{game.LiftDebugResult}\n游戏反馈：{game.LastEvent}", style);
            GUILayout.EndArea();
        }
    }
}
#endif
