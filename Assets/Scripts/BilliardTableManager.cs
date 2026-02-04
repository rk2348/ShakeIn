using UnityEngine;
using Fusion;
using System.Collections.Generic;

public class BilliardTableManager : NetworkBehaviour
{
    private List<BilliardBall> allBalls = new List<BilliardBall>();

    public static BilliardTableManager Instance { get; private set; }

    public override void Spawned()
    {
        Instance = this;
    }

    /// ボールがSpawnedされたタイミングで呼び出し
    public void RegisterBall(BilliardBall ball)
    {
        if (!allBalls.Contains(ball))
        {
            allBalls.Add(ball);
        }
    }

    /// ボールがDespawnedされるタイミングで呼び出し
    public void UnregisterBall(BilliardBall ball)
    {
        if (allBalls.Contains(ball))
        {
            allBalls.Remove(ball);
        }
    }

    public override void FixedUpdateNetwork()
    {
        //ボールのペアごとに衝突判定を実行
        for (int i = 0; i < allBalls.Count; i++)
        {
            //念の為、オブジェクトが無効になっていないかチェック
            if (allBalls[i] == null || !allBalls[i].Object.IsValid) continue;

            for (int j = i + 1; j < allBalls.Count; j++)
            {
                if (allBalls[j] == null || !allBalls[j].Object.IsValid) continue;

                //衝突解決メソッド呼び出し
                allBalls[i].ResolveBallCollision(allBalls[j]);
            }
        }
    }
}