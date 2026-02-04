using UnityEngine;
using Fusion;
using System.Collections.Generic;

public class BilliardTableManager : NetworkBehaviour
{
    // シーン内のボールを保持するリスト
    private List<BilliardBall> allBalls = new List<BilliardBall>();

    // どこからでもアクセスできるようにシングルトン化（任意ですが便利です）
    public static BilliardTableManager Instance { get; private set; }

    public override void Spawned()
    {
        Instance = this;
    }

    /// <summary>
    /// ボールがSpawnedされたタイミングで、ボール自身から呼び出されます。
    /// </summary>
    public void RegisterBall(BilliardBall ball)
    {
        if (!allBalls.Contains(ball))
        {
            allBalls.Add(ball);
        }
    }

    /// <summary>
    /// ボールがDespawnedされるタイミングで呼び出されます。
    /// </summary>
    public void UnregisterBall(BilliardBall ball)
    {
        if (allBalls.Contains(ball))
        {
            allBalls.Remove(ball);
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 以前の RefreshBallList() は削除しました。
        // リストは RegisterBall/UnregisterBall で自動管理されるため、毎フレーム検索する必要はありません。

        // ボールのペアごとに衝突判定を実行
        for (int i = 0; i < allBalls.Count; i++)
        {
            // 念の為、オブジェクトが無効になっていないかチェック
            if (allBalls[i] == null || !allBalls[i].Object.IsValid) continue;

            for (int j = i + 1; j < allBalls.Count; j++)
            {
                if (allBalls[j] == null || !allBalls[j].Object.IsValid) continue;

                // BilliardBall.cs に定義されている衝突解決メソッドを呼び出す
                allBalls[i].ResolveBallCollision(allBalls[j]);
            }
        }
    }
}