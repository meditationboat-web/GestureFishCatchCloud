using System;
using UnityEngine;

namespace GestureFistGame
{
  /// <summary>
  /// Render-texture ripple layer based on the same interaction pattern used by
  /// the open-source Interactive Stylized Water reference: impacts are written
  /// into a small map, then the URP water shader turns the map into moving
  /// normals, foam and refraction. The CPU map is deliberately small so this
  /// remains safe for mobile builds.
  /// </summary>
  [DisallowMultipleComponent]
  public sealed class WaterRippleRenderTexture : MonoBehaviour
  {
    [Range(32, 256)] public int resolution = 128;
    [Range(0.2f, 3f)] public float lifetime = 1.35f;
    [Range(0.1f, 8f)] public float waveFrequency = 5.4f;
    [Range(0.1f, 8f)] public float waveSpeed = 2.6f;
    [Range(0.1f, 4f)] public float damping = 1.25f;
    [Range(0.01f, 0.25f)] public float ringWidth = 0.075f;
    [Range(1, 12)] public int maxImpacts = 6;

    private struct Impact
    {
      public Vector2 uv;
      public float age;
      public float strength;
    }

    private Texture2D _map;
    private RenderTexture _renderTexture;
    private Color32[] _pixels;
    private Impact[] _impacts;
    private int _impactCount;
    private float _width = 8.4f;
    private float _depth = 8f;
    private Transform _surface;
    private float _time;

    public Texture RippleTexture => _renderTexture;

    public void Configure(Transform surface, float width, float depth, int requestedResolution,
      float requestedLifetime, float requestedWaveFrequency, float requestedWaveSpeed)
    {
      _surface = surface;
      _width = Mathf.Max(.1f, width);
      _depth = Mathf.Max(.1f, depth);
      resolution = Mathf.Clamp(requestedResolution, 32, 256);
      lifetime = Mathf.Clamp(requestedLifetime, .2f, 3f);
      waveFrequency = Mathf.Clamp(requestedWaveFrequency, .1f, 8f);
      waveSpeed = Mathf.Clamp(requestedWaveSpeed, .1f, 8f);
      EnsureResources();
    }

    public void AddRipple(Vector3 worldPosition, float strength)
    {
      if (_surface == null) _surface = transform;
      Vector3 local = _surface.InverseTransformPoint(worldPosition);
      float u = local.x / _width + .5f;
      float v = local.z / _depth + .5f;
      if (u <= 0f || u >= 1f || v <= 0f || v >= 1f) return;
      EnsureResources();
      strength = Mathf.Clamp01(Mathf.Abs(strength) * 3.2f);
      if (strength <= .001f) return;
      if (_impactCount < _impacts.Length)
      {
        _impacts[_impactCount++] = new Impact { uv = new Vector2(u, v), age = 0f, strength = strength };
      }
      else
      {
        int replace = 0;
        float oldest = _impacts[0].age;
        for (int i = 1; i < _impacts.Length; i++)
        {
          if (_impacts[i].age > oldest) { oldest = _impacts[i].age; replace = i; }
        }
        _impacts[replace] = new Impact { uv = new Vector2(u, v), age = 0f, strength = strength };
      }
    }

    private void Awake()
    {
      _surface = transform;
      EnsureResources();
    }

    private void Update()
    {
      if (_map == null || _renderTexture == null || _pixels == null) return;
      // Feed the same global bindings used by the open-source
      // Interactive Stylized Water graph. This lets the imported E material
      // consume the ripple map without a second interaction camera.
      Shader.SetGlobalTexture("_GlobalEffectRT", _renderTexture);
      Shader.SetGlobalFloat("_ProjectionSize", Mathf.Max(_width, _depth) * .5f);
      Shader.SetGlobalVector("_Position", transform.position);
      float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
      _time += dt;
      bool active = false;
      for (int i = _impactCount - 1; i >= 0; i--)
      {
        var impact = _impacts[i];
        impact.age += dt;
        _impacts[i] = impact;
        if (impact.age > lifetime)
        {
          _impacts[i] = _impacts[_impactCount - 1];
          _impactCount--;
        }
        else active = true;
      }
      if (!active && _time < .15f) return;
      RenderMap();
    }

    private void RenderMap()
    {
      Array.Clear(_pixels, 0, _pixels.Length);
      float inv = 1f / Mathf.Max(1, resolution - 1);
      for (int y = 0; y < resolution; y++)
      {
        float v = y * inv;
        for (int x = 0; x < resolution; x++)
        {
          float u = x * inv;
          float value = 0f;
          for (int j = 0; j < _impactCount; j++)
          {
            Impact impact = _impacts[j];
            float distance = Vector2.Distance(new Vector2(u, v), impact.uv);
            float radius = impact.age * waveSpeed * .13f;
            float envelope = Mathf.Exp(-impact.age * damping);
            float ring = Mathf.Exp(-Mathf.Abs(distance - radius) / Mathf.Max(.001f, ringWidth));
            value = Mathf.Max(value, ring * impact.strength * envelope);
          }
          _pixels[y * resolution + x] = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f), 0, 0, 255);
        }
      }
      _map.SetPixels32(_pixels);
      _map.Apply(false, false);
      Graphics.Blit(_map, _renderTexture);
    }

    private void EnsureResources()
    {
      if (_map != null && _map.width == resolution && _renderTexture != null && _renderTexture.IsCreated()) return;
      ReleaseResources();
      _map = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true)
      {
        name = "InteractiveStylizedWater_RippleMap",
        wrapMode = TextureWrapMode.Clamp,
        filterMode = FilterMode.Bilinear
      };
      _renderTexture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.R8)
      {
        name = "InteractiveStylizedWater_RippleRenderTexture",
        wrapMode = TextureWrapMode.Clamp,
        filterMode = FilterMode.Bilinear,
        useMipMap = false,
        autoGenerateMips = false
      };
      _renderTexture.Create();
      _pixels = new Color32[resolution * resolution];
      _impacts = new Impact[Mathf.Clamp(maxImpacts, 1, 12)];
      Graphics.Blit(Texture2D.blackTexture, _renderTexture);
      Shader.SetGlobalTexture("_GlobalEffectRT", _renderTexture);
      Shader.SetGlobalFloat("_ProjectionSize", Mathf.Max(_width, _depth) * .5f);
      Shader.SetGlobalVector("_Position", transform.position);
    }

    private void ReleaseResources()
    {
      if (_renderTexture != null)
      {
        if (_renderTexture.IsCreated()) _renderTexture.Release();
        Destroy(_renderTexture);
      }
      if (_map != null) Destroy(_map);
      _renderTexture = null; _map = null; _pixels = null; _impacts = null; _impactCount = 0;
    }

    private void OnDestroy() => ReleaseResources();
  }
}
