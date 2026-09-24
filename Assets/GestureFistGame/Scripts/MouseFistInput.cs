using UnityEngine;
using UnityEngine.EventSystems;

namespace GestureFistGame
{
  // Mouse positions become relative hand targets; the existing support physics moves the body.
  public sealed class MouseFistInput : MonoBehaviour
  {
    public FistPlayerController player;
    [Range(2,12)] public float dragScale=6;
    [Range(2,12)] public float heightDragScale=6;
    [Range(.1f,.8f)] public float wheelHeight=.35f;
    public float returnSpeed=6;
    private readonly Vector3[] _offset={new Vector3(-1,-.4f,1.2f),new Vector3(1,-.4f,1.2f)};
    private readonly bool[] _captured=new bool[2],_held=new bool[2];
    private Vector2 _lastPointer;
    private bool _hasPointer,_ignoreUntilRelease;
    public Vector3 LeftOffset=>_offset[0];
    public Vector3 RightOffset=>_offset[1];
    public bool LeftHeld=>_captured[0];
    public bool RightHeld=>_captured[1];

    public void Tick(bool blocked)
    {
      Vector2 pointer=Input.mousePosition;
      var delta=_hasPointer?(pointer-_lastPointer)/Mathf.Max(1,Screen.height):Vector2.zero;
      _lastPointer=pointer;_hasPointer=true;
      bool overUI=EventSystem.current!=null && EventSystem.current.IsPointerOverGameObject();
      ProcessFrame(delta,Input.mouseScrollDelta.y,Input.GetMouseButton(0),Input.GetMouseButton(1),overUI,blocked,Time.unscaledDeltaTime);
    }

    // Shared by live mouse input and deterministic input-replay checks.
    public void ProcessFrame(Vector2 drag,float wheel,bool left,bool right,bool overUI,bool blocked,float dt)
    {
      if(player==null) return;
      if(blocked || !player.allowControl) { ResetControl();return; }
      if(_ignoreUntilRelease)
      {
        if(left || right) return;
        _ignoreUntilRelease=false;
      }
      for(int i=0;i<2;i++)
      {
        bool down=i==0?left:right;
        bool began=down && !_held[i];
        if(began) _captured[i]=!overUI;
        if(!down) _captured[i]=false;
        if(_captured[i])
        {
          if(!began)
          {
            // A downward mouse stroke is the primary support action: lower the fist
            // and retract it at the same time, matching the physical "push off" motion.
            // The wheel remains a fine height trim for landing on steps.
            _offset[i]+=new Vector3(
              drag.x*dragScale,
              drag.y*heightDragScale+wheel*wheelHeight,
              drag.y*dragScale);
          }
          _offset[i]=Vector3.ClampMagnitude(_offset[i],3.4f);
        }
        else _offset[i]=Vector3.MoveTowards(_offset[i],new Vector3(i==0?-1:1,-.4f,1.2f),returnSpeed*dt);
        player.SubmitHand(i,_offset[i],_captured[i],true);
        _held[i]=down;
      }
    }

    public void ResetControl(bool waitUntilRelease=true)
    {
      _captured[0]=_captured[1]=_held[0]=_held[1]=false;
      _offset[0]=new Vector3(-1,-.4f,1.2f);_offset[1]=new Vector3(1,-.4f,1.2f);
      _hasPointer=false;_ignoreUntilRelease=waitUntilRelease;
      player?.InvalidateHands();
    }
    private void OnApplicationFocus(bool focus) { if(!focus) ResetControl(); }
    private void OnDisable() { ResetControl(); }
  }
}
