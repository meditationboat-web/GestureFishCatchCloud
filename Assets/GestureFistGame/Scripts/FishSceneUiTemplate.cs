using UnityEngine;
using UnityEngine.UI;
namespace GestureFistGame {
 [DefaultExecutionOrder(1000)] public sealed class FishSceneUiTemplate:MonoBehaviour {
  public GameObject layout;
  void Awake(){if(layout!=null)layout.SetActive(false);}
  void Start(){
   var ui=GetComponent<FishGameUI>();if(layout==null||ui==null)return;
   foreach(var source in layout.GetComponentsInChildren<RectTransform>(true)){
    if(source==layout.transform)continue;
    string path=Path(source,layout.transform);var target=ui.transform.Find(path) as RectTransform;if(target==null)continue;
    target.anchorMin=source.anchorMin;target.anchorMax=source.anchorMax;target.pivot=source.pivot;target.sizeDelta=source.sizeDelta;target.anchoredPosition=source.anchoredPosition;target.localScale=source.localScale;
    var a=source.GetComponent<Graphic>();var b=target.GetComponent<Graphic>();if(a!=null&&b!=null)b.color=a.color;
    var st=source.GetComponent<Text>();var tt=target.GetComponent<Text>();if(st!=null&&tt!=null){tt.fontSize=st.fontSize;if(!string.IsNullOrEmpty(st.text))tt.text=st.text;}
   }
  }
  static string Path(Transform t,Transform root){string p=t.name;while(t.parent!=root){t=t.parent;p=t.name+"/"+p;}return p;}
 }
}
