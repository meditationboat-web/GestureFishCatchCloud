using UnityEngine;

namespace GestureFistGame
{
  /// <summary>
  /// Pose-driven spring layer for the stylized fisherman avatars. It preserves
  /// the authored hierarchy and adds a soft waist, head and shoulder rebound.
  /// </summary>
  [DefaultExecutionOrder(50)]
  public sealed class FishAvatarRagdoll : MonoBehaviour
  {
    public bool enabledForThrow = true;
    [Range(0f, 2f)] public float kickStrength = 1f;
    [Range(40f, 180f)] public float stiffness = 100f;
    [Range(2f, 20f)] public float damping = 8f;
    [Range(2f, 20f)] public float maximumAngle = 12f;
    [Range(0f, .3f)] public float squashAmount = .18f;
    public float MotionAmount => Mathf.Abs(_angle) + Mathf.Abs(_headAngle);

    private FishPlayerAvatar _avatar;
    private FishGameController _game;
    private Transform _body, _head, _hair;
    private Quaternion _headRestRotation, _hairRestRotation;
    private Vector3 _bodyRestScale;
    private float _angle, _angleVelocity, _headAngle, _headVelocity;
    private float _squash, _squashVelocity;

    private void Awake()
    {
      _avatar = GetComponent<FishPlayerAvatar>();
      _body = _avatar != null ? _avatar.bodyVisual : transform.Find("AvatarBody");
      if (_body == null) _body = transform.Find("AvatarBody");
      _head = transform.Find("AvatarHead");
      _hair = _head != null ? _head.Find("AvatarHair") : null;
      if (_hair == null) _hair = transform.Find("AvatarHair");
      _headRestRotation = _head != null ? _head.localRotation : Quaternion.identity;
      _hairRestRotation = _hair != null ? _hair.localRotation : Quaternion.identity;
      _bodyRestScale = _body != null ? _body.localScale : Vector3.one;
    }

    private void Start()
    {
      _game = FindObjectOfType<FishGameController>();
      if (_game != null) _game.FishCaught += OnFishCaught;
    }

    public void Kick(float direction)
    {
      if (!enabledForThrow) return;
      _angleVelocity = Mathf.Clamp(_angleVelocity + Mathf.Clamp(direction, -1f, 1f) * 110f * kickStrength, -160f, 160f);
      _squashVelocity = Mathf.Clamp(_squashVelocity + 2.8f * kickStrength, -4f, 4f);
    }

    private void OnFishCaught(FishTarget fish)
    {
      if (_avatar != null) Kick(_avatar.netDirection * .35f);
    }

    private void LateUpdate()
    {
      if (_avatar == null || !enabledForThrow) return;
      float remaining = Mathf.Min(Time.unscaledDeltaTime, .1f);
      while (remaining > 0f)
      {
        float dt = Mathf.Min(remaining, 1f / 120f);
        Integrate(ref _angle, ref _angleVelocity, 0f, stiffness, damping, dt);
        Integrate(ref _headAngle, ref _headVelocity, -_angle * .65f, stiffness * .65f, damping * .7f, dt);
        Integrate(ref _squash, ref _squashVelocity, 0f, stiffness * 1.25f, damping, dt);
        remaining -= dt;
      }
      float angle = Mathf.Clamp(_angle, -maximumAngle, maximumAngle);
      float squash = Mathf.Clamp(_squash * squashAmount, -.07f, .07f);
      transform.localRotation = transform.localRotation * Quaternion.Euler(0f, 0f, angle * .42f);
      if (_body != null)
      {
        _body.localRotation = _body.localRotation * Quaternion.Euler(-angle * .28f, 0f, angle * .18f);
        _body.localScale = new Vector3(_bodyRestScale.x * (1f + squash),
          _bodyRestScale.y * (1f - squash * 1.35f), _bodyRestScale.z * (1f + squash));
      }
      if (_head != null) _head.localRotation = _headRestRotation * Quaternion.Euler(_headAngle, 0f, -_headAngle * .25f);
      if (_hair != null) _hair.localRotation = _hairRestRotation * Quaternion.Euler(_headAngle * 1.25f, 0f, -_headAngle * .4f);
      if (_avatar.gestureArm != null)
        _avatar.gestureArm.localRotation *= Quaternion.Euler(angle * .3f, 0f, 0f);
      if (_avatar.supportArm != null)
        _avatar.supportArm.localRotation *= Quaternion.Euler(-angle * .2f, 0f, 0f);
    }

    public void ResetMotion()
    {
      _angle = _angleVelocity = _headAngle = _headVelocity = _squash = _squashVelocity = 0f;
      if (_body != null) _body.localScale = _bodyRestScale;
      if (_head != null) _head.localRotation = _headRestRotation;
      if (_hair != null) _hair.localRotation = _hairRestRotation;
    }

    private void OnDisable() => ResetMotion();
    private void OnDestroy()
    {
      if (_game != null) _game.FishCaught -= OnFishCaught;
    }

    private static void Integrate(ref float value, ref float velocity, float target, float stiffness, float damping, float dt)
    {
      velocity += (stiffness * (target - value) - damping * velocity) * dt;
      value += velocity * dt;
    }
  }
}
