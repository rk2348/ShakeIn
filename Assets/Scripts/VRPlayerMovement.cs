// VRPlayerMovement.cs
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Fusion;
using NativeWebSocket; // ★ これを追加（パッケージ導入済み前提）

public class VRPlayerMovement : NetworkBehaviour
{
    [Header("WebSocket設定")]
    [SerializeField] private string serverUrl = "ws://localhost:8080";
    [SerializeField] private string clientId = "vr-1";

    private WebSocket socket;

    [Header("ビリヤード: ショット設定")]
    [SerializeField] private float launchPower = 10.0f;
    [SerializeField] private float friction = 0.98f;
    [SerializeField] private float stopThreshold = 0.01f;

    [Header("通常: スティック移動設定")]
    [SerializeField] private float normalMoveSpeed = 2.0f;

    private Transform cameraRigRoot;
    private Transform centerEyeAnchor;
    private Vector3 currentVelocity;

    // ===== JSON 用の小さなクラス群 =====

    [Serializable]
    private class InputPayloadData
    {
        public float stickLeftX;
        public float stickLeftY;
        public float stickRightX;
        public float stickRightY;
        public bool pressA;
        public long timestamp;
    }

    [Serializable]
    private class InputMessage
    {
        public string type = "input";
        public string clientId;
        public string role = "vr";
        public InputPayloadData data;
    }

    // ===== WebSocket 接続まわり =====

    private async void Awake()
    {
        // HasStateAuthority は Spawned でしか正しくないので、ここではまだ接続だけ用意しておく
        socket = new WebSocket(serverUrl);

        socket.OnOpen += () =>
        {
            Debug.Log("[WS] Connected to server");
        };

        socket.OnError += (e) =>
        {
            Debug.LogError("[WS] Error: " + e);
        };

        socket.OnClose += (e) =>
        {
            Debug.Log("[WS] Closed with code: " + e);
        };

        socket.OnMessage += (bytes) =>
        {
            var msg = Encoding.UTF8.GetString(bytes);
            Debug.Log("[WS] Received: " + msg);
        };

        await Connect();
    }

    private async Task Connect()
    {
        try
        {
            await socket.Connect();
        }
        catch (Exception e)
        {
            Debug.LogError("[WS] Connect Exception: " + e);
        }
    }

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        socket?.DispatchMessageQueue();
#endif
    }

    private async void OnApplicationQuit()
    {
        if (socket != null)
        {
            await socket.Close();
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
                cameraRigRoot = rig.transform;
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

        // === ここでは「入力を読んでサーバーへ送るだけ」にする ===

        Vector2 leftInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        bool pressA = OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch);

        Vector2 rightInput = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);

        SendVrInput(leftInput, rightInput, pressA);

        // 実際の移動処理は一旦無視したいなら、ここでは position をいじらない
        // （今は「サーバーに入力が飛べば勝ち」だから）
    }

    // ===== 入力送信用メソッド =====

    private async void SendVrInput(Vector2 leftStick, Vector2 rightStick, bool pressA)
    {
        if (socket == null || socket.State != WebSocketState.Open)
        {
            // まだ接続されていない/切れている場合は何もしない
            return;
        }

        var payload = new InputMessage
        {
            clientId = clientId,
            data = new InputPayloadData
            {
                stickLeftX = leftStick.x,
                stickLeftY = leftStick.y,
                stickRightX = rightStick.x,
                stickRightY = rightStick.y,
                pressA = pressA,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }
        };

        string json = JsonUtility.ToJson(payload);

        try
        {
            await socket.SendText(json);
            // Debug.Log("[WS] Sent: " + json);
        }
        catch (Exception e)
        {
            Debug.LogError("[WS] Send Exception: " + e);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 今回は無視でOK（ローカルでボール動かさないなら）
    }
}
