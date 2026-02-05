using UnityEngine;
using TMPro;
using Fusion;

public class ScoreBoard : NetworkBehaviour
{
    // UI参照変数を削除（必要であれば残して非表示にするなどの対応も可）
    // [SerializeField] private TextMeshProUGUI myScoreText;
    // [SerializeField] private TextMeshProUGUI enemyScoreText;

    public override void Render()
    {
        // スコア表示更新処理を削除
    }
}