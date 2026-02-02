using Fusion;
using UnityEngine;

public class SyncAvatar : NetworkBehaviour
{
    // 役割を同期し、値が変わったらビジュアルを更新
    [Networked, OnChangedRender(nameof(UpdateVisuals))]
    public PlayerRole MyRole { get; set; }

    [Header("Visual Objects per Role")]
    [SerializeField] private GameObject idolVisualObject;
    [SerializeField] private GameObject fanVisualObject;

    [Header("Avatar Parts to Sync")]
    [SerializeField] private Transform headVisual;
    [SerializeField] private Transform leftHandVisual;
    [SerializeField] private Transform rightHandVisual;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 2.0f;

    private OVRCameraRig _rig;
    private Transform _localCenterEye;
    private Transform _localLeftHand;
    private Transform _localRightHand;

    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            // VR Rigの参照を取得
            _rig = FindFirstObjectByType<OVRCameraRig>();
            if (_rig != null)
            {
                _localCenterEye = _rig.centerEyeAnchor;
                _localLeftHand = _rig.leftHandAnchor;
                _localRightHand = _rig.rightHandAnchor;
            }

            // 初期設定の試行
            TrySyncRole();
        }

        // 全員に対して現在の値でビジュアルを適用
        UpdateVisuals();
    }

    public override void FixedUpdateNetwork()
    {
        // 入力権限を持つローカルプレイヤーの処理
        if (Object.HasInputAuthority)
        {
            // 役割がまだ同期されていない（None）場合は再送を試みる
            if (MyRole == PlayerRole.None)
            {
                TrySyncRole();
            }

            if (_rig != null)
            {
                HandleMovement();
                SyncTransform();
            }
        }
    }

    private void TrySyncRole()
    {
        PlayerRole selectedRole = RoleIdentifier.GetRole();
        if (selectedRole != PlayerRole.None)
        {
            Debug.Log($"[SyncAvatar] Requesting role sync: {selectedRole}");
            RpcRequestSetRole(selectedRole);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcRequestSetRole(PlayerRole role)
    {
        // サーバー側でネットワーク変数を更新
        MyRole = role;
        Debug.Log($"[SyncAvatar] Role assigned on Server: {MyRole}");

        // ホスト自身の画面でも即座に反映させるための呼び出し
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (idolVisualObject == null || fanVisualObject == null) return;

        // Noneの場合は一旦すべて非表示
        if (MyRole == PlayerRole.None)
        {
            idolVisualObject.SetActive(false);
            fanVisualObject.SetActive(false);
            return;
        }

        // 役割に応じた表示切り替え
        bool isStaff = (MyRole == PlayerRole.Idol || MyRole == PlayerRole.Admin);
        bool isGuest = (MyRole == PlayerRole.Guest);

        idolVisualObject.SetActive(isStaff);
        fanVisualObject.SetActive(isGuest);

        Debug.Log($"[SyncAvatar] Visuals Updated for {Object.Id}: Role={MyRole}");
    }

    private void SyncTransform()
    {
        // ヘッドセットの位置同期
        transform.position = _localCenterEye.position;
        transform.rotation = _localCenterEye.rotation;

        // 手の位置同期
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