package com.lcq.camera;

import android.app.Activity;
import android.graphics.ImageFormat;
import android.hardware.camera2.CameraAccessException;
import android.hardware.camera2.CameraCaptureSession;
import android.hardware.camera2.CameraCharacteristics;
import android.hardware.camera2.CameraDevice;
import android.hardware.camera2.CameraManager;
import android.hardware.camera2.CaptureRequest;
import android.media.Image;
import android.media.ImageReader;
import android.os.Handler;
import android.os.HandlerThread;
import android.view.Surface;

import com.unity3d.player.UnityPlayer;

import java.nio.ByteBuffer;
import java.util.Arrays;
import java.util.List;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public final class NativeCamera {

    private static final Controller controller = new Controller();

    private NativeCamera() {
    }

    public static int availableCameras() {
        return controller.availableCameras();
    }

    public static void open(int width, int height) {
        controller.open(width, height);
    }

    public static void close() {
        controller.close();
    }

    public static boolean isOpen() {
        return controller.isOpen();
    }

    /** Returns {width, height, serial}; serial == -1 means no frame ready yet. */
    public static int[] frameInfo() {
        return controller.frameInfo();
    }

    /** Latest RGBA8888 (top row first, vertically flipped), or null if none. */
    public static byte[] framePixels() {
        return controller.framePixels();
    }

    public static String lastError() {
        return controller.lastError();
    }

    // ------------------------------------------------------------------

    private static final class Controller {

        private final Object lock = new Object();
        private final ExecutorService convertPool = Executors.newSingleThreadExecutor();

        private CameraManager manager;
        private CameraDevice camera;
        private CameraCaptureSession session;
        private ImageReader imageReader;
        private HandlerThread cameraThread;
        private String error = "";
        private boolean open = false;

        private int width;
        private int height;
        private int serial = -1;
        private byte[] pixels;

        Activity activity() {
            return UnityPlayer.currentActivity;
        }

        private CameraManager manager() {
            if (manager == null) {
                manager = (CameraManager) activity().getSystemService(Activity.CAMERA_SERVICE);
            }
            return manager;
        }

        int availableCameras() {
            try {
                String[] ids = manager().getCameraIdList();
                return ids == null ? 0 : ids.length;
            } catch (Throwable t) {
                return 0;
            }
        }

        boolean isOpen() {
            synchronized (lock) {
                return open;
            }
        }

        void open(final int reqW, final int reqH) {
            synchronized (lock) {
                closeInternal();
                error = "";
                serial = -1;
                pixels = null;
            }

            final CameraManager mgr = manager();
            try {
                final String[] ids = mgr.getCameraIdList();
                if (ids == null || ids.length == 0) {
                    setError("no camera devices");
                    return;
                }

                final String camId = chooseId(mgr, ids);
                final int w = reqW > 0 ? reqW : 640;
                final int h = reqH > 0 ? reqH : 480;

                cameraThread = new HandlerThread("NativeCameraThread");
                cameraThread.start();
                final Handler handler = new Handler(cameraThread.getLooper());

                imageReader = ImageReader.newInstance(w, h, ImageFormat.YUV_420_888, 3);
                imageReader.setOnImageAvailableListener(new Frames(), handler);
                final List<Surface> outputs = Arrays.asList(imageReader.getSurface());

                mgr.openCamera(camId, new CameraDevice.StateCallback() {
                    @Override
                    public void onOpened(CameraDevice device) {
                        createSession(device, outputs, handler);
                    }

                    @Override
                    public void onDisconnected(CameraDevice device) {
                        setError("camera disconnected");
                        closeInternal();
                    }

                    @Override
                    public void onError(CameraDevice device, int errorCode) {
                        setError("open error " + describeError(errorCode));
                        closeInternal();
                    }
                }, handler);

            } catch (CameraAccessException e) {
                setError("CameraAccessException: " + accessErrName(e.getReason()));
            } catch (SecurityException e) {
                setError("SecurityException: CAMERA permission not granted");
            } catch (Throwable t) {
                setError("open failed: " + t.getClass().getSimpleName() + ": " + t.getMessage());
            }
        }

        private void createSession(final CameraDevice device, List<Surface> outputs, Handler handler) {
            try {
                final CaptureRequest.Builder builder = device.createCaptureRequest(CameraDevice.TEMPLATE_PREVIEW);
                builder.addTarget(imageReader.getSurface());
                final CaptureRequest request = builder.build();

                device.createCaptureSession(outputs, new CameraCaptureSession.StateCallback() {
                    @Override
                    public void onConfigured(CameraCaptureSession s) {
                        synchronized (lock) {
                            camera = device;
                            session = s;
                            open = true;
                        }
                        try {
                            s.setRepeatingRequest(request, null, handler);
                        } catch (CameraAccessException e) {
                            setError("capture start: " + accessErrName(e.getReason()));
                            closeInternal();
                        }
                    }

                    @Override
                    public void onConfigureFailed(CameraCaptureSession s) {
                        setError("session configure failed");
                        closeInternal();
                    }
                }, handler);

            } catch (CameraAccessException e) {
                setError("session create: " + accessErrName(e.getReason()));
                closeInternal();
            } catch (Throwable t) {
                setError("session create: " + t.getClass().getSimpleName() + ": " + t.getMessage());
                closeInternal();
            }
        }

        void close() {
            synchronized (lock) {
                closeInternal();
                error = "";
            }
        }

        private void closeInternal() {
            synchronized (lock) {
                open = false;
                if (session != null) {
                    try { session.close(); } catch (Throwable ignored) { }
                    session = null;
                }
                if (camera != null) {
                    try { camera.close(); } catch (Throwable ignored) { }
                    camera = null;
                }
                if (imageReader != null) {
                    try { imageReader.close(); } catch (Throwable ignored) { }
                    imageReader = null;
                }
                if (cameraThread != null) {
                    try { cameraThread.quitSafely(); } catch (Throwable ignored) { }
                    cameraThread = null;
                }
            }
        }

        int[] frameInfo() {
            synchronized (lock) {
                return new int[]{width, height, serial};
            }
        }

        byte[] framePixels() {
            synchronized (lock) {
                return pixels;
            }
        }

        String lastError() {
            synchronized (lock) {
                return error;
            }
        }

        private void setError(String message) {
            synchronized (lock) {
                error = message;
            }
        }

        private String chooseId(CameraManager mgr, String[] ids) {
            try {
                for (String id : ids) {
                    Integer facing = mgr.getCameraCharacteristics(id)
                            .get(CameraCharacteristics.LENS_FACING);
                    if (facing != null && facing == CameraCharacteristics.LENS_FACING_BACK) {
                        return id;
                    }
                }
            } catch (Throwable ignored) { }
            return ids[0];
        }

        private void convert(final Image image) {
            try {
                final int w = image.getWidth();
                final int h = image.getHeight();
                final Image.Plane[] planes = image.getPlanes();

                final ByteBuffer yBuf = planes[0].getBuffer();
                final ByteBuffer uBuf = planes[1].getBuffer();
                final ByteBuffer vBuf = planes[2].getBuffer();

                final int yRow = planes[0].getRowStride();
                final int uvRow = planes[1].getRowStride();
                final int uvPix = planes[1].getPixelStride();

                final byte[] yData = new byte[yBuf.remaining()];
                final byte[] uData = new byte[uBuf.remaining()];
                final byte[] vData = new byte[vBuf.remaining()];
                yBuf.get(yData);
                uBuf.get(uData);
                vBuf.get(vData);

                final byte[] rgba = new byte[w * h * 4];
                int idx = 0;
                for (int j = 0; j < h; j++) {
                    final int yRowStart = j * yRow;
                    final int uvRowStart = (j >> 1) * uvRow;
                    final int destStart = (h - 1 - j) * w * 4;
                    for (int i = 0; i < w; i++) {
                        final int y = yData[yRowStart + i] & 0xff;
                        final int u = uData[uvRowStart + (i >> 1) * uvPix] & 0xff;
                        final int v = vData[uvRowStart + (i >> 1) * uvPix] & 0xff;

                        final int c = y - 16;
                        final int d = u - 128;
                        final int e = v - 128;

                        final int r = clamp((298 * c + 409 * e + 128) >> 8);
                        final int g = clamp((298 * c - 100 * d - 208 * e + 128) >> 8);
                        final int b = clamp((298 * c + 516 * d + 128) >> 8);

                        final int p = destStart + i * 4;
                        rgba[p] = (byte) r;
                        rgba[p + 1] = (byte) g;
                        rgba[p + 2] = (byte) b;
                        rgba[p + 3] = (byte) 0xff;
                    }
                }

                synchronized (lock) {
                    pixels = rgba;
                    width = w;
                    height = h;
                    serial++;
                }
            } catch (RuntimeException e) {
                setError("convert: " + e.getClass().getSimpleName() + ": " + e.getMessage());
            }
        }

        private final class Frames implements ImageReader.OnImageAvailableListener {
            @Override
            public void onImageAvailable(ImageReader reader) {
                Image image = null;
                try {
                    image = reader.acquireLatestImage();
                    if (image == null) {
                        return;
                    }
                    final Image im = image;
                    convertPool.execute(new Runnable() {
                        @Override
                        public void run() {
                            try {
                                convert(im);
                            } finally {
                                im.close();
                            }
                        }
                    });
                } catch (Throwable t) {
                    if (image != null) {
                        image.close();
                    }
                }
            }
        }

        private static int clamp(int v) {
            return v < 0 ? 0 : (v > 255 ? 255 : v);
        }

        private static String accessErrName(int reason) {
            switch (reason) {
                case CameraAccessException.CAMERA_ERROR: return "CAMERA_ERROR";
                case CameraAccessException.CAMERA_DISABLED: return "CAMERA_DISABLED";
                case CameraAccessException.CAMERA_DISCONNECTED: return "CAMERA_DISCONNECTED";
                case CameraAccessException.CAMERA_IN_USE: return "CAMERA_IN_USE";
                case CameraAccessException.MAX_CAMERAS_IN_USE: return "MAX_CAMERAS_IN_USE";
                default: return "reason=" + reason;
            }
        }

        private static String describeError(int code) {
            switch (code) {
                case CameraDevice.StateCallback.ERROR_CAMERA_DEVICE: return "ERROR_CAMERA_DEVICE";
                case CameraDevice.StateCallback.ERROR_CAMERA_DISABLED: return "ERROR_CAMERA_DISABLED";
                case CameraDevice.StateCallback.ERROR_CAMERA_IN_USE: return "ERROR_CAMERA_IN_USE";
                case CameraDevice.StateCallback.ERROR_CAMERA_SERVICE: return "ERROR_CAMERA_SERVICE";
                case CameraDevice.StateCallback.ERROR_MAX_CAMERAS_IN_USE: return "ERROR_MAX_CAMERAS_IN_USE";
                default: return "code=" + code;
            }
        }
    }
}