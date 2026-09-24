# MediaPipeUnity 原生相机回退改动清单

- **日期**：2026-09-15
- **时间**：15:31
- **目的**：解决 Android TV 盒子（游戏盒子）上 `WebCamTexture.devices` 返回空数组、MediaPipe demo 打不开摄像头的问题；在检测不到 WebCamTexture 设备时回退到自定义原生 Camera2 插件（`com.lcq.camera.NativeCamera`）。

## 背景

- 手机正常：`WebCamTexture.devices` 非空，走原逻辑。
- 盒子异常：`WebCamTexture.devices` 为空，Unity 枚举不到内置摄像头，但原生 Camera2 插件（`NativeCamera.java`）可以正常打开。
- 方案：仅当 `WebCamTexture.devices` 为空且原生相机可用时，自动回退到原生 Camera2 插件；其他情况保持原行为不变。

## 新增文件

| 文件 | 说明 |
| --- | --- |
| `Assets/MediaPipeUnity/Samples/Common/Scripts/ImageSource/NativeCamera.cs` | C# 静态封装，通过 JNI 调用 `com.lcq.camera.NativeCamera`。提供 `isSupported`、`AvailableCameras()`、`Open(w,h)`、`Close()`、`FrameInfo()`、`FramePixels()`、`LastError()`。非 Android 平台返回安全默认值。 |
| `Assets/MediaPipeUnity/Samples/Common/Scripts/ImageSource/NativeCameraSource.cs` | 实现 `ImageSource` 抽象类，作为原生相机输入源。`Play()` 请求相机权限 → 检查设备 → `Open(1280,720)` → 等待首帧 → 创建 `Texture2D(RGBA32)`；`GetCurrentTexture()` 每帧从 `FramePixels()` 刷新纹理。 |

## 修改文件

| 文件 | 改动 |
| --- | --- |
| `Assets/MediaPipeUnity/Samples/Common/Scripts/ImageSource/WebCamSource.cs` | 1) 构造函数新增 `nativeCameraWidth/Height/FrameRate` 参数（默认 1280/720/30）。2) `Initialize()` 中：当 `availableSources` 为空且 `NativeCamera.isSupported && AvailableCameras() > 0` 时创建 `NativeCameraSource` 回退源。3) 各属性/方法按回退状态委托：`textureWidth/Height`、`isVerticallyFlipped`、`isFrontFacing`、`rotation`、`sourceName`、`sourceCandidateNames`、`availableResolutions`、`isPrepared/isPlaying`、`Play/Resume/Pause/Stop/GetCurrentTexture/SelectSource`。 |
| `Assets/MediaPipeUnity/Samples/Common/Scripts/AppSettings.cs` | 新增 WebCam 回退分辨率配置：`_nativeCameraWidth=1280`、`_nativeCameraHeight=720`、`_nativeCameraFrameRate=30`，并传入 `BuildWebCamSource()`。 |

## 运行时逻辑

```
Play()
  └─ Initialize()
       ├─ WebCamTexture.devices 非空 → 正常走 WebCamTexture（手机行为不变）
       └─ 为空 且 原生相机可用 → 创建 NativeCameraSource 回退（控制台打印 "falling back to NativeCamera"）
```
## 注意事项 / 风险

- 若盒子内置摄像头**不支持 1280x720**，`ImageReader.newInstance` 或 session 配置可能失败（报 `session configure failed`），需要在 `NativeCamera.java` 增加"按支持列表协商分辨率"的逻辑。
- 相机权限仍需在盒子上授予（`AndroidManifest.xml` 已含 `CAMERA` 权限）。