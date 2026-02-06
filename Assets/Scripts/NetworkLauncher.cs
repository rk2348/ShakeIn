using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;

public class NetworkLauncher : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Network Settings")]
    [SerializeField] private List<NetworkPrefabRef> playerPrefabs;
    [SerializeField] private int targetSceneIndex = 1;

    [Header("UI References")]
    [SerializeField] private TMP_InputField roomNameInputField;

    private NetworkRunner _runner;

    private async void Start()
    {
        Debug.Log("3秒後に自動でStartGameを呼び出します...");
        await Task.Delay(2000);
        StartGame();
    }

    public async void StartGame()
    {
        if (roomNameInputField == null)
        {
            Debug.LogError("Room Name Input Field がインスペクターで設定されていません！");
            return;
        }

        if (_runner != null) Destroy(_runner.gameObject);

        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;

        string sessionName = string.IsNullOrEmpty(roomNameInputField.text) ? "TestRoom" : roomNameInputField.text;
        SceneRef sceneRef = SceneRef.FromIndex(targetSceneIndex);

        await _runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Shared,
            SessionName = sessionName,
            Scene = sceneRef,
            PlayerCount = 2, // 最大人数制限
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (player == runner.LocalPlayer)
        {
            if (playerPrefabs != null && playerPrefabs.Count > 0)
            {
                int playerCount = runner.SessionInfo.PlayerCount;
                int index = (playerCount - 1) % playerPrefabs.Count;
                Debug.Log($"【Spawn】プレイヤー人数: {playerCount}人目 -> Prefab Index: {index} を生成します。");
                runner.Spawn(playerPrefabs[index], Vector3.zero, Quaternion.identity, player);
            }
            else
            {
                Debug.LogError("【Error】NetworkLauncherの 'Player Prefabs' リストが空です！");
            }
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        // プレイヤーが退出したときの処理
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
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