// WorldStateSender.cs (PC側)
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using UnityEngine;

public class WorldStateSender : MonoBehaviour
{
    [Header("WebSocket設定")]
    [SerializeField] private string serverUrl = "wss://b400-202-13-170-200.ngrok-free.app"; // 前と同じURL
    [SerializeField] private string clientId = "pc-sender";

    // 送信間隔（秒）。0.033f だと 約30fps。早すぎると詰まるので調整。
    [SerializeField] private float sendInterval = 0.033f;

    [Header("同期対象オブジェクト")]
    [SerializeField] private Transform selfTransform; // 自分 (VRPlayer)
    [SerializeField] private Transform ball1;         // 球 1
    [SerializeField] private Transform ball2;         // 球 2

    private ClientWebSocket socket;
    private CancellationTokenSource cts = new CancellationTokenSource();
    private float lastSendTime;

    // ==== 送信するデータ構造 ====
    [Serializable]
    private class ObjectState
    {
        public float posX, posY, posZ;
        public float rotX, rotY, rotZ, rotW;
    }

    [Serializable]
    private class WorldStatePayload
    {
        public ObjectState player;
        public ObjectState ball1;
        public ObjectState ball2;
        public long timestamp;
    }

    [Serializable]
    private class StateMessage
    {
        public string type = "state"; // 識別用タグ
        public string clientId;
        public WorldStatePayload data;
    }

    private async void Start()
    {
        socket = new ClientWebSocket();
        await Connect();
    }

    private async Task Connect()
    {
        try
        {
            var uri = new Uri(serverUrl);
            await socket.ConnectAsync(uri, cts.Token);
            Debug.Log("[WS-Sender] Connected");
        }
        catch (Exception e)
        {
            Debug.LogError("[WS-Sender] Connect Error: " + e);
        }
    }

    private void Update()
    {
        // 通信が開いていなければ何もしない
        if (socket == null || socket.State != WebSocketState.Open) return;

        // 一定間隔で送信
        if (Time.time - lastSendTime >= sendInterval)
        {
            SendWorldState();
            lastSendTime = Time.time;
        }
    }

    private async void SendWorldState()
    {
        // 1. データを詰める
        var payload = new WorldStatePayload
        {
            player = CreateState(selfTransform),
            ball1  = CreateState(ball1),
            ball2  = CreateState(ball2),
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        var message = new StateMessage
        {
            clientId = this.clientId,
            data = payload
        };

        // 2. JSON化
        string json = JsonUtility.ToJson(message);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        // 3. 送信
        try
        {
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);
        }
        catch (Exception e)
        {
            Debug.LogError("[WS-Sender] Send Error: " + e);
        }
    }

    // Transform から軽量なデータクラスを作るヘルパー
    private ObjectState CreateState(Transform t)
    {
        if (t == null) return new ObjectState();
        Vector3 p = t.position;
        Quaternion r = t.rotation;
        return new ObjectState
        {
            posX = p.x, posY = p.y, posZ = p.z,
            rotX = r.x, rotY = r.y, rotZ = r.z, rotW = r.w
        };
    }

    private async void OnApplicationQuit()
    {
        cts.Cancel();
        if (socket != null && socket.State == WebSocketState.Open)
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Quit", CancellationToken.None);
            socket.Dispose();
        }
    }
}