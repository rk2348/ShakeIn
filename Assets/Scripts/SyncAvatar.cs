using Fusion;
using UnityEngine;

public class SyncAvatar : NetworkBehaviour
{
    // Sync the role and trigger visual update when the value changes
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
            // Request the server to set the role based on local selection
            PlayerRole selectedRole = RoleIdentifier.GetRole();
            Debug.Log($"[SyncAvatar] Requesting role sync: {selectedRole}");
            RpcRequestSetRole(selectedRole);

            // Set up VR Rig references
            _rig = FindFirstObjectByType<OVRCameraRig>();
            if (_rig != null)
            {
                _localCenterEye = _rig.centerEyeAnchor;
                _localLeftHand = _rig.leftHandAnchor;
                _localRightHand = _rig.rightHandAnchor;
            }
        }

        // Apply initial visuals (will be updated again once MyRole is synced)
        UpdateVisuals();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcRequestSetRole(PlayerRole role)
    {
        // State Authority (Host) updates the networked variable
        MyRole = role;

        // Fix: Manually trigger update on the Host to ensure immediate feedback
        UpdateVisuals();
        Debug.Log($"[SyncAvatar] Role assigned on Server: {MyRole}");
    }

    private void UpdateVisuals()
    {
        // If role is not yet determined, hide all visuals to avoid default object issues
        if (MyRole == PlayerRole.None)
        {
            if (idolVisualObject != null) idolVisualObject.SetActive(false);
            if (fanVisualObject != null) fanVisualObject.SetActive(false);
            return;
        }

        bool isStaff = (MyRole == PlayerRole.Idol || MyRole == PlayerRole.Admin);
        bool isGuest = (MyRole == PlayerRole.Guest);

        if (idolVisualObject != null) idolVisualObject.SetActive(isStaff);
        if (fanVisualObject != null) fanVisualObject.SetActive(isGuest);

        Debug.Log($"[SyncAvatar] Visuals Updated: Role={MyRole}, isStaff={isStaff}, isGuest={isGuest}");
    }

    public override void FixedUpdateNetwork()
    {
        // Only the local player updates their own position
        if (Object.HasInputAuthority && _rig != null)
        {
            HandleMovement();

            // Sync Head
            transform.position = _localCenterEye.position;
            transform.rotation = _localCenterEye.rotation;

            // Sync Hands
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