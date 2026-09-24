// Copyright (c) 2021 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Mediapipe.Unity
{
  public class WebCamSource : ImageSource
  {
    private readonly int _preferableDefaultWidth = 1280;

    private const string _TAG = nameof(WebCamSource);

    private readonly ResolutionStruct[] _defaultAvailableResolutions;

    private readonly int _nativeCameraWidth = 1280;
    private readonly int _nativeCameraHeight = 720;
    private readonly double _nativeCameraFrameRate = 30.0;

    private NativeCameraSource _nativeCameraFallback;
    private NativeCameraSource nativeCameraFallback
    {
      get => _nativeCameraFallback;
      set
      {
        if (_nativeCameraFallback != null)
        {
          _nativeCameraFallback.Stop();
        }
        _nativeCameraFallback = value;
      }
    }
    private bool isUsingNativeCameraFallback => _nativeCameraFallback != null;

    public WebCamSource(int preferableDefaultWidth, ResolutionStruct[] defaultAvailableResolutions,
      int nativeCameraWidth = 1280, int nativeCameraHeight = 720, double nativeCameraFrameRate = 30.0)
    {
      _preferableDefaultWidth = preferableDefaultWidth;
      _defaultAvailableResolutions = defaultAvailableResolutions;
      _nativeCameraWidth = nativeCameraWidth;
      _nativeCameraHeight = nativeCameraHeight;
      _nativeCameraFrameRate = nativeCameraFrameRate;
    }

    private static readonly object _PermissionLock = new object();
    private static bool _IsPermitted = false;

    private WebCamTexture _webCamTexture;
    private WebCamTexture webCamTexture
    {
      get => _webCamTexture;
      set
      {
        if (_webCamTexture != null)
        {
          _webCamTexture.Stop();
        }
        _webCamTexture = value;
      }
    }

    public override int textureWidth => isUsingNativeCameraFallback ? _nativeCameraFallback.textureWidth : !isPrepared ? 0 : webCamTexture.width;
    public override int textureHeight => isUsingNativeCameraFallback ? _nativeCameraFallback.textureHeight : !isPrepared ? 0 : webCamTexture.height;

    public override bool isVerticallyFlipped => isUsingNativeCameraFallback ? _nativeCameraFallback.isVerticallyFlipped : isPrepared && webCamTexture.videoVerticallyMirrored;
    public override bool isFrontFacing => isUsingNativeCameraFallback ? _nativeCameraFallback.isFrontFacing : isPrepared && (webCamDevice is WebCamDevice valueOfWebCamDevice) && valueOfWebCamDevice.isFrontFacing;
    public override RotationAngle rotation => isUsingNativeCameraFallback ? _nativeCameraFallback.rotation : !isPrepared ? RotationAngle.Rotation0 : (RotationAngle)webCamTexture.videoRotationAngle;

    private WebCamDevice? _webCamDevice;
    private WebCamDevice? webCamDevice
    {
      get => _webCamDevice;
      set
      {
        if (_webCamDevice is WebCamDevice valueOfWebCamDevice)
        {
          if (value is WebCamDevice valueOfValue && valueOfValue.name == valueOfWebCamDevice.name)
          {
            // not changed
            return;
          }
        }
        else if (value == null)
        {
          // not changed
          return;
        }
        _webCamDevice = value;
        resolution = GetDefaultResolution();
      }
    }
    public override string sourceName => isUsingNativeCameraFallback ? _nativeCameraFallback.sourceName : (webCamDevice is WebCamDevice valueOfWebCamDevice) ? valueOfWebCamDevice.name : null;

    private WebCamDevice[] _availableSources;
    private WebCamDevice[] availableSources
    {
      get
      {
        if (_availableSources == null)
        {
          _availableSources = WebCamTexture.devices;
        }

        return _availableSources;
      }
      set => _availableSources = value;
    }

    public override string[] sourceCandidateNames => isUsingNativeCameraFallback ? _nativeCameraFallback.sourceCandidateNames : availableSources?.Select(device => device.name).ToArray();

#pragma warning disable IDE0025
    public override ResolutionStruct[] availableResolutions
    {
      get
      {
        if (isUsingNativeCameraFallback)
        {
          return _nativeCameraFallback.availableResolutions;
        }
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        if (webCamDevice is WebCamDevice valueOfWebCamDevice) {
          return valueOfWebCamDevice.availableResolutions.Select(resolution => new ResolutionStruct(resolution)).ToArray();
        }
#endif
        return webCamDevice == null ? null : _defaultAvailableResolutions;
      }
    }
#pragma warning restore IDE0025

    public override bool isPrepared => isUsingNativeCameraFallback ? _nativeCameraFallback.isPrepared : webCamTexture != null;
    public override bool isPlaying => isUsingNativeCameraFallback ? _nativeCameraFallback.isPlaying : webCamTexture != null && webCamTexture.isPlaying;

    private IEnumerator Initialize()
    {
      yield return GetPermission();

      if (!_IsPermitted)
      {
        yield break;
      }

      if (webCamDevice != null)
      {
        yield break;
      }

      availableSources = WebCamTexture.devices;

      if (availableSources != null && availableSources.Length > 0)
      {
        webCamDevice = availableSources[0];
        yield break;
      }

      // WebCamTexture.devices is empty. On some Android TV boxes, Camera2 still
      // exposes the camera, so fall back to the native Camera2 plugin.
      if (NativeCamera.isSupported && NativeCamera.AvailableCameras() > 0)
      {
        Debug.Log("WebCamTexture.devices is empty, falling back to NativeCamera");
        nativeCameraFallback = new NativeCameraSource(_nativeCameraWidth, _nativeCameraHeight, _nativeCameraFrameRate);
        resolution = nativeCameraFallback.resolution;
      }
    }

    private IEnumerator GetPermission()
    {
      lock (_PermissionLock)
      {
        if (_IsPermitted)
        {
          yield break;
        }

#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
          Permission.RequestUserPermission(Permission.Camera);
          yield return new WaitForSeconds(0.1f);
        }
#elif UNITY_IOS
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam)) {
          yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        }
#endif

#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
          Debug.LogWarning("Not permitted to use Camera");
          yield break;
        }
