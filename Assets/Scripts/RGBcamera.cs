using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

using System;
using WebSocketSharp;
using Unity.Collections;

public class RGBcamera : MonoBehaviour
{
    [SerializeField] Camera carCamera;
    public string CameraTopic = "/camera/image/compressed";
    private Texture2D texture2D;
    private Rect rect;
    private RenderTexture renderTexture;

    
    private WebSocket rosWebSocket;

    void Start()
    {
        
        rosWebSocket = new WebSocket("ws://localhost:9090"); 
        rosWebSocket.OnOpen += (sender, e) => Debug.Log("Connected to ROS bridge");
        rosWebSocket.OnMessage += (sender, e) => Debug.Log("Received message from ROS bridge: " + e.Data);
        rosWebSocket.Connect();

        
        int width = 640;
        int height = 480;
        renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        renderTexture.Create();

        
        texture2D = new Texture2D(width, height, TextureFormat.RGBA32, false);

        AdvertiseTopic();
        StartCoroutine(PublishImage());
    }

    private IEnumerator PublishImage()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.05f);

           
            carCamera.targetTexture = renderTexture;
            carCamera.Render();
            carCamera.targetTexture = null;

            
            AsyncGPUReadback.Request(renderTexture, 0, TextureFormat.RGBA32, OnCompleteReadback);
        }
    }

    private void OnCompleteReadback(AsyncGPUReadbackRequest request)
    {
        if (request.hasError)
        {
            Debug.LogError("AsyncGPUReadback error¡I");
            return;
        }

        NativeArray<byte> data = request.GetData<byte>();

        texture2D.LoadRawTextureData(data);
        texture2D.Apply();

        int compressionQuality = 50;
        byte[] imagebytes = texture2D.EncodeToJPG(compressionQuality);

        PublishImage(CameraTopic, imagebytes, "camera");
    }

    void AdvertiseTopic()
    {
        string advertiseMessage = $@"{{
            ""op"": ""advertise"",
            ""topic"": ""{CameraTopic}"",
            ""type"": ""sensor_msgs/msg/CompressedImage""
        }}";
        rosWebSocket.Send(advertiseMessage);
    }

    public void PublishImage(string topic, byte[] imagebytes, string frame_id = "camera")
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string imageString = System.Convert.ToBase64String(imagebytes);
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
                ""format"": ""jpeg"",
                ""data"": ""{imageString}""
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