using UnityEngine;

namespace GestureFistGame
{
  public sealed class FishNetController : MonoBehaviour
  {
    public FishGameController game;
    public Transform visual;
    public float restHeight = .65f;
    public float raisedHeight = 1.75f;
    public float raiseDuration = .16f;
    public float holdDuration = .26f;
    public float fallDuration = .30f;
    public float cooldownDuration = .12f;
    public bool IsCatchWindow => _phase == Phase.Raised || _phase == Phase.Falling;
    public bool CanThrow => _phase == Phase.Ready;
    public string PhaseName => _phase.ToString();

    private enum Phase { Ready, Raising, Raised, Falling, Cooldown }
    private Phase _phase = Phase.Ready;
    private float _phaseTime;
    private Vector3 _basePosition;

    private void Awake()
    {
      _basePosition = transform.localPosition;
      SetHeight(restHeight);
    }

    private void Update()
    {
      _phaseTime += Time.unscaledDeltaTime;
      switch (_phase)
      {
        case Phase.Raising:
          SetHeight(Mathf.Lerp(restHeight, raisedHeight, Mathf.Clamp01(_phaseTime / Mathf.Max(.01f, raiseDuration))));
          if (_phaseTime >= raiseDuration) { _phase = Phase.Raised; _phaseTime = 0; }
          break;
        case Phase.Raised:
          SetHeight(raisedHeight);
          if (_phaseTime >= holdDuration) { _phase = Phase.Falling; _phaseTime = 0; }
          break;
        case Phase.Falling:
          SetHeight(Mathf.Lerp(raisedHeight, restHeight, Mathf.Clamp01(_phaseTime / Mathf.Max(.01f, fallDuration))));
          if (_phaseTime >= fallDuration) { _phase = Phase.Cooldown; _phaseTime = 0; }
          break;
        case Phase.Cooldown:
          SetHeight(restHeight);
          if (_phaseTime >= cooldownDuration) { _phase = Phase.Ready; _phaseTime = 0; }
          break;
        default:
          SetHeight(restHeight);
          break;
      }
    }

    public bool TryThrow()
    {
      if (!CanThrow) return false;
      _phase = Phase.Raising;
      _phaseTime = 0;
      return true;
    }

    public void ResetNet()
    {
      _phase = Phase.Ready;
      _phaseTime = 0;
      SetHeight(restHeight);
    }

    public void TryCatch(FishTarget fish)
    {
      if (IsCatchWindow) game?.CatchFish(fish);
    }

    private void SetHeight(float height)
    {
      var p = _basePosition;
      p.y = height;
      transform.localPosition = p;
    }
  }
}
