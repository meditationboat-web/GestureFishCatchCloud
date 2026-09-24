using UnityEngine;
namespace GestureFistGame
{
  public sealed class CameraFollow : MonoBehaviour
  {
    public Transform target;
    public Vector3 offset=new Vector3(7,7,-10);
    public Vector3 lookOffset=new Vector3(0,.3f,2);
    public float followSharpness=4;
    private void LateUpdate()
    {
      if(target==null)return;
      transform.position=Vector3.Lerp(transform.position,target.position+offset,1-Mathf.Exp(-followSharpness*Time.deltaTime));
      transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(target.position+lookOffset-transform.position),1-Mathf.Exp(-7*Time.deltaTime));
    }
  }
}

