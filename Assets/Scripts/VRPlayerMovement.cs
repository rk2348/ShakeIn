// VRPlayerMovement.cs（PC側用・入力受信＆球操作）
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
    [SerializeField] private string clientId = "sim-1"; // PC側はシミュレータなので別IDにしておく

    private ClientWebSocket socket;
    private CancellationTokenSource cts = new CancellationTokenSource();

    [Header("操作対象のボール")]
    [SerializeField] private Rigidbody ballRigidbody; // 動かしたい球の Rigidbody をここにアサイン

    [Header("移動パラメータ")]
    [SerializeField] private float moveForce = 5.0f;   // 左スティックで加える力
    [SerializeField] private float dashForce = 10.0f;  // Aボタンでのショットの力

    // ===== 受信した入力を保持する構造体 =====

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

    private Vector2 latestLeftInput = Vector2.zero;
    private bool latestPressA = false;

    private void Awake()
    {
        if (ballRigidbody == null)
        {
            ballRigidbody = GetComponent<Rigidbody>();
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

            // 接続できたら受信ループ開始
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
                    result = await socket.ReceiveAsync(segment, cts.Token);
                    count += result.Count;
                }

                string json = Encoding.UTF8.GetString(buffer, 0, count);
                // Debug.Log("[WS-PC] Received: " + json);

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
        Debug.Log("[WS-PC] Raw json: " + json); // ★ これ追加

        try
        {
            var msg = JsonUtility.FromJson<InputMessage>(json);
            if (msg == null || msg.type != "input" || msg.data == null)
            {
                Debug.LogWarning("[WS-PC] Unknown or invalid message");
                return;
            }

            latestLeftInput = new Vector2(msg.data.stickLeftX, msg.data.stickLeftY);
            latestPressA = msg.data.pressA;

            Debug.Log($"[WS-PC] Parsed input: L({latestLeftInput.x}, {latestLeftInput.y}) A={latestPressA}");
        }
        catch (Exception e)
        {
            Debug.LogError("[WS-PC] JSON Parse Exception: " + e + " / raw: " + json);
        }
    }


    private void FixedUpdate()
    {
        if (ballRigidbody == null) return;

        var moveDir = new Vector3(latestLeftInput.x, 0f, latestLeftInput.y);
        if (moveDir.sqrMagnitude > 0.01f)
        {
            ballRigidbody.AddForce(moveDir.normalized * moveForce, ForceMode.Acceleration);
        }

        if (latestPressA)
        {
            var dashDir = (moveDir.sqrMagnitude > 0.01f ? moveDir.normalized : Vector3.forward);
            ballRigidbody.AddForce(dashDir * dashForce, ForceMode.VelocityChange);
            latestPressA = false;
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
}
