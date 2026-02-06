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
    [SerializeField] private float sendInterval = 0.033f;

    [Header("最適化設定")]
    [SerializeField] private float moveThreshold = 0.001f; 
    [SerializeField] private float angleThreshold = 0.1f;

    [Header("同期対象オブジェクト")]
    [SerializeField] private Transform selfTransform; // 自分
    [SerializeField] private Transform[] syncBalls;   // ★配列に変更 (Size 10にする)

    private ClientWebSocket socket;
    private CancellationTokenSource cts = new CancellationTokenSource();
    private float lastSendTime;

    // 前回の位置・回転を覚えておく変数
    private Vector3 prevPosSelf;
    private Quaternion prevRotSelf;
    
    // ボール用の履歴配列
    private Vector3[] prevPosBalls;
    private Quaternion[] prevRotBalls;
    
    private bool firstSend = true;

    // ==== データ構造 ====
    [Serializable] private class ObjectState { public float posX, posY, posZ; public float rotX, rotY, rotZ, rotW; }
    
    [Serializable] 
    private class WorldStatePayload 
    { 
        public ObjectState player; 
        public ObjectState[] balls; // ★配列に変更
        public long timestamp; 
    }

    [Serializable] private class StateMessage { public string type = "state"; public string clientId; public WorldStatePayload data; }

    private async void Start()
    {
        // 履歴配列の初期化
        if (syncBalls != null)
        {
            prevPosBalls = new Vector3[syncBalls.Length];
            prevRotBalls = new Quaternion[syncBalls.Length];
        }

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
            bool anyChanged = false;

            // 1. 自分の変更チェック
            if (HasChanged(selfTransform, ref prevPosSelf, ref prevRotSelf))
            {
                anyChanged = true;
            }

            // 2. ボールの変更チェック (ループ)
            if (syncBalls != null)
            {
                for (int i = 0; i < syncBalls.Length; i++)
                {
                    if (HasChanged(syncBalls[i], ref prevPosBalls[i], ref prevRotBalls[i]))
                    {
                        anyChanged = true;
                    }
                }
            }

            // どれか1つでも動いていれば（または初回なら）送信
            if (anyChanged || firstSend)
            {
                SendWorldState();
                lastSendTime = Time.time;
                firstSend = false;
            }
        }
    }

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
        // データの作成
        var payload = new WorldStatePayload
        {
            player = CreateState(selfTransform),
            balls  = new ObjectState[syncBalls.Length], // 配列確保
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        // ボールデータを詰める
        for (int i = 0; i < syncBalls.Length; i++)
        {
            payload.balls[i] = CreateState(syncBalls[i]);
        }

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
            // Debug.Log($"[WS-Sender] Sent State! Time: {payload.timestamp}");
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