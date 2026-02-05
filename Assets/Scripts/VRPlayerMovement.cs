// VRPlayerMovement_PC.cs
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using UnityEngine;

/// <summary>
/// PC側用:
/// - WebSocket で Quest から入力を受信
/// - 元のビリヤード風VRPlayerMovementのロジックでカメラリグ（またはプレイヤーのTransform）を動かす
/// - Rigidbody は使わず、currentVelocity と transform.position で物理挙動を再現
/// </summary>
public class VRPlayerMovement_PC : MonoBehaviour
{
    [Header("WebSocket設定")]
    [SerializeField] private string serverUrl = "wss://b400-202-13-170-200.ngrok-free.app";
    [SerializeField] private string clientId = "sim-1";

    private ClientWebSocket socket;
    private CancellationTokenSource cts = new CancellationTokenSource();

    [Header("ビリヤード: ショット設定")]
    [SerializeField] private float launchPower = 10.0f;
    [SerializeField] private float friction = 0.98f;
    [SerializeField] private float stopThreshold = 0.01f;

    [Header("通常: スティック移動設定")]
    [SerializeField] private float normalMoveSpeed = 2.0f;

    [Header("移動対象（カメラリグ相当）")]
    [SerializeField] private Transform cameraRigRoot;    // 動かしたいプレイヤーのルート
    [SerializeField] private Transform centerEyeAnchor;  // 向きの基準になるTransform（なければ cameraRigRoot.forward を使う）

    // 慣性移動用ベクトル
    private Vector3 currentVelocity;

    // WebSocket から受け取った最新入力（スレッドセーフを強く気にしない簡易版）
    private Vector2 latestLeftInput = Vector2.zero;
    private Vector2 latestRightInput = Vector2.zero;
    private bool latestPressA = false;

    #region JSON定義

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
        public string type;
        public string clientId;
        public string role;
        public InputPayloadData data;
    }

    #endregion

    private void Awake()
    {
        if (cameraRigRoot == null)
        {
            cameraRigRoot = this.transform;
        }

        // centerEyeAnchor が未指定なら cameraRigRoot を向きの基準にする
        if (centerEyeAnchor == null)
        {
            centerEyeAnchor = cameraRigRoot;
        }

        socket = new ClientWebSocket();
        ConnectAndStartReceiveLoop();
    }

    private async void ConnectAndStartReceiveLoop()
    {
        try
        {
            var uri = new Uri(serverUrl);
            Debug.Log("[WS-PC] Connecting to " + uri);
            await socket.ConnectAsync(uri, cts.Token);
            Debug.Log("[WS-PC] Connected: " + socket.State);

            _ = ReceiveLoop();
        }
        catch (Exception e)
        {
            Debug.LogError("[WS-PC] Connect Exception: " + e);
        }
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[4096];

        while (socket != null && socket.State == WebSocketState.Open)
        {
            try
            {
                var segment = new ArraySegment<byte>(buffer);
                WebSocketReceiveResult result = await socket.ReceiveAsync(segment, cts.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Debug.Log("[WS-PC] Server closed connection");
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by server", cts.Token);
                    break;
                }

                int count = result.Count;
                while (!result.EndOfMessage)
                {
                    if (count >= buffer.Length)
                    {
                        Debug.LogWarning("[WS-PC] Message too long, truncating");
                        break;
                    }
                    segment = new ArraySegment<byte>(buffer, count, buffer.Length - count);
                    result = await socket.ReceiveAsync(segment, cts.Token);
                    count += result.Count;
                }

                string json = Encoding.UTF8.GetString(buffer, 0, count);
                HandleInputJson(json);
            }
            catch (Exception e)
            {
                Debug.LogError("[WS-PC] Receive Exception: " + e);
                break;
            }
        }
    }

    private void HandleInputJson(string json)
    {
        // Debug.Log("[WS-PC] Raw json: " + json);

        try
        {
            var msg = JsonUtility.FromJson<InputMessage>(json);
            if (msg == null || msg.type != "input" || msg.data == null)
            {
                // Debug.LogWarning("[WS-PC] Unknown or invalid message");
                return;
            }

            latestLeftInput = new Vector2(msg.data.stickLeftX, msg.data.stickLeftY);
            latestRightInput = new Vector2(msg.data.stickRightX, msg.data.stickRightY);
            latestPressA = msg.data.pressA;

            // Debug.Log($"[WS-PC] Parsed input: L({latestLeftInput.x}, {latestLeftInput.y}) A={latestPressA}");
        }
        catch (Exception e)
        {
            Debug.LogError("[WS-PC] JSON Parse Exception: " + e + " / raw: " + json);
        }
    }

    private void FixedUpdate()
    {
        if (cameraRigRoot == null || centerEyeAnchor == null)
        {
            return;
        }

        // ===== 1. 既存ロジックと同じ「慣性移動」 =====
        if (currentVelocity.magnitude > stopThreshold)
        {
            cameraRigRoot.position += currentVelocity * Time.fixedDeltaTime;
            currentVelocity *= friction;
        }
        else
        {
            currentVelocity = Vector3.zero;
        }

        // ===== 2. 向きベクトル（centerEyeAnchor 基準） =====
        Vector3 forward = centerEyeAnchor.forward;
        Vector3 right = centerEyeAnchor.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        // WebSocketからの入力をここで使用
        Vector2 leftInput = latestLeftInput;
        Vector2 rightInput = latestRightInput;
        bool pressA = latestPressA;

        // Aボタンは1回だけ消費
        latestPressA = false;

        // ===== 3. ビリヤード風ショット（左スティック + Aボタン） =====
        if (leftInput.magnitude > 0.1f && pressA)
        {
            Vector3 launchDir = (forward * leftInput.y + right * leftInput.x).normalized;
            currentVelocity = launchDir * launchPower;
            Debug.Log($"[WS-PC] Shot! dir:{launchDir} power:{launchPower}");
        }

        // ===== 4. 通常移動（右スティック） =====
        if (rightInput.magnitude > 0.1f)
        {
            Vector3 moveDirection = (forward * rightInput.y + right * rightInput.x);
            cameraRigRoot.position += moveDirection * normalMoveSpeed * Time.fixedDeltaTime;
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
            Debug.LogError("[WS-PC] Close Exception: " + e);
        }
        finally
        {
            cts.Cancel();
            socket?.Dispose();
        }
    }

    // ★ OnCollisionEnter 部分は、PC側の「プレイヤー」が物理衝突を受ける形で使っていたなら、
    //   そのままコピペして、BilliardBall 側の実装と組み合わせればOK。
    //   （サーバー経由に変えても、そのロジック自体はほぼそのまま使える）
}
