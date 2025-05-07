
// SceneObjectsPublisher.cs
// Unity C# script: 多物件座標與多相機距離 ROS 发布器
// 形式參考 RosCameraPublish
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WebSocketSharp;

public class SceneObjectsPublisher : MonoBehaviour
{
    [Header("RosBridge WebSocket")]
    public ConnectRosBridge connectRos;

    [Header("Topic 設定")]
    [Tooltip("基底 topic，例如 /object_info")] public string topicBase = "/object_info";
    [Tooltip("若勾選，每個物件發布到 topicBase/物件名稱")] public bool perObjectTopic = false;

    [Header("Publish Rate (Hz)")]
    [Range(0.1f, 60f)] public float publishRateHz = 10f;

    [Header("場景物件與相機")]
    [Tooltip("在此填入要發布的所有 GameObject")] public GameObject[] sceneObjects;
    [Tooltip("可選，若要計算距離則填入相機列表")] public Camera[] cameras;

    void Start()
    {
        // 確保 RosBridge 連線已開
        if (connectRos == null || connectRos.ws == null || connectRos.ws.ReadyState != WebSocketState.Open)
        {
            Debug.LogError("ConnectRosBridge 尚未配置或未連線");
            return;
        }

        AdvertiseTopics();
        StartCoroutine(PublishLoop());
    }

    void AdvertiseTopics()
    {
        foreach (var obj in sceneObjects)
        {
            string topic = perObjectTopic ? $"{topicBase}/{obj.name}" : topicBase;
            string advertiseMessage = $@"{{
                ""op"": ""advertise"",
                ""topic"": ""{topic}"",
                ""type"": ""unity_object_info_msg/msg/ObjectInfo""
            }}";

           
            connectRos.ws.Send(advertiseMessage);
        }
    }

    IEnumerator PublishLoop()
    {
        var wait = new WaitForSeconds(1f / publishRateHz);
        while (true)
        {
            yield return wait;
            PublishAll();
        }
    }

    void PublishAll()
    {
        foreach (var obj in sceneObjects)
        {
            
            Vector3 pos = obj.transform.localPosition;
            // float[] dists = ComputeDistances(pos);
            float[] dists = ComputeLocalDepths(pos);
            // 構建距離陣列 JSON
            string distancesJson = "[" + string.Join(",", Array.ConvertAll(dists, d => d.ToString())) + "]";

            // 構建 msg JSON
            string msgJson = $@"{{
                ""name"": ""{obj.name}"",
                ""position"": {{""x"": {pos.x}, ""y"": {pos.y}, ""z"": {pos.z}}},
                ""distances"": {distancesJson}
            }}";

            string topic = perObjectTopic ? $"{topicBase}/{obj.name}" : topicBase;
            
            string publishMessage = $@"{{
                ""op"": ""publish"",
                ""topic"": ""{topic}"",
                ""msg"": {msgJson}
            }}";

            connectRos.ws.Send(publishMessage);
        }
    }
    float[] ComputeLocalDepths(Vector3 objLocalPos)
    {
        if (cameras == null || cameras.Length == 0)
            return new float[0];

        float[] depths = new float[cameras.Length];
        for (int i = 0; i < cameras.Length; i++)
        {
            var cam = cameras[i];
            // 1) 取得物件在父空間下的 local 向量
            Vector3 toObjLocal = objLocalPos - cam.transform.localPosition;
            // 2) 取得相機在父空間下的 forward 向量
            //    （localRotation * Vector3.forward = 把 local-space forward 旋轉到父空間）
            Vector3 forwardLocal = cam.transform.localRotation * Vector3.forward;
            // 3) 投影取得沿光軸距離
            float depth = Vector3.Dot(toObjLocal, forwardLocal);
            depths[i] = depth * 1000f; // 換成毫米
        }
        return depths;
    }
    float[] ComputeDistances(Vector3 pos)
    {
        if (cameras == null || cameras.Length == 0)
            return new float[0];

        float[] dists = new float[cameras.Length];
        for (int i = 0; i < cameras.Length; i++)
            dists[i] = Vector3.Distance(pos, cameras[i].transform.localPosition) * 1000f; // 轉換為毫米
        return dists;
    }
}


