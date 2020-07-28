//  
// Copyright (c) 2017 Vulcan, Inc. All rights reserved.  
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.
//

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class LocatableCameraUtils
{
    /// <summary>
    /// Helper method for pixel projection into Unity3D world space.
    /// This method return a Vector3 with direction: optical center of the camera to the pixel coordinate
    /// The method is based on: https://developer.microsoft.com/en-us/windows/mixed-reality/locatable_camera#pixel_to_application-specified_coordinate_system
    /// </summary>
    /// <param name="cameraToWorldMatrix">The camera to Unity world matrix.</param>
    /// <param name="projectionMatrix">Projection Matrix.</param>
    /// <param name="pixelCoordinates">The coordinate of the pixel that should be converted to world-space.</param>
    /// <param name="cameraResolution">The resolution of the image that the pixel came from.</param>
    /// <returns>Vector3 with direction: optical center to camera world-space coordinates</returns>
    public static Vector3 PixelCoordToWorldCoord(Matrix4x4 cameraToWorldMatrix, Matrix4x4 projectionMatrix, HoloLensCameraStream.Resolution cameraResolution, Vector2 pixelCoordinates)
    {
        pixelCoordinates = ConvertPixelCoordsToScaledCoords(pixelCoordinates, cameraResolution); // -1 to 1 coords

        float focalLengthX = projectionMatrix.GetColumn(0).x;
        float focalLengthY = projectionMatrix.GetColumn(1).y;
        float centerX = projectionMatrix.GetColumn(2).x;
        float centerY = projectionMatrix.GetColumn(2).y;

        // On Microsoft Webpage the centers are normalized 
        float normFactor = projectionMatrix.GetColumn(2).z;
        centerX = centerX / normFactor;
        centerY = centerY / normFactor;

        Vector3 dirRay = new Vector3((pixelCoordinates.x - centerX) / focalLengthX, (pixelCoordinates.y - centerY) / focalLengthY, 1.0f / normFactor); //Direction is in camera space
        Vector3 direction = new Vector3(Vector3.Dot(cameraToWorldMatrix.GetRow(0), dirRay), Vector3.Dot(cameraToWorldMatrix.GetRow(1), dirRay), Vector3.Dot(cameraToWorldMatrix.GetRow(2), dirRay));

        return direction;
    }

    public static Vector3 GetNormalOfPose(Matrix4x4 pose)
    {
        return new Vector3(Vector3.Dot(Vector3.forward, pose.GetRow(0)), Vector3.Dot(Vector3.forward, pose.GetRow(1)), Vector3.Dot(Vector3.forward, pose.GetRow(2)));
    }

    public static Quaternion GetRotationFacingView(Matrix4x4 viewTransform)
    {
        return Quaternion.LookRotation(-viewTransform.GetColumn(2), viewTransform.GetColumn(1));
    }

    public static Matrix4x4 BytesToMatrix(byte[] inMatrix)
    {
        //Then convert the floats to a matrix.
        Matrix4x4 outMatrix = new Matrix4x4
        {
            m00 = inMatrix[0],
            m01 = inMatrix[1],
            m02 = inMatrix[2],
            m03 = inMatrix[3],
            m10 = inMatrix[4],
            m11 = inMatrix[5],
            m12 = inMatrix[6],
            m13 = inMatrix[7],
            m20 = inMatrix[8],
            m21 = inMatrix[9],
            m22 = inMatrix[10],
            m23 = inMatrix[11],
            m30 = inMatrix[12],
            m31 = inMatrix[13],
            m32 = inMatrix[14],
            m33 = inMatrix[15]
        };
        return outMatrix;
    }

    /// <summary>
    /// Helper method for converting into UnityEngine.Matrix4x4
    /// </summary>
    /// <param name="matrixAsArray"></param>
    /// <returns></returns>
    public static Matrix4x4 ConvertFloatArrayToMatrix4x4(float[] matrixAsArray)
    {
        //There is probably a better way to be doing this but System.Numerics.Matrix4x4 is not available 
        //in Unity and we do not include UnityEngine in the plugin.
        Matrix4x4 m = new Matrix4x4();
        m.m00 = matrixAsArray[0];
        m.m01 = matrixAsArray[1];
        m.m02 = matrixAsArray[2];
        m.m03 = matrixAsArray[3];
        m.m10 = matrixAsArray[4];
        m.m11 = matrixAsArray[5];
        m.m12 = matrixAsArray[6];
        m.m13 = matrixAsArray[7];
        m.m20 = matrixAsArray[8];
        m.m21 = matrixAsArray[9];
        m.m22 = matrixAsArray[10];
        m.m23 = matrixAsArray[11];
        m.m30 = matrixAsArray[12];
        m.m31 = matrixAsArray[13];
        m.m32 = matrixAsArray[14];
        m.m33 = matrixAsArray[15];

        return m;
    }

    /// <summary>
    /// Converts pixel coordinates to screen-space coordinates that span from -1 to 1 on both axes.
    /// This is the format that is required to determine the z-depth of a given pixel taken by the HoloLens camera.
    /// </summary>
    /// <param name="pixelCoords">The coordinate of the pixel that should be converted to screen-space.</param>
    /// <param name="res">The resolution of the image that the pixel came from.</param>
    /// <returns>A 2D vector with values between -1 and 1, representing the left-to-right scale within the image dimensions.</returns>
    static Vector2 ConvertPixelCoordsToScaledCoords(Vector2 pixelCoords, HoloLensCameraStream.Resolution resolution)
    {
        float halfWidth = (float)resolution.width / 2f;
        float halfHeight = (float)resolution.height / 2f;

        //Translate registration to image center;
        pixelCoords.x -= halfWidth;
        pixelCoords.y -= halfHeight;

        //Scale pixel coords to percentage coords (-1 to 1)
        pixelCoords = new Vector2(pixelCoords.x / halfWidth, pixelCoords.y / halfHeight * -1f);

        return pixelCoords;
    }
}
