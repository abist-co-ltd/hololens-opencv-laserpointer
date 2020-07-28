using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils.Helper;
using OpenCVForUnity.UnityUtils;
using Rect = OpenCVForUnity.CoreModule.Rect;
using HoloLensWithOpenCVForUnity.UnityUtils.Helper;
using TMPro;
using Microsoft.MixedReality.Toolkit;

#if ENABLE_WINMD_SUPPORT
using Windows.Media.Devices.Core;
#endif

namespace HoloLensWithOpenCVForUnityExample
{
    /// <summary>
    /// HoloLens Red Area Detection Example
    /// An example of image processing (Red area detection) using OpenCVForUnity on Hololens.
    /// Comic filter processing is implemented when running on Unity.
    /// </summary>
    [RequireComponent(typeof(HololensCameraStreamToMatHelper))]
    public class HololensLaserPointerDetection: MonoBehaviour
    {

        [SerializeField, TooltipAttribute("Unprojection Calibration: Set offset of unprojected pixel coordinates; Offset unit is meter.")]
        private Vector2 unprojectionOffset = new Vector2(0.0f, -0.05f);
        [SerializeField, TooltipAttribute("Centered portion of the image used as detection area. Factors for x and y coordinates between 0 and 1.")]
        private Vector2 detectionArea = new Vector2(0.5f, 0.5f);
        [SerializeField, TooltipAttribute("Capture data display flag for debugging.")]
        private bool isVisibleImage = false;
        [SerializeField, TooltipAttribute("Enable display of processed frames per second for performance measurements.")]
        private bool showFPS = false;
        [SerializeField, TooltipAttribute("Use fast pointer detection algorithm optimized for performance rather than accuracy.")]
        private bool fastDetection = false;

        /// <summary>
        /// The texture.
        /// </summary>
        Texture2D texture;

        /// <summary>
        /// The quad renderer.
        /// </summary>
        Renderer quad_renderer;

        /// <summary>
        /// The web cam texture to mat helper.
        /// </summary>
        HololensCameraStreamToMatHelper webCamTextureToMatHelper;

        // Mask to RayCast only the spatial recognition layer
        readonly int SpatialAwarnessLayerMask = 1 << 31;

        GameObject redSphere;
        TextMeshPro toolTipText;

        readonly static Queue<Action> ExecuteOnMainThread = new Queue<Action>();

        private float timeSpan = 0.0f;    // time keeping
        private float prevTime = -1.0f;    // used for
        private int numFrames = 0;        // calculating FPS 

        // Use this for initialization
        protected void Start()
        {
            webCamTextureToMatHelper = gameObject.GetComponent<HololensCameraStreamToMatHelper>();
#if ENABLE_WINMD_SUPPORT
            webCamTextureToMatHelper.frameMatAcquired += OnFrameMatAcquired;
#endif
            if (!isVisibleImage)
            {
                this.gameObject.transform.localScale = new Vector3(0.0f, 0.0f, 0.0f);
            }
            redSphere = GameObject.Find("Sphere");
            toolTipText = redSphere.transform.Find("ToolTip/Label").GetComponent<TextMeshPro>();
            webCamTextureToMatHelper.Initialize();
        }

