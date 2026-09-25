using System.Collections.Generic;
using UnityEngine;

namespace GestureFistGame
{
  /// <summary>
  /// Keeps the supplied Doro texture/material colors and adds a colored silhouette
  /// behind the model so fish value is readable without recoloring Doro itself.
  /// </summary>
  public sealed class DoroFishPresentation : MonoBehaviour
  {
    [Range(1.005f, 1.12f)] public float outlineScale = 1.045f;
    public Color outlineColor = Color.cyan;
    private bool _built;
    private readonly List<GameObject> _outlineObjects = new List<GameObject>();
    private Material _outlineMaterial;

    public void Configure(Color color)
    {
      outlineColor = color;
      if (_outlineMaterial != null) _outlineMaterial.color = color;
      if (!_built) Build();
    }

    private void Awake()
    {
      Build();
    }

    private void Build()
    {
      if (_built) return;
      // Fish prototypes are instantiated after this component has generated
      // their outline children. Unity resets private runtime state on the
      // clone, so detect those children and avoid creating nested outlines.
      var existing = GetComponentsInChildren<Transform>(true);
      foreach (var child in existing)
      {
        if (child != transform && child.name.EndsWith("_ValueOutline", System.StringComparison.Ordinal))
        {
          _built = true;
          return;
        }
      }
      var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
      if (shader == null) return;
      _outlineMaterial = new Material(shader);
      _outlineMaterial.name = name + " Outline Material";
      _outlineMaterial.color = outlineColor;
      if (_outlineMaterial.HasProperty("_Glossiness")) _outlineMaterial.SetFloat("_Glossiness", 0f);
      if (_outlineMaterial.HasProperty("_Metallic")) _outlineMaterial.SetFloat("_Metallic", 0f);
      // Render the expanded copy after Doro, with front-face culling and no
      // depth write. This leaves only a silhouette around the original texture.
      _outlineMaterial.renderQueue = 3001;
      if (_outlineMaterial.HasProperty("_Cull")) _outlineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Front);
      if (_outlineMaterial.HasProperty("_ZWrite")) _outlineMaterial.SetInt("_ZWrite", 0);
      if (_outlineMaterial.HasProperty("_ZTest")) _outlineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
      _built = true;

      var renderers = GetComponentsInChildren<Renderer>(true);
      foreach (var source in renderers)
      {
        // A second call to Build is guarded, so generated children are never copied again.
        if (source == null || source.transform == transform) continue;
        if (source.gameObject.name.EndsWith("_ValueOutline", System.StringComparison.Ordinal)) continue;
        var sourceSkinned = source as SkinnedMeshRenderer;
        var sourceMesh = source as MeshRenderer;
        if (sourceSkinned != null && sourceSkinned.sharedMesh != null)
          CreateSkinnedOutline(sourceSkinned);
        else if (sourceMesh != null)
        {
          var filter = sourceMesh.GetComponent<MeshFilter>();
          if (filter != null && filter.sharedMesh != null) CreateMeshOutline(sourceMesh, filter.sharedMesh);
        }
      }
    }

    private void CreateMeshOutline(MeshRenderer source, Mesh mesh)
    {
      var go = CreateOutlineObject(source.transform);
      var filter = go.AddComponent<MeshFilter>();
      filter.sharedMesh = mesh;
      var renderer = go.AddComponent<MeshRenderer>();
      renderer.sharedMaterials = OutlineMaterials(mesh.subMeshCount);
      renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      renderer.receiveShadows = false;
    }

    private void CreateSkinnedOutline(SkinnedMeshRenderer source)
    {
      var go = CreateOutlineObject(source.transform);
      var renderer = go.AddComponent<SkinnedMeshRenderer>();
      renderer.sharedMesh = source.sharedMesh;
      renderer.bones = source.bones;
      renderer.rootBone = source.rootBone;
      renderer.localBounds = source.localBounds;
      renderer.updateWhenOffscreen = true;
      renderer.sharedMaterials = OutlineMaterials(source.sharedMesh.subMeshCount);
      renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      renderer.receiveShadows = false;
    }

    private GameObject CreateOutlineObject(Transform source)
    {
      var go = new GameObject(source.name + "_ValueOutline");
      go.transform.SetParent(source.parent, false);
      go.transform.localPosition = source.localPosition;
      go.transform.localRotation = source.localRotation;
      go.transform.localScale = source.localScale * outlineScale;
      go.layer = source.gameObject.layer;
      _outlineObjects.Add(go);
      return go;
    }

    private Material[] OutlineMaterials(int count)
    {
      var materials = new Material[Mathf.Max(1, count)];
      for (int i = 0; i < materials.Length; i++) materials[i] = _outlineMaterial;
      return materials;
    }

    private void OnDestroy()
    {
      foreach (var go in _outlineObjects) if (go != null) Destroy(go);
      // Instantiated fish share the prototype's generated material. The
      // original owner cleans it up; clones only destroy their child objects.
      if (_outlineMaterial != null && _outlineObjects.Count > 0) Destroy(_outlineMaterial);
    }
  }
}
