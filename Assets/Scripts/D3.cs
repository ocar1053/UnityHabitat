using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using WebSocketSharp;
using Unity.Collections;
using System.Collections.Generic;

public class D3 : MonoBehaviour
{
    [SerializeField] private Camera rgbCamera;
    [SerializeField] private Camera depthCamera;
    [SerializeField] private int cameraId;
    [SerializeField][Range(0, 100)] private int jpegQuality = 50;
    [SerializeField][Range(0.1f, 30.0f)] private float captureFrequency = 2f;
    [SerializeField] private bool useDepthCamera = true;
    private string rgbTopic;
    private string depthTopic;

    private Texture2D rgbTexture;
    private Texture2D depthTexture;
    private RenderTexture rgbRenderTexture;
    private RenderTexture depthRenderTexture;
    private WebSocket rosWebSocket;
    private bool isConnected = false;

    void Start()
    {
        rgbTopic = $"/unity_camera_{cameraId}/rgb/compressed";
        depthTopic = $"/unity_camera_{cameraId}/depth";

        rosWebSocket = new WebSocket("ws://localhost:9090");
        rosWebSocket.OnOpen += (sender, e) =>
        {
            Debug.Log($"Camera {cameraId} Connected to ROS bridge");
            isConnected = true;
            AdvertiseTopics();
        };
        rosWebSocket.OnClose += (sender, e) =>
        {
            Debug.Log($"Camera {cameraId} Disconnected from ROS bridge");
            isConnected = false;
            StartCoroutine(Reconnect());
        };
        rosWebSocket.ConnectAsync();

        int width = 1280;
        int height = 720;
        rgbRenderTexture = RenderTexturePool.GetRenderTexture(width, height, RenderTextureFormat.ARGB32);
        depthRenderTexture = RenderTexturePool.GetRenderTexture(width, height, RenderTextureFormat.R16);

        rgbTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        depthTexture = new Texture2D(width, height, TextureFormat.R16, false);

        StartCoroutine(CaptureAndPublish());
    }

    private IEnumerator CaptureAndPublish()
    {
        var waitForEndOfFrame = new WaitForEndOfFrame();
        float targetFrameRate = 1f / captureFrequency;  
        while (true)
        {
            yield return new WaitForSeconds(targetFrameRate);  

            if (!isConnected) continue;

            // Capture RGB
            rgbCamera.targetTexture = rgbRenderTexture;
            rgbCamera.Render();
            rgbCamera.targetTexture = null;
            AsyncGPUReadback.Request(rgbRenderTexture, 0, TextureFormat.RGBA32, OnRGBReadback);

            if (useDepthCamera) {
                // Capture Depth
                depthCamera.targetTexture = depthRenderTexture;
                depthCamera.Render();
                depthCamera.targetTexture = null;
                AsyncGPUReadback.Request(depthRenderTexture, 0, TextureFormat.R16, OnDepthReadback);
            }

        }
    }

    private void OnRGBReadback(AsyncGPUReadbackRequest request)
    {
        if (request.hasError)
        {
            Debug.LogError($"Camera {cameraId} RGB AsyncGPUReadback error!");
            return;
        }

        var data = request.GetData<byte>();
        rgbTexture.LoadRawTextureData(data);
        rgbTexture.Apply();

        byte[] jpgBytes = rgbTexture.EncodeToJPG(jpegQuality);
        PublishImage(rgbTopic, jpgBytes, "jpeg");
    }

    private void OnDepthReadback(AsyncGPUReadbackRequest request)
    {
        if (request.hasError)
        {
            Debug.LogError($"Camera {cameraId} Depth AsyncGPUReadback error!");
            return;
        }

        var data = request.GetData<byte>();
        depthTexture.LoadRawTextureData(data);
        depthTexture.Apply();

        byte[] depthBytes = depthTexture.EncodeToPNG();
        PublishImage(depthTopic, depthBytes, "png");
    }

    private void AdvertiseTopics()
    {
        AdvertiseTopic(rgbTopic, "sensor_msgs/msg/CompressedImage");
        AdvertiseTopic(depthTopic, "sensor_msgs/msg/CompressedImage");
    }

    private void AdvertiseTopic(string topic, string type)
    {
        string advertiseMessage = $@"{{
            ""op"": ""advertise"",
            ""topic"": ""{topic}"",
            ""type"": ""{type}""
        }}";
        rosWebSocket.Send(advertiseMessage);
    }

    private void PublishImage(string topic, byte[] imageBytes, string format, string frame_id = "camera")
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string imageString = Convert.ToBase64String(imageBytes);
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
                ""format"": ""{format}"",
                ""data"": ""{imageString}""
            }}
        }}";
        rosWebSocket.Send(publishMessage);
    }

    private IEnumerator Reconnect()
    {
        while (!isConnected)
        {
            yield return new WaitForSeconds(1);
            rosWebSocket.ConnectAsync();
        }
    }

    void OnApplicationQuit()
    {
        if (rosWebSocket != null)
        {
            rosWebSocket.Close();
        }

        RenderTexturePool.ReleaseRenderTexture(rgbRenderTexture);
        RenderTexturePool.ReleaseRenderTexture(depthRenderTexture);
    }
}

public static class RenderTexturePool
{
    private static Queue<RenderTexture> argb32Pool = new Queue<RenderTexture>();
    private static Queue<RenderTexture> r16Pool = new Queue<RenderTexture>();

    public static RenderTexture GetRenderTexture(int width, int height, RenderTextureFormat format)
    {
        Queue<RenderTexture> pool = GetPool(format);

        foreach (var rt in pool)
        {
            if (rt.width == width && rt.height == height)
            {
                pool.Dequeue();
                return rt;
            }
        }

        RenderTexture newRt = new RenderTexture(width, height, 0, format);
        newRt.Create();
        return newRt;
    }

    public static void ReleaseRenderTexture(RenderTexture rt)
    {
        if (rt == null)
            return;

        Queue<RenderTexture> pool = GetPool(rt.format);
        pool.Enqueue(rt);
    }

    private static Queue<RenderTexture> GetPool(RenderTextureFormat format)
    {
        switch (format)
        {
            case RenderTextureFormat.ARGB32:
                return argb32Pool;
            case RenderTextureFormat.R16:
                return r16Pool;
            default:
                throw new System.ArgumentException("Unsupported RenderTextureFormat: " + format);
        }
    }
}
