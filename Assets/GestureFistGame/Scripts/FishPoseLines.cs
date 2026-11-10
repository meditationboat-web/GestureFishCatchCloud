using System.Collections.Generic;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using UnityEngine;
using UnityEngine.UI;
namespace GestureFistGame {
  // Draw directly over the same RawImage, sharing its crop, rotation and mirroring.
  [RequireComponent(typeof(CanvasRenderer))]
  public sealed class FishPoseLines : MaskableGraphic {
    public FishGestureInput input;
    public RawImage preview;
    readonly List<Vector2[]> bodies = new List<Vector2[]>();
    readonly List<bool[]> visible = new List<bool[]>();
    float received;
    static readonly int[] edges={0,2,2,7,0,5,5,8,11,12,11,13,13,15,12,14,14,16,11,23,12,24,23,24,23,25,25,27,24,26,26,28,27,29,29,31,28,30,30,32};
    protected override void OnEnable(){base.OnEnable();if(input!=null)input.PoseUpdated+=Receive;}
    protected override void OnDisable(){if(input!=null)input.PoseUpdated-=Receive;bodies.Clear();visible.Clear();base.OnDisable();}
    void Receive(PoseLandmarkerResult result){
      bodies.Clear();visible.Clear();received=Time.unscaledTime;
      if(result.poseLandmarks!=null)foreach(var pose in result.poseLandmarks){
        if(pose.landmarks==null||pose.landmarks.Count<33)continue;
        var points=new Vector2[33];var valid=new bool[33];
        for(int i=0;i<33;i++){var p=pose.landmarks[i];points[i]=new Vector2(p.x,p.y);valid[i]=(p.visibility??1)>=.5f&&(p.presence??1)>=.5f&&p.x>=0&&p.x<=1&&p.y>=0&&p.y<=1;}
        bodies.Add(points);visible.Add(valid);
      }
      // Native detection order can change between frames; retain the same left/right ownership as input.
      for(int a=0;a<bodies.Count;a++)for(int b=a+1;b<bodies.Count;b++)
        if(bodies[a][11].x+bodies[a][12].x>bodies[b][11].x+bodies[b][12].x){
          var points=bodies[a];bodies[a]=bodies[b];bodies[b]=points;
          var valid=visible[a];visible[a]=visible[b];visible[b]=valid;
        }
      SetVerticesDirty();
    }
    void Update(){SetVerticesDirty();}
    Vector2 Point(Vector2 p){
      // Landmarks use the rotated inference image. Undo rotation before mapping to preview UV.
      var tracker=input!=null?input.tracker:null;
      float angle=(tracker!=null?tracker.PoseImageRotation:0)*Mathf.Deg2Rad;
      Vector2 q=new Vector2(p.x-.5f,.5f-p.y);
      q=new Vector2(Mathf.Cos(angle)*q.x-Mathf.Sin(angle)*q.y,Mathf.Sin(angle)*q.x+Mathf.Cos(angle)*q.y)+Vector2.one*.5f;
      // The inference texture has top-first rows; preview uses the original webcam texture.
      if(tracker!=null && tracker.PoseImageVerticallyMirrored)q.y=1-q.y;
      Rect uv=preview.uvRect; q=new Vector2((q.x-uv.x)/uv.width,(q.y-uv.y)/uv.height);
      Rect r=rectTransform.rect;return new Vector2(r.x+q.x*r.width,r.y+q.y*r.height);
    }
    protected override void OnPopulateMesh(VertexHelper vh){
      vh.Clear();if(preview==null||input==null||input.mouseMode||Time.unscaledTime-received>.5f)return;
      for(int b=0;b<bodies.Count;b++){
        // With only one visible body, avoid assigning an incorrect P1/P2 identity.
        Color c=bodies.Count==1?Color.white:b==0?new Color(.91f,.28f,.29f):new Color(.23f,.5f,.8f);
        for(int e=0;e<edges.Length;e+=2){int a=edges[e],z=edges[e+1];if(!visible[b][a]||!visible[b][z])continue;
          Vector2 start=Point(bodies[b][a]),end=Point(bodies[b][z]);Vector2 n=new Vector2(-(end-start).y,(end-start).x).normalized*2.5f;
          Quad(vh,start-n,start+n,end+n,end-n,c);
        }
        for(int i=0;i<33;i++)if(visible[b][i]){Vector2 p=Point(bodies[b][i]);Quad(vh,p+new Vector2(-5,-5),p+new Vector2(-5,5),p+new Vector2(5,5),p+new Vector2(5,-5),c);}
      }
    }
    static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color){int i=vh.currentVertCount;vh.AddVert(a,color,Vector2.zero);vh.AddVert(b,color,Vector2.zero);vh.AddVert(c,color,Vector2.zero);vh.AddVert(d,color,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);}
  }
}
