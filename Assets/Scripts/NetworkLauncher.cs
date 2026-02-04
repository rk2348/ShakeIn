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
    // ★変更: 単一のプレハブ指定から、リスト形式に変更しました
    [SerializeField] private List<NetworkPrefabRef> playerPrefabs;
    [SerializeField] private int targetSceneIndex = 1;

    [Header("UI References")]
    [SerializeField] private TMP_InputField roomNameInputField;

    private NetworkRunner _runner;

    // --- 起動5秒後にStartGameを呼ぶ ---
    private async void Start()
    {
        Debug.Log("5秒後に自動でStartGameを呼び出します...");

        // 5000ミリ秒 = 5秒待機 (※元のコードに合わせて1000msのままにしていますが必要なら変更してください)
        await Task.Delay(1000);

        // StartGameを実行
        StartGame();
    }

    public async void StartGame()
    {
        // NullReferenceException対策
        if (roomNameInputField == null)
        {
            Debug.LogError("Room Name Input Field がインスペクターで設定されていません！");
            return;
        }

        if (_runner != null) Destroy(_runner.gameObject);

        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;

        string sessionName = string.IsNullOrEmpty(roomNameInputField.text) ? "TestRoom" : roomNameInputField.text;

        // intをSceneRefに変換して渡す
        SceneRef sceneRef = SceneRef.FromIndex(targetSceneIndex);

        await _runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Shared,
            SessionName = sessionName,
            Scene = sceneRef,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });
    }

    // --- インターフェースの実装 ---
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (player == runner.LocalPlayer)
        {
            // ★変更: 参加順序に応じてプレハブを選択して生成する処理

            // プレハブリストが正しく設定されているか確認
            if (playerPrefabs != null && playerPrefabs.Count > 0)
            {
                // 現在の部屋のプレイヤー人数を取得 (1人目=1, 2人目=2...)
                int playerCount = runner.SessionInfo.PlayerCount;

                // 配列のインデックスを決定 (1人目=0, 2人目=1...)
                // ※人数が登録プレハブ数を超えた場合はループします (例: 3種類登録で4人目が来たら0番目を使用)
                int index = (playerCount - 1) % playerPrefabs.Count;

                Debug.Log($"【Spawn】プレイヤー人数: {playerCount}人目 -> Prefab Index: {index} を生成します。");

                // 選択されたプレハブを生成
                runner.Spawn(playerPrefabs[index], Vector3.zero, Quaternion.identity, player);
            }
            else
            {
                Debug.LogError("【Error】NetworkLauncherの 'Player Prefabs' リストが空です！インスペクターで設定してください。");
            }
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