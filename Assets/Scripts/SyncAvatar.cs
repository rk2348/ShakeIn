using Fusion;
using UnityEngine;

public class SyncAvatar : NetworkBehaviour
{
    // ネットワーク全体で同期される「自分の役割」
    [Networked] public PlayerRole MyRole { get; set; }

    [Header("役割ごとの見た目")]
    [SerializeField] private GameObject idolVisualObject; // アイドル用モデルのルート
    [SerializeField] private GameObject fanVisualObject;  // ファン用モデルのルート

    [Header("同期対象（アバター側）")]
    [SerializeField] private Transform headVisual;
    [SerializeField] private Transform leftHandVisual;
    [SerializeField] private Transform rightHandVisual;

    [Header("移動設定")]
    [SerializeField] private float moveSpeed = 2.0f;

    private OVRCameraRig _rig;
    private Transform _localCenterEye;
    private Transform _localLeftHand;
    private Transform _localRightHand;

    public override void Spawned()
    {
        // 1. 自分が生成したアバターの場合、デバイスIDから判定した役割をネットワーク変数にセット
        if (Object.HasInputAuthority)
        {
            MyRole = RoleIdentifier.GetRole(); // RoleIdentifierを使って役割を取得

            _rig = FindFirstObjectByType<OVRCameraRig>();
            if (_rig != null)
            {
                _localCenterEye = _rig.centerEyeAnchor;
                _localLeftHand = _rig.leftHandAnchor;
                _localRightHand = _rig.rightHandAnchor;
            }
        }

        // 2. 役割に応じて見た目を更新
        UpdateVisuals();
    }

    // 役割が変わったことを検知して見た目を切り替える（Fusion 2 の Render などで呼び出す）
    public override void Render()
    {
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        // MyRole の値に応じてオブジェクトをアクティブ/非アクティブにする
        if (idolVisualObject != null)
            idolVisualObject.SetActive(MyRole == PlayerRole.Staff);

        if (fanVisualObject != null)
            fanVisualObject.SetActive(MyRole == PlayerRole.Guest);
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasInputAuthority && _rig != null)
        {
            HandleMovement(); // 右スティック移動

            transform.position = _localCenterEye.position;
            transform.rotation = _localCenterEye.rotation;

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
        Vector2 stickInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
        if (stickInput.magnitude > 0.1f)
        {
            Vector3 forward = _localCenterEye.forward;
            forward.y = 0;
            forward.Normalize();
            Vector3 right = _localCenterEye.right;
            right.y = 0;
            right.Normalize();
            Vector3 moveDirection = (forward * stickInput.y + right * stickInput.x);
            _rig.transform.position += moveDirection * moveSpeed * Runner.DeltaTime;
        }
    }
}