        /// <summary>
        /// Raises the web cam texture to mat helper initialized event.
        /// </summary>
        public void OnWebCamTextureToMatHelperInitialized()
        {
            Debug.Log("OnWebCamTextureToMatHelperInitialized");

            Mat webCamTextureMat = webCamTextureToMatHelper.GetMat();


#if ENABLE_WINMD_SUPPORT
            // HololensCameraStream always returns image data in BGRA format.
            texture = new Texture2D (webCamTextureMat.cols (), webCamTextureMat.rows (), TextureFormat.BGRA32, false);
#else
            texture = new Texture2D(webCamTextureMat.cols(), webCamTextureMat.rows(), TextureFormat.RGBA32, false);
#endif

            texture.wrapMode = TextureWrapMode.Clamp;

            Debug.Log("Screen.width " + Screen.width + " Screen.height " + Screen.height + " Screen.orientation " + Screen.orientation);

            Matrix4x4 projectionMatrix;

#if ENABLE_WINMD_SUPPORT
            projectionMatrix = webCamTextureToMatHelper.GetProjectionMatrix ();
#else

            //This value is obtained from PhotoCapture's TryGetProjectionMatrix() method.I do not know whether this method is good.
            //Please see the discussion of this thread.Https://forums.hololens.com/discussion/782/live-stream-of-locatable-camera-webcam-in-unity
            projectionMatrix = Matrix4x4.identity;
            projectionMatrix.m00 = 2.31029f;
            projectionMatrix.m01 = 0.00000f;
            projectionMatrix.m02 = 0.09614f;
            projectionMatrix.m03 = 0.00000f;
            projectionMatrix.m10 = 0.00000f;
            projectionMatrix.m11 = 4.10427f;
            projectionMatrix.m12 = -0.06231f;
            projectionMatrix.m13 = 0.00000f;
            projectionMatrix.m20 = 0.00000f;
            projectionMatrix.m21 = 0.00000f;
            projectionMatrix.m22 = -1.00000f;
            projectionMatrix.m23 = 0.00000f;
            projectionMatrix.m30 = 0.00000f;
            projectionMatrix.m31 = 0.00000f;
            projectionMatrix.m32 = -1.00000f;
            projectionMatrix.m33 = 0.00000f;
#endif
            quad_renderer = gameObject.GetComponent<Renderer>() as Renderer;
            quad_renderer.sharedMaterial.SetTexture("_MainTex", texture);
            quad_renderer.sharedMaterial.SetMatrix("_CameraProjectionMatrix", projectionMatrix);

            float halfOfVerticalFov = Mathf.Atan(1.0f / projectionMatrix.m11);
            float aspectRatio = (1.0f / Mathf.Tan(halfOfVerticalFov)) / projectionMatrix.m00;
            Debug.Log("halfOfVerticalFov " + halfOfVerticalFov);
            Debug.Log("aspectRatio " + aspectRatio);
        }

        /// <summary>
        /// Raises the web cam texture to mat helper disposed event.
        /// </summary>
        public void OnWebCamTextureToMatHelperDisposed()
        {
            Debug.Log("OnWebCamTextureToMatHelperDisposed");

            lock (ExecuteOnMainThread)
            {
                ExecuteOnMainThread.Clear();
            }
        }

        /// <summary>
        /// Raises the web cam texture to mat helper error occurred event.
        /// </summary>
        /// <param name="errorCode">Error code.</param>
        public void OnWebCamTextureToMatHelperErrorOccurred(WebCamTextureToMatHelper.ErrorCode errorCode)
        {
            Debug.Log("OnWebCamTextureToMatHelperErrorOccurred " + errorCode);
        }

        /// <summary>
        /// Fast version: Find brightest red pixel in a masked image 
        /// Use Laplacian filter to detect areas where the red color component has changed more than blue or green
        /// </summary>
        /// <param name="bgraMat">Image data in BGRA format</param>
        /// <param name="mask">mask of relavant areas to search</param>
        /// <param name="originalWidth">width of the original (non-cropped) image used for parameter tuning</param>
        private Point FastFindBrightestPoint(Mat bgraMat, Mat mask, int originalWidth)
        {

            var ksize = originalWidth > 1600 ? 15 : 11;
            Mat blurred = new Mat();
            Imgproc.blur(bgraMat, blurred, new Size(ksize, ksize));

            Mat redBlurred = new Mat();
            Core.extractChannel(blurred, redBlurred, 2);

            Mat red = new Mat();
            Core.extractChannel(bgraMat, red, 2);

            red -= redBlurred;

            var minMax = Core.minMaxLoc(red, mask);    // get pixel with strongest contrast towards red among reddish things

            // experimental size detection: only accept small objects as laser pointer
            //Scalar zero = new Scalar(0);
            //Mat fillMask = new Mat(red.height(), red.width(), CvType.CV_8UC1, zero);
            //Rect rect = new Rect();
            //Imgproc.floodFill(red, fillMask, minMax.maxLoc, zero, rect, zero /*new Scalar(minMax.maxVal/2)*/, new Scalar(minMax.maxVal), 4 | Imgproc.FLOODFILL_FIXED_RANGE);
            //if (rect.area() > 1)
            //{
            //    return null;
            //}

            blurred.Dispose();
            redBlurred.Dispose();
            red.Dispose();

            return minMax.maxLoc;
        }


