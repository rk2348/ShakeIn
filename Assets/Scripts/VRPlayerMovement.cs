using UnityEngine;
using Fusion;

public class VRPlayerMovement : NetworkBehaviour
{
    [Header("左手: ビリヤード移動設定")]
    [SerializeField] private float launchPower = 10.0f;
    [SerializeField] private float friction = 0.98f;
    [SerializeField] private float stopThreshold = 0.01f;
    [SerializeField][Range(0f, 1f)] private float collisionSpeedRetention = 0.2f;

    [Header("ガイド線設定")]
    [SerializeField] private LineRenderer directionLine;
    [SerializeField] private float lineLength = 2.0f;

    [Header("右手: 通常移動設定")]
    [SerializeField] private float normalMoveSpeed = 2.0f;

    private Transform cameraRigRoot;
    private Transform centerEyeAnchor;
    private Vector3 currentVelocity;

    private bool isLaunchRequested = false;

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            // 初期化試行
            FindCameraRig();
        }

        if (directionLine != null) directionLine.enabled = false;
    }

    // --- 追加: リグ検索用メソッド ---
    private void FindCameraRig()
    {
        var rig = FindObjectOfType<OVRCameraRig>();
        if (rig != null)
        {
            cameraRigRoot = rig.transform;
            centerEyeAnchor = rig.centerEyeAnchor;
            Debug.Log("【VRPlayerMovement】OVRCameraRig を発見しました。");
        }
    }
    // -----------------------------

    private void Update()
    {
        if (!HasStateAuthority) return;

        // --- 追加: リグが未取得なら再検索して、それでもなければ中断 ---
        if (cameraRigRoot == null)
        {
            FindCameraRig();
            if (cameraRigRoot == null) return;
        }
        // ---------------------------------------------------------

        // 左スティック入力チェック
        Vector2 leftInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

        // ガイド線の表示処理
        if (directionLine != null && centerEyeAnchor != null)
        {
            if (leftInput.magnitude > 0.1f)
            {
                directionLine.enabled = true;

                Vector3 forward = centerEyeAnchor.forward;
                Vector3 right = centerEyeAnchor.right;
                forward.y = 0f;
                right.y = 0f;
                forward.Normalize();
                right.Normalize();

                Vector3 aimDir = (forward * leftInput.y + right * leftInput.x).normalized;
                Vector3 startPos = cameraRigRoot.position;

                directionLine.SetPosition(0, startPos);
                directionLine.SetPosition(1, startPos + aimDir * lineLength);
            }
            else
            {
                directionLine.enabled = false;
            }
        }

        if (leftInput.magnitude > 0.1f && OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            isLaunchRequested = true;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        // --- 追加: FixedUpdateでもリグがなければ再検索 ---
        if (cameraRigRoot == null || centerEyeAnchor == null)
        {
            FindCameraRig();
            if (cameraRigRoot == null) return;
        }
        // --------------------------------------------

        // 慣性移動の処理
        if (currentVelocity.magnitude > stopThreshold)
        {
            cameraRigRoot.position += currentVelocity * Runner.DeltaTime;
            currentVelocity *= friction;
        }
        else
        {
            currentVelocity = Vector3.zero;
        }

        Vector3 forward = centerEyeAnchor.forward;
        Vector3 right = centerEyeAnchor.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        // ビリヤード移動（発射）処理
        if (isLaunchRequested)
        {
            Vector2 leftInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

            if (leftInput.magnitude > 0.1f)
            {
                Vector3 launchDir = (forward * leftInput.y + right * leftInput.x).normalized;
                currentVelocity = launchDir * launchPower;
                Debug.Log($"【移動】Aボタンショット実行！ 方向:{launchDir} 速度:{launchPower}");
            }

            isLaunchRequested = false;
        }

        // 通常移動処理
        Vector2 rightInput = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
        if (rightInput.magnitude > 0.1f)
        {
            Vector3 moveDirection = (forward * rightInput.y + right * rightInput.x);
            cameraRigRoot.position += moveDirection * normalMoveSpeed * Runner.DeltaTime;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!HasStateAuthority) return;

        var ball = collision.gameObject.GetComponent<BilliardBall>();
        if (ball != null)
        {
            Vector3 dir = (ball.transform.position - transform.position).normalized;
            dir.y = 0;
            float power = Mathf.Max(currentVelocity.magnitude, 1.0f);
            Vector3 newVelocity = dir * power * 1.2f;
            ball.OnHit(newVelocity, Runner.LocalPlayer);
            currentVelocity *= collisionSpeedRetention;
            Debug.Log($"【衝突】ボールに衝突。速度維持率: {collisionSpeedRetention}, 残り速度: {currentVelocity.magnitude}");
        }
        else if (collision.gameObject.CompareTag("Wall"))
        {
            Vector3 normal = collision.contacts[0].normal;
            currentVelocity = Vector3.Reflect(currentVelocity, normal);
            currentVelocity *= 0.8f;
            Debug.Log("【反射】壁に当たって跳ね返りました。");
        }
    }
}