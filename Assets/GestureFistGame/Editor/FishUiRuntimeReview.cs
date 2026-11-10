using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
namespace GestureFistGame.Editor {
 [InitializeOnLoad] public static class FishUiRuntimeReview {
 static double next;static int step;static bool failed;
 static FishUiRuntimeReview(){EditorApplication.playModeStateChanged+=Changed;}
 public static void Run(){SessionState.SetBool("FishUiReview",true);EditorSceneManager.OpenScene(BuildFishGameScene.OutputScene);EditorApplication.delayCall+=()=>EditorApplication.isPlaying=true;}
 static void Changed(PlayModeStateChange s){if(!SessionState.GetBool("FishUiReview",false))return;if(s==PlayModeStateChange.EnteredPlayMode){failed=false;step=0;next=EditorApplication.timeSinceStartup+2;Application.logMessageReceived+=Log;EditorApplication.update+=Tick;}if(s==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("FishUiReview",false);EditorApplication.delayCall+=()=>EditorApplication.Exit(failed?1:0);}}
 static void Log(string m,string trace,LogType type){if(type==LogType.Exception||type==LogType.Error){failed=true;}}
 static void Check(bool ok,string text){if(!ok)throw new Exception("UI_REVIEW_FAIL "+text);Debug.Log("UI_REVIEW_OK "+text);}
 static void Click(Button button){
 Canvas.ForceUpdateCanvases();var data=new PointerEventData(EventSystem.current);data.position=RectTransformUtility.WorldToScreenPoint(null,button.transform.position);
 var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
 Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"button unobstructed: "+button.name);
 ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
 }
 static void CheckCameraFill(FishGameUI ui){
 var texture=new Texture2D(640,480);var preview=ui.cameraPreview;var original=preview.texture;var rotation=preview.rectTransform.localEulerAngles;
 try{preview.texture=texture;
 foreach(float angle in new[]{0f,90f,180f,270f}){
 preview.rectTransform.localEulerAngles=new Vector3(0,0,angle);Canvas.ForceUpdateCanvases();
 typeof(FishCameraSplitLayout).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui.GetComponent<FishCameraSplitLayout>(),null);
 Canvas.ForceUpdateCanvases();var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(preview.transform.parent,preview.rectTransform);var frame=((RectTransform)preview.transform.parent).rect;
 Check(bounds.min.x<=frame.xMin+.1f&&bounds.max.x>=frame.xMax-.1f&&bounds.min.y<=frame.yMin+.1f&&bounds.max.y>=frame.yMax-.1f,"camera covers background at "+angle+" degrees");
 }
 Check(ui.tracker.previewRotationOffset==0,"fish scene has no extra 180 degree offset");
 }finally{preview.texture=original;preview.rectTransform.localEulerAngles=rotation;UnityEngine.Object.Destroy(texture);}
 }
 static void Tick(){if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;
 try{var ui=UnityEngine.Object.FindObjectOfType<FishGameUI>();var start=ui.GetComponent<FishStartScreen>();var safe=ui.transform.Find("SafeArea");
 switch(step++){
 case 0:Check(start.IsMenuOpen,"homepage initializes");ui.GetComponent<FishPageCards>().ShowTutorial(false);break;
 
 case 1:Check(safe.Find("ActionTutorial").gameObject.activeInHierarchy,"tutorial visible");Click(safe.Find("ActionTutorial/知道了，开始").GetComponent<Button>());step=7;break;
 case 7:step=2;CheckCameraFill(ui);Check(safe.Find("CompactCoopHUD").gameObject.activeInHierarchy,"HUD visible after start");Check(ui.cameraPreview.transform.IsChildOf(ui.transform.Find("CameraBackground")),"preview moved to background");Check(safe.Find("CentralRiver").gameObject.activeInHierarchy,"vertical river visible");Check(Vector3.Dot(Camera.main.transform.up,Vector3.right)>.99f,"river and fish travel axis is screen vertical");Check(Camera.main.cullingMask==(1<<29),"grass excluded from river camera");Check(GameObject.Find("CameraBackgroundDisplay").GetComponent<Camera>().targetTexture==null,"display camera prevents no-camera overlay");Check(ui.GetComponent<Canvas>().renderMode==RenderMode.ScreenSpaceOverlay,"canvas independent of render texture camera");ui.game.PauseRound();typeof(FishGameController).GetProperty("Phase").GetSetMethod(true).Invoke(ui.game,new object[]{FishGameController.RoundPhase.Playing});ui.game.PauseRound();break;
 case 2:Check(!ui.game.PlayerLifted(0)&&!ui.game.PlayerLifted(1),"paused lift feedback cleared");Check(safe.Find("ConfirmTeamResume").gameObject.activeInHierarchy,"resume button visible");Check(safe.Find("CompactCoopHUD/ReconnectCamera").gameObject.activeInHierarchy,"reconnect available");typeof(FishGameController).GetMethod("FinishRound",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui.game,new object[]{false});break;
 case 3:
 Check(safe.Find("TeamResultCards/历史最佳").gameObject.activeInHierarchy,"best score has independent card");
 Check(safe.Find("TeamResultCards/PlayerReport1").gameObject.activeInHierarchy&&safe.Find("TeamResultCards/PlayerReport2").gameObject.activeInHierarchy,"player reports separated");
 Check(safe.Find("TeamResultCards").gameObject.activeInHierarchy,"results visible");Check(safe.Find("TeamResultCards/返回首页").GetComponent<Button>().IsInteractable(),"home enabled");Click(safe.Find("TeamResultCards/返回首页").GetComponent<Button>());break;
 case 4:Check(start.IsMenuOpen,"return home");start.Begin(false);break;
 case 5:Check(safe.Find("CompactCoopHUD").gameObject.activeInHierarchy,"second round HUD restored");Check(!ui.finishPanel.activeSelf,"old result cleared");Check(!failed,"no runtime errors during complete navigation");Debug.Log("FISH_UI_RUNTIME_PASS");Stop();break;
 }}catch(Exception e){failed=true;Debug.LogException(e);Stop();}}
 static void Stop(){EditorApplication.update-=Tick;EditorApplication.isPlaying=false;}
 }
}
