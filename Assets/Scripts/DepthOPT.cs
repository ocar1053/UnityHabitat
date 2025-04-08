
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using WebSocketSharp;
using Unity.Collections;

public class DepthOPT : MonoBehaviour
{
    [SerializeField] private Camera depthCamera;
    [SerializeField] private int cameraId;
    private string depthTopic;

    private Texture2D depthTexture;
    private RenderTexture depthRenderTexture;
    private WebSocket rosWebSocket;

    void Start()
    {
        depthTopic = $"/unity_camera_{cameraId}/depth";

        rosWebSocket = new WebSocket("ws://localhost:9090");
        rosWebSocket.OnOpen += (sender, e) => Debug.Log($"Camera {cameraId} Connected to ROS bridge");
        rosWebSocket.OnMessage += (sender, e) => Debug.Log($"Camera {cameraId} Received message: {e.Data}");
        rosWebSocket.Connect();

        int width = 640;
        int height = 480;
        depthRenderTexture = new RenderTexture(width, height, 16, RenderTextureFormat.R16);
        depthRenderTexture.Create();

        depthTexture = new Texture2D(width, height, TextureFormat.R16, false);

        depthCamera.depthTextureMode = DepthTextureMode.Depth;

        AdvertiseTopic();
        StartCoroutine(PublishDepthImage());
    }

    private IEnumerator PublishDepthImage()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.1f);

            depthCamera.targetTexture = depthRenderTexture;
            depthCamera.Render();
            depthCamera.targetTexture = null;

            AsyncGPUReadback.Request(depthRenderTexture, 0, TextureFormat.R16, OnCompleteReadback);
        }
    }

    private void OnCompleteReadback(AsyncGPUReadbackRequest request)
    {
        if (request.hasError)
        {
            Debug.LogError($"Camera {cameraId} AsyncGPUReadback error¡I");
            return;
        }

        NativeArray<byte> data = request.GetData<byte>();
        depthTexture.LoadRawTextureData(data);
        depthTexture.Apply();

        NativeArray<ushort> ushortData = data.Reinterpret<ushort>(sizeof(byte));

        PublishDepthImage(ushortData.ToArray(), depthTexture.width, depthTexture.height);
    }

    void AdvertiseTopic()
    {
        string advertiseMessage = $@"{{
            ""op"": ""advertise"",
            ""topic"": ""{depthTopic}"",
            ""type"": ""sensor_msgs/msg/Image""
        }}";
        rosWebSocket.Send(advertiseMessage);
    }

    public void PublishDepthImage(ushort[] depthData, int width, int height, string frame_id = "camera")
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        byte[] byteArray = new byte[depthData.Length * sizeof(ushort)];
        Buffer.BlockCopy(depthData, 0, byteArray, 0, byteArray.Length);
        string depthString = Convert.ToBase64String(byteArray);

        string publishMessage = $@"{{
            ""op"": ""publish"",
            ""topic"": ""{depthTopic}"",
            ""msg"": {{
                ""header"": {{
                    ""stamp"": {{
                        ""secs"": {timestamp / 1000},
                        ""nsecs"": {(timestamp % 1000) * 1000000}
                    }},
                    ""frame_id"": ""{frame_id}""
                }},
                ""height"": {height},
                ""width"": {width},
                ""encoding"": ""mono16"",  
                ""is_bigendian"": 0,
                ""step"": {width * sizeof(ushort)},
                ""data"": ""{depthString}""
            }}
        }}";

        rosWebSocket.Send(publishMessage);
    }

    void OnApplicationQuit()
    {
        if (rosWebSocket != null && rosWebSocket.ReadyState == WebSocketState.Open)
        {
            rosWebSocket.Close();
        }
    }
}
