using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class MockDataSync : MonoBehaviour
{
    // PCなら "ws://localhost:8080", QuestならIP指定
    public string serverAddress = "ws://localhost:8080";
    public float sendInterval = 0.1f; // 0.1秒ごとに送信

    private ClientWebSocket _ws;
    private CancellationTokenSource _cts;
    private float _timer;

    // 送受信するデータのクラス定義
    [Serializable]
    public class BallData
    {
        public Vector3[] positions; // 8個分の座標配列
    }

    // 自分のモックデータ（送信元）
    private BallData _myData = new BallData();

    private async void Start()
    {
        // 配列の初期化
        _myData.positions = new Vector3[8];

        _ws = new ClientWebSocket();
        _cts = new CancellationTokenSource();

        Debug.Log("Connecting...");
        try
        {
            await _ws.ConnectAsync(new Uri(serverAddress), _cts.Token);
            Debug.Log("✅ Connected! Starting sync...");

            // 受信タスクを裏で走らせる
            _ = ReceiveLoop();
        }
        catch (Exception e)
        {
            Debug.LogError($"Connection Error: {e.Message}");
        }
    }

    private void Update()
    {
        if (_ws == null || _ws.State != WebSocketState.Open) return;

        // 1. モックデータ（座標）を計算で動かす
        UpdateMockCoordinates();

        // 2. 一定間隔で送信
        _timer += Time.deltaTime;
        if (_timer >= sendInterval)
        {
            _timer = 0f;
            BroadcastData();
        }
    }

    // 偽の動きを作る関数（Sin波でゆらゆらさせる）
    private void UpdateMockCoordinates()
    {
        float t = Time.time;
        for (int i = 0; i < 8; i++)
        {
            // ボールごとに少しズラして動かす
            float x = Mathf.Sin(t + i) * 2.0f;
            float y = Mathf.Cos(t + i * 0.5f) * 1.0f;
            float z = i * 1.0f; // Zは固定っぽく配置
            _myData.positions[i] = new Vector3(x, y, z);
        }
    }

    private async void BroadcastData()
    {
        // オブジェクトをJSON文字列に変換
        string json = JsonUtility.ToJson(_myData);
        byte[] buffer = Encoding.UTF8.GetBytes(json);

        try
        {
            await _ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, _cts.Token);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Send Error: {e.Message}");
        }
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[2048]; // データサイズに合わせて調整

        while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
        {
            try
            {
                var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
                else
                {
                    string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    
                    // 受信したデータをログに出す
                    Debug.Log($"📩 Received: {json}");
                    
                    // データの中身を確認したい場合
                    BallData receivedData = JsonUtility.FromJson<BallData>(json);
                    // 例: 1つ目のボールの座標だけログに出してみる
                    // Debug.Log($"Ball 0 Pos: {receivedData.positions[0]}"); 
                }
            }
            catch (Exception)
            {
                break;
            }
        }
    }

    private void OnDestroy()
    {
        _cts?.Cancel();
        _ws?.Dispose();
    }
}