using UnityEngine;

namespace GestureFistGame
{
  /// <summary>
  /// Mobile-friendly 2D shallow-water simulation. Impacts inject height,
  /// horizontal flow and foam; the grid then advances by pressure gradients
  /// and mass conservation. No artificial ring meshes are drawn.
  /// </summary>
  public sealed class InteractiveWaterSurface : MonoBehaviour
  {
    public float width = 8.4f;
    public float depth = 8f;
    public float baseHeight = .16f;
    public int columns = 56;
    public int rows = 40;
    public float waveHeight = .085f;
    public float waveSpeed = 1.7f;
    [Range(0.1f, 1f)] public float waterDamping = .94f;
    public float wavePropagation = 5.5f;
    [Range(0.1f, 1f)] public float flowDamping = .90f;
    public float gravity = 2.2f;
    public float maxDisplacement = .22f;
    public float foamDissipation = 1.7f;
    public int splashParticleCount = 42;
    public bool enableRenderTextureRipples = true;
    [Range(32, 256)] public int rippleMapResolution = 128;
    [Range(.2f, 3f)] public float rippleLifetime = 1.35f;
    public Shader waterShader;
    [Tooltip("Optional material from the imported Interactive Stylized Water package.")]
    public Material waterMaterialOverride;
    public bool circular = false;

    private Mesh _mesh;
    private Vector3[] _vertices;
    private float[] _height, _displayHeight, _nextHeight, _flowX, _flowZ, _foam, _nextFoam;
    private Color[] _interactionPixels;
    private Texture2D _interactionTexture;
    private float _time;
    private Transform _surface;
    private Material _material;
    private float _cellX, _cellZ;
    private ParticleSystem _splash;
    private Material _splashMaterial;
    private WaterRippleRenderTexture _rippleLayer;

    private void Awake() => BuildSurface();

    /// <summary>World-space height of the simulated surface for swimming objects.</summary>
    public float SampleSurfaceHeight(Vector3 worldPosition)
    {
      if (_surface == null || _vertices == null) return transform.position.y + baseHeight;
      var local = _surface.InverseTransformPoint(worldPosition);
      float gx = Mathf.Clamp01(local.x / width + .5f) * columns;
      float gz = Mathf.Clamp01(local.z / depth + .5f) * rows;
      int x = Mathf.Min(Mathf.FloorToInt(gx), columns - 1);
      int z = Mathf.Min(Mathf.FloorToInt(gz), rows - 1);
      int index = z * (columns + 1) + x;
      float a = Mathf.Lerp(_vertices[index].y, _vertices[index + 1].y, gx - x);
      float b = Mathf.Lerp(_vertices[index + columns + 1].y, _vertices[index + columns + 2].y, gx - x);
      local.y = Mathf.Lerp(a, b, gz - z);
      return _surface.TransformPoint(local).y;
    }

    private void Update()
    {
      if (_mesh == null || _vertices == null) return;
      if (_rippleLayer == null && enableRenderTextureRipples) EnsureRippleLayer();
      float frameDt = Mathf.Min(Time.unscaledDeltaTime, .033f);
      _time += frameDt;
      int substeps = 4;
      float dt = frameDt / substeps;
      for (int step = 0; step < substeps; step++) Simulate(dt);
      UpdateMesh();
      UploadInteractionTexture();
      if (_rippleLayer != null) _rippleLayer.enabled = enableRenderTextureRipples;
    }

