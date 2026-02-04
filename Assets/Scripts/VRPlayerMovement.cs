using UnityEngine;
using Fusion;

public class VRPlayerMovement : NetworkBehaviour
{
    [Header("左手: ビリヤード移動設定")]
    [SerializeField] private float launchPower = 10.0f; // 打ち出しの強さ
    [SerializeField] private float friction = 0.98f;    // 摩擦（滑りやすさ）
    [SerializeField] private float stopThreshold = 0.01f; // 停止判定速度

    [Header("右手: 通常移動設定")]
    [SerializeField] private float normalMoveSpeed = 2.0f; // 右手スティックでの移動速度

    // シーン上のカメラリグ（移動させる対象）
    private Transform cameraRigRoot;
    // 進行方向の基準となるカメラの目
    private Transform centerEyeAnchor;

    // 現在の慣性速度ベクトル
    private Vector3 currentVelocity;

    public override void Spawned()
    {
        // 自分自身（操作権限があるプレイヤー）のときだけ実行
        if (HasStateAuthority)
        {
            var rig = FindObjectOfType<OVRCameraRig>();
            if (rig != null)
            {
                cameraRigRoot = rig.transform;
                centerEyeAnchor = rig.centerEyeAnchor;
                Debug.Log("【成功】OVRCameraRig を発見しました。移動制御を開始します。");
            }
            else
            {
                Debug.LogError("【エラー】シーン内に OVRCameraRig が見つかりません！");
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 権限がない、またはカメラが見つかっていない場合は処理しない
        if (!HasStateAuthority || cameraRigRoot == null || centerEyeAnchor == null)
        {
            return;
        }

        // =========================================================
        // 1. 慣性移動の処理（左手で弾いたあとの滑り）
        // =========================================================
        if (currentVelocity.magnitude > stopThreshold)
        {
            // 現在の速度分だけ移動
            cameraRigRoot.position += currentVelocity * Runner.DeltaTime;

            // 摩擦によって減速させる
            currentVelocity *= friction;
        }
        else
        {
            currentVelocity = Vector3.zero;
        }

        // カメラの向き基準を取得（Y軸回転のみ考慮）
        Vector3 forward = centerEyeAnchor.forward;
        Vector3 right = centerEyeAnchor.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        // =========================================================
        // 2. 左手: 弾く移動 (PrimaryThumbstick + Aボタン)
        // =========================================================
        // 左手スティックの入力を取得
        Vector2 leftInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

        // スティックを倒しながら、右手Aボタン(RTouchのButton.One)を押した瞬間
        // ※Button.Oneは右手ではA、左手ではXですが、RTouch指定で確実にAボタンを取得します
        if (leftInput.magnitude > 0.1f && OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            // スティックの入力方向を3D空間の方向に変換
            Vector3 launchDir = (forward * leftInput.y + right * leftInput.x).normalized;

            // 初速を与える（現在の慣性を上書きして弾く）
            currentVelocity = launchDir * launchPower;

            Debug.Log($"【移動】Aボタンショット実行！ 方向:{launchDir} 速度:{launchPower}");
        }

        // =========================================================
        // 3. 右手: 通常移動 (SecondaryThumbstick)
        // =========================================================
        Vector2 rightInput = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);

        if (rightInput.magnitude > 0.1f)
        {
            // 移動方向を決定
            Vector3 moveDirection = (forward * rightInput.y + right * rightInput.x);

            // 直接位置を動かす（慣性とは別計算で加算）
            cameraRigRoot.position += moveDirection * normalMoveSpeed * Runner.DeltaTime;
        }
    }

    // ★追加: 衝突判定
    // プレイヤーオブジェクトに Collider と Rigidbody が必要です。
    private void OnCollisionEnter(Collision collision)
    {
        // 自分の動作でなければ無視
        if (!HasStateAuthority) return;

        // 衝突相手が「BilliardBall」コンポーネントを持っているかチェック
        // またはタグで判定する場合: if (collision.gameObject.CompareTag("Ball"))
        if (collision.gameObject.GetComponent<BilliardBall>() != null)
        {
            // 速度をゼロにして停止させる
            currentVelocity = Vector3.zero;
            Debug.Log($"【停止】ボール ({collision.gameObject.name}) に衝突したため停止しました。");
        }
    }
}