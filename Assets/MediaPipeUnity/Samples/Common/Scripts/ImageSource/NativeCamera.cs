// Copyright (c) 2026 lcq
//
// Wraps com.lcq.camera.NativeCamera (Camera2 based) for Android.
// On non-Android platforms, all methods return safe defaults so that
// WebCamSource can fall back to it only when necessary.

using UnityEngine;

namespace Mediapipe.Unity
{
  public static class NativeCamera
  {
    private const string _JarClassName = "com.lcq.camera.NativeCamera";

    public static bool isSupported
    {
      get
      {
#if UNITY_ANDROID && !UNITY_EDITOR
        return true;
#else
        return false;
#endif
      }
    }

    public static int AvailableCameras()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
      try
      {
        using (var clazz = new AndroidJavaClass(_JarClassName))
        {
          return clazz.CallStatic<int>("availableCameras");
        }
      }
      catch (System.Exception e)
      {
        Debug.LogWarning($"[NativeCamera] availableCameras failed: {e.Message}");
        return 0;
      }
#else
      return 0;
#endif
    }

    public static void Open(int width, int height)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
      try
      {
        using (var clazz = new AndroidJavaClass(_JarClassName))
        {
          clazz.CallStatic("open", width, height);
        }
      }
      catch (System.Exception e)
      {
        Debug.LogWarning($"[NativeCamera] open failed: {e.Message}");
      }
#endif
    }

    public static void Close()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
      try
      {
        using (var clazz = new AndroidJavaClass(_JarClassName))
        {
          clazz.CallStatic("close");
        }
      }
      catch (System.Exception e)
      {
        Debug.LogWarning($"[NativeCamera] close failed: {e.Message}");
      }
#endif
    }

    /// <summary>
    ///   Returns {width, height, serial}. serial == -1 means no frame ready yet.
    /// </summary>
    public static int[] FrameInfo()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
      try
      {
        using (var clazz = new AndroidJavaClass(_JarClassName))
        {
          return clazz.CallStatic<int[]>("frameInfo");
        }
      }
      catch (System.Exception e)
      {
        Debug.LogWarning($"[NativeCamera] frameInfo failed: {e.Message}");
        return new int[] { 0, 0, -1 };
      }
#else
      return new int[] { 0, 0, -1 };
#endif
    }

    /// <summary>
    ///   Latest RGBA8888 pixels (top row first), or null if none.
    /// </summary>
    public static byte[] FramePixels()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
      try
      {
        using (var clazz = new AndroidJavaClass(_JarClassName))
        {
          return clazz.CallStatic<byte[]>("framePixels");
        }
      }
      catch (System.Exception e)
      {
        Debug.LogWarning($"[NativeCamera] framePixels failed: {e.Message}");
        return null;
      }
#else
      return null;
#endif
    }

    public static string LastError()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
      try
      {
        using (var clazz = new AndroidJavaClass(_JarClassName))
        {
          return clazz.CallStatic<string>("lastError");
        }
      }
      catch (System.Exception e)
      {
        return e.Message;
      }
#else
      return null;
#endif
    }
  }
}