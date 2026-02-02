using Fusion;
using UnityEngine;

public class SyncAvatar : NetworkBehaviour
{
    // 同期される変数
    [Networked] public PlayerRole MyRole { get; set; }

    [Header("役割ごとの見た目")]
    [SerializeField] private GameObject idolVisualObject;
    [SerializeField] private GameObject fanVisualObject;

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

    // Fusion 2 で値の変化を検知するための変数
    private ChangeDetector _changeDetector;

    public override void Spawned()
    {
        // 変化検知の初期化
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (Object.HasInputAuthority)
        {
            // 自分の役割を取得
            PlayerRole selectedRole = RoleIdentifier.GetRole();
            Debug.Log($"[SyncAvatar] 自分の役割は {selectedRole} です。ホストに設定を依頼します...");

            // ホスト(StateAuthority)に役割の設定を依頼する
            RpcRequestSetRole(selectedRole);

            _rig = FindFirstObjectByType<OVRCameraRig>();
            if (_rig != null)
            {
                _localCenterEye = _rig.centerEyeAnchor;
                _localLeftHand = _rig.leftHandAnchor;
                _localRightHand = _rig.rightHandAnchor;
            }
        }

        // 初回の見た目更新
        UpdateVisuals();
    }

    // 依頼を受け取るメソッド (RPC)
    // 呼び出し元: InputAuthority (自分) / 実行先: StateAuthority (ホスト)
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcRequestSetRole(PlayerRole role)
    {
        Debug.Log($"[RPC] ホストが役割設定を承認: {role}");
        // 権限を持つホストが書き換えることで、全員に同期される
        MyRole = role;
    }

    public override void Render()
    {
        // MyRoleに変化があったかチェック
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(MyRole))
            {
                UpdateVisuals();
            }
        }
    }

    private void UpdateVisuals()
    {
        // ログで現在の同期状態を確認
        Debug.Log($"[UpdateVisuals] 現在のMyRole: {MyRole}");

        if (MyRole == PlayerRole.None) return;

        bool isStaff = (MyRole == PlayerRole.Idol || MyRole == PlayerRole.Admin);
        bool isGuest = (MyRole == PlayerRole.Guest);

        if (idolVisualObject != null) idolVisualObject.SetActive(isStaff);
        if (fanVisualObject != null) fanVisualObject.SetActive(isGuest);
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasInputAuthority && _rig != null)
        {
            HandleMovement();
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