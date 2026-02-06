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
    [SerializeField] private LineRenderer directionLine; // InspectorでLineRendererをアタッチ
    [SerializeField] private float lineLength = 2.0f;    // 線の長さ

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
            var rig = FindObjectOfType<OVRCameraRig>();
            if (rig != null)
            {
                cameraRigRoot = rig.transform;
                centerEyeAnchor = rig.centerEyeAnchor;
                Debug.Log("【成功】OVRCameraRig を発見しました。");
            }
            else
            {
                Debug.LogError("【エラー】シーン内に OVRCameraRig が見つかりません");
            }
        }

        if (directionLine != null) directionLine.enabled = false;
    }

    private void Update()
    {
        if (!HasStateAuthority || cameraRigRoot == null) return;

        // 左スティック入力チェック
        Vector2 leftInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

        // --- 追加: ガイド線の表示処理 ---
        if (directionLine != null && centerEyeAnchor != null)
        {
            // スティックがある程度倒されている場合のみ線を表示
            if (leftInput.magnitude > 0.1f)
            {
                directionLine.enabled = true;

                // 移動方向の計算（FixedUpdateNetworkと同じロジック）
                Vector3 forward = centerEyeAnchor.forward;
                Vector3 right = centerEyeAnchor.right;
                forward.y = 0f;
                right.y = 0f;
                forward.Normalize();
                right.Normalize();

                Vector3 aimDir = (forward * leftInput.y + right * leftInput.x).normalized;

                // 線の始点と終点を設定
                // 始点: 現在の足元（CameraRigの位置）
                // 終点: 向いている方向 * 長さ
                Vector3 startPos = cameraRigRoot.position;

                // 少し上に持ち上げたい場合は startPos.y += 0.1f; など調整可能

                directionLine.SetPosition(0, startPos);
                directionLine.SetPosition(1, startPos + aimDir * lineLength);
            }
            else
            {
                // 入力がないときは線を消す
                directionLine.enabled = false;
            }
        }
        // -----------------------------

        // 「スティックが倒されている」かつ「Aボタンが押された」瞬間を検知
        if (leftInput.magnitude > 0.1f && OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            isLaunchRequested = true;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || cameraRigRoot == null || centerEyeAnchor == null)
        {
            return;
        }

        // --- 慣性移動の処理 ---
        if (currentVelocity.magnitude > stopThreshold)
        {
            cameraRigRoot.position += currentVelocity * Runner.DeltaTime;
            currentVelocity *= friction;
        }
        else
        {
            currentVelocity = Vector3.zero;
        }

        // --- 移動方向の計算 ---
        Vector3 forward = centerEyeAnchor.forward;
        Vector3 right = centerEyeAnchor.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        // --- ビリヤード移動（発射）処理 ---
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

        // --- 通常移動処理 ---
        Vector2 rightInput = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
        if (rightInput.magnitude > 0.1f)
        {
            Vector3 moveDirection = (forward * rightInput.y + right * rightInput.x);
            cameraRigRoot.position += moveDirection * normalMoveSpeed * Runner.DeltaTime;
        }
    }

    // OnCollisionEnter は省略（変更なし）
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