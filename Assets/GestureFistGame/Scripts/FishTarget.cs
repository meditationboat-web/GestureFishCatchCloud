using UnityEngine;

namespace GestureFistGame
{
  public sealed class FishTarget : MonoBehaviour
  {
    public FishGameController game;
    public int points = 1;
    public string displayName = "小鱼";
    public float speed = 1.8f;
    public float launchHeight = 2.8f;
    public float launchDuration = .6f;
    public bool Caught { get; private set; }
    private Vector3 _startPosition;
    private float _launchTime;
    private bool _launched;
    private Rigidbody _body;

    private void Awake()
    {
      _body = GetComponent<Rigidbody>();
      _startPosition = transform.position;
    }

    private void FixedUpdate()
    {
      if (Caught && _launched)
      {
        _launchTime += Time.fixedDeltaTime;
        var p = transform.position;
        p.y = Mathf.Lerp(_startPosition.y, launchHeight, Mathf.Clamp01(_launchTime / Mathf.Max(.05f, launchDuration)));
        p.x += Mathf.Sin(_launchTime * 15f) * .012f;
        transform.position = p;
        transform.Rotate(0, 240f * Time.fixedDeltaTime, 180f * Time.fixedDeltaTime, Space.Self);
        if (_launchTime >= launchDuration) Destroy(gameObject);
        return;
      }
      if (Caught) return;
      transform.position += Vector3.back * speed * Time.fixedDeltaTime;
      if (transform.position.z < -2.5f)
      {
        game?.MissedFish();
        Destroy(gameObject);
      }
    }

    public void MarkCaught()
    {
      if (Caught) return;
      Caught = true;
      _launched = true;
      _launchTime = 0;
      _startPosition = transform.position;
      if (_body != null) _body.isKinematic = true;
      foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
      var net = other.GetComponentInParent<FishNetController>();
      if (net != null) net.TryCatch(this);
    }
  }
}