    /// <summary>Inject a vertical impact and radial horizontal flow.</summary>
    public void AddRipple(Vector3 worldPosition, float strength = .06f)
    {
      if (_surface == null || _height == null) return;
      if (_rippleLayer != null && enableRenderTextureRipples) _rippleLayer.AddRipple(worldPosition, strength);
      Vector3 local = _surface.InverseTransformPoint(worldPosition);
      if (!IsInsideWater(local)) return;
      local = ClampToWater(local);
      float gx = (local.x / width + .5f) * columns;
      float gz = (local.z / depth + .5f) * rows;
      int centerX = Mathf.RoundToInt(gx), centerZ = Mathf.RoundToInt(gz);
      int radius = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(strength) * 22f), 1, 8);
      float sign = strength >= 0f ? 1f : -1f;
      float amount = Mathf.Clamp(Mathf.Abs(strength), .01f, .45f);
      for (int z = centerZ - radius; z <= centerZ + radius; z++)
      {
        if (z < 1 || z >= rows) continue;
        for (int x = centerX - radius; x <= centerX + radius; x++)
        {
          if (x < 1 || x >= columns) continue;
          float nx = (x - centerX) / (float)Mathf.Max(1, radius);
          float nz = (z - centerZ) / (float)Mathf.Max(1, radius);
          float d = Mathf.Sqrt(nx * nx + nz * nz);
          if (d > 1f) continue;
          float falloff = 1f - d * d;
          int i = z * (columns + 1) + x;
          _height[i] = Mathf.Clamp(_height[i] + sign * amount * (.55f + .45f * falloff), -maxDisplacement, maxDisplacement);
          float inv = 1f / Mathf.Max(.001f, d);
          _flowX[i] += nx * inv * amount * .9f;
          _flowZ[i] += nz * inv * amount * .9f;
          _foam[i] = Mathf.Clamp01(_foam[i] + amount * (.75f + falloff * .9f));
        }
      }
    }

    /// <summary>Emits a short-lived burst of water droplets at a net impact.</summary>
    public void SplashAt(Vector3 worldPosition, float strength = 1f)
    {
      if (_splash == null) CreateSplashSystem();
      if (_splash == null) return;
      Vector3 local = _surface.InverseTransformPoint(worldPosition);
      if (!IsInsideWater(local)) return;
      int count = Mathf.Clamp(Mathf.RoundToInt(splashParticleCount * Mathf.Clamp01(strength)), 12, 90);
      var emit = new ParticleSystem.EmitParams();
      for (int i = 0; i < count; i++)
      {
        float angle = (i / (float)count) * Mathf.PI * 2f + Random.value * .35f;
        float radial = Random.Range(.12f, .42f) * Mathf.Lerp(.65f, 1.35f, strength);
        emit.position = new Vector3(local.x + Mathf.Cos(angle) * radial, baseHeight + .015f, local.z + Mathf.Sin(angle) * radial);
        emit.velocity = new Vector3(Mathf.Cos(angle) * Random.Range(.35f, 1.2f), Random.Range(1.0f, 2.3f) * Mathf.Lerp(.7f, 1.25f, strength), Mathf.Sin(angle) * Random.Range(.35f, 1.2f));
        emit.startLifetime = Random.Range(.42f, .82f);
        emit.startSize = Random.Range(.018f, .045f) * Mathf.Lerp(.8f, 1.25f, strength);
        emit.startColor = new Color(.72f, .94f, 1f, Random.Range(.62f, .95f));
        _splash.Emit(emit, 1);
      }
    }

    private void Simulate(float dt)
    {
      int stride = columns + 1;
      float invTwoX = .5f / Mathf.Max(.01f, _cellX);
      float invTwoZ = .5f / Mathf.Max(.01f, _cellZ);
      float flowDecay = Mathf.Pow(flowDamping, dt * 60f);
      float heightDecay = Mathf.Pow(waterDamping, dt * 60f);
      for (int z = 1; z < rows; z++)
      {
        for (int x = 1; x < columns; x++)
        {
          int i = z * stride + x;
          float dhdx = (_height[i + 1] - _height[i - 1]) * invTwoX;
          float dhdz = (_height[i + stride] - _height[i - stride]) * invTwoZ;
          _flowX[i] = Mathf.Clamp((_flowX[i] - gravity * dhdx * dt) * flowDecay, -.9f, .9f);
          _flowZ[i] = Mathf.Clamp((_flowZ[i] - gravity * dhdz * dt) * flowDecay, -.9f, .9f);
          float divergence = (_flowX[i + 1] - _flowX[i - 1]) * invTwoX + (_flowZ[i + stride] - _flowZ[i - stride]) * invTwoZ;
          _nextHeight[i] = Mathf.Clamp((_height[i] - divergence * dt * wavePropagation) * heightDecay, -maxDisplacement, maxDisplacement);
          float speed = Mathf.Sqrt(_flowX[i] * _flowX[i] + _flowZ[i] * _flowZ[i]);
          _nextFoam[i] = Mathf.Clamp01(Mathf.Max(0f, _foam[i] - dt * foamDissipation) + Mathf.Clamp01(speed * .06f + Mathf.Abs(divergence) * .02f) * dt);
        }
      }
      for (int z = 0; z <= rows; z++)
      {
        int left = z * stride, right = left + columns;
        _flowX[left] = _flowX[right] = 0f;
      }
      for (int x = 0; x <= columns; x++) _flowZ[x] = _flowZ[rows * stride + x] = 0f;
      for (int i = 0; i < _height.Length; i++)
      {
        int z = i / stride, x = i % stride;
        if (z == 0 || z == rows || x == 0 || x == columns) { _nextHeight[i] = 0f; _nextFoam[i] = 0f; }
        _height[i] = _nextHeight[i]; _foam[i] = _nextFoam[i];
      }
    }

    private void UpdateMesh()
    {
      int stride = columns + 1;
      for (int z = 0; z <= rows; z++) for (int x = 0; x <= columns; x++)
      {
        int i = z * stride + x;
        float px = _vertices[i].x, pz = _vertices[i].z;
        float ambient = Mathf.Sin(px * 1.7f + _time * waveSpeed) * waveHeight * .18f + Mathf.Cos(pz * 1.45f - _time * waveSpeed * .82f) * waveHeight * .12f;
        // Low-pass the rendered surface so the solver's grid-scale energy does
        // not become visible as frame-to-frame jitter.
        _displayHeight[i] = Mathf.Lerp(_displayHeight[i], _height[i], .22f);
        _vertices[i].y = baseHeight + _displayHeight[i] + ambient;
      }
      _mesh.vertices = _vertices; _mesh.RecalculateNormals(); _mesh.RecalculateBounds();
    }

    private void UploadInteractionTexture()
    {
      if (_interactionTexture == null) return;
      for (int i = 0; i < _interactionPixels.Length; i++)
      {
        float signedHeight = Mathf.Clamp(_displayHeight[i] / Mathf.Max(.001f, maxDisplacement), -1f, 1f) * .5f + .5f;
        float speed = Mathf.Clamp01(Mathf.Sqrt(_flowX[i] * _flowX[i] + _flowZ[i] * _flowZ[i]) * .22f);
        _interactionPixels[i] = new Color(signedHeight, speed, _foam[i], 1f);
      }
      _interactionTexture.SetPixels(_interactionPixels); _interactionTexture.Apply(false, false);
    }

    private void BuildSurface()
    {
      var originalRenderer = GetComponent<Renderer>();
      if (originalRenderer != null) originalRenderer.enabled = false;
      _cellX = width / Mathf.Max(1, columns); _cellZ = depth / Mathf.Max(1, rows);
      var go = new GameObject("ShallowWaterSimulationSurface", typeof(MeshFilter), typeof(MeshRenderer));
      go.transform.SetParent(transform, false); _surface = go.transform;
      var parentScale = transform.lossyScale;
      _surface.localScale = new Vector3(Mathf.Abs(parentScale.x) > .0001f ? 1f / parentScale.x : 1f, Mathf.Abs(parentScale.y) > .0001f ? 1f / parentScale.y : 1f, Mathf.Abs(parentScale.z) > .0001f ? 1f / parentScale.z : 1f);
      int count = (columns + 1) * (rows + 1);
      _mesh = new Mesh { name = "ShallowWaterSimulationMesh" };
      _vertices = new Vector3[count]; _height = new float[count]; _displayHeight = new float[count]; _nextHeight = new float[count]; _flowX = new float[count]; _flowZ = new float[count]; _foam = new float[count]; _nextFoam = new float[count]; _interactionPixels = new Color[count];
      _interactionTexture = new Texture2D(columns + 1, rows + 1, TextureFormat.RGBA32, false, true) { name = "ShallowWaterState", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
      for (int z = 0; z <= rows; z++) for (int x = 0; x <= columns; x++) _vertices[z * (columns + 1) + x] = new Vector3(Mathf.Lerp(-width * .5f, width * .5f, x / (float)columns), baseHeight, Mathf.Lerp(-depth * .5f, depth * .5f, z / (float)rows));
      int[] triangles = new int[columns * rows * 6]; int t = 0;
      for (int z = 0; z < rows; z++) for (int x = 0; x < columns; x++) { int a = z * (columns + 1) + x, b = a + 1, c = a + columns + 1, d = c + 1; triangles[t++] = a; triangles[t++] = c; triangles[t++] = b; triangles[t++] = b; triangles[t++] = c; triangles[t++] = d; }
      _mesh.vertices = _vertices; _mesh.triangles = triangles; _mesh.RecalculateNormals(); _mesh.RecalculateBounds(); go.GetComponent<MeshFilter>().sharedMesh = _mesh;
      var shader = waterShader ?? Shader.Find("GestureFistGame/InteractiveWater") ?? Shader.Find("Universal Render Pipeline/Lit");
      _material = waterMaterialOverride != null ? new Material(waterMaterialOverride) : new Material(shader);
      _material.name = waterMaterialOverride != null ? "Interactive Stylized Water (E) Runtime" : "Shallow Water Material";
      if (waterMaterialOverride == null && shader != null && shader.name == "GestureFistGame/InteractiveWater")
      {
        _material.SetColor("_ShallowColor", new Color(.10f, .78f, .90f, .90f)); _material.SetColor("_DeepColor", new Color(.008f, .16f, .30f, .94f)); _material.SetColor("_FoamColor", new Color(.90f, .99f, 1f, .98f)); _material.SetFloat("_Alpha", .92f); _material.SetFloat("_WaterWidth", width); _material.SetFloat("_WaterDepth", depth); _material.SetFloat("_Circular", circular ? 1f : 0f); _material.SetFloat("_WaveHeight", waveHeight); _material.SetFloat("_Gloss", .97f); _material.SetFloat("_SurfaceDetail", 1.25f); _material.SetFloat("_CausticStrength", .8f); _material.SetFloat("_RippleStrength", 1.15f); _material.SetTexture("_InteractionMap", _interactionTexture);
      }
      else if (waterMaterialOverride != null)
      {
        // Public controls exposed by E's Shader Graph. The ripple map itself
        // is bound globally by WaterRippleRenderTexture.
        SetMaterialFloat("_WaterDepth", depth);
        SetMaterialFloat("_WaveAmplitude", Mathf.Clamp(waveHeight * 5.5f, .05f, .45f));
        SetMaterialFloat("_WaveFrequency", 1.15f);
        SetMaterialFloat("_WaveSpeed", .72f);
        SetMaterialFloat("_MovementScale", .58f);
        SetMaterialFloat("_MovementSpeed", .14f);
        SetMaterialFloat("_NormalStrength", .18f);
        SetMaterialFloat("_FoamAmount", 14f);
        SetMaterialFloat("_InteractionRippleCutoff", .16f);
        SetMaterialFloat("_RippleCutoff", .34f);
        SetMaterialFloat("_RefractionStrength", .035f);
      }
      EnsureRippleLayer();
      go.GetComponent<MeshRenderer>().sharedMaterial = _material; UploadInteractionTexture(); CreateSplashSystem();
    }

    private void EnsureRippleLayer()
    {
      if (!enableRenderTextureRipples || _surface == null) return;
      _rippleLayer = GetComponent<WaterRippleRenderTexture>();
      if (_rippleLayer == null) _rippleLayer = gameObject.AddComponent<WaterRippleRenderTexture>();
      _rippleLayer.Configure(_surface, width, depth, rippleMapResolution, rippleLifetime, 2.6f, 2.6f);
      if (_material != null && _material.HasProperty("_RippleMap")) _material.SetTexture("_RippleMap", _rippleLayer.RippleTexture);
    }

    private void CreateSplashSystem()
    {
      if (_splash != null) return;
      // Adding ParticleSystem automatically creates its renderer; requesting a
      // second ParticleSystemRenderer here would produce a Unity warning.
      var go = new GameObject("WaterSprayParticles", typeof(ParticleSystem));
      go.transform.SetParent(_surface, false);
      _splash = go.GetComponent<ParticleSystem>();
      var main = _splash.main; main.playOnAwake = false; main.loop = false; main.simulationSpace = ParticleSystemSimulationSpace.Local; main.maxParticles = 256; main.startSpeed = 0f; main.startSize = .03f; main.startLifetime = .6f; main.gravityModifier = .9f;
      var emission = _splash.emission; emission.enabled = false;
      var shape = _splash.shape; shape.enabled = false;
      var renderer = go.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Billboard; renderer.alignment = ParticleSystemRenderSpace.Facing;
      var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
      if (shader != null) { _splashMaterial = new Material(shader) { name = "Water Droplet Material" }; _splashMaterial.color = new Color(.72f, .94f, 1f, .82f); renderer.sharedMaterial = _splashMaterial; }
    }

    private bool IsInsideWater(Vector3 p) => Mathf.Abs(p.x) <= width * .5f && Mathf.Abs(p.z) <= depth * .5f && (!circular || new Vector2(p.x / (width * .5f), p.z / (depth * .5f)).sqrMagnitude <= 1f);
    private Vector3 ClampToWater(Vector3 p) => new Vector3(Mathf.Clamp(p.x, -width * .5f + _cellX, width * .5f - _cellX), p.y, Mathf.Clamp(p.z, -depth * .5f + _cellZ, depth * .5f - _cellZ));

    private void SetMaterialFloat(string property, float value)
    {
      if (_material != null && _material.HasProperty(property)) _material.SetFloat(property, value);
    }
  }
}
