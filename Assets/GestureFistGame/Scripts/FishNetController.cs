using UnityEngine;

namespace GestureFistGame
{
  [DefaultExecutionOrder(100)]
  public sealed class FishNetController : MonoBehaviour
  {
    public FishGameController game;
    public Transform visual;
    // Kept as a serialized compatibility field for older scenes.  The old
    // second net that lived below the water was visually confusing, so the
    // current scene intentionally uses one rope mesh for both idle and throw.
    public Transform submergedVisual;
    // Keep the net origin below the fish while idle. The same rope visual is
    // always present and moves up during a two-hand throw.
    public float restHeight = FishNetGeometry.RestHeight;
    public float raisedHeight = FishNetGeometry.RaisedHeight;
    public float raiseDuration = .16f;
    public float holdDuration = .26f;
    public float fallDuration = .30f;
    public float cooldownDuration = .12f;
    [Tooltip("Active net visual follows the two inward-facing throw hands.")]
    public bool linkVisualToHands = true;
    public float handVisualHeightOffset = -.15f;
    public float handVisualWidth = .72f;
    public bool IsCatchWindow => _phase == Phase.Raising || _phase == Phase.Raised || _phase == Phase.Falling;
    public bool CanThrow => _phase == Phase.Ready;
    public string PhaseName => _phase.ToString();
    public bool HasHandAnchors => _leftHandAnchor != null && _rightHandAnchor != null;

    private enum Phase { Ready, Raising, Raised, Falling, Cooldown }
    private Phase _phase = Phase.Ready;
    private float _phaseTime;
    private Vector3 _basePosition;
    private BoxCollider _catchTrigger;
    private Transform _leftHandAnchor;
    private Transform _rightHandAnchor;
    private Vector3 _visualBaseScale = Vector3.one;
    private bool _handAnchorsSearched;
    private LineRenderer _leftTether;
    private LineRenderer _rightTether;
    private InteractiveWaterSurface _water;

    private void Awake()
    {
      _basePosition = transform.localPosition;
      _catchTrigger = GetComponent<BoxCollider>();
      _water = FindObjectOfType<InteractiveWaterSurface>();
      if (visual != null) _visualBaseScale = visual.localScale;
      CreateHandTethers();
      FindHandAnchors();
      SetHeight(restHeight);
      SetCatchVisual(false);
      UpdateHandLinkedVisual();
    }

    private void Update()
    {
      _phaseTime += Time.unscaledDeltaTime;
      switch (_phase)
      {
        case Phase.Raising:
          SetCatchVisual(true);
          SetHeight(Mathf.Lerp(restHeight, raisedHeight, Mathf.Clamp01(_phaseTime / Mathf.Max(.01f, raiseDuration))));
          if (_phaseTime >= raiseDuration) { _phase = Phase.Raised; _phaseTime = 0; }
          break;
        case Phase.Raised:
          SetCatchVisual(true);
          SetHeight(raisedHeight);
          if (_phaseTime >= holdDuration) { _phase = Phase.Falling; _phaseTime = 0; }
          break;
        case Phase.Falling:
          SetCatchVisual(true);
          SetHeight(Mathf.Lerp(raisedHeight, restHeight, Mathf.Clamp01(_phaseTime / Mathf.Max(.01f, fallDuration))));
          if (_phaseTime >= fallDuration) { _phase = Phase.Cooldown; _phaseTime = 0; }
          break;
        case Phase.Cooldown:
          SetCatchVisual(false);
          SetHeight(restHeight);
          if (_phaseTime >= cooldownDuration) { _phase = Phase.Ready; _phaseTime = 0; }
          break;
        default:
          SetCatchVisual(false);
          SetHeight(restHeight);
          break;
      }
    }

    private void LateUpdate()
    {
      // Read the hands after the avatar's elastic pose is applied.
      UpdateHandLinkedVisual();
      SyncCatchTrigger();
      if (IsCatchWindow) TryCatchOverlappingFish();
    }

    public bool TryThrow()
    {
      if (!CanThrow) return false;
      _phase = Phase.Raising;
      _phaseTime = 0;
      SetCatchVisual(true);
      UpdateHandLinkedVisual();
      SyncCatchTrigger();
      TryCatchOverlappingFish();
      var water = FindObjectOfType<InteractiveWaterSurface>();
      if (water != null)
      {
        // The net displaces water at the instant it leaves the surface:
        // inject a stable impulse into the shallow-water solver and emit
        // droplets with the same upward momentum.
        water.AddRipple(transform.position, .09f);
        water.SplashAt(transform.position, .95f);
      }
      return true;
    }

