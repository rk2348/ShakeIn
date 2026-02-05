using UnityEngine;
using TMPro;
using Fusion; // Fusionを追加

public class CountUp: NetworkBehaviour // NetworkBehaviourに変更
{
    [Header("設定")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private float countSpeed = 1.0f;

    // ネットワーク間で同期される変数
    [Networked] private float NetworkedTime { get; set; }

    public override void Spawned()
    {
        // 初期値を設定（権限がある場合のみ）
        if (Object.HasStateAuthority && NetworkedTime == 0)
        {
            NetworkedTime = 1.0f;
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 状態権限（State Authority）を持つクライアントだけが時間を進める
        if (Object.HasStateAuthority)
        {
            NetworkedTime += Runner.DeltaTime * countSpeed;
        }
    }

    // Renderは毎フレーム呼ばれ、視覚的な更新に適しています
    public override void Render()
    {
        if (timerText != null)
        {
            // 同期されている NetworkedTime を表示
            timerText.text = Mathf.FloorToInt(NetworkedTime).ToString();
        }
    }
}