#elif UNITY_IOS
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam)) {
          Debug.LogWarning("Not permitted to use WebCam");
          yield break;
        }
#endif
        _IsPermitted = true;

        yield return new WaitForEndOfFrame();
      }
    }

    public override void SelectSource(int sourceId)
    {
      if (isUsingNativeCameraFallback)
      {
        if (sourceId != 0)
        {
          throw new System.ArgumentException($"Invalid source ID: {sourceId}");
        }
        return;
      }

      if (sourceId < 0 || sourceId >= availableSources.Length)
      {
        throw new ArgumentException($"Invalid source ID: {sourceId}");
      }

      webCamDevice = availableSources[sourceId];
    }

    public override IEnumerator Play()
    {
      yield return Initialize();
      if (!_IsPermitted)
      {
        throw new InvalidOperationException("Not permitted to access cameras");
      }

      if (isUsingNativeCameraFallback)
      {
        yield return _nativeCameraFallback.Play();
        yield break;
      }

      InitializeWebCamTexture();
      webCamTexture.Play();
      yield return WaitForWebCamTexture();
    }

    public override IEnumerator Resume()
    {
      if (isUsingNativeCameraFallback)
      {
        yield return _nativeCameraFallback.Resume();
        yield break;
      }

      if (!isPrepared)
      {
        throw new InvalidOperationException("WebCamTexture is not prepared yet");
      }
      if (!webCamTexture.isPlaying)
      {
        webCamTexture.Play();
      }
      yield return WaitForWebCamTexture();
    }

    public override void Pause()
    {
      if (isUsingNativeCameraFallback)
      {
        _nativeCameraFallback.Pause();
        return;
      }

      if (isPlaying)
      {
        webCamTexture.Pause();
      }
    }

    public override void Stop()
    {
      if (isUsingNativeCameraFallback)
      {
        _nativeCameraFallback.Stop();
        return;
      }

      if (webCamTexture != null)
      {
        webCamTexture.Stop();
      }
      webCamTexture = null;
    }

    public override Texture GetCurrentTexture() => isUsingNativeCameraFallback ? _nativeCameraFallback.GetCurrentTexture() : webCamTexture;

    private ResolutionStruct GetDefaultResolution()
    {
      var resolutions = availableResolutions;
      return resolutions == null || resolutions.Length == 0 ? new ResolutionStruct() : resolutions.OrderBy(resolution => resolution, new ResolutionStructComparer(_preferableDefaultWidth)).First();
    }

    private void InitializeWebCamTexture()
    {
      Stop();
      if (webCamDevice is WebCamDevice valueOfWebCamDevice)
      {
        webCamTexture = new WebCamTexture(valueOfWebCamDevice.name, resolution.width, resolution.height, (int)resolution.frameRate);
        return;
      }
      throw new InvalidOperationException("Cannot initialize WebCamTexture because WebCamDevice is not selected");
    }

    private IEnumerator WaitForWebCamTexture()
    {
      const int timeoutFrame = 2000;
      var count = 0;
      Debug.Log("Waiting for WebCamTexture to start");
      yield return new WaitUntil(() => count++ > timeoutFrame || webCamTexture.width > 16);

      if (webCamTexture.width <= 16)
      {
        throw new TimeoutException("Failed to start WebCam");
      }
    }

    private class ResolutionStructComparer : IComparer<ResolutionStruct>
    {
      private readonly int _preferableDefaultWidth;

      public ResolutionStructComparer(int preferableDefaultWidth)
      {
        _preferableDefaultWidth = preferableDefaultWidth;
      }

      public int Compare(ResolutionStruct a, ResolutionStruct b)
      {
        var aDiff = Mathf.Abs(a.width - _preferableDefaultWidth);
        var bDiff = Mathf.Abs(b.width - _preferableDefaultWidth);
        if (aDiff != bDiff)
        {
          return aDiff - bDiff;
        }
        if (a.height != b.height)
        {
          // prefer smaller height
          return a.height - b.height;
        }
        // prefer smaller frame rate
        return (int)(a.frameRate - b.frameRate);
      }
    }
  }
}
