using UnityEngine;
using Fusion;

public class VRPlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float launchPower = 10.0f;
    [SerializeField] private float normalMoveSpeed = 2.0f;
    [SerializeField] private float friction = 0.98f;

    [Header("Visuals")]
    [SerializeField] private LineRenderer directionLine;

    private Transform cameraRigRoot;
    private Transform centerEyeAnchor;
    private Vector3 currentVelocity;

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            FindCameraRig();
        }
        if (directionLine != null) directionLine.enabled = false;
    }

    private void FindCameraRig()
    {
        var rig = FindObjectOfType<OVRCameraRig>();
        if (rig != null)
        {
            cameraRigRoot = rig.transform;
            centerEyeAnchor = rig.centerEyeAnchor;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (cameraRigRoot == null)
        {
            FindCameraRig();
            if (cameraRigRoot == null) return;
        }

        // 慣性移動
        if (currentVelocity.magnitude > 0.01f)
        {
            cameraRigRoot.position += currentVelocity * Runner.DeltaTime;
            currentVelocity *= friction;
        }
        else
        {
            currentVelocity = Vector3.zero;
        }

        // 入力処理
        HandleInput();
    }

    private void HandleInput()
    {
        if (centerEyeAnchor == null) return;

        Vector3 forward = centerEyeAnchor.forward;
        Vector3 right = centerEyeAnchor.right;
        forward.y = 0; right.y = 0;
        forward.Normalize(); right.Normalize();

        Vector2 leftStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        Vector2 rightStick = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);

        // 左手：ビリヤードショット（ガイド線表示と発射）
        if (leftStick.magnitude > 0.1f)
        {
            if (directionLine != null)
            {
                directionLine.enabled = true;
                Vector3 dir = (forward * leftStick.y + right * leftStick.x).normalized;
                directionLine.SetPosition(0, cameraRigRoot.position);
                directionLine.SetPosition(1, cameraRigRoot.position + dir * 2.0f);
            }

            // Aボタンで発射
            if (OVRInput.GetDown(OVRInput.Button.One))
            {
                Vector3 launchDir = (forward * leftStick.y + right * leftStick.x).normalized;
                currentVelocity = launchDir * launchPower;
            }
        }
        else
        {
            if (directionLine != null) directionLine.enabled = false;
        }

        // 右手：通常移動
        if (rightStick.magnitude > 0.1f)
        {
            Vector3 moveDir = (forward * rightStick.y + right * rightStick.x).normalized;
            cameraRigRoot.position += moveDir * normalMoveSpeed * Runner.DeltaTime;
        }
    }

    // 壁やボールとの衝突処理（OVRCameraRig側にColliderとRigidbodyが必要）
    private void OnCollisionEnter(Collision collision)
    {
        if (!HasStateAuthority) return;

        // ボールを弾く処理
        var ball = collision.gameObject.GetComponent<BilliardBall>();
        if (ball != null)
        {
            Vector3 dir = (ball.transform.position - transform.position).normalized;
            dir.y = 0;
            // 自分の速度をボールに与える
            ball.OnHit(dir * Mathf.Max(currentVelocity.magnitude, 2.0f), Runner.LocalPlayer);

            // 自分は減速
            currentVelocity *= 0.5f;
        }
    }
}