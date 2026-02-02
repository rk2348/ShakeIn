using Fusion;
using UnityEngine;

/// <summary>
/// プレイヤーが参加した際にアバターを生成するクラス
/// </summary>
public class PlayerSpawner : SimulationBehaviour, IPlayerJoined
{
    [SerializeField] private NetworkObject playerPrefab; // 生成するアバターのプレハブ

    public void PlayerJoined(PlayerRef player)
    {
        // Photon FusionのHostモードでは、ホスト（Server）のみがネットワークオブジェクトを生成できます
        if (Runner.IsServer)
        {
            Debug.Log($"Player {player} joined. Spawning Avatar for them...");

            // アバターをネットワーク上に生成
            // 第4引数に player を渡すことで、そのプレイヤーに「入力権限（Input Authority）」を与えます
            // これにより、SyncAvatar.cs 内の Object.HasInputAuthority が正しく判定されます
            Runner.Spawn(playerPrefab, Vector3.zero, Quaternion.identity, player);
        }
    }
}