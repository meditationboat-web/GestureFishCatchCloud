using UnityEngine;
using UnityEngine.UI;
namespace GestureFistGame
{
  // A project-owned, scalable rounded card; no external UI textures required.
  public static class FishUiTheme
  {
    private static Sprite _rounded;
    public static void Card(Image image, Color color)
    {
      if (image == null) return;
      if (_rounded == null)
      {
        const int size = 128; const float radius = 38f;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "LeafLagoonRoundedCard"; texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
          var nearest = new Vector2(Mathf.Clamp(x, radius, size - 1 - radius), Mathf.Clamp(y, radius, size - 1 - radius));
          float alpha = Mathf.Clamp01(radius - Vector2.Distance(new Vector2(x, y), nearest) + .5f);
          texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
        }
        texture.Apply();
        _rounded = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, Vector4.one * 40);
      }
      image.sprite = _rounded; image.type = Image.Type.Sliced; image.color = color;
    }
    public static void Apply(Transform root)
    {
      foreach (var image in root.GetComponentsInChildren<Image>(true))
      {
        bool button = image.GetComponent<Button>() != null;
        Card(image, button ? new Color(.70f,.86f,.34f,1) : new Color(.97f,1f,.88f,.97f));
        if (button)
        {
          var colors = image.GetComponent<Button>().colors;
          colors.normalColor = Color.white; colors.highlightedColor = new Color(.9f,1f,.67f);
          colors.pressedColor = new Color(.7f,.88f,.42f); colors.disabledColor = new Color(.80f,.86f,.64f,.75f);
          image.GetComponent<Button>().colors = colors;
        }
      }
      foreach (var text in root.GetComponentsInChildren<Text>(true))
      {
        text.color = new Color(.23f,.36f,.14f);
        var outline = text.GetComponent<Outline>(); if (outline != null) outline.enabled = false;
        text.fontStyle = FontStyle.Normal;
      }
    }
  }
}
