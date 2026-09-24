@echo off
setlocal
start "" "D:\Unityhub\UnityEditor\2022.3.25f1\Editor\Unity.exe" -projectPath "%~dp0." -executeMethod GestureFistGame.Editor.GestureEditorStartup.Play
endlocal
