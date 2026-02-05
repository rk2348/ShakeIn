using UnityEngine;
using TMPro;
using Fusion;

public class ScoreBoard : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI myScoreText;    // 自分のスコア表示用
    [SerializeField] private TextMeshProUGUI enemyScoreText; // 相手のスコア表示用

    // CountUp.csと同様、Renderは毎フレーム呼ばれ、画面表示の更新に適しています
    public override void Render()
    {
        // マネージャーがまだ生成されていない場合は何もしない
        if (BilliardTableManager.Instance == null) return;

        // 現在セッションに参加している全プレイヤーをループして確認
        foreach (var player in Runner.ActivePlayers)
        {
            // マネージャーからそのプレイヤーの現在のスコアを取得
            int score = BilliardTableManager.Instance.GetScore(player);

            // 自分か相手かを判定して表示を更新
            if (player == Runner.LocalPlayer)
            {
                // 自分 (LocalPlayer) の場合
                if (myScoreText != null)
                {
                    myScoreText.text = $"Me: {score}";
                }
            }
            else
            {
                // 自分以外（相手）の場合
                if (enemyScoreText != null)
                {
                    enemyScoreText.text = $"Enemy: {score}";
                }
            }
        }
    }
}