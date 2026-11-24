using UnityEngine;

namespace GestureFistGame
{
  // Keep the depth-tested scene label in sync when Unity rebuilds the Chinese font atlas.
  [ExecuteAlways, RequireComponent(typeof(TextMesh), typeof(MeshRenderer))]
  public sealed class GateLabelFont : MonoBehaviour
  {
    private TextMesh _text;
    private Renderer _renderer;
    private MaterialPropertyBlock _properties;
    private void OnEnable()
    {
      _text=GetComponent<TextMesh>();_renderer=GetComponent<Renderer>();
      _properties=new MaterialPropertyBlock();Font.textureRebuilt+=Rebuilt;Refresh();
    }
    private void OnDisable(){Font.textureRebuilt-=Rebuilt;}
    private void Rebuilt(Font font){if(_text!=null && font==_text.font) Refresh();}
    private void LateUpdate(){Refresh();}
    private void Refresh()
    {
      if(_text==null || _text.font==null || _renderer==null) return;
      _renderer.GetPropertyBlock(_properties);
      _properties.SetTexture("_MainTex",_text.font.material.mainTexture);
      _properties.SetColor("_Color",_text.color);_renderer.SetPropertyBlock(_properties);
    }
  }
}
