using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using UnityEngine;
using Fusion;

public class VRPlayerMovement : NetworkBehaviour
{
    [Header("WebSocket設定")]
    [SerializeField] private string serverUrl = "wss://b400-202-13-170-200.ngrok-free.app";
    [SerializeField] private string clientId = "vr-1";

    [Header("同期対象 (自分以外のボールも動かすため)")]
    [SerializeField] private Transform ball1; // インスペクタで球1をセット
    [SerializeField] private Transform ball2; // インスペクタで球2をセット

    [Header("入力送信設定")]
    [SerializeField] private float sendInterval = 0.0f; // 0なら毎フレーム送信チェック

    private ClientWebSocket socket;
    private CancellationTokenSource cts = new CancellationTokenSource();
    private Transform cameraRigRoot;
    private Transform centerEyeAnchor;

    // ==== 受信するデータ構造 (PC側と合わせる) ====
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
    private class ServerMessage
    {
        public string type; // "state" or "input"
        public string clientId;
        public WorldStatePayload data; // type="state" の場合の中身
    }

    // 最新の受信データ
    private Vector3? targetPosPlayer, targetPosB1, targetPosB2;
    private Quaternion? targetRotPlayer, targetRotB1, targetRotB2;

    // ==== 送信するデータ構造 ====
    [Serializable]
    private class InputPayloadData
    {
        public float stickLeftX, stickLeftY;
        public float stickRightX, stickRightY;
        public bool  pressA;
        public float forwardX, forwardZ;
        public float rightX, rightZ;
        public float launchDirX, launchDirZ;
        public long  timestamp;
    }

    [Serializable]
    private class InputMessage
    {
        public string type = "input";
        public string clientId;
        public string role = "vr";
        public InputPayloadData data;
    }

    private void Awake()
    {
        socket = new ClientWebSocket();
        ConnectAndStart();
    }

    private async void ConnectAndStart()
    {
        try
        {
            var uri = new Uri(serverUrl);
            Debug.Log("[WS-VR] Connecting...");
            await socket.ConnectAsync(uri, cts.Token);
            Debug.Log("[WS-VR] Connected");

            _ = ReceiveLoop(); // 受信ループ開始
        }
        catch (Exception e)
        {
            Debug.LogError("[WS-VR] Connect Error: " + e);
        }
    }

    // ★ サーバーからの座標を受信するループ
    private async Task ReceiveLoop()
    {
        var buffer = new byte[4096];
        while (socket != null && socket.State == WebSocketState.Open)
        {
            try
            {
                var segment = new ArraySegment<byte>(buffer);
                var result = await socket.ReceiveAsync(segment, cts.Token);
                
                if (result.MessageType == WebSocketMessageType.Close) break;

                int count = result.Count;
                while (!result.EndOfMessage)
                {
                    if (count >= buffer.Length) break; 
                    segment = new ArraySegment<byte>(buffer, count, buffer.Length - count);
                    result = await socket.ReceiveAsync(segment, cts.Token);
                    count += result.Count;
                }

                string json = Encoding.UTF8.GetString(buffer, 0, count);
                HandleServerMessage(json);
            }
            catch (Exception) { break; }
        }
    }

    private void HandleServerMessage(string json)
    {
        try
        {
            // まずタイプを確認
            var msg = JsonUtility.FromJson<ServerMessage>(json);
            
            // PCから送られてくる "state" メッセージのみ処理する
            if (msg != null && msg.type == "state" && msg.data != null)
            {
                // データを取り出してメインスレッド反映用の変数に入れる
                var d = msg.data;
                
                targetPosPlayer = new Vector3(d.player.posX, d.player.posY, d.player.posZ);
                targetRotPlayer = new Quaternion(d.player.rotX, d.player.rotY, d.player.rotZ, d.player.rotW);

                targetPosB1 = new Vector3(d.ball1.posX, d.ball1.posY, d.ball1.posZ);
                targetRotB1 = new Quaternion(d.ball1.rotX, d.ball1.rotY, d.ball1.rotZ, d.ball1.rotW);

                targetPosB2 = new Vector3(d.ball2.posX, d.ball2.posY, d.ball2.posZ);
                targetRotB2 = new Quaternion(d.ball2.rotX, d.ball2.rotY, d.ball2.rotZ, d.ball2.rotW);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("JSON Parse Error: " + e);
        }
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            var rig = FindObjectOfType<OVRCameraRig>();
            if (rig != null)
            {
                cameraRigRoot   = rig.transform;
                centerEyeAnchor = rig.centerEyeAnchor;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || cameraRigRoot == null || centerEyeAnchor == null) return;

        // ===============================================
        // 1. 受信した座標を反映 (ローカル計算は廃止)
        // ===============================================
        
        // 自分の位置 (Player)
        if (targetPosPlayer.HasValue)
        {
            // 素早く同期させるため Lerp でなめらかに動かす (t=0.5f くらいで強めに補間)
            // ※完全にカチッと合わせたい場合は Lerp をやめて直接代入してください
            cameraRigRoot.position = Vector3.Lerp(cameraRigRoot.position, targetPosPlayer.Value, 0.5f);
            // 回転はHMDの自由度を奪うので同期しないのが一般的ですが、必要ならここで行います
        }

        // 球1
        if (ball1 != null && targetPosB1.HasValue)
        {
            ball1.position = Vector3.Lerp(ball1.position, targetPosB1.Value, 0.5f);
            ball1.rotation = Quaternion.Lerp(ball1.rotation, targetRotB1.Value, 0.5f);
        }

        // 球2
        if (ball2 != null && targetPosB2.HasValue)
        {
            ball2.position = Vector3.Lerp(ball2.position, targetPosB2.Value, 0.5f);
            ball2.rotation = Quaternion.Lerp(ball2.rotation, targetRotB2.Value, 0.5f);
        }

        // ===============================================
        // 2. 入力を送信 (操作のみ送る)
        // ===============================================
        
        Vector3 forward = centerEyeAnchor.forward;
        Vector3 right   = centerEyeAnchor.right;
        forward.y = 0f; right.y = 0f;
        forward.Normalize(); right.Normalize();

        Vector2 leftInput  = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        Vector2 rightInput = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
        bool pressA        = OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch);

        Vector3 launchDir = Vector3.zero;
        if (leftInput.magnitude > 0.1f)
        {
            launchDir = (forward * leftInput.y + right * leftInput.x).normalized;
        }

        // Aボタンを押したときだけ送信
        if (pressA)
        {
            SendVrInput(leftInput, rightInput, pressA, forward, right, launchDir);
        }
    }

    private async void SendVrInput(Vector2 leftStick, Vector2 rightStick, bool pressA, Vector3 forward, Vector3 right, Vector3 launchDir)
    {
        if (socket == null || socket.State != WebSocketState.Open) return;

        var payload = new InputMessage
        {
            clientId = clientId,
            data = new InputPayloadData
            {
                stickLeftX  = leftStick.x, stickLeftY = leftStick.y,
                stickRightX = rightStick.x, stickRightY = rightStick.y,
                pressA      = pressA,
                forwardX    = forward.x, forwardZ = forward.z,
                rightX      = right.x, rightZ = right.z,
                launchDirX  = launchDir.x, launchDirZ = launchDir.z,
                timestamp   = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }
        };

        string json  = JsonUtility.ToJson(payload);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        try
        {
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);
        }
        catch (Exception e) { Debug.LogError("[WS] Send Error: " + e); }
    }

    private async void OnApplicationQuit()
    {
        cts.Cancel();
        if (socket != null) { socket.Dispose(); }
    }
}