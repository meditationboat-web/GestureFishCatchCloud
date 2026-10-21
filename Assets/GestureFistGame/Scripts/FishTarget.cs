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
    public float launchDuration = .42f;
    [Tooltip("游动时模型高度浸入水面的比例；0.5 为半入水。"), Range(0f, 1f)]
    public float submergedFraction = .5f;
    public bool Caught { get; private set; }
    private Vector3 _startPosition;
    private float _launchTime;
    private bool _launched;
    private Rigidbody _body;
    private InteractiveWaterSurface _water;
    private float _visualCenterOffsetY;
    private float _visualHeight;
    private Renderer[] _renderers;

    public Bounds VisualBounds
    {
      get
      {
        if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>();
        bool found = false;
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        foreach (var renderer in _renderers)
        {
          if (renderer == null || !renderer.enabled || renderer is ParticleSystemRenderer || renderer is LineRenderer) continue;
          if (!found) { bounds = renderer.bounds; found = true; }
          else bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
      }
    }

    private void OnEnable()
    {
      if (_water == null) _water = FindObjectOfType<InteractiveWaterSurface>();
      // FBX pivots need not be at the body center. Use the rendered bounds
      // after spawn rotation/scale instead of lowering every pivot by a constant.
      var renderers = GetComponentsInChildren<Renderer>();
      bool found = false;
      var bounds = new Bounds();
      foreach (var renderer in renderers)
      {
        if (!renderer.enabled || renderer is ParticleSystemRenderer || renderer is LineRenderer) continue;
        if (!found) { bounds = renderer.bounds; found = true; }
        else bounds.Encapsulate(renderer.bounds);
      }
      _visualCenterOffsetY = found ? bounds.center.y - transform.position.y : 0f;
      _visualHeight = found ? bounds.size.y : 0f;
      FollowWater(true);
    }

    private void FollowWater(bool snap)
    {
      if (Caught || _water == null) return;
      var position = transform.position;
      float target = _water.SampleSurfaceHeight(position) - _visualCenterOffsetY
        + _visualHeight * (.5f - submergedFraction);
      position.y = snap ? target : Mathf.Lerp(position.y, target, 1f - Mathf.Exp(-10f * Time.fixedDeltaTime));
      transform.position = position;
    }

    private void Awake()
    {
      _body = GetComponent<Rigidbody>();
      _startPosition = transform.position;
      _water = FindObjectOfType<InteractiveWaterSurface>();
    }

    private void FixedUpdate()
    {
      if (Caught && _launched)
      {
        _launchTime += Time.fixedDeltaTime;
        var p = transform.position;
        float normalized = Mathf.Clamp01(_launchTime / Mathf.Max(.05f, launchDuration));
        // Start above the raised net so the rope mesh never hides the fish.
        p.y = Mathf.Lerp(_startPosition.y, launchHeight, normalized);
        p.x += Mathf.Sin(_launchTime * 15f) * .012f;
        transform.position = p;
        transform.Rotate(0, 240f * Time.fixedDeltaTime, 180f * Time.fixedDeltaTime, Space.Self);
        if (_launchTime >= launchDuration) Destroy(gameObject);
        return;
      }
      if (Caught) return;
      // The reference game shows the fish crossing the net from right to left.
      // Keep the fish on the water lane's z range and remove it after the left bank.
      transform.position += Vector3.left * speed * Time.fixedDeltaTime;
      FollowWater(false);
      if (_water != null && Time.frameCount % 6 == 0) _water.AddRipple(transform.position, .012f);
      if (transform.position.x < -4.7f)
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
      var net = FindObjectOfType<FishNetController>();
      if (net != null)
      {
        float netTop = net.visual != null ? net.visual.position.y + .05f : net.transform.position.y;
        _startPosition.y = Mathf.Max(_startPosition.y, netTop + .30f);
        launchHeight = Mathf.Max(launchHeight, _startPosition.y + 1.45f);
        transform.position = _startPosition;
      }
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
