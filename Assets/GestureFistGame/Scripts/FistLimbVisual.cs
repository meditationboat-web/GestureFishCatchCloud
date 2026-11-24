using UnityEngine;

namespace GestureFistGame
{
  public sealed class FistLimbVisual : MonoBehaviour
  {
    public Transform start;
    public Transform end;
    public float thickness = 0.24f;

    private void LateUpdate()
    {
      if (start == null || end == null) return;
      var delta = end.position - start.position;
      transform.position = (start.position + end.position) * 0.5f;
      transform.rotation = Quaternion.FromToRotation(Vector3.up, delta.sqrMagnitude > 0.0001f ? delta : Vector3.up);
      transform.localScale = new Vector3(thickness, Mathf.Max(0.1f, delta.magnitude * 0.5f), thickness);
    }
  }
}
