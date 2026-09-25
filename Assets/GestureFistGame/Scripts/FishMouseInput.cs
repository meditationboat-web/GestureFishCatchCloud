using UnityEngine;

namespace GestureFistGame
{
  /// <summary>
  /// Editor fallback for the two-person rule. Drag upward on the left/right
  /// half of the window, or press Q/P for the left/right player's hand.
  /// </summary>
  public sealed class FishMouseInput : MonoBehaviour
  {
    public FishGameController game;
    public FishGestureInput gestureInput;
    [Range(.02f, .3f)] public float upwardDragThreshold = .08f;
    private Vector2 _lastPointer;
    private bool _hasPointer;
    private bool _held;
    private FishGestureInput.HandSide _dragSide;
    private bool _dragSideKnown;
    private float _dragStartY;
    private bool _leftKeyHeld;
    private bool _rightKeyHeld;
    private Vector2 _legacyPointer;

    public void Tick(bool blocked)
    {
      if (game == null || blocked) return;
      var pointer = (Vector2)Input.mousePosition;
      var delta = _hasPointer ? (pointer - _lastPointer) / Mathf.Max(1f, Screen.height) : Vector2.zero;
      _lastPointer = pointer;
      _hasPointer = true;
      bool held = Input.GetMouseButton(0);
      ProcessFrame(pointer, delta, held, blocked);

      bool leftKey = Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.W);
      bool rightKey = Input.GetKey(KeyCode.P) || Input.GetKey(KeyCode.UpArrow);
      if (leftKey && !_leftKeyHeld) gestureInput?.RegisterMouseWave(FishGestureInput.HandSide.Left);
      if (rightKey && !_rightKeyHeld) gestureInput?.RegisterMouseWave(FishGestureInput.HandSide.Right);
      _leftKeyHeld = leftKey;
      _rightKeyHeld = rightKey;
      if (Input.GetKeyDown(KeyCode.Space))
      {
        gestureInput?.RegisterMouseWave(FishGestureInput.HandSide.Left);
        gestureInput?.RegisterMouseWave(FishGestureInput.HandSide.Right);
      }
    }

    public bool ProcessFrame(Vector2 pointer, Vector2 delta, bool held, bool blocked)
    {
      if (game == null || blocked) { _held = held; return false; }
      if (!held)
      {
        _held = false;
        _dragSideKnown = false;
        return false;
      }
      var side = pointer.x < Screen.width * .5f
        ? FishGestureInput.HandSide.Left
        : FishGestureInput.HandSide.Right;
      if (!_held || !_dragSideKnown || side != _dragSide)
      {
        _dragSide = side;
        _dragSideKnown = true;
        _dragStartY = pointer.y;
      }
      _held = true;
      if (pointer.y - _dragStartY >= upwardDragThreshold * Screen.height)
      {
        _dragStartY = pointer.y;
        gestureInput?.RegisterMouseWave(side);
        return true;
      }
      return false;
    }

    // Compatibility overload used by the existing command-line scene check.
    // Its Vector2 argument is a normalized frame delta from the previous input.
    public bool ProcessFrame(Vector2 delta, bool held, bool blocked)
    {
      _legacyPointer += delta * Screen.height;
      return ProcessFrame(_legacyPointer, delta, held, blocked);
    }
  }
}
