using UnityEngine;
using UnityEngine.Rendering;

namespace GestureFistGame
{
  // Shared by scene authoring and migration of older saved scenes.
  public static class FishNetGeometry
  {
    // The net is a compact horizontal throw target.  It should read as a
    // catchable patch in the middle of the channel rather than a second
    // rectangle covering the whole water surface.
    public const float HalfWidth = 2.4f;
    public const float HalfDepth = 1.55f;
    public const float RestHeight = -.12f;
    public const float RaisedHeight = 1.05f;
    public static readonly Vector3 TriggerCenter = new Vector3(0, .15f, 0);
    public static readonly Vector3 TriggerSize = new Vector3(4.8f, .45f, 3.1f);

    public static Transform Create(Transform parent, string name, Material rope, Material edge, bool submerged)
    {
      var root = new GameObject(name).transform;
      root.SetParent(parent, false);
      const int strands = 13;
      const int samples = 25;
      for (int axis = 0; axis < 2; axis++)
      {
        for (int i = 0; i < strands; i++)
        {
          bool border = i == 0 || i == strands - 1;
          var go = new GameObject("RopeNet_" + (axis == 0 ? "X_" : "Z_") + i);
          go.transform.SetParent(root, false);
          var line = go.AddComponent<LineRenderer>();
          line.useWorldSpace = false;
          line.positionCount = samples;
          line.startWidth = line.endWidth = border ? .055f : (submerged ? .021f : .028f);
          line.numCapVertices = 2;
          line.numCornerVertices = 2;
          line.shadowCastingMode = ShadowCastingMode.Off;
          line.receiveShadows = false;
          line.sharedMaterial = border ? edge : rope;
          for (int p = 0; p < samples; p++)
          {
            float along = p / (float)(samples - 1);
            float across = i / (float)(strands - 1);
            float u = axis == 0 ? along : across;
            float v = axis == 0 ? across : along;
            // Both strand directions share the same surface at intersections.
            float sag = Mathf.Sin(u * Mathf.PI) * Mathf.Sin(v * Mathf.PI) * .10f;
            line.SetPosition(p, new Vector3(Mathf.Lerp(-HalfWidth, HalfWidth, u),
              .05f - sag, Mathf.Lerp(-HalfDepth, HalfDepth, v)));
          }
        }
      }
      return root;
    }
  }
}
