// Copyright (c) 2026 lcq
//
// ImageSource backed by com.lcq.camera.NativeCamera (Camera2 API).
// Used by WebCamSource as a fallback when WebCamTexture.devices is empty
// (e.g. on some Android TV boxes).

using System.Collections;
using UnityEngine;

namespace Mediapipe.Unity
{
  public class NativeCameraSource : ImageSource
  {
    private readonly int _requestedWidth;
    private readonly int _requestedHeight;
    private readonly double _frameRate;

    private Texture2D _outputTexture;
    private bool _isPlaying = false;
    private bool _isVerticallyFlipped = false;
    private RotationAngle _rotation = RotationAngle.Rotation0;
    private bool _isFrontFacing = false;

    private int _width;
    private int _height;

    public NativeCameraSource(int width, int height, double frameRate = 30.0)
    {
      _requestedWidth = width;
      _requestedHeight = height;
      _frameRate = frameRate;
      resolution = new ResolutionStruct(width, height, frameRate);
    }

    public bool isVerticallyFlippedOverride
    {
      get => _isVerticallyFlipped;
      set => _isVerticallyFlipped = value;
    }

    public RotationAngle rotationOverride
    {
      get => _rotation;
      set => _rotation = value;
    }

    public bool isFrontFacingOverride
    {
      get => _isFrontFacing;
      set => _isFrontFacing = value;
    }

    public override string sourceName => "NativeCamera";

    public override string[] sourceCandidateNames => NativeCamera.isSupported ? new string[] { sourceName } : null;

    public override ResolutionStruct[] availableResolutions => new ResolutionStruct[] { resolution };

    public override bool isPrepared => _outputTexture != null;

    public override bool isPlaying => _isPlaying;

    public override int textureWidth => isPrepared ? _outputTexture.width : 0;

    public override int textureHeight => isPrepared ? _outputTexture.height : 0;

    public override bool isVerticallyFlipped => _isVerticallyFlipped;

    public override bool isFrontFacing => _isFrontFacing;

    public override RotationAngle rotation => _rotation;

    public override double frameRate => _frameRate;

    public override void SelectSource(int sourceId)
    {
      if (sourceId != 0)
      {
        throw new System.ArgumentException($"Invalid source ID: {sourceId}");
      }
    }

    public override IEnumerator Play()
    {
      if (_isPlaying)
      {
        yield break;
      }

      yield return GetPermission();

      if (NativeCamera.AvailableCameras() <= 0)
      {
        throw new System.InvalidOperationException($"Cannot open the camera: {NativeCamera.LastError()}");
      }

      NativeCamera.Open(_requestedWidth, _requestedHeight);

      const int timeoutFrame = 2000;
      var count = 0;
      yield return new WaitUntil(() =>
      {
        count++;
        var info = NativeCamera.FrameInfo();
        return count > timeoutFrame || info[2] >= 0;
      });

      var frameInfo = NativeCamera.FrameInfo();
      if (frameInfo[2] < 0)
      {
        throw new System.TimeoutException($"Failed to start NativeCamera: {NativeCamera.LastError()}");
      }

      _width = frameInfo[0];
      _height = frameInfo[1];
      _outputTexture = new Texture2D(_width, _height, TextureFormat.RGBA32, false);
      _isPlaying = true;
      resolution = new ResolutionStruct(_width, _height, _frameRate);
    }

    public override IEnumerator Resume()
    {
      if (!isPrepared)
      {
        throw new System.InvalidOperationException("NativeCamera is not prepared yet");
      }
      if (!_isPlaying)
      {
        NativeCamera.Open(_width, _height);
        _isPlaying = true;
      }
      yield return null;
    }

    public override void Pause()
    {
      if (_isPlaying)
      {
        NativeCamera.Close();
        _isPlaying = false;
      }
    }

    public override void Stop()
    {
      NativeCamera.Close();
      _isPlaying = false;
      _outputTexture = null;
    }

    public override Texture GetCurrentTexture()
    {
      if (!isPrepared)
      {
        return null;
      }

      var framePixels = NativeCamera.FramePixels();
      if (framePixels != null)
      {
        var expectedLength = _outputTexture.width * _outputTexture.height * 4;
        if (framePixels.Length == expectedLength)
        {
          _outputTexture.LoadRawTextureData(framePixels);
          _outputTexture.Apply();
        }
      }

      return _outputTexture;
    }

    private IEnumerator GetPermission()
    {
#if UNITY_ANDROID
      if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
      {
        UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
        yield return new WaitForSeconds(0.5f);
      }
      if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
      {
        Debug.LogWarning("Not permitted to use Camera");
        yield break;
      }
#endif
      yield return null;
    }
  }
}