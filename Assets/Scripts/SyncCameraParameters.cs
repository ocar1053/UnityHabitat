using UnityEngine;

[ExecuteAlways]
public class SyncCameraParameters : MonoBehaviour
{
    void Update()
    {
        Camera mainCam = GetComponent<Camera>();
        if (mainCam == null) return;

        foreach (Transform child in transform)
        {
            Camera subCam = child.GetComponent<Camera>();
            if (subCam == null) continue;
            subCam.fieldOfView = mainCam.fieldOfView;
            subCam.aspect = mainCam.aspect;
            subCam.orthographic = mainCam.orthographic;
            subCam.orthographicSize = mainCam.orthographicSize;
            subCam.nearClipPlane = mainCam.nearClipPlane;
            subCam.farClipPlane = mainCam.farClipPlane;
            subCam.depth = mainCam.depth;
            subCam.clearFlags = mainCam.clearFlags;
            subCam.backgroundColor = mainCam.backgroundColor;
            subCam.cullingMask = mainCam.cullingMask;
            subCam.allowHDR = mainCam.allowHDR;
            subCam.allowMSAA = mainCam.allowMSAA;
            subCam.allowDynamicResolution = mainCam.allowDynamicResolution;
            subCam.usePhysicalProperties = mainCam.usePhysicalProperties;
            subCam.projectionMatrix = mainCam.projectionMatrix;

            // 
            if (mainCam.usePhysicalProperties)
            {
                subCam.iso = mainCam.iso;
                subCam.shutterSpeed = mainCam.shutterSpeed;
                subCam.aperture = mainCam.aperture;
                subCam.focusDistance = mainCam.focusDistance;
                subCam.bladeCount = mainCam.bladeCount;
                subCam.curvature = mainCam.curvature;
                subCam.barrelClipping = mainCam.barrelClipping;
                subCam.anamorphism = mainCam.anamorphism;
                subCam.focalLength = mainCam.focalLength;
                subCam.anamorphism = mainCam.anamorphism;
                subCam.sensorSize = mainCam.sensorSize;
                subCam.lensShift = mainCam.lensShift;
                subCam.gateFit = mainCam.gateFit;
                subCam.nearClipPlane = mainCam.nearClipPlane;
                subCam.farClipPlane = mainCam.farClipPlane;
                subCam.usePhysicalProperties = true;
            }
        }
    }
}
