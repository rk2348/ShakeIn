// VRPlayerMovement.cs（Quest側・pressA のフレームだけ送信）
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

    private ClientWebSocket socket;
    private CancellationTokenSource cts = new CancellationTokenSource();

    [Header("ビリヤード: ショット設定")]
    [SerializeField] private float launchPower = 10.0f;
    [SerializeField] private float friction = 0.98f;
    [SerializeField] private float stopThreshold = 0.01f;

    [Header("通常: スティック移動設定")]
    [SerializeField] private float normalMoveSpeed = 2.0f;

    private Transform cameraRigRoot;
    private Transform centerEyeAnchor;
    private Vector3 currentVelocity;

    [Serializable]
    private class InputPayloadData
    {
        public float stickLeftX;
        public float stickLeftY;
        public float stickRightX;
        public float stickRightY;
        public bool  pressA;
        public float forwardX;
        public float forwardZ;
        public float rightX;
        public float rightZ;
        public float launchDirX;
        public float launchDirZ;
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

    // ===== WebSocket 接続 =====

    private async void Awake()
    {
        socket = new ClientWebSocket();

        try
        {
            var uri = new Uri(serverUrl);
            Debug.Log("[WS] Connecting to " + uri);
            await socket.ConnectAsync(uri, cts.Token);
            Debug.Log("[WS] Connected: " + socket.State);
        }
        catch (Exception e)
        {
            Debug.LogError("[WS] Connect Exception: " + e);
        }
    }

    private async void OnApplicationQuit()
    {
        try
        {
            if (socket != null && socket.State == WebSocketState.Open)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Quit", cts.Token);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[WS] Close Exception: " + e);
        }
        finally
        {
            cts.Cancel();
            socket?.Dispose();
        }
    }

    // ===== Fusion ライフサイクル =====

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            var rig = FindObjectOfType<OVRCameraRig>();
            if (rig != null)
            {
                cameraRigRoot   = rig.transform;
                centerEyeAnchor = rig.centerEyeAnchor;
                Debug.Log("OVRCameraRig を発見、入力送信モード開始");
            }
            else
            {
                Debug.LogError("OVRCameraRig が見つかりません");
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || cameraRigRoot == null || centerEyeAnchor == null)
        {
            return;
        }

        // 1. 慣性移動（ローカル）
        if (currentVelocity.magnitude > stopThreshold)
        {
            cameraRigRoot.position += currentVelocity * Runner.DeltaTime;
            currentVelocity *= friction;
        }
        else
        {
            currentVelocity = Vector3.zero;
        }

        // 2. 向きベクトル
        Vector3 forward = centerEyeAnchor.forward;
        Vector3 right   = centerEyeAnchor.right;
        forward.y = 0f;
        right.y   = 0f;
        forward.Normalize();
        right.Normalize();

        // 3. 入力取得
        Vector2 leftInput  = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        Vector2 rightInput = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
        bool pressA        = OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch);

        // 4. ショット方向
        Vector3 launchDir = Vector3.zero;
        if (leftInput.magnitude > 0.1f)
        {
            launchDir = (forward * leftInput.y + right * leftInput.x).normalized;
        }

        // ローカルでも今まで通り動かすなら
        if (leftInput.magnitude > 0.1f && pressA)
        {
            currentVelocity = launchDir * launchPower;
            Debug.Log($"[VR] Shot local dir:{launchDir} power:{launchPower}");
        }

        if (rightInput.magnitude > 0.1f)
        {
            Vector3 moveDirection = (forward * rightInput.y + right * rightInput.x);
            cameraRigRoot.position += moveDirection * normalMoveSpeed * Runner.DeltaTime;
        }

        // 5. ★ pressA が true のフレームだけ送信 ★
        if (pressA)
        {
            SendVrInput(leftInput, rightInput, pressA, forward, right, launchDir);
        }
    }

    // ===== 入力送信用 =====

    private async void SendVrInput(
        Vector2 leftStick,
        Vector2 rightStick,
        bool pressA,
        Vector3 forward,
        Vector3 right,
        Vector3 launchDir
    )
    {
        if (socket == null || socket.State != WebSocketState.Open)
        {
            return;
        }

        var payload = new InputMessage
        {
            clientId = clientId,
            data = new InputPayloadData
            {
                stickLeftX  = leftStick.x,
                stickLeftY  = leftStick.y,
                stickRightX = rightStick.x,
                stickRightY = rightStick.y,
                pressA      = pressA,
                forwardX    = forward.x,
                forwardZ    = forward.z,
                rightX      = right.x,
                rightZ      = right.z,
                launchDirX  = launchDir.x,
                launchDirZ  = launchDir.z,
                timestamp   = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }
        };

        string json  = JsonUtility.ToJson(payload);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        var segment  = new ArraySegment<byte>(bytes);

        try
        {
            await socket.SendAsync(segment, WebSocketMessageType.Text, true, cts.Token);
            // Debug.Log("[WS] Sent: " + json);
        }
        catch (Exception e)
        {
            Debug.LogError("[WS] Send Exception: " + e);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // サーバー経由の実験中はローカル衝突は無視でもOK
    }
}
