using UnityEngine;
using UnityEngine.UI;
namespace GestureFistGame
{
  public sealed class GestureOverlay : Graphic
  {
    public FistGestureInput input;
    private static readonly int[] Edges={11,12,11,13,13,15,12,14,14,16,11,23,12,24,23,24};
    private void Update() { SetVerticesDirty(); }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
      vh.Clear();
      if(input==null || Time.unscaledTime-input.LastPoseTime>.35f) return;
      for(int e=0;e<Edges.Length;e+=2) Line(vh,Point(Edges[e]),Point(Edges[e+1]),1.6f,new Color(.2f,1,.85f,.9f));
      foreach(var id in new[]{11,12,13,14,15,16})
      {
        var p=Point(id);
        Line(vh,p-Vector2.right*3,p+Vector2.right*3,6,id==15?new Color(.1f,.9f,1):new Color(1,.4f,.25f));
      }
    }
    private Vector2 Point(int id)
    {
      var p=input.PreviewPoints[id]; var r=rectTransform.rect;
      return new Vector2(r.x+(input.mirrorInput?1-p.x:p.x)*r.width,r.y+(1-p.y)*r.height);
    }
    private void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color c)
    {
      var d=b-a; if(d.sqrMagnitude<.001f)return;
      Vector2 n=new Vector2(-d.y,d.x).normalized*width*.5f;
      int start=vh.currentVertCount;
      vh.AddVert(a-n,c,Vector2.zero); vh.AddVert(a+n,c,Vector2.zero);
      vh.AddVert(b+n,c,Vector2.zero); vh.AddVert(b-n,c,Vector2.zero);
      vh.AddTriangle(start,start+1,start+2); vh.AddTriangle(start,start+2,start+3);
    }
  }
}