    public void ResetNet()
    {
      _phase = Phase.Ready;
      _phaseTime = 0;
      SetHeight(restHeight);
      SetCatchVisual(false);
      RestoreCenteredVisual();
      UpdateHandLinkedVisual();
    }

    public void TryCatch(FishTarget fish)
    {
      if (IsCatchWindow && ContainsSwimmingFish(fish)) game?.CatchFish(fish);
    }

    /// <summary>
    /// Kept for compatibility with older callers. The single net remains
    /// visible throughout a catch so it can settle back under the water.
    /// </summary>
    public void HideRaisedVisualForCatch()
    {

    }

    private void TryCatchOverlappingFish()
    {
      if (game == null) return;
      foreach (var fish in FindObjectsOfType<FishTarget>())
        TryCatch(fish);
    }

    /// <summary>
    /// The rope surface rises above the swimming fish, so a vertical BoxCollider
    /// can miss a fish that is visibly inside the net during the short throw.
    /// Test the same rotated horizontal footprint used by the rope visual as a
    /// fallback, while keeping the collider for normal physics callbacks.
    /// </summary>
    public bool ContainsSwimmingFish(FishTarget fish)
    {
      if (fish == null || fish.Caught || visual == null || !visual.gameObject.activeInHierarchy) return false;
      Bounds bounds = fish.VisualBounds;
      float waterY = _water != null ? _water.SampleSurfaceHeight(bounds.center) : 0f;
      // Only swimming fish count, never fish flying above the pool.
      if (bounds.min.y > waterY + .35f || bounds.max.y < waterY - .65f) return false;
      Vector3 delta = bounds.center - visual.position;
      Vector3 right = visual.right;
      Vector3 forward = visual.forward;
      Vector3 e = bounds.extents;
      Vector3 scale = visual.lossyScale;
      float halfX = FishNetGeometry.HalfWidth * Mathf.Abs(scale.x);
      float halfZ = FishNetGeometry.HalfDepth * Mathf.Abs(scale.z);
      float radiusX = Mathf.Abs(right.x) * e.x + Mathf.Abs(right.z) * e.z;
      float radiusZ = Mathf.Abs(forward.x) * e.x + Mathf.Abs(forward.z) * e.z;
      // Separating-axis test in the water plane, including world axes for
      // corner rejection. All distances are world units, even under scaling.
      return Mathf.Abs(Vector3.Dot(delta, right)) <= halfX + radiusX &&
             Mathf.Abs(Vector3.Dot(delta, forward)) <= halfZ + radiusZ &&
             Mathf.Abs(delta.x) <= Mathf.Abs(right.x) * halfX + Mathf.Abs(forward.x) * halfZ + e.x &&
             Mathf.Abs(delta.z) <= Mathf.Abs(right.z) * halfX + Mathf.Abs(forward.z) * halfZ + e.z;
    }

    private void SyncCatchTrigger()
    {
      if (_catchTrigger == null || visual == null) return;
      Vector3 x = transform.InverseTransformVector(visual.TransformVector(Vector3.right * FishNetGeometry.HalfWidth));
      Vector3 z = transform.InverseTransformVector(visual.TransformVector(Vector3.forward * FishNetGeometry.HalfDepth));
      Vector3 center = visual.position;
      center.y = _water != null ? _water.SampleSurfaceHeight(center) : 0f;
      _catchTrigger.center = transform.InverseTransformPoint(center);
      _catchTrigger.size = new Vector3(2f * (Mathf.Abs(x.x) + Mathf.Abs(z.x)), .45f,
        2f * (Mathf.Abs(x.z) + Mathf.Abs(z.z)));
    }

    private void SetHeight(float height)
    {
      var p = _basePosition;
      p.y = height;
      transform.localPosition = p;
    }

    /// <summary>Rebases the net after an older serialized scene is loaded.</summary>
    public void ConfigureLayout(float restingHeight, float throwHeight)
    {
      restHeight = restingHeight;
      raisedHeight = throwHeight;
      _basePosition = transform.localPosition;
      ResetNet();
    }

    public void SetSubmergedVisual(Transform marker)
    {
      submergedVisual = marker;
      SetCatchVisual(_phase != Phase.Ready && _phase != Phase.Cooldown);
    }

