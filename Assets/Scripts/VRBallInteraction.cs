using UnityEngine;
using Fusion;

public class VRBallInteraction : NetworkBehaviour
{
    [SerializeField] private BilliardBall cueBall;
    [SerializeField] private float hitMultiplier = 1.5f;
    [SerializeField] private float minHitVelocity = 0.2f;
    [SerializeField] private Transform rightHandAnchor;

    // --- 設定項目 ---
    [SerializeField] private float autoShootPower = 3.0f; // 発射パワー
    [SerializeField] private float requiredHoldTime = 1.0f; // 発射までの溜め時間（秒）

    // --- 内部変数 ---
    private Vector3 lastHandPosition;
    private Vector3 handVelocity;

    // 溜め処理用
    private float holdTimer = 0f;
    private bool hasFired = false; // 押しっぱなしでの連射防止用フラグ

    public override void FixedUpdateNetwork()
    {
        // 1. 基本的なnullチェック
        if (cueBall == null || rightHandAnchor == null) return;
        if (cueBall.Object == null || !cueBall.Object.IsValid) return;

        // =================================================================
        // 【追加】左手スティックの溜め撃ち処理
        // =================================================================
        HandleStickChargeShot();

        // --- 権限がない場合、物理挙動（右手の衝突）の処理は行わない ---
        if (!HasStateAuthority) return;

        // --- 以下、既存の右手衝突処理 ---
        handVelocity = (rightHandAnchor.position - lastHandPosition) / Runner.DeltaTime;
        lastHandPosition = rightHandAnchor.position;

        float distance = Vector3.Distance(rightHandAnchor.position, cueBall.transform.position);

        if (distance < 0.05f)
        {
            if (handVelocity.magnitude > minHitVelocity)
            {
                Vector3 newVelocity = handVelocity * hitMultiplier;
                newVelocity.y = 0;
                cueBall.Velocity = newVelocity;
                cueBall.transform.position += newVelocity.normalized * 0.01f;
            }
        }
    }

    /// <summary>
    /// 左スティックを1秒倒すと発射する処理
    /// </summary>
    private void HandleStickChargeShot()
    {
        // 1. スティック入力を取得
        Vector2 inputStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

        // 2. スティックが倒されているか（デッドゾーン判定 0.2以上）
        if (inputStick.magnitude > 0.2f)
        {
            // --- 権限の自動取得（重要） ---
            // 操作しようとしているのに権限がない場合、権限をリクエストする
            if (!HasStateAuthority)
            {
                cueBall.Object.RequestStateAuthority();
                return; // 権限が取れるまで待つ（次のフレームへ）
            }

            // まだ発射していない場合のみタイマーを進める
            if (!hasFired)
            {
                // 時間を加算 (Fusionでは Time.deltaTime の代わりに Runner.DeltaTime 推奨)
                holdTimer += Runner.DeltaTime;

                // 3. 規定時間（1秒）経過したかチェック
                if (holdTimer >= requiredHoldTime)
                {
                    // 発射処理
                    FireBall(inputStick);

                    // 連射防止フラグを立てる
                    hasFired = true;

                    // タイマーリセット（または溜め完了のエフェクトなどを出すならここで）
                    holdTimer = 0f;
                }
            }
        }
        else
        {
            // スティックを離したらタイマーとフラグをリセット
            holdTimer = 0f;
            hasFired = false;
        }
    }

    private void FireBall(Vector2 direction)
    {
        Debug.Log("【発射】溜め撃ち実行！");

        // 入力方向(x, y) を 3D空間の水平方向(x, z) に変換
        Vector3 shootVelocity = new Vector3(direction.x, 0, direction.y).normalized * autoShootPower;

        // 速度を適用
        cueBall.Velocity = shootVelocity;
    }
}