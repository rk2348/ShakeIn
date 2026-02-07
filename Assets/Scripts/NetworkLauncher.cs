using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;

public class NetworkLauncher : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("UI References")]
    [SerializeField] private GameObject matchingStatusUI;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Game Settings")]
    [SerializeField] private NetworkPrefabRef playerPrefab;
    [SerializeField] private int gameSceneIndex = 1;

    // 横並びの間隔（メートル）
    [SerializeField] private float spawnSpacing = 1.5f;

    private NetworkRunner _runner;

    public void OnFindMatchButtonClicked()
    {
        StartGame(GameMode.Shared);
    }

    async void StartGame(GameMode mode)
    {
        if (matchingStatusUI != null) matchingStatusUI.SetActive(true);
        if (statusText != null) statusText.text = "Connecting...";

        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;
        var sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

        await _runner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = "",
            Scene = SceneRef.FromIndex(gameSceneIndex),
            PlayerCount = 4,
            SceneManager = sceneManager
        });
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (player == runner.LocalPlayer)
        {
            Debug.Log("【NetworkLauncher】プレイヤーを生成します。");

            // --- 修正箇所: 横並び配置の計算 ---

            // 何人目のプレイヤーか (0, 1, 2...)
            int index = runner.SessionInfo.PlayerCount - 1;

            // 基本の位置（台の手前）
            // X軸を index * spawnSpacing 分だけずらす
            // 例: 1人目(0) -> X=0, 2人目(1) -> X=1.5, 3人目(2) -> X=3.0
            // ※「1人目の横」にしたいので、少し右（または左）に配置します
            float xOffset = index * spawnSpacing;

            // 2人目以降が極端に遠くならないよう、左右に振り分ける場合（オプション）
            // float xOffset = (index % 2 == 0) ? index * spacing / 2 : -((index + 1) * spacing / 2);

            Vector3 spawnPos = new Vector3(xOffset, 1, -2);

            // プレイヤー生成
            // 向きは全員同じ方向（台の方）を向くように設定
            runner.Spawn(playerPrefab, spawnPos, Quaternion.identity, player);
            // ------------------------------------

            if (matchingStatusUI != null) matchingStatusUI.SetActive(false);
        }
    }

    // --- その他のコールバック（変更なし） ---
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        if (statusText != null) statusText.text = $"Connect Failed: {reason}";
    }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        if (statusText != null) statusText.text = "Disconnected";
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
}