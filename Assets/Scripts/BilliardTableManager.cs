using UnityEngine;
using Fusion;
using System.Collections.Generic;

public class BilliardTableManager : NetworkBehaviour
{
    // シーン内のボールを保持するリスト
    private List<BilliardBall> allBalls = new List<BilliardBall>();

    public override void FixedUpdateNetwork()
    {
        // 1. シーン内の全てのボールを取得（※パフォーマンスのため、実際はSpawn時に登録するのが理想的です）
        RefreshBallList();

        // 2. ボールのペアごとに衝突判定を実行（二重計算を避けるためのループ構成）
        for (int i = 0; i < allBalls.Count; i++)
        {
            for (int j = i + 1; j < allBalls.Count; j++)
            {
                // BilliardBall.cs に定義されている衝突解決メソッドを呼び出す
                allBalls[i].ResolveBallCollision(allBalls[j]);
            }
        }
    }

    private void RefreshBallList()
    {
        allBalls.Clear();
        allBalls.AddRange(FindObjectsOfType<BilliardBall>());
    }
}