        /// <summary>
        /// Find brightest red pixel in a masked image 
        /// Use Laplacian filter to detect areas where the red color component has changed more than blue or green
        /// </summary>
        /// <param name="bgraMat">Image data in BGRA format</param>
        /// <param name="mask">mask of relavant areas to search</param>
        /// <param name="originalWidth">width of the original (non-cropped) image used for parameter tuning</param>
        private Point FindBrightestPoint(Mat bgraMat, Mat mask, int originalWidth)
        {
            Mat channel = new Mat(bgraMat.height(), bgraMat.width(), CvType.CV_8UC1);
            Mat blueContrast = new Mat(bgraMat.height(), bgraMat.width(), CvType.CV_16SC1);
            Mat greenContrast = new Mat(bgraMat.height(), bgraMat.width(), CvType.CV_16SC1);
            Mat redContrast = new Mat(bgraMat.height(), bgraMat.width(), CvType.CV_16SC1);
            Mat sum = new Mat(bgraMat.height(), bgraMat.width(), CvType.CV_16SC1);

            var kernelSize = originalWidth > 1600 ? 5 : 3; 
            Core.extractChannel(bgraMat, channel, 0);
            Imgproc.Laplacian(channel, blueContrast, CvType.CV_16S, kernelSize, 1, 0, Core.BORDER_REPLICATE);    // calculate contrast in blue channel to detect sudden increase of red component
            Core.extractChannel(bgraMat, channel, 1);
            Imgproc.Laplacian(channel, greenContrast, CvType.CV_16S, kernelSize, 1, 0, Core.BORDER_REPLICATE);   // calculate contrast in green channel to detect sudden increase of red component
            Core.extractChannel(bgraMat, channel, 2);
            Imgproc.Laplacian(channel, redContrast, CvType.CV_16S, kernelSize, 1, 0, Core.BORDER_REPLICATE);     // calculate contrast in red channel to detect sudden increase of red component

            channel.convertTo(sum, CvType.CV_16S);
            sum *= 2;                                  // give absolute brightness of red component double weight
            sum -= 3*redContrast;                      // boost weight of pixels with a negative red curvature, i.e. whose neighbors are less red.
            sum += blueContrast;                       // oposite weights for changes in blue and green contrast, to give pixels a stronger boost whose red component changed but blue and green remained constant 
            sum += greenContrast;
            
            var minMax = Core.minMaxLoc(sum, mask);    // get pixel with strongest contrast towards red among reddish things
            
            sum.Dispose();
            redContrast.Dispose();
            greenContrast.Dispose();
            blueContrast.Dispose();
            channel.Dispose();

            return minMax.maxLoc;
        }

        /// <summary>
        /// Extract detection area from camera image 
        /// </summary>
        /// <param name="img">Image data</param>
        private Mat getROI(Mat img)
        {
            int w = img.width();
            int h = img.height();
            int ROIwidth = Math.Max(0, Math.Min(w, ((int) Math.Round(w * detectionArea.x * 0.5 )) * 2));
            int ROIheight = Math.Max(0, Math.Min(h, ((int)Math.Round(h * detectionArea.y * 0.5)) * 2));
            Rect ROI = new Rect((w - ROIwidth) / 2, (h - ROIheight) / 2, ROIwidth, ROIheight);

            return new Mat(img, ROI);
        }

        /// <summary>
        /// Find location of red laser pointer in image 
        /// </summary>
        /// <param name="bgraMat">Image data in BGRA format</param>
        private Point FindLaserPointer(Mat bgraMat)
        {
            Mat croppedBgra = getROI(bgraMat);

            if (!fastDetection && bgraMat.width() > 1600)
            {
                Imgproc.GaussianBlur(croppedBgra, croppedBgra, new Size(3, 3), 0);    // apply a gentle blur to high resultion images to remove noise
            }
            
            Mat hsvMat = new Mat(croppedBgra.height(), croppedBgra.width(), CvType.CV_8UC3);
            Mat maskMat1 = new Mat(croppedBgra.height(), croppedBgra.width(), CvType.CV_8UC1);
            Mat maskMat2 = new Mat(croppedBgra.height(), croppedBgra.width(), CvType.CV_8UC1);

            // Color conversion from BGRA to HSV
            Imgproc.cvtColor(croppedBgra, hsvMat, Imgproc.COLOR_BGRA2BGR);
            Imgproc.cvtColor(hsvMat, hsvMat, Imgproc.COLOR_BGR2HSV);

            // Acquire a mask image of reddish pixels using the inRange method. 
            // Red is separated in two areas in the HSV color space
            Scalar s_min = new Scalar(0, 30, 220);
            Scalar s_max = new Scalar(10, 240, 255);
            Core.inRange(hsvMat, s_min, s_max, maskMat1);
            s_min = new Scalar(170, 30, 220);
            s_max = new Scalar(180, 240, 255);
            Core.inRange(hsvMat, s_min, s_max, maskMat2);

            maskMat1 |= maskMat2;

            Point point = null;
            if (Core.countNonZero(maskMat1) > 0)
            {
                point = fastDetection ? FastFindBrightestPoint(croppedBgra, maskMat1, bgraMat.width()) : FindBrightestPoint(croppedBgra, maskMat1, bgraMat.width());
                if (point != null)
                {
                    point.x += (bgraMat.width() - croppedBgra.width()) / 2;     // correct detection coordinates to original full image
                    point.y += (bgraMat.height() - croppedBgra.height()) / 2;
                }
            }

            croppedBgra.Dispose();
            hsvMat.Dispose();
            maskMat1.Dispose();
            maskMat2.Dispose();

            return point;

            // disabled red area size detection because it is too slow
            /*  
            Mat labels = new Mat();
            Mat stats = new Mat();
            Mat centroids = new Mat();

            var nLabels = Imgproc.connectedComponentsWithStats(maskMat1, labels, stats, centroids);
            
            
            for (int label = 1; label < nLabels; label++)
            {
                var area = stats.get(label, Imgproc.CC_STAT_AREA)[0];
                if (area < 25)
                {
                    var centroid = new Point(centroids.get(label, 0)[0], centroids.get(label, 1)[0]);
                    hsvMat.Dispose();
                    maskMat1.Dispose();
                    maskMat2.Dispose();
                    grayMat.Dispose();
                    labels.Dispose();
                    stats.Dispose();
                    centroids.Dispose();
                    return centroid;
                }
            }
            //grayMat.Dispose();
            // labels.Dispose();
            // stats.Dispose();
            // centroids.Dispose();
            */

        }


