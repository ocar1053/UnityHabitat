using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Rendering; // 必須引用
using Unity.Collections;

public class RosCameraPublish : MonoBehaviour
{
    [SerializeField] Camera carCamera;
    public ConnectRosBridge connectRos;
    public string CameraTopic = "/camera/image/compressed";
    private Texture2D texture2D;
    private Rect rect;
    private RenderTexture renderTexture;
    public int width = 1280;
    public int height = 720;
    public float publishhz = 4f; // 10 Hz

    void Start()
    {
        // sleep until ros bridge connected
        
        carCamera.cullingMask = LayerMask.GetMask("Default");
        renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        renderTexture.Create();

        // 建立 Texture2D 接受 GPU 回讀資料，必須與 renderTexture 尺寸與格式對應
        // 這裡使用 RGBA32，假設資料能直接對應，如有色彩偏差可能需要調整。
        texture2D = new Texture2D(width, height, TextureFormat.RGBA32, false);
        // rect = new Rect(0, 0, carCamera.pixelWidth, carCamera.pixelHeight);

        AdvertiseTopic();
        StartCoroutine(PublishImage());
    }

    private IEnumerator PublishImage()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f/ publishhz);

            // 將 camera 畫面繪製到 renderTexture
            carCamera.targetTexture = renderTexture;
            carCamera.Render();
            carCamera.targetTexture = null;

            // 發起非同步 GPU Readback
            AsyncGPUReadback.Request(renderTexture, 0, TextureFormat.RGBA32, OnCompleteReadback);
        }
    }

    private void OnCompleteReadback(AsyncGPUReadbackRequest request)
    {
        if (request.hasError)
        {
            Debug.LogError("AsyncGPUReadback 發生錯誤！");
            return;
        }

        // 取得 GPU 回讀的 byte 資料
        NativeArray<byte> data = request.GetData<byte>();

        // 將取回的影像資料載入到 Texture2D
        texture2D.LoadRawTextureData(data);
        texture2D.Apply();

        // 編碼成 JPEG
        int compressionQuality = 50;
        byte[] imagebytes = texture2D.EncodeToJPG(compressionQuality);

        // 傳送至 ROS
        PublishImage(CameraTopic, imagebytes, "camera");
    }

    void AdvertiseTopic()
    {
        string advertiseMessage = $@"{{
            ""op"": ""advertise"",
            ""topic"": ""{CameraTopic}"",
            ""type"": ""sensor_msgs/msg/CompressedImage""
        }}";
        connectRos.ws.Send(advertiseMessage);
    }

    public void PublishImage(string topic, byte[] imagebytes, string frame_id = "camera")
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // string imageString = System.Convert.ToBase64String(imagebytes);
        // 將 byte[] 轉換為 JSON 陣列格式的字串
        string imageDataJson = "[" + string.Join(",", imagebytes) + "]";
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
                ""data"": {imageDataJson}
            }}
        }}";
        connectRos.ws.Send(publishMessage);
    }

    // void OnDestroy()
    // {


    //     // 釋放 RenderTexture
    //     if (renderTexture != null)
    //     {
    //         renderTexture.Release();
    //         Destroy(renderTexture);
    //     }

    //     // 釋放 Texture2D
    //     if (texture2D != null)
    //     {
    //         Destroy(texture2D);
    //     }

    //     Debug.Log("Resources have been released.");
    // }
}
