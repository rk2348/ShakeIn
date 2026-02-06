using UnityEngine;
using Fusion;
using System.Collections.Generic;

public class BilliardTableManager : NetworkBehaviour
{
    // 全ボールを管理するリスト
    private List<BilliardBall> allBalls = new List<BilliardBall>();

    public static BilliardTableManager Instance { get; private set; }

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

    public int GetNextTargetBallNumber()
    {
        int minNumber = int.MaxValue;
        bool found = false;

        foreach (var ball in allBalls)
        {
            if (ball != null && ball.Object != null && ball.Object.IsValid && ball.BallNumber > 0)
            {
                if (ball.BallNumber < minNumber)
                {
                    minNumber = ball.BallNumber;
                    found = true;
                }
            }
        }

        return found ? minNumber : 0;
    }

    public override void FixedUpdateNetwork()
    {
        // リスト内のボール同士の衝突判定を回す
        for (int i = 0; i < allBalls.Count; i++)
        {
            // ボールが無効ならスキップ
            if (allBalls[i] == null || !allBalls[i].Object.IsValid) continue;

            // 自分が権限を持っているボールについてのみ、衝突判定を行う
            if (!allBalls[i].Object.HasStateAuthority) continue;

            for (int j = i + 1; j < allBalls.Count; j++)
            {
                if (allBalls[j] == null || !allBalls[j].Object.IsValid) continue;

                // 衝突解決処理の呼び出し
                allBalls[i].ResolveBallCollision(allBalls[j]);
            }
        }
    }
}