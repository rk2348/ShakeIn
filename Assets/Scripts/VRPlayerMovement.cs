// VRPlayerMovement.cs（PC側・WebSocket受信で元のビリヤード挙動を再現）
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
    [SerializeField] private string clientId = "sim-1";

    private ClientWebSocket socket;
    private CancellationTokenSource cts = new CancellationTokenSource();

    [Header("ビリヤード: ショット設定")]
    [SerializeField] private float launchPower = 10.0f;  // ぶっ飛ばす初速
    [SerializeField] private float friction = 0.98f;     // 減速率
    [SerializeField] private float stopThreshold = 0.01f; // 停止とみなす速度

    [Header("通常: スティック移動設定")]
    [SerializeField] private float normalMoveSpeed = 2.0f; // 通常移動の速度

    [Header("移動対象（元の cameraRigRoot 相当）")]
    [SerializeField] private Transform cameraRigRoot;    // 実際に動かすプレイヤーのRoot
    [SerializeField] private Transform centerEyeAnchor;  // 向きの基準（なければ cameraRigRoot を使う）

    // 慣性用
    private Vector3 currentVelocity;

    // ==== WebSocket から受信するデータ構造 ====
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
        public string type;
        public string clientId;
        public string role;
        public InputPayloadData data;
    }

    // 受信した最新入力（簡易版なので lock は省略）
    private Vector2 latestLeftInput  = Vector2.zero;
    private Vector2 latestRightInput = Vector2.zero;
    private bool   latestPressA      = false;
    private Vector3 latestForward    = Vector3.forward;
    private Vector3 latestRight      = Vector3.right;
    private Vector3 latestLaunchDir  = Vector3.zero;

    private void Awake()
    {
        if (cameraRigRoot == null)
        {
            cameraRigRoot = this.transform;
        }
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
                WebSocketReceiveResult result =
                    await socket.ReceiveAsync(segment, cts.Token);

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
                    result  = await socket.ReceiveAsync(segment, cts.Token);
                    count  += result.Count;
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
        try
        {
            var msg = JsonUtility.FromJson<InputMessage>(json);
            if (msg == null || msg.type != "input" || msg.data == null)
            {
                return;
            }

            latestLeftInput  = new Vector2(msg.data.stickLeftX, msg.data.stickLeftY);
            latestRightInput = new Vector2(msg.data.stickRightX, msg.data.stickRightY);
            latestPressA     = msg.data.pressA;

            latestForward = new Vector3(msg.data.forwardX, 0f, msg.data.forwardZ);
            latestRight   = new Vector3(msg.data.rightX,   0f, msg.data.rightZ);
            latestLaunchDir = new Vector3(msg.data.launchDirX, 0f, msg.data.launchDirZ);

            if (latestForward.sqrMagnitude > 0.0001f) latestForward.Normalize();
            if (latestRight.sqrMagnitude   > 0.0001f) latestRight.Normalize();

            // Debug.Log($"[WS-PC] Parsed input: L({latestLeftInput.x},{latestLeftInput.y}) A={latestPressA} launchDir={latestLaunchDir}");
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

        // ===== 1. 元コードと同じ「慣性移動」 =====
        if (currentVelocity.magnitude > stopThreshold)
        {
            cameraRigRoot.position += currentVelocity * Time.fixedDeltaTime;
            currentVelocity *= friction;
        }
        else
        {
            currentVelocity = Vector3.zero;
        }

        // ===== 2. 向きベクトル =====
        Vector3 forward = latestForward;
        Vector3 right   = latestRight;

        // 念のため、forward/right がゼロになっていたら centerEyeAnchor から再計算
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = centerEyeAnchor.forward;
            forward.y = 0f;
            forward.Normalize();
        }
        if (right.sqrMagnitude < 0.0001f)
        {
            right = centerEyeAnchor.right;
            right.y = 0f;
            right.Normalize();
        }

        // ===== 3. 入力を取り出し =====
        Vector2 leftInput  = latestLeftInput;
        Vector2 rightInput = latestRightInput;
        bool     pressA    = latestPressA;

        // pressA は 1 フレームで消費
        latestPressA = false;

        // ===== 4. ビリヤード風ショット =====
        // Quest 側は pressA のフレームだけ送ってくるので、そのときだけ currentVelocity を更新
        if (pressA)
        {
            Vector3 dir = latestLaunchDir;

            // 念のため、launchDir がゼロならここで作り直す
            if (dir.sqrMagnitude < 0.0001f && leftInput.magnitude > 0.1f)
            {
                dir = (forward * leftInput.y + right * leftInput.x).normalized;
            }

            if (dir.sqrMagnitude > 0.0001f)
            {
                currentVelocity = dir.normalized * launchPower;
                Debug.Log($"[WS-PC] Remote Shot dir:{dir} power:{launchPower}");
            }
        }

        // ===== 5. 通常移動（必要なら） =====
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

    // 元の OnCollisionEnter の「ボールに Velocity を渡す処理」も、
    // このクラスにそのままコピペすれば、PC側での当たり判定＆ボール挙動も再現できます。
    // （Fusion を使うなら NetworkBehaviour に戻して HasStateAuthority チェックを復活させるイメージ）
}
