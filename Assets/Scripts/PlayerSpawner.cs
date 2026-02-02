using Fusion;
using UnityEngine;

// NetworkBehaviourを継承し、Callbacksインターフェースを実装する
public class PlayerSpawner : SimulationBehaviour, IPlayerJoined
{
    [SerializeField] private NetworkObject playerPrefab; // 生成するアバターのプレハブ

    public void PlayerJoined(PlayerRef player)
    {
        // 参加したのが自分（ローカルプレイヤー）の場合のみ生成する
        if (player == Runner.LocalPlayer)
        {
            Debug.Log("Local Player Joined. Spawning Avatar...");

            // アバターをネットワーク上に生成
            // 引数: プレハブ, 位置, 回転, 入力権限を与えるプレイヤー
            Runner.Spawn(playerPrefab, Vector3.zero, Quaternion.identity, player);
        }
    }
}