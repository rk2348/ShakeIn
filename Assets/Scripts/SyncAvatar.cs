using Fusion;
using UnityEngine;

public class SyncAvatar : NetworkBehaviour
{
    [Header("同期対象（アバター側）")]
    [SerializeField] private Transform headVisual;
    [SerializeField] private Transform leftHandVisual;
    [SerializeField] private Transform rightHandVisual;

    [Header("移動設定")]
    [SerializeField] private float moveSpeed = 2.0f; // 移動速度

    // 現実のリグ（シーン内の OVRCameraRig）の参照
    private OVRCameraRig _rig;
    private Transform _localCenterEye;
    private Transform _localLeftHand;
    private Transform _localRightHand;

    public override void Spawned()
    {
        // 自分が生成したアバター（自分自身）の場合のみ、リグを探す
        if (Object.HasInputAuthority)
        {
            _rig = FindFirstObjectByType<OVRCameraRig>();
            if (_rig != null)
            {
                _localCenterEye = _rig.centerEyeAnchor;
                _localLeftHand = _rig.leftHandAnchor;
                _localRightHand = _rig.rightHandAnchor;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 自分が操作権（InputAuthority）を持っている場合のみ実行
        if (Object.HasInputAuthority && _rig != null)
        {
            // 1. 右スティックでの移動処理（Local Rigを物理的に動かす）
            HandleMovement();

            // 2. ネットワークアバターの位置をRigの最新位置に合わせる
            // これにより、NetworkTransformを通じて相手に位置が同期されます
            transform.position = _localCenterEye.position;
            transform.rotation = _localCenterEye.rotation;

            // 3. 手の位置を同期
            if (leftHandVisual != null)
            {
                leftHandVisual.position = _localLeftHand.position;
                leftHandVisual.rotation = _localLeftHand.rotation;
            }
            if (rightHandVisual != null)
            {
                rightHandVisual.position = _localRightHand.position;
                rightHandVisual.rotation = _localRightHand.rotation;
            }
        }
    }

    private void HandleMovement()
    {
        // 右コントローラーのスティック入力を取得
        Vector2 stickInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);

        if (stickInput.magnitude > 0.1f)
        {
            // カメラ（視線）の向きに基づいた移動方向を計算
            // HMDが傾いていても地面を這うように移動するため、y軸は0にする
            Vector3 forward = _localCenterEye.forward;
            forward.y = 0;
            forward.Normalize();

            Vector3 right = _localCenterEye.right;
            right.y = 0;
            right.Normalize();

            // 入力に応じた移動ベクトル
            Vector3 moveDirection = (forward * stickInput.y + right * stickInput.x);

            // シーン内に置かれている OVRCameraRig の親オブジェクトを動かす
            _rig.transform.position += moveDirection * moveSpeed * Runner.DeltaTime;
        }
    }
}