using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using UnityEngine;

public class WorldStateSender : MonoBehaviour
{
    [Header("WebSocket設定")]
    [SerializeField] private string serverUrl = "wss://b400-202-13-170-200.ngrok-free.app";
    [SerializeField] private string clientId = "pc-sender";
    [SerializeField] private float sendInterval = 0.033f; // 約30fps

    [Header("最適化設定")]
    // これ以上動いたら送信する（1mm / 0.1度）
    [SerializeField] private float moveThreshold = 0.001f; 
    [SerializeField] private float angleThreshold = 0.1f;

    [Header("同期対象オブジェクト")]
    [SerializeField] private Transform selfTransform; // 自分
    [SerializeField] private Transform ball1;         // 球 1
    [SerializeField] private Transform ball2;         // 球 2

    private ClientWebSocket socket;
    private CancellationTokenSource cts = new CancellationTokenSource();
    private float lastSendTime;

    // 前回の位置・回転を覚えておく変数
    private Vector3 prevPosSelf, prevPosB1, prevPosB2;
    private Quaternion prevRotSelf, prevRotB1, prevRotB2;
    private bool firstSend = true; // 初回は必ず送る用

    // ==== データ構造 ====
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
        public string type = "state";
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
        if (socket == null || socket.State != WebSocketState.Open) return;

        if (Time.time - lastSendTime >= sendInterval)
        {
            // ★変更チェック：どれか一つでも動いていたら送信
            if (HasChanged(selfTransform, ref prevPosSelf, ref prevRotSelf) ||
                HasChanged(ball1, ref prevPosB1, ref prevRotB1) ||
                HasChanged(ball2, ref prevPosB2, ref prevRotB2) ||
                firstSend)
            {
                SendWorldState();
                lastSendTime = Time.time;
                firstSend = false;
            }
        }
    }

    // 変化判定 & 履歴更新メソッド
    private bool HasChanged(Transform t, ref Vector3 prevPos, ref Quaternion prevRot)
    {
        if (t == null) return false;

        bool moved = Vector3.Distance(t.position, prevPos) > moveThreshold;
        bool turned = Quaternion.Angle(t.rotation, prevRot) > angleThreshold;

        if (moved || turned)
        {
            prevPos = t.position;
            prevRot = t.rotation;
            return true;
        }
        return false;
    }

    private async void SendWorldState()
    {
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

        string json = JsonUtility.ToJson(message);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        try
        {
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);
        }
        catch (Exception e)
        {
            Debug.LogError("[WS-Sender] Send Error: " + e);
        }
    }

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