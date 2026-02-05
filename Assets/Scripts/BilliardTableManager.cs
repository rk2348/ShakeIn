using UnityEngine;
using Fusion;
using System.Collections.Generic;

public class BilliardTableManager : NetworkBehaviour
{
    private List<BilliardBall> allBalls = new List<BilliardBall>();

    public static BilliardTableManager Instance { get; private set; }

    // Dictionaryは => default; で初期化
    [Networked, Capacity(4)] private NetworkDictionary<PlayerRef, int> PlayerScores => default;

    public override void Spawned()
    {
        Instance = this;
    }

    public void RegisterBall(BilliardBall ball)
    {
        if (!allBalls.Contains(ball)) allBalls.Add(ball);
    }

    public void UnregisterBall(BilliardBall ball)
    {
        if (allBalls.Contains(ball)) allBalls.Remove(ball);
    }

    public override void FixedUpdateNetwork()
    {
        for (int i = 0; i < allBalls.Count; i++)
        {
            if (allBalls[i] == null || !allBalls[i].Object.IsValid) continue;

            for (int j = i + 1; j < allBalls.Count; j++)
            {
                if (allBalls[j] == null || !allBalls[j].Object.IsValid) continue;
                allBalls[i].ResolveBallCollision(allBalls[j]);
            }
        }
    }

    // スコア加算
    public void AddScore(PlayerRef player, int points)
    {
        // 【修正】IsValid ではなく PlayerRef.None と比較してチェックする
        if (player != PlayerRef.None)
        {
            int current = 0;
            if (PlayerScores.ContainsKey(player))
            {
                current = PlayerScores.Get(player);
            }

            PlayerScores.Set(player, current + points);
            Debug.Log($"Player {player.PlayerId} Score: {current + points}");
        }
    }

    // スコア取得
    public int GetScore(PlayerRef player)
    {
        if (PlayerScores.ContainsKey(player))
        {
            return PlayerScores.Get(player);
        }
        return 0;
    }
}