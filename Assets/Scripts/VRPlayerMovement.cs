using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using UnityEngine;

public class VRPlayerMovement : MonoBehaviour
{
    [Header("WebSocket設定")]
    [SerializeField] private string serverUrl = "wss://b400-202-13-170-200.ngrok-free.app";
    [SerializeField] private string clientId = "vr-1";

    [Header("同期対象 (Sizeを10にしてボールを入れる)")]
    [SerializeField] private Transform[] syncBalls; // ★配列に変更

    private ClientWebSocket socket;
    private CancellationTokenSource cts = new CancellationTokenSource();
    
    private Transform cameraRigRoot;
    private Transform centerEyeAnchor;

    // ==== データ構造 (配列対応に変更) ====
    [Serializable] private class ObjectState { public float posX, posY, posZ; public float rotX, rotY, rotZ, rotW; }
    
    [Serializable] 
    private class WorldStatePayload 
    { 
        public ObjectState player; 
        public ObjectState[] balls; // ★ここを配列に変更
        public long timestamp; 
    }

    [Serializable] private class ServerMessage { public string type; public string clientId; public WorldStatePayload data; }
    [Serializable] private class InputPayloadData { public float stickLeftX, stickLeftY; public float stickRightX, stickRightY; public bool pressA; public float forwardX, forwardZ; public float rightX, rightZ; public float launchDirX, launchDirZ; public long timestamp; }
    [Serializable] private class InputMessage { public string type = "input"; public string clientId; public string role = "vr"; public InputPayloadData data; }

    // 最新データ保持用
    private Vector3? targetPosPlayer;
    private Vector3[] targetPosBalls; // ★配列
    private Quaternion[] targetRotBalls; // ★配列

    private async void Start()
    {
        // 配列の初期化
        if (syncBalls != null)
        {
            targetPosBalls = new Vector3[syncBalls.Length];
            targetRotBalls = new Quaternion[syncBalls.Length];
        }

        var rig = GetComponent<OVRCameraRig>();
        if (rig != null)
        {
            cameraRigRoot = rig.transform;
            centerEyeAnchor = rig.centerEyeAnchor;
        }
        else
        {
            rig = FindObjectOfType<OVRCameraRig>();
            if (rig != null)
            {
                cameraRigRoot = rig.transform;
                centerEyeAnchor = rig.centerEyeAnchor;
            }
        }

        socket = new ClientWebSocket();
        await ConnectAndStart();
    }

    private async Task ConnectAndStart()
    {
        try
        {
            var uri = new Uri(serverUrl);
            Debug.Log($"[WS-VR] Connecting to {uri} ...");
            await socket.ConnectAsync(uri, cts.Token);
            Debug.Log("[WS-VR] Connected");
            _ = ReceiveLoop();
        }
        catch (Exception e)
        {
            Debug.LogError("[WS-VR] Connect Error: " + e);
        }
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[4096 * 4]; // データ量が増えるのでバッファを少し拡大
        while (socket != null && socket.State == WebSocketState.Open)
        {
            try
            {
                var segment = new ArraySegment<byte>(buffer);
                var result = await socket.ReceiveAsync(segment, cts.Token);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Debug.Log("[WS-VR] Closed by Server");
                    break;
                }

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
            catch (Exception e) 
            { 
                Debug.LogError("[WS-VR] ReceiveLoop Error: " + e);
                break; 
            }
        }
    }

    private void HandleServerMessage(string json)
    {
        try
        {
            var msg = JsonUtility.FromJson<ServerMessage>(json);
            if (msg != null && msg.type == "state" && msg.data != null)
            {
                var d = msg.data;
                
                // プレイヤー更新
                targetPosPlayer = new Vector3(d.player.posX, d.player.posY, d.player.posZ);
                // 回転は同期しない方針ならスキップ

                // ボール更新 (ループ処理)
                if (d.balls != null && syncBalls != null)
                {
                    int count = Mathf.Min(d.balls.Length, syncBalls.Length);
                    for (int i = 0; i < count; i++)
                    {
                        var b = d.balls[i];
                        targetPosBalls[i] = new Vector3(b.posX, b.posY, b.posZ);
                        targetRotBalls[i] = new Quaternion(b.rotX, b.rotY, b.rotZ, b.rotW);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[WS-VR] JSON Parse Error: " + e);
        }
    }

    private void Update()
    {
        if (cameraRigRoot == null || centerEyeAnchor == null) return;

        // 1. 座標反映
        if (targetPosPlayer.HasValue)
            cameraRigRoot.position = Vector3.Lerp(cameraRigRoot.position, targetPosPlayer.Value, 0.5f);

        // ボール反映 (ループ処理)
        if (syncBalls != null)
        {
            for (int i = 0; i < syncBalls.Length; i++)
            {
                // データが来ていない、または参照がない場合はスキップ
                if (syncBalls[i] == null) continue;
                
                // 初回受信前などで座標が (0,0,0) のままならスキップする処理を入れても良いが、
                // 今回は単純にLerpし続ける
                syncBalls[i].position = Vector3.Lerp(syncBalls[i].position, targetPosBalls[i], 0.5f);
                syncBalls[i].rotation = Quaternion.Lerp(syncBalls[i].rotation, targetRotBalls[i], 0.5f);
            }
        }

        // 2. 入力送信
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

        if (pressA)
        {
            Debug.Log("[WS-VR] Sending Input (A Button)");
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
        catch (Exception e) { Debug.LogError("[WS-VR] Send Error: " + e); }
    }

    private async void OnApplicationQuit()
    {
        cts.Cancel();
        if (socket != null) { socket.Dispose(); }
    }
}