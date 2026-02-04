using UnityEngine;
using Fusion;

public class VRBallInteraction : NetworkBehaviour
{
    [SerializeField] private BilliardBall cueBall; // 操作対象の手玉
    [SerializeField] private float hitMultiplier = 1.5f; // 突く強さの補正
    [SerializeField] private float minHitVelocity = 0.2f; // 最低限必要な速度

    // コントローラーのアンカー（OVRCameraRig内のRightHandAnchorなどをアタッチ）
    [SerializeField] private Transform rightHandAnchor;

    private Vector3 lastHandPosition;
    private Vector3 handVelocity;

    public override void FixedUpdateNetwork()
    {
        // 操作権限がある場合のみ計算
        if (!HasStateAuthority || cueBall == null || rightHandAnchor == null) return;

        // 1. コントローラーの速度を計算（自前計算）
        // OVRInput.GetLocalControllerVelocity も使えますが、ワールド座標での計算が確実です
        handVelocity = (rightHandAnchor.position - lastHandPosition) / Runner.DeltaTime;
        lastHandPosition = rightHandAnchor.position;

        // 2. 距離の判定
        float distance = Vector3.Distance(rightHandAnchor.position, cueBall.transform.position);

        // ボールの半径（例: 0.03m）より近ければ「当たった」とみなす
        if (distance < 0.05f)
        {
            // 3. 速度が一定以上の時だけ反発させる
            if (handVelocity.magnitude > minHitVelocity)
            {
                // ボールの速度をコントローラーの速度に合わせる
                // Y軸方向への力は、ビリヤード台に抑えられるため制限するのがコツ
                Vector3 newVelocity = handVelocity * hitMultiplier;
                newVelocity.y = 0;

                cueBall.Velocity = newVelocity;

                // 連続衝突を防ぐため、少しボールを押し出す処理を入れると安定します
                cueBall.transform.position += newVelocity.normalized * 0.01f;
            }
        }
    }
}