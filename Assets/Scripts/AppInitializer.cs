using Fusion;
using UnityEngine;

public class AppInitializer : MonoBehaviour
{
    [SerializeField] private NetworkRunner runnerPrefab;

    async void Start()
    {
        // デバイスIDに基づいて役割を自動判定
        PlayerRole myRole = RoleIdentifier.GetRole();

        var runner = Instantiate(runnerPrefab);

        // 運営側（Staff）なら部屋を作成(Host)、ファン（Guest）なら参加(Client)
        GameMode mode = (myRole == PlayerRole.Staff) ? GameMode.Host : GameMode.Client;

        // シーンの同期（同じ3D空間に合流するため）に必要なマネージャーを追加
        var sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

        await runner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = "HandshakeRoom", // 共通の部屋名
            SceneManager = sceneManager    // エラー箇所を削除し、こちらを追加
        });

        Debug.Log($"Role confirmed: {myRole} / GameMode: {mode}");
    }
}