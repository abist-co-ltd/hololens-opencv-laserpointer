# hololens-opencv-laserpointer

Laser pointer detection sample application, demonstrating a
minimal setup for 

* HoloLens camera image processing using OpenCV for Unity 
* unprojection of camera pixels to 3D coordinates in the world space.

Tested on HoloLens 2 and Gen 1.

[![Alt Laser Pointer Detection](/Document/LaserPointerDetection.PNG)](https://www.youtube.com/watch?v=zsmA6J5sJh4)

## Dependencies

 * Unity 2019.4.x LTS + UWP Build Support + Visual Studio 2019
 * MRTK 2.4.0 
 * OpenCV for Unity 2.3.9
 
 Basic setup of HoloLens dev environment (Emulator is not required): [Install the tools](https://docs.microsoft.com/en-us/windows/mixed-reality/install-the-tools) 

## Setup

After cloning, open the project in Unity and import the OpenCV for Unity asset.<br>
Setup OpenCVForUnity: Tools > OpenCV for Unity > Set Plugin Import Settings

**Note**: MRTK 2.4.0 package files are already included and configured in this repository.

## Usage
### Build and Deployment
For HoloLens 2, configure Unity Build Settings as follows:

![Unity Build Settings](/Document/UnityBuildSettings.PNG)

Build and deploy the application to HoloLens as described on
[Building your application to your HoloLens 2](https://docs.microsoft.com/en-us/windows/mixed-reality/mr-learning-base-02#building-your-application-to-your-hololens-2)


### SampleScene Structure and Configuration

In Unity, open <tt>SampleScene</tt> and select <tt>ImageView</tt>.
Its components <tt>HoloLens Camera Stream To Mat Helper</tt> and <tt>Hololens Laser Pointer Detection</tt> implement the detection of a laser pointer in the camera image. 

![ImageView Components](/Document/ImageViewInspector.PNG)

<tt>HololensLaserPointerDetection.cs</tt> detects a red laser pointer using OpenCV for Unity, determines its 3D coordinates, and displays the distance in a tooltip.

**Note**: To aid debugging, camera images are retrieved from the PC's webcam instead of HoloLens, when executed in the Unity editor.

#### HoloLens Camera Stream To Mat Helper - Options 

The original version of this component is available on [HoloLens With OpenCVForUnity Example](https://github.com/EnoxSoftware/HoloLensWithOpenCVForUnityExample)

Adjust the options of <tt>HoloLens Camera Stream To Mat Helper</tt> 
to balance accurracy and performance.

| Option | Values |
|--------|--------|
| Requested Width  | 1280, 1920, etc |
| Requested Height | 720, 1080, etc |
| Requested FPS    | 15, 30, etc |

See [Locatable camera](https://docs.microsoft.com/en-us/windows/mixed-reality/locatable-camera) for available resolutions and frame rates.

#### Hololens Laser Pointer Detection - Options 

| Option | Values | Explanation |
|--------|--------|-------------|
| Unprojection Offset  | (float x, float y) | Calibration of 2D -> 3D unprojection of detected laser pointer. Adjust to reduce unprojection error.
| Detection Area | (float x, float y) | Portion of the camera image used for laser pointer detection. x and y must be between 0 and 1. Smaller values increase speed but narrow the detection area.
| Is Visible Image    | bool | Enables a panel showing the real time camera image for debugging purposes in front of the user.|
|Show FPS | bool | Enables the output of frames per seconds the laser pointer detection algorithm processes.
| Fast Detection | bool | Enables a detection algorithm favoring performance over accuracy.| 

#### Additional Customization
Depending on the environment and type of laser pointer, it may be necessary to adjust the color range for the detected laser light in <tt>HololensLaserPointerDetection.cs</tt> method <tt>FindLaserPointer</tt>

```
            // Acquire a mask image of reddish pixels using the inRange method. 
            // Red is separated in two areas in the HSV color space
            Scalar s_min = new Scalar(0, 30, 220);
            Scalar s_max = new Scalar(10, 240, 255);
            Core.inRange(hsvMat, s_min, s_max, maskMat1);
            s_min = new Scalar(170, 30, 220);
            s_max = new Scalar(180, 240, 255);
            Core.inRange(hsvMat, s_min, s_max, maskMat2);
```

See the class and sequence diagrams in the [Document](/Document/) folder
for information on the static structure and dynamic behavior of the code.


## Known Issues and Limitations

This is work in progress. Known issues are listed in **Issues** of this repository. Please help to improve this work by reporting bugs and areas of improvement. Ideas for laser pointer detection are particulary welcome!

Currently, detection is limited to a single red laser pointer. Red and shiny objects other than laser pointers are likely to be detected as well (false positives). 


## Related Information

* [HoloLens
で OpenCV をどう使うか
レーザーポインター
認識で試してみた](/Document/200729HoloLensOpenCVLaserpointer.pdf)<br>
Lightning Talk held at [Tokyo HoloLens ミートアップ vol.21](https://hololens.connpass.com/event/181938/), 2020-07-29 (in Japanese)
* https://docs.microsoft.com/en-us/windows/mixed-reality/locatable-camera
* https://docs.microsoft.com/en-us/uwp/api/windows.media.devices.core.cameraintrinsics.unprojectatunitdepth


## Copyright

This code is provided by the [AR Team](https://ar.abist-ai.com/) of [ABIST AI Solution](https://www.abist.co.jp/service/AI_solution.html) under Apache License Version 2.0. It is based on the following source code:

 * VulcanTechnologies [HoloLensCameraStream for Unity](https://github.com/VulcanTechnologies/HoloLensCameraStream) (Apache License Version 2.0)
    * Source files  from [HoloLensCameraStream for Unity](https://github.com/VulcanTechnologies/HoloLensCameraStream) are included to
    Assets/Script/HoloLensCameraStream/ with the the following modifications: 

      1.  Code referring to Windows Runtime API is wrapped by <br> <tt>#if ENABLE_WINMD_SUPPORT ... #endif</tt> <br>
      to avoid compile errors in the Unity editor.
      2. Added method <tt>GetCameraIntrinsics</tt> returning the camera intrinsics object of a video media frame.

       | file name | modification a) | modification b) |
       |-----------|---------------|---------------|
       |CameraParameters.cs||
       |CapturePixelFormat.cs||
       |LocatableCameraUtils.cs||
       |Resolution.cs||
       |ResultType.cs||
       |VideoCapture.cs| ○|
       |VideoCaptureResult.cs||
       |VideoCaptureSample.cs| ○ | ○ |

       
 * EnoxSoftware [HoloLens With OpenCVForUnity Example](https://github.com/EnoxSoftware/HoloLensWithOpenCVForUnityExample)

    * Source file <tt>HololensCameraStreamToMatHelper.cs</tt> from [HoloLens With OpenCVForUnity Example](https://github.com/EnoxSoftware/HoloLensWithOpenCVForUnityExample) is included to
    Assets/Script/HoloLensWithOpenCVForUnityExample/ with the following modifications:
      * Retrieve <tt>CameraIntrinsics</tt> from <tt>VidoCaptureSample</tt> object and pass it to <tt>FameMatAcquiredCallback</tt>
   * The structure of <tt>HololensLaserPointerDetection.cs</tt> is inspired by <tt>HoloLensComicFilterExample.cs</tt> from [HoloLens With OpenCVForUnity Example](https://github.com/EnoxSoftware/HoloLensWithOpenCVForUnityExample)


* Microsoft [MixedRealityToolkit-Unity](https://github.com/microsoft/MixedRealityToolkit-Unity) (MIT License)


 
