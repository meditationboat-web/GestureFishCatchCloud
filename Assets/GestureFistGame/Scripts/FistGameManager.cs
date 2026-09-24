using UnityEngine;
namespace GestureFistGame
{
  public sealed class FistGameManager : MonoBehaviour
  {
    public FistPlayerController player;
    public FistGestureInput input;
    public Transform checkpoint;
    public Transform finish;
    public Transform[] checkpoints;
    public TrainingDummy[] enemies;
    public string gapStage="03 / 峡谷与台阶";
    public string finalStage="04 / 登顶";
    public string finishMessage="抵达山顶";
    public string Stage {get;private set;}="01 / 支点训练";
    public bool Completed {get;private set;}
    public int Falls {get;private set;}
    public int CheckpointIndex {get;private set;}
    public float Elapsed {get;private set;}
    private void Update()
    {
      if(player==null) return;
      if(Input.GetKeyDown(KeyCode.R)) Respawn();
      if(!Completed)
      {
        Elapsed+=Time.deltaTime;
        if(player.transform.position.y<-4 || Mathf.Abs(player.transform.position.x)>22) { Falls++; Respawn(); }
        for(int i=CheckpointIndex+1;i<checkpoints.Length;i++)
          if(Vector3.Distance(player.transform.position,checkpoints[i].position)<2.2f)
          { CheckpointIndex=i; checkpoint=checkpoints[i]; }
        float z=player.transform.position.z;
        Stage=z<7?"01 / 支点训练":z<14?"02 / 击飞守卫":z<23?gapStage:finalStage;
        if(finish!=null && Vector3.Distance(player.transform.position,finish.position)<2.2f) Complete();
      }
    }
    public void Respawn()
    {
      if(Completed) { Restart(); return; }
      player.ResetAt(checkpoint!=null?checkpoint.position:new Vector3(0,1.3f,0));
    }
    public void Restart()
    {
      Completed=false; Falls=0; Elapsed=0; CheckpointIndex=0;
      checkpoint=checkpoints[0]; player.allowControl=true;
      player.leftFist.ResetHitCount(); player.rightFist.ResetHitCount();
      player.ResetAt(checkpoint.position);
      foreach(var enemy in enemies) enemy.ResetDummy();
    }
    public void Complete()
    {
      Completed=true; player.allowControl=false; player.ReleaseAll();
      player.Body.velocity=Vector3.zero;
      player.Body.angularVelocity=Vector3.zero;
      player.Body.constraints=RigidbodyConstraints.FreezeAll;
      Stage=finishMessage+"！";
    }
  }
}

