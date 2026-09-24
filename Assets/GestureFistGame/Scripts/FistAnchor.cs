using System.Collections.Generic;
using UnityEngine;
namespace GestureFistGame
{
  [RequireComponent(typeof(Rigidbody),typeof(SphereCollider))]
  public sealed class FistAnchor : MonoBehaviour
  {
    public float radius=.42f;
    public float maxReach=3.5f;
    public float followSpeed=18;
    public LayerMask supportMask=1<<8;
    public LayerMask enemyMask=1<<10;
    public Transform supportMarker;
    public Renderer glove;
    public TrailRenderer trail;
    public ParticleSystem impact;
    public AudioSource hitAudio;
    public bool IsPlanted {get;private set;}
    public Vector3 SupportPoint {get;private set;}
    public Vector3 SupportNormal {get;private set;}
    public int HitCount {get;private set;}
    private Rigidbody _body;
    private Vector3 _plantOffset;
    private float _plantBlockedUntil, _nextHit;
    private readonly HashSet<TrainingDummy> _hitSet=new HashSet<TrainingDummy>();

    private void Awake()
    {
      _body=GetComponent<Rigidbody>();
      _body.isKinematic=true; _body.useGravity=false;
      _body.interpolation=RigidbodyInterpolation.Interpolate;
      GetComponent<SphereCollider>().radius=radius;
      GetComponent<SphereCollider>().isTrigger=true;
    }
    public void Release(float blockTime=.12f)
    {
      IsPlanted=false;
      _plantBlockedUntil=Time.time+blockTime;
      if(supportMarker!=null) supportMarker.gameObject.SetActive(false);
    }
    public void ResetHitCount() { HitCount=0; }
    public void ResetFist(Vector3 position)
    {
      Release(.25f);
      transform.position=position;
      if(_body!=null) _body.position=position;
      else transform.position=position;
      if(trail!=null) trail.Clear();
    }
    public void Step(Vector3 bodyPosition,Vector3 offset,Vector3 offsetVelocity,bool grip,bool valid,float dt)
    {
      if(_body==null) return;
      if(IsPlanted && (!valid || !grip || Vector3.Dot(offset-_plantOffset,SupportNormal)>.32f
         || Vector3.Distance(bodyPosition,SupportPoint)>maxReach+.65f)) Release();
      if(IsPlanted)
      {
        _body.MovePosition(SupportPoint);
        if(supportMarker!=null)
        {
          supportMarker.position=SupportPoint-SupportNormal*(radius-.015f);
          supportMarker.rotation=Quaternion.FromToRotation(Vector3.up,SupportNormal);
        }
        if(trail!=null) trail.emitting=false;
        return;
      }
      Vector3 desired=bodyPosition+Vector3.ClampMagnitude(offset,maxReach);
      Vector3 start=_body.position;
      Vector3 next=Vector3.MoveTowards(start,desired,followSpeed*dt);
      Vector3 displacement=next-start;
      bool contact=false;
      RaycastHit hit=default;
      if(displacement.sqrMagnitude>.000001f && Physics.SphereCast(start,radius,displacement.normalized,out hit,
          displacement.magnitude,supportMask,QueryTriggerInteraction.Ignore))
      {
        next=start+displacement.normalized*Mathf.Max(0,hit.distance-.012f);
        contact=true;
      }
      // Tiny contact probe, not a remote ray that pulls the fist down from mid-air.
      if(!contact && Physics.SphereCast(next,.98f*radius,Vector3.down,out hit,.065f,supportMask,QueryTriggerInteraction.Ignore))
      {
        next=hit.point+hit.normal*(radius+.012f);
        contact=true;
      }
      var fromBody=next-bodyPosition;
      if(fromBody.magnitude>radius && Physics.SphereCast(bodyPosition,radius,fromBody.normalized,out var block,
         fromBody.magnitude,supportMask,QueryTriggerInteraction.Ignore))
      {
        next=bodyPosition+fromBody.normalized*Mathf.Max(0,block.distance-.012f);
        hit=block; contact=true;
      }
      if(valid && grip && Time.time>=_plantBlockedUntil && contact)
      {
        IsPlanted=true; SupportPoint=next; SupportNormal=hit.normal; _plantOffset=offset;
        if(supportMarker!=null) supportMarker.gameObject.SetActive(true);
      }
      if(valid && grip && !IsPlanted && offsetVelocity.magnitude>2.5f && Time.time>=_nextHit)
        TryHit(start,next,offsetVelocity);
      _body.MovePosition(next);
      if(trail!=null) trail.emitting=valid && offsetVelocity.magnitude>2;
    }

    private void TryHit(Vector3 start,Vector3 end,Vector3 velocity)
    {
      _hitSet.Clear();
      var delta=end-start;
      if(delta.sqrMagnitude>.00001f)
        foreach(var hit in Physics.SphereCastAll(start,radius+.08f,delta.normalized,delta.magnitude,enemyMask,QueryTriggerInteraction.Ignore))
        {
          var dummy=hit.collider.GetComponentInParent<TrainingDummy>();
          if(dummy!=null) _hitSet.Add(dummy);
        }
      foreach(var col in Physics.OverlapSphere(end,radius+.12f,enemyMask,QueryTriggerInteraction.Ignore))
      {
        var dummy=col.GetComponentInParent<TrainingDummy>();
        if(dummy!=null) _hitSet.Add(dummy);
      }
      bool any=false;
      foreach(var dummy in _hitSet)
      {
        if(dummy.TakeHit(end,velocity.normalized*Mathf.Clamp(velocity.magnitude,4,10)+Vector3.up*3))
        { HitCount++; any=true; }
      }
      if(!any) return;
      _nextHit=Time.time+.28f;
      if(impact!=null) { impact.transform.position=end; impact.Play(); }
      if(hitAudio!=null) hitAudio.Play();
    }
    private void OnDrawGizmosSelected()
    {
      Gizmos.color=IsPlanted?Color.cyan:Color.yellow;
      Gizmos.DrawWireSphere(transform.position,radius);
    }
  }
}

