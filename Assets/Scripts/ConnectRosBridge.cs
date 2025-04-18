using UnityEngine;
using WebSocketSharp;

public class ConnectRosBridge : MonoBehaviour
{
    public WebSocket ws;
    public const string RosbridgeUrl = "ws://localhost:9090";

    private bool isConnected = false;

    void Start()
    {
        ConnectToRosBridge();
    }

    void OnDestroy()
    {
        if (ws != null && ws.IsAlive)
            ws.Close();
    }

    void ConnectToRosBridge()
    {
        ws = new WebSocket(RosbridgeUrl);

        ws.OnOpen += (sender, e) =>
        {
            isConnected = true;
            Debug.Log("已連線到 Rosbridge");
        };

        ws.OnClose += (sender, e) =>
        {
            isConnected = false;
            Debug.LogError("Rosbridge 連線已關閉");
        };

        ws.OnError += (sender, e) =>
        {
            isConnected = false;
            Debug.LogError("Rosbridge 連線錯誤：" + e.Message);
        };

        try
        {
            ws.Connect();
            if (!ws.IsAlive)
                Debug.LogError("連線失敗，WebSocket.IsAlive 為 false");
        }
        catch (System.Exception ex)
        {
            isConnected = false;
            Debug.LogError($"WebSocket 連線例外：{ex.Message}");
        }
    }

    public void Send(string message)
    {
        if (ws == null || ws.ReadyState != WebSocketState.Open)
        {
            Debug.LogError("WebSocket 尚未連線，無法傳送訊息");
            return;
        }

        try
        {
            ws.Send(message);
            Debug.Log("已送出訊息: " + message);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("傳送失敗：" + ex.Message);
        }
    }
}
