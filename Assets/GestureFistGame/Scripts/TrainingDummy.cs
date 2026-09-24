using UnityEngine;
namespace GestureFistGame
{
  [RequireComponent(typeof(Rigidbody))]
  public sealed class TrainingDummy : MonoBehaviour
  {
    public FistPlayerController target;
    public Renderer bodyRenderer;
    public float maxHealth=2;
    public float activationRange=6;
    public bool mobile=true;
    public float respawnDelay=10;
    public bool IsKnockedOut {get;private set;}
    public float Health {get;private set;}
    private Rigidbody _body;
    private Vector3 _spawn;
    private Quaternion _rotation;
    private float _nextHit,_stunnedUntil,_respawnAt;
    private MaterialPropertyBlock _block;
    private Color _original;
    private void Awake()
    {
      _body=GetComponent<Rigidbody>(); _spawn=transform.position; _rotation=transform.rotation;
      Health=maxHealth;
      _block=new MaterialPropertyBlock();
      if(bodyRenderer!=null) _original=bodyRenderer.sharedMaterial.color;
      _body.constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;
    }
    public bool TakeHit(Vector3 point,Vector3 impulse)
    {
      if(IsKnockedOut || Time.time<_nextHit) return false;
      _nextHit=Time.time+.25f; _stunnedUntil=Time.time+.65f;
      Health--;
      _body.AddForceAtPosition(impulse,point,ForceMode.Impulse);
      if(Health<=0)
      {
        IsKnockedOut=true; _respawnAt=Time.time+respawnDelay;
        _body.constraints=RigidbodyConstraints.None;
        _body.AddTorque(new Vector3(2,1,3),ForceMode.Impulse);
      }
      return true;
    }
    private void FixedUpdate()
    {
      if(IsKnockedOut || !mobile || target==null || Time.time<_stunnedUntil || target.SupportCount>1) return;
      Vector3 delta=target.transform.position-transform.position; delta.y=0;
      if(delta.magnitude<activationRange && delta.magnitude>1.4f)
      {
        Vector3 next=transform.position+delta.normalized*.5f;
        // Do not walk off platforms or over the gap.
        if(Physics.Raycast(next+Vector3.up,Vector3.down,2.4f,1<<8))
          _body.AddForce(delta.normalized*2-_body.velocity*.6f,ForceMode.Acceleration);
      }
    }
    private void Update()
    {
      if((IsKnockedOut && Time.time>_respawnAt) || transform.position.y<-7) ResetDummy();
      if(bodyRenderer!=null)
      {
        _block.SetColor("_Color",Time.time<_nextHit?new Color(1,.8f,.4f):_original);
        bodyRenderer.SetPropertyBlock(_block);
      }
    }
    public void ResetDummy()
    {
      _body.position=_spawn; _body.rotation=_rotation;
      _body.velocity=Vector3.zero; _body.angularVelocity=Vector3.zero;
      _body.constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;
      Health=maxHealth; IsKnockedOut=false; _nextHit=0;
    }
  }
}