        /// <summary>
        /// return frames per second processed by laserpointer detection on average over the last 3 seconds
        /// </summary>
        private float getFPS()
        {
            var now = Time.time;
            if (prevTime < 0)  // if this is the first time measurment...
            {                  // ...return 0
                prevTime = now;
                return 0.0f;
            }

            if (timeSpan > 3 && numFrames > 0)  // adjust time span or number of frames wrt. to 3 seconds measurement window
            {
                timeSpan *= (numFrames - 1.0f) / numFrames;
            } else
            {
                numFrames++;
            }
            timeSpan += now - prevTime;
            prevTime = now;
            return numFrames / timeSpan;
        }

#if ENABLE_WINMD_SUPPORT
        private Vector3 raycast(Point cameraPoint, Matrix4x4 cameraToWorldMatrix, CameraIntrinsics camIntrinsics)
        {
            // Convert the first point of the detected contour to Unity world coordinates
            //            	Point[] countoursPoint = contours[0].toArray();
            //              Windows.Foundation.Point orgpoint = new Windows.Foundation.Point(countoursPoint[0].x, countoursPoint[0].y);
            Windows.Foundation.Point orgpoint = new Windows.Foundation.Point(cameraPoint.x, cameraPoint.y);

            // Unprojects pixel coordinates into a camera space ray from the camera origin, expressed as a X, Y coordinates on a plane one meter from the camera.
            System.Numerics.Vector2 result = camIntrinsics.UnprojectAtUnitDepth(orgpoint);
            // manual calibration: correct y-axes by 5 cm to get better unprojection accurracy
            UnityEngine.Vector3 pos = new UnityEngine.Vector3(result.X + unprojectionOffset.x, result.Y + unprojectionOffset.y, 1.0f);

            // Convert from camera coordinates to world coordinates and RayCast in that direction.
            // convert right-handed coord-sys to Unity left-handed coord-sys  
            Quaternion rotation = Quaternion.LookRotation(-cameraToWorldMatrix.GetColumn(2), cameraToWorldMatrix.GetColumn(1));
            Vector3 layForward = Vector3.Normalize(rotation * pos);
            Vector3 cameraPos =  cameraToWorldMatrix.GetColumn(3);

            RaycastHit hit = new RaycastHit();
            return Physics.Raycast(cameraPos, layForward, out hit, Mathf.Infinity, this.SpatialAwarnessLayerMask) ?
                hit.point : cameraPos + layForward * 5.0f;
        }

