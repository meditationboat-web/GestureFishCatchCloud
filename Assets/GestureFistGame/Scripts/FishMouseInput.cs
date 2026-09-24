using UnityEngine;

namespace GestureFistGame
{
  public sealed class FishMouseInput : MonoBehaviour
  {
    public FishGameController game;
    [Range(.02f, .3f)] public float upwardDragThreshold = .08f;
    private Vector2 _lastPointer;
    private bool _hasPointer;
    private bool _held;

    public void Tick(bool blocked)
    {
      if (game == null || blocked) return;
      var pointer = (Vector2)Input.mousePosition;
      var delta = _hasPointer ? (pointer - _lastPointer) / Mathf.Max(1f, Screen.height) : Vector2.zero;
      _lastPointer = pointer;
      _hasPointer = true;
      bool held = Input.GetMouseButton(0);
      ProcessFrame(delta, held, blocked);
      if (Input.GetKeyDown(KeyCode.Space)) game.TryThrow("空格测试");
    }

    public bool ProcessFrame(Vector2 delta, bool held, bool blocked)
    {
      if (game == null || blocked) { _held = held; return false; }
      bool upStroke = held && _held && delta.y > upwardDragThreshold;
      _held = held;
      return upStroke && game.TryThrow("鼠标上挥");
    }
  }
}
