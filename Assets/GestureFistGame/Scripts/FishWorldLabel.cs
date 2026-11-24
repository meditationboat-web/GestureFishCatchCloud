using UnityEngine;

namespace GestureFistGame
{
  /// <summary>Faces the small in-world player labels toward the active camera.</summary>
  public sealed class FishWorldLabel : MonoBehaviour
  {
    private void LateUpdate()
    {
      var camera = Camera.main;
      if (camera == null) return;
      var direction = camera.transform.position - transform.position;
      if (direction.sqrMagnitude < .0001f) return;
      transform.rotation = Quaternion.LookRotation(direction.normalized, camera.transform.up);
    }
  }
}