        public void OnFrameMatAcquired (Mat bgraMat, Matrix4x4 projectionMatrix, Matrix4x4 cameraToWorldMatrix, CameraIntrinsics camIntrinsics)
        {
            // Implement the HoloLens process here. -->

            Vector3 hitPoint = new Vector3(0,0,0);
            var laserPointerPosition = FindLaserPointer(bgraMat);
            if (laserPointerPosition != null) 
            {
                hitPoint = raycast(laserPointerPosition, cameraToWorldMatrix, camIntrinsics);
            }
            
            // Implement the HoloLens process here. <--

            Enqueue(() => {

                var fps = showFPS ? getFPS() : 0;

                if (laserPointerPosition != null && (redSphere.transform.position - hitPoint).magnitude >= 0.01) 
                {
                    redSphere.transform.position = hitPoint;
                    Vector3 cameraPos = cameraToWorldMatrix.GetColumn(3);
                    toolTipText.text = (hitPoint - cameraPos).magnitude.ToString("0.00") + " m" + (showFPS ? fps.ToString(", 0.0 FPS") : "");
               }

                if (!(webCamTextureToMatHelper.IsPlaying() && isVisibleImage))
                {
                    bgraMat.Dispose();
                    return;
                }

                Utils.fastMatToTexture2D(bgraMat, texture);
                bgraMat.Dispose();

                Matrix4x4 worldToCameraMatrix = cameraToWorldMatrix.inverse;
                quad_renderer.sharedMaterial.SetMatrix ("_WorldToCameraMatrix", worldToCameraMatrix);

                // Position the canvas object slightly in front
                // of the real world web camera.
                Vector3 position = cameraToWorldMatrix.GetColumn(3) - cameraToWorldMatrix.GetColumn(2) * 2.2f;

                // Rotate the canvas object so that it faces the user.
                Quaternion rotation = Quaternion.LookRotation (-cameraToWorldMatrix.GetColumn (2), cameraToWorldMatrix.GetColumn (1));

                gameObject.transform.position = position;
                gameObject.transform.rotation = rotation;
            });
        }

        private void Update()
        {
            lock (ExecuteOnMainThread)
            {
                while (ExecuteOnMainThread.Count > 0)
                {
                    ExecuteOnMainThread.Dequeue().Invoke();
                }
            }
        }

        private void Enqueue(Action action)
        {
            lock (ExecuteOnMainThread)
            {
                ExecuteOnMainThread.Enqueue(action);
            }
        }

#else

        // Update is called once per frame
        void Update()
        {
            if (webCamTextureToMatHelper.IsPlaying() && webCamTextureToMatHelper.DidUpdateThisFrame())
            {
                Mat rgbaMat = webCamTextureToMatHelper.GetMat();
                Vector3 hitPoint = new Vector3(0,0,0);
                var laserPointerPosition = FindLaserPointer(rgbaMat);
                if (laserPointerPosition != null) 
                {
                    // Unprojects pixel coordinates into a camera space ray from the camera origin, expressed as a X, Y coordinates on a plane one meter from the camera.
                    UnityEngine.Vector3 pos = new UnityEngine.Vector3((float)((laserPointerPosition.x / rgbaMat.width()) * Screen.currentResolution.width),
                                                                (float)((laserPointerPosition.y / rgbaMat.height()) * Screen.currentResolution.height), 1.0f);
                    UnityEngine.Vector3 toPos = Camera.main.ScreenToWorldPoint(pos);

                    if ((redSphere.transform.position - toPos).magnitude >= 0.1)
                    {
                        redSphere.transform.position = toPos;
                        toolTipText.text = (toPos - Camera.main.transform.position).magnitude.ToString("0.00") + " m";
                    }
                }
                Utils.fastMatToTexture2D(rgbaMat, texture);
            }

            if (webCamTextureToMatHelper.IsPlaying() && isVisibleImage)
            {
                Matrix4x4 cameraToWorldMatrix = webCamTextureToMatHelper.GetCameraToWorldMatrix();
                Matrix4x4 worldToCameraMatrix = cameraToWorldMatrix.inverse;

                quad_renderer.sharedMaterial.SetMatrix("_WorldToCameraMatrix", worldToCameraMatrix);

                // Position the canvas object slightly in front
                // of the real world web camera.
                Vector3 position = cameraToWorldMatrix.GetColumn(3) - cameraToWorldMatrix.GetColumn(2) * 2.2f;

                // Rotate the canvas object so that it faces the user.
                Quaternion rotation = Quaternion.LookRotation(-cameraToWorldMatrix.GetColumn(2), cameraToWorldMatrix.GetColumn(1));

                gameObject.transform.position = position;
                gameObject.transform.rotation = rotation;
            }
        }
#endif

        /// <summary>
        /// Raises the destroy event.
        /// </summary>
        void OnDestroy()
        {
#if ENABLE_WINMD_SUPPORT
            webCamTextureToMatHelper.frameMatAcquired -= OnFrameMatAcquired;
#endif
            webCamTextureToMatHelper.Dispose();
        }
    }
}