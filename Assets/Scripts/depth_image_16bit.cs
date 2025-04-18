using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections;
using System.Threading.Tasks;
using Unity.Collections;
using System.Threading;
using Hjg.Pngcs;
using System.IO;


[RequireComponent(typeof(Camera))]
public class DepthRosbridge16Bit : MonoBehaviour
{
    [Header("Shader Setup")]
    public Shader uberReplacementShader;

    [Header("ROS Setup")]
    public ConnectRosBridge connectRos;
    public string DepthTopic;
    public string frame_id = "camera";

    [Header("Depth Settings")]
    public int depthWidth = 1280;
    public int depthHeight = 720;
    public float publishHz = 4f; 

    [SerializeField] Camera depthCam;
    private RenderTexture depthRT;
    // Use a 16-bit texture to store the converted depth values.
    private Texture2D depthTex;

    public GameObject lineRendererObject;
    private LineRenderer lineRenderer;

    void Start()
    {
        if (lineRendererObject != null)
        {
            lineRenderer = lineRendererObject.GetComponent<LineRenderer>();
            lineRenderer.sortingLayerName = "IgnoreDepthRendering";
        }

        depthCam = GetComponent<Camera>();
        depthCam.allowMSAA = false;
        depthCam.allowHDR = false;
        depthCam.cullingMask = LayerMask.GetMask("Default");

        if (!uberReplacementShader)
            uberReplacementShader = Shader.Find("Hidden/UberReplacement");

        // Setup camera with the replacement shader.
        SetupCameraWithReplacementShader(depthCam, uberReplacementShader, 6, Color.white);

        // Use a RenderTexture with RFloat format to capture depth (as float).
        depthRT = new RenderTexture(depthWidth, depthHeight, 24, RenderTextureFormat.RFloat);
        depthRT.Create();
        depthCam.targetTexture = depthRT;

        // Create a Texture2D with a 16-bit format (each pixel stores one 16-bit value).
        depthTex = new Texture2D(depthWidth, depthHeight, TextureFormat.R16, false);

     
        AdvertiseTopic();
        StartCoroutine(CaptureDepthCoroutine());
        
    }

    private IEnumerator CaptureDepthCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f/ publishHz);
            depthCam.Render();

            AsyncGPUReadback.Request(depthRT, 0, req =>
            {
                if (req.hasError)
                {
                    Debug.LogError("[DepthImagePNG] GPU readback error!");
                    return;
                }

                var data = req.GetData<float>();
                ProcessAndPublishDepth(data);
            });
        }
    }

    // Converts the depth (in meters) to a 16-bit image (in millimeters)
    // then uses PNG encoding to compress it.
    private void ProcessAndPublishDepth(NativeArray<float> data)
    {
        // int centerX = depthWidth / 2;
        // int centerY = depthHeight / 2;
        // int centerIndex = centerY * depthWidth + centerX;
        // float centerDepth = data[centerIndex];
        // Debug.Log($"Center pixel depth: {centerDepth:F3} meters ({centerDepth * 1000:F0} mm)");
        // Allocate a ushort array to store 16-bit depth values.
        ushort[] rawUShorts = new ushort[depthWidth * depthHeight];
        Parallel.For(0, depthHeight, y =>
        {
            for (int x = 0; x < depthWidth; x++)
            {
                int index = y * depthWidth + x;
                float depthValue = data[index];
                // Convert depth (meters) to millimeters.
                // Clamp to the maximum value of ushort.
                rawUShorts[index] = (ushort)Mathf.Clamp(depthValue * 1000.0f, 0, 65535);
            }
        });

        byte[] pngBytes = EncodeTo16BitPng(rawUShorts, depthWidth, depthHeight);

        // Publish to ROS in the background.
        Task.Run(() => PublishDepthImage(DepthTopic, pngBytes, frame_id));
    }

    // Publishes the PNG data as a JSON message.
    public void PublishDepthImage(string topic, byte[] imagebytes, string frame_id)
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string imageString = "[" + string.Join(",", imagebytes) + "]";

        string publishMessage = $@"{{
            ""op"": ""publish"",
            ""topic"": ""{topic}"",
            ""msg"": {{
                ""header"": {{
                    ""stamp"": {{
                        ""secs"": {timestamp / 1000},
                        ""nsecs"": {(timestamp % 1000) * 1000000}
                    }},
                    ""frame_id"": ""{frame_id}""
                }},
                ""format"": ""16UC1; compressedDepth"",
                ""data"": {imageString}
            }}
        }}";

        connectRos.ws.Send(publishMessage);
    }

    private void AdvertiseTopic()
    {
        string advertiseMessage = $@"{{
            ""op"": ""advertise"",
            ""topic"": ""{DepthTopic}"",
            ""type"": ""sensor_msgs/msg/CompressedImage""
        }}";
        connectRos.ws.Send(advertiseMessage);
    }

    static private void SetupCameraWithReplacementShader(Camera cam, Shader shader, int mode, Color clearColor)
    {
        var cb = new CommandBuffer();
        cb.SetGlobalFloat("_OutputMode", mode);

        cam.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, cb);
        cam.AddCommandBuffer(CameraEvent.BeforeFinalPass, cb);

        cam.SetReplacementShader(shader, "");
        cam.backgroundColor = clearColor;
        cam.clearFlags = CameraClearFlags.SolidColor;
    }

    private void OnDestroy()
    {
        if (depthRT != null) depthRT.Release();
    }
    private byte[] EncodeTo16BitPng(ushort[] rawUShorts, int width, int height)
    {
        // 建立一個 ImageInfo，指定寬度、高度、位深(16)；沒有 Alpha，且為灰階圖像
        ImageInfo imgInfo = new ImageInfo(width, height, 16, false, true, false);
        MemoryStream ms = new MemoryStream();

        // 使用 PngWriter 建構子，第三個參數為描述文字 (這裡給空字串)
        PngWriter pngw = new PngWriter(ms, imgInfo, "");

        // 每一列資料用 int[] 傳入 (pngcs 提供 WriteRowInt)
        for (int row = 0; row < height; row++)
        {
            int[] rowData = new int[width];
            for (int col = 0; col < width; col++)
            {
                // 將 ushort 資料轉成 int 型別
                rowData[col] = rawUShorts[row * width + col];
            }
            pngw.WriteRowInt(rowData, row);
        }
        pngw.End(); // 完成寫入
        return ms.ToArray();
    }

}