using UnityEngine;
namespace GestureFistGame
{
  [RequireComponent(typeof(Rigidbody),typeof(CapsuleCollider))]
  public sealed class FistPlayerController : MonoBehaviour
  {
    public FistAnchor leftFist,rightFist;
    public float supportSpring=100;
    public float supportDamping=16;
    public float maxAcceleration=85;
    public float maxSpeed=11;
    public float launchRetention=1.0f;
    public bool allowControl=true;
    public int SupportCount {get;private set;}
    public int Hits=>(leftFist!=null?leftFist.HitCount:0)+(rightFist!=null?rightFist.HitCount:0);
    public string StatusText=>!allowControl?"已抵达终点":SupportCount==2?"双拳支撑":SupportCount==1?"单拳支撑":"拳头自由 · 保留惯性";
    public Rigidbody Body=>_body;
    private Rigidbody _body;
    private Vector3[] _offset={new Vector3(-1,-.4f,1),new Vector3(1,-.4f,1)};
    private Vector3[] _previous={new Vector3(-1,-.4f,1),new Vector3(1,-.4f,1)};
    private Vector3[] _velocity={Vector3.zero,Vector3.zero};
    private bool[] _grip={false,false},_valid={false,false},_previousValid={false,false};
    private float[] _received={-100,-100},_resumeAfter={0,0};
    private void Awake()
    {
      _body=GetComponent<Rigidbody>();
      _body.mass=4;
      _body.drag=.25f;
      _body.constraints=RigidbodyConstraints.FreezeRotation;
      _body.interpolation=RigidbodyInterpolation.Interpolate;
      _body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
      _body.maxDepenetrationVelocity=4;
    }
    public void SubmitHand(int side,Vector3 offset,bool grip,bool valid)
    {
      if(side<0 || side>1) return;
      _offset[side]=Vector3.ClampMagnitude(offset,3.5f);
      _grip[side]=grip; _valid[side]=valid; _received[side]=Time.time;
    }
    public void ReleaseAll()
    {
      leftFist?.Release(); rightFist?.Release();
      SupportCount=0;
    }
    public void InvalidateHands()
    {
      for(int i=0;i<2;i++) { _valid[i]=false;_previousValid[i]=false;_received[i]=-100;_velocity[i]=Vector3.zero; }
      ReleaseAll();
    }
    private void FixedUpdate()
    {
      if(_body==null || leftFist==null || rightFist==null) return;
      float dt=Time.fixedDeltaTime;
      Vector3 sumError=Vector3.zero, sumVelocity=Vector3.zero;
      SupportCount=0;
      for(int i=0;i<2;i++)
      {
        var fist=i==0?leftFist:rightFist;
        bool valid=_valid[i] && Time.time-_received[i]<.35f && allowControl;
        if(valid && !_previousValid[i])
        {
          _previous[i]=_offset[i]; _velocity[i]=Vector3.zero;
          _resumeAfter[i]=Time.time+.12f;
          fist.Release(.12f);
        }
        Vector3 velocity=Vector3.ClampMagnitude((_offset[i]-_previous[i])/dt,15);
        _velocity[i]=Vector3.Lerp(_velocity[i],velocity,.5f);
        fist.Step(_body.position,_offset[i],_velocity[i],_grip[i] && Time.time>=_resumeAfter[i],valid,dt);
        if(fist.IsPlanted && valid)
        {
          sumError+=fist.SupportPoint-(_body.position+_offset[i]);
          sumVelocity+=-_velocity[i];
          SupportCount++;
        }
        _previous[i]=_offset[i]; _previousValid[i]=valid;
      }
      if(SupportCount>0)
      {
        // The desired body position is anchor minus current hand offset.
        // Holding the same input holds still; only changing an anchored hand moves the body.
        Vector3 acceleration=sumError/SupportCount*supportSpring
          +(sumVelocity/SupportCount-_body.velocity)*supportDamping-Physics.gravity;
        _body.AddForce(Vector3.ClampMagnitude(acceleration,maxAcceleration),ForceMode.Acceleration);
      }
      _body.velocity=Vector3.ClampMagnitude(_body.velocity,maxSpeed);
    }
    public void ResetAt(Vector3 position)
    {
      if(_body==null) return;
      _body.constraints=RigidbodyConstraints.FreezeRotation;
      transform.position=position;
      _body.position=position;
      _body.velocity=Vector3.zero; _body.angularVelocity=Vector3.zero;
      for(int i=0;i<2;i++) { _previous[i]=_offset[i]; _velocity[i]=Vector3.zero; _previousValid[i]=false; }
      ReleaseAll();
      leftFist?.ResetFist(position+_offset[0]); rightFist?.ResetFist(position+_offset[1]);
    }
  }
}

