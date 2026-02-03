using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks; // 追加：Task.Delayを使うため
using UnityEngine;
using TMPro;

public class NetworkLauncher : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Network Settings")]
    [SerializeField] private NetworkPrefabRef playerPrefab;
    [SerializeField] private int targetSceneIndex = 1;

    [Header("UI References")]
    [SerializeField] private TMP_InputField roomNameInputField;

    private NetworkRunner _runner;

    // --- 追加：起動5秒後にStartGameを呼ぶ ---
    private async void Start()
    {
        Debug.Log("5秒後に自動でStartGameを呼び出します...");
        
        // 5000ミリ秒 = 5秒待機
        await Task.Delay(1000);

        // StartGameを実行
        StartGame();
    }

    public async void StartGame()
    {
        // NullReferenceException対策：InputFieldが空でないかチェック
        if (roomNameInputField == null)
        {
            Debug.LogError("Room Name Input Field がインスペクターで設定されていません！");
            return;
        }

        if (_runner != null) Destroy(_runner.gameObject);

        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;

        string sessionName = string.IsNullOrEmpty(roomNameInputField.text) ? "TestRoom" : roomNameInputField.text;

        // 【修正：CS0029対策】 intをSceneRefに変換して渡す
        SceneRef sceneRef = SceneRef.FromIndex(targetSceneIndex);

        await _runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Shared,
            SessionName = sessionName,
            Scene = sceneRef,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });
    }

    // --- 以下、インターフェースの実装（変更なし） ---
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (player == runner.LocalPlayer)
        {
            runner.Spawn(playerPrefab, Vector3.zero, Quaternion.identity, player);
        }
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}