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

    // ==== 提供されたコードに基づく物理設定 ====
    [Header("ビリヤード: ショット設定")]
    [SerializeField] private float launchPower = 10.0f;
    [SerializeField] private float friction = 0.98f;
    [SerializeField] private float stopThreshold = 0.01f;
    [SerializeField][Range(0f, 1f)] private float collisionSpeedRetention = 0.2f; // 衝突後に残る速度割合

    // ==== 提供されたコードに基づくガイドライン設定 ====
    [Header("ガイド線設定")]
    [SerializeField] private LineRenderer directionLine; // InspectorでLineRendererをアタッチ
    [SerializeField] private float lineLength = 2.0f;    // 線の長さ

    [Header("通常: スティック移動設定")]
    [SerializeField] private float normalMoveSpeed = 2.0f;

    [Header("移動対象")]
    [SerializeField] private Transform cameraRigRoot;
    [SerializeField] private Transform centerEyeAnchor;

    // 内部変数
    private Vector3 currentVelocity;
    private Rigidbody myRigidbody;

    // ==== WebSocket データ構造 ====
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
        public string type;
        public string clientId;
        public string role;
        public InputPayloadData data;
    }

    // 受信データ保持用
    private Vector2 latestLeftInput  = Vector2.zero;
    private Vector2 latestRightInput = Vector2.zero;
    private bool    latestPressA     = false;
    private Vector3 latestForward    = Vector3.forward;
    private Vector3 latestRight      = Vector3.right;
    private Vector3 latestLaunchDir  = Vector3.zero;

    private void Awake()
    {
        if (cameraRigRoot == null) cameraRigRoot = this.transform;
        if (centerEyeAnchor == null) centerEyeAnchor = cameraRigRoot;

        // 物理挙動用 Rigidbody 設定
        myRigidbody = cameraRigRoot.GetComponent<Rigidbody>();
        // if (myRigidbody == null)
        // {
        //     myRigidbody = cameraRigRoot.gameObject.AddComponent<Rigidbody>();
        //     myRigidbody.useGravity = false;
        //     myRigidbody.isKinematic = true; // プログラム制御のためKinematic
        // }

        // ガイド線の初期化
        if (directionLine != null) directionLine.enabled = false;

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
                var result = await socket.ReceiveAsync(segment, cts.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Debug.Log("[WS-PC] Server closed connection");
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
                HandleInputJson(json);
            }
            catch (Exception) { break; }
        }
    }

    private void HandleInputJson(string json)
    {
        try
        {
            var msg = JsonUtility.FromJson<InputMessage>(json);
            if (msg == null || msg.type != "input" || msg.data == null) return;

            latestLeftInput  = new Vector2(msg.data.stickLeftX, msg.data.stickLeftY);
            latestRightInput = new Vector2(msg.data.stickRightX, msg.data.stickRightY);
            latestPressA     = msg.data.pressA;

            latestForward = new Vector3(msg.data.forwardX, 0f, msg.data.forwardZ);
            latestRight   = new Vector3(msg.data.rightX,   0f, msg.data.rightZ);
            latestLaunchDir = new Vector3(msg.data.launchDirX, 0f, msg.data.launchDirZ);

            if (latestForward.sqrMagnitude > 0.0001f) latestForward.Normalize();
            if (latestRight.sqrMagnitude   > 0.0001f) latestRight.Normalize();
        }
        catch (Exception e) { Debug.LogError("[WS-PC] Parse Error: " + e); }
    }

    private void Update()
    {
        // ==== ガイド線の描画 (PC画面上のデバッグ用として機能します) ====
        if (directionLine != null)
        {
            // 受信したスティック入力を使用
            if (latestLeftInput.magnitude > 0.1f)
            {
                directionLine.enabled = true;

                // 向き計算
                Vector3 forward = latestForward;
                Vector3 right   = latestRight;

                Vector3 aimDir = (forward * latestLeftInput.y + right * latestLeftInput.x).normalized;
                Vector3 startPos = cameraRigRoot.position;

                directionLine.SetPosition(0, startPos);
                directionLine.SetPosition(1, startPos + aimDir * lineLength);
            }
            else
            {
                directionLine.enabled = false;
            }
        }
    }

    private void FixedUpdate()
    {
        if (cameraRigRoot == null) return;

        // ===== 1. 慣性移動 =====
        if (currentVelocity.magnitude > stopThreshold)
        {
            if (myRigidbody != null)
                myRigidbody.MovePosition(myRigidbody.position + currentVelocity * Time.fixedDeltaTime);
            else
                cameraRigRoot.position += currentVelocity * Time.fixedDeltaTime;
            
            currentVelocity *= friction; // 摩擦による減速
        }
        else
        {
            currentVelocity = Vector3.zero;
        }

        // ===== 2. 入力データの取得 =====
        Vector3 forward = latestForward;
        Vector3 right   = latestRight;
        Vector2 leftInput  = latestLeftInput;
        Vector2 rightInput = latestRightInput;
        bool    pressA     = latestPressA;
        latestPressA = false; // フレーム消費

        // ===== 3. ショット処理 =====
        if (pressA)
        {
            Vector3 launchDir = latestLaunchDir;
            
            // もしLaunchDirが来てなければ計算
            if (launchDir.sqrMagnitude < 0.001f && leftInput.magnitude > 0.1f)
            {
                launchDir = (forward * leftInput.y + right * leftInput.x).normalized;
            }

            if (launchDir.sqrMagnitude > 0.001f)
            {
                currentVelocity = launchDir * launchPower;
                Debug.Log($"[WS-PC] Shot! Dir:{launchDir} Power:{launchPower}");
            }
        }

        // ===== 4. 通常移動 =====
        if (rightInput.magnitude > 0.1f)
        {
            Vector3 moveDirection = (forward * rightInput.y + right * rightInput.x);
            if (myRigidbody != null)
                myRigidbody.MovePosition(myRigidbody.position + moveDirection * normalMoveSpeed * Time.fixedDeltaTime);
            else
                cameraRigRoot.position += moveDirection * normalMoveSpeed * Time.fixedDeltaTime;
        }
    }

    // ===== 5. 衝突判定 (指定されたロジックを実装) =====
    private void OnCollisionEnter(Collision collision)
    {
        // 壁との衝突 (Wallタグ判定)
        if (collision.gameObject.CompareTag("Wall"))
        {
            Vector3 normal = collision.contacts[0].normal;
            currentVelocity = Vector3.Reflect(currentVelocity, normal);
            currentVelocity *= 0.8f; // 壁に当たると速度が0.8倍になる
            
            Debug.Log("[Physics] Hit Wall (Reflect)");
        }
        else
        {
            // ボールとの衝突 (相手がRigidbodyを持っているかで判定)
            Rigidbody targetRb = collision.gameObject.GetComponent<Rigidbody>();
            if (targetRb != null)
            {
                // 自分 -> 相手 へのベクトル
                Vector3 dir = (targetRb.position - transform.position).normalized;
                dir.y = 0; // 高さは無視

                float power = Mathf.Max(currentVelocity.magnitude, 1.0f);
                
                // 相手を弾き飛ばす (1.2倍のパワー)
                Vector3 newVelocity = dir * power * 1.2f;
                targetRb.linearVelocity = newVelocity;

                // 自分は速度を落として少し残る (collisionSpeedRetention)
                currentVelocity *= collisionSpeedRetention;

                Debug.Log($"[Physics] Hit Ball. Retention:{collisionSpeedRetention}, TargetVel:{newVelocity.magnitude}");
            }
        }
    }

    private async void OnApplicationQuit()
    {
        cts.Cancel();
        if (socket != null) { socket.Dispose(); }
    }
}