    private void SetCatchVisual(bool active)
    {
      if (visual != null) visual.gameObject.SetActive(true);
      // Do not show the legacy duplicate net below the water.  This also
      // clears it when an older serialized scene still contains the object.
      if (submergedVisual != null) submergedVisual.gameObject.SetActive(false);
      if (_catchTrigger != null) _catchTrigger.enabled = active;
      if (_leftTether != null) _leftTether.gameObject.SetActive(true);
      if (_rightTether != null) _rightTether.gameObject.SetActive(true);
    }

    private void FindHandAnchors()
    {
      if (_handAnchorsSearched && _leftHandAnchor != null && _rightHandAnchor != null) return;
      _handAnchorsSearched = true;
      _leftHandAnchor = null;
      _rightHandAnchor = null;
      var avatars = FindObjectsOfType<FishPlayerAvatar>(true);
      for (int i = 0; i < avatars.Length; i++)
      {
        var avatar = avatars[i];
        if (avatar == null || avatar.gestureArm == null) continue;
        Transform hand = null;
        var children = avatar.gestureArm.GetComponentsInChildren<Transform>(true);
        for (int c = 0; c < children.Length; c++)
          if (children[c] != avatar.gestureArm && children[c].name.IndexOf("Hand", System.StringComparison.OrdinalIgnoreCase) >= 0)
          { hand = children[c]; break; }
        if (hand == null) continue;
        if (avatar.transform.position.z < transform.position.z) _leftHandAnchor = hand;
        else _rightHandAnchor = hand;
      }
    }

    private void UpdateHandLinkedVisual()
    {
      if (!linkVisualToHands || visual == null) return;
      if (_leftHandAnchor == null || _rightHandAnchor == null) { FindHandAnchors(); return; }
      Vector3 left = transform.InverseTransformPoint(_leftHandAnchor.position);
      Vector3 right = transform.InverseTransformPoint(_rightHandAnchor.position);
      Vector3 span = right - left;
      float length = new Vector2(span.x, span.z).magnitude;
      if (length < .1f) return;
      Vector3 center = (left + right) * .5f;
      // Horizontal placement follows the two hands, while vertical placement
      // stays relative to the net root. The root is under water at rest and
      // raised during a throw, so the same mesh is used for both states.
      center.y = handVisualHeightOffset;
      visual.localPosition = center;
      visual.localRotation = Quaternion.LookRotation(new Vector3(span.x, 0f, span.z).normalized, Vector3.up);
      float lengthScale = length / Mathf.Max(.1f, FishNetGeometry.HalfDepth * 2f);
      visual.localScale = new Vector3(_visualBaseScale.x * handVisualWidth, _visualBaseScale.y, _visualBaseScale.z * lengthScale);
      UpdateTether(_leftTether, _leftHandAnchor.position, visual.TransformPoint(new Vector3(0f, .05f, -FishNetGeometry.HalfDepth)));
      UpdateTether(_rightTether, _rightHandAnchor.position, visual.TransformPoint(new Vector3(0f, .05f, FishNetGeometry.HalfDepth)));
    }

    private void RestoreCenteredVisual()
    {
      if (visual == null) return;
      visual.localPosition = Vector3.zero;
      visual.localRotation = Quaternion.identity;
      visual.localScale = _visualBaseScale;
    }

    private void CreateHandTethers()
    {
      if (visual == null) return;
      var source = visual.GetComponentInChildren<LineRenderer>(true);
      var material = source != null ? source.sharedMaterial : null;
      _leftTether = CreateTether("NetHandTether_Left", material);
      _rightTether = CreateTether("NetHandTether_Right", material);
    }

    private LineRenderer CreateTether(string name, Material material)
    {
      var go = new GameObject(name);
      go.transform.SetParent(transform, false);
      var line = go.AddComponent<LineRenderer>();
      line.useWorldSpace = true;
      line.positionCount = 2;
      line.startWidth = .035f;
      line.endWidth = .025f;
      line.numCapVertices = 2;
      line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      line.receiveShadows = false;
      line.sharedMaterial = material;
      go.SetActive(false);
      return line;
    }

    private static void UpdateTether(LineRenderer line, Vector3 hand, Vector3 netEdge)
    {
      if (line == null) return;
      line.SetPosition(0, hand);
      line.SetPosition(1, netEdge);
    }

    /// <summary>Used by the editor preview to draw the hand-linked net.</summary>
    public void RefreshHandLinkedVisualForPreview()
    {
      FindHandAnchors();
      UpdateHandLinkedVisual();
    }
  }
}
