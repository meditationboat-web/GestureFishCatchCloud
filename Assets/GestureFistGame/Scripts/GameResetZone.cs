using UnityEngine;
namespace GestureFistGame
{
  public sealed class GameResetZone : MonoBehaviour
  {
    public FistGameManager game;
    private void OnTriggerEnter(Collider other)
    {
      if(game!=null && other.GetComponentInParent<FistPlayerController>()==game.player) game.Respawn();
    }
  }
}

