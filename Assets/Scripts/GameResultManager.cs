using UnityEngine;
using Fusion;
using TMPro;

public class GameResultManager : NetworkBehaviour
{
    public static GameResultManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject losePanel;
    //[SerializeField] private TextMeshProUGUI winText; // オプション: 勝者名表示用

    public override void Spawned()
    {
        Instance = this;
        // ゲーム開始時はパネルを隠す
        if (winPanel) winPanel.SetActive(false);
        if (losePanel) losePanel.SetActive(false);
    }

    // 9番ボールが入った時にBilliardBallから呼ばれる
    public void OnNineBallPotted(PlayerRef winner)
    {
        // 権限を持つクライアントから全プレイヤーへ通知
        RPC_ShowResult(winner);
    }

    // 全員の画面でUIを切り替えるRPC
    [Rpc(RpcSources.StateAuthority | RpcSources.All, RpcTargets.All)]
    private void RPC_ShowResult(PlayerRef winner)
    {
        Debug.Log($"勝者決定: Player {winner}");

        // 自分が勝者かどうか判定
        if (Runner.LocalPlayer == winner)
        {
            // 自分はWIN
            if (winPanel) winPanel.SetActive(true);
            if (losePanel) losePanel.SetActive(false);
        }
        else
        {
            // 自分はLOSE
            if (winPanel) winPanel.SetActive(false);
            if (losePanel) losePanel.SetActive(true);
        }

        // ゲーム進行を止める場合などはここに追記
    }
}