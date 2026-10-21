using UnityEngine;

namespace GestureFistGame
{
  /// <summary>Simple readable avatar feedback for the hand assigned to a player.</summary>
  public sealed class FishPlayerAvatar : MonoBehaviour
  {
    public FishGestureInput.HandSide handSide;
    public Transform gestureArm;
    public Transform supportArm;
    public Transform bodyVisual;
    public FishAvatarRagdoll ragdoll;
    public float netDirection = 1f;
    public float restAngle = 18f;
    public float throwAngle = 78f;
    public float waveDuration = .42f;
    [Range(0f, 1f)] public float supportArmWeight = .72f;

    private FishGestureInput _input;
    private float _waveUntil = -1f;
    private Quaternion _restAvatarRotation;
    private Quaternion _restGestureRotation;
    private Quaternion _restSupportRotation;
    private Quaternion _restBodyRotation;
    private Vector3 _restBodyPosition;

    private void Awake()
    {
      ragdoll = GetComponent<FishAvatarRagdoll>();
      if (ragdoll == null) ragdoll = gameObject.AddComponent<FishAvatarRagdoll>();
    }

    private void Start()
    {
      _input = FindObjectOfType<FishGestureInput>();
      if (_input != null) _input.HandWaveDetected += OnHandWave;
      _restAvatarRotation = transform.localRotation;
      _restGestureRotation = gestureArm != null ? gestureArm.localRotation : Quaternion.identity;
      _restSupportRotation = supportArm != null ? supportArm.localRotation : Quaternion.identity;
      _restBodyRotation = bodyVisual != null ? bodyVisual.localRotation : Quaternion.identity;
      _restBodyPosition = bodyVisual != null ? bodyVisual.localPosition : Vector3.zero;
      ApplyArmPose(restAngle);
    }

    private void OnDestroy()
    {
      if (_input != null) _input.HandWaveDetected -= OnHandWave;
    }

    private void Update()
    {
      float amount = 0f;
      if (_waveUntil >= 0f)
      {
        float elapsed = waveDuration - (_waveUntil - Time.unscaledTime);
        float normalized = Mathf.Clamp01(elapsed / Mathf.Max(.05f, waveDuration));
        amount = Mathf.Sin(normalized * Mathf.PI);
        if (Time.unscaledTime >= _waveUntil) _waveUntil = -1f;
      }
      ApplyArmPose(Mathf.Lerp(restAngle, throwAngle, amount));
      // A short anticipation, release and recovery arc makes the gesture read
      // as a two-handed cast instead of a single arm toggle.
      float anticipation = Mathf.Sin(Mathf.Clamp01(amount) * Mathf.PI);
      float release = Mathf.Sin(Mathf.Clamp01(amount) * Mathf.PI * .82f);
      if (supportArm != null)
        supportArm.localRotation = _restSupportRotation * Quaternion.Euler(-netDirection * (restAngle + (throwAngle - restAngle) * supportArmWeight * release), 0f, 0f);
      transform.localRotation = _restAvatarRotation * Quaternion.Euler(0f, 0f, netDirection * anticipation * 7.5f);
      if (bodyVisual != null)
      {
        bodyVisual.localRotation = _restBodyRotation * Quaternion.Euler(-netDirection * anticipation * 9f, 0f, 0f);
        bodyVisual.localPosition = _restBodyPosition + new Vector3(0f, anticipation * .035f, 0f);
      }
    }

    private void OnHandWave(FishGestureInput.HandSide side)
    {
      if (side == handSide)
      {
        _waveUntil = Time.unscaledTime + waveDuration;
        ragdoll?.Kick(netDirection);
      }
    }

    private void ApplyArmPose(float angle)
    {
      if (gestureArm == null) return;
      float release = _waveUntil < 0f ? 0f : Mathf.Sin(Mathf.Clamp01((waveDuration - (_waveUntil - Time.unscaledTime)) / Mathf.Max(.05f, waveDuration)) * Mathf.PI * .82f);
      gestureArm.localRotation = _restGestureRotation * Quaternion.Euler(-netDirection * (angle + release * 8f), 0f, 0f);
    }
  }
}
