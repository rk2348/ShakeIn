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

    // ★追加: 対戦開始パネル（「対戦相手が揃いました」等のUI）
    [SerializeField] private GameObject matchFoundPanel;

    private NetworkRunner _runner;

    private async void Start()
    {
        // ★追加: 開始時はパネルを隠しておく（インスペクターで非表示にしてあれば不要ですが念の為）
        if (matchFoundPanel != null) matchFoundPanel.SetActive(false);

        Debug.Log("5秒後に自動でStartGameを呼び出します...");
        await Task.Delay(1000);
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
            PlayerCount = 2, // ★ここで最大人数を2人に制限します
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });
    }

    // --- インターフェースの実装 ---
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        // ★追加: プレイヤー人数をチェックしてパネルを表示
        // FusionのPlayerCountは参加済みの人数を返します
        if (runner.SessionInfo.PlayerCount >= 2)
        {
            Debug.Log("対戦相手が揃いました！");
            if (matchFoundPanel != null) matchFoundPanel.SetActive(true);
        }

        if (player == runner.LocalPlayer)
        {
            // --- 自分のアバター生成処理（既存のコード） ---
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
        // ★追加: 誰かが抜けて2人未満になったらパネルを消す（任意）
        if (runner.SessionInfo.PlayerCount < 2)
        {
            if (matchFoundPanel != null) matchFoundPanel.SetActive(false);
        }
    }

    // --- 他のコールバック（変更なし） ---
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