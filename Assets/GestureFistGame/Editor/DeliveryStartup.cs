using UnityEditor;
using UnityEditor.SceneManagement;
[InitializeOnLoad] public static class DeliveryStartup {
 static DeliveryStartup(){EditorApplication.delayCall+=()=>{if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||UnityEngine.Application.isBatchMode)return;var s=EditorSceneManager.GetActiveScene();if(string.IsNullOrEmpty(s.path)&&!s.isDirty)EditorSceneManager.OpenScene("Assets/GestureFistGame/Scenes/GestureFishCatchGame.unity");};}
}
