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

    /// <summary>
    /// 現在盤面にある的球（BallNumber > 0）の中で最小の番号を返す。
    /// 的球がない場合は0を返す。
    /// </summary>
    public int GetNextTargetBallNumber()
    {
        int minNumber = int.MaxValue;
        bool found = false;

        foreach (var ball in allBalls)
        {
            // ボールオブジェクトが有効で、かつ手球(0)ではない場合
            if (ball != null && ball.Object != null && ball.Object.IsValid && ball.BallNumber > 0)
            {
                if (ball.BallNumber < minNumber)
                {
                    minNumber = ball.BallNumber;
                    found = true;
                }
            }
        }

        // 見つかった場合はその番号、見つからなければ0（すべて落ちた等）を返す
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