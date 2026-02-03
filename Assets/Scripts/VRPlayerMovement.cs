using UnityEngine;
using Fusion;

public class VRPlayerMovement : NetworkBehaviour
{
    [Header("移動速度")]
    [SerializeField] private float speed = 2.0f;

    // シーン上のカメラリグ（移動させる対象）
    private Transform cameraRigRoot;
    // 進行方向の基準となるカメラの目
    private Transform centerEyeAnchor;

    public override void Spawned()
    {
        // 自分自身（操作権限があるプレイヤー）のときだけ実行
        if (HasStateAuthority)
        {
            // シーンにある OVRCameraRig を探してセットする
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

        // 右手のスティック入力を取得 (左手の場合は PrimaryThumbstick に変更)
        Vector2 input = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);

        // 入力が少しでもある場合
        if (input.magnitude > 0.1f)
        {
            // カメラが向いている方向を基準に移動ベクトルを作成
            Vector3 forward = centerEyeAnchor.forward;
            Vector3 right = centerEyeAnchor.right;

            // 上下（Y軸）には進まないように補正
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            // 移動方向を決定
            Vector3 moveDirection = (forward * input.y + right * input.x);

            // 【重要】アバター(transform)ではなく、カメラリグ(cameraRigRoot)を動かす
            cameraRigRoot.position += moveDirection * speed * Runner.DeltaTime;
        }
    }
}