using UnityEngine;
using TMPro;
using Fusion;

public class ScoreBoard : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI nextTargetText;

    // 毎フレーム（レンダリング時）呼ばれる
    public override void Render()
    {
        // マネージャーまたはテキストUIがない場合は何もしない
        if (BilliardTableManager.Instance == null || nextTargetText == null) return;

        // 次に狙うべきボール番号を取得
        int nextBall = BilliardTableManager.Instance.GetNextTargetBallNumber();

        if (nextBall > 0)
        {
            nextTargetText.text = $"Next Target: {nextBall}";
        }
        else
        {
            // 的球が一つもない場合
            nextTargetText.text = "Game Clear / No Balls";
        }
    }
}