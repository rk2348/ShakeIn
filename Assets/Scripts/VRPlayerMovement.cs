using UnityEngine;
using Fusion;

public class VRPlayerMovement : NetworkBehaviour
{
    [Header("左手: ビリヤード移動設定")]
    [SerializeField] private float launchPower = 10.0f;
    [SerializeField] private float friction = 0.98f;
    [SerializeField] private float stopThreshold = 0.01f;

    [Header("右手: 通常移動設定")]
    [SerializeField] private float normalMoveSpeed = 2.0f;

    private Transform cameraRigRoot;
    private Transform centerEyeAnchor;
    private Vector3 currentVelocity;

    // 【修正】入力の取りこぼしを防ぐためのフラグ
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
    }

    // 【修正】入力判定は毎フレーム行われる Update で確実に拾う
    private void Update()
    {
        // 権限がない、または初期化前なら何もしない
        if (!HasStateAuthority || cameraRigRoot == null) return;

        // 左スティック入力チェック (入力値の保持のため、ここでも取得しても良いが、GetDown判定が重要)
        Vector2 leftInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

        // 「スティックが倒されている」かつ「Aボタンが押された」瞬間を検知
        if (leftInput.magnitude > 0.1f && OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            isLaunchRequested = true; // フラグを立てる（処理は FixedUpdateNetwork に任せる）
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
        // 【修正】Updateで立てたフラグをここでチェックして消費する
        if (isLaunchRequested)
        {
            Vector2 leftInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

            // フラグが立っていても、処理の瞬間にスティックが戻っている可能性を考慮して再度チェック
            // (もしくは、Update時点の input を保存しておくアプローチもありですが、簡易的にはこれでOK)
            if (leftInput.magnitude > 0.1f)
            {
                Vector3 launchDir = (forward * leftInput.y + right * leftInput.x).normalized;
                currentVelocity = launchDir * launchPower;
                Debug.Log($"【移動】Aボタンショット実行！ 方向:{launchDir} 速度:{launchPower}");
            }

            // 処理が終わったらフラグを下ろす
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

    private void OnCollisionEnter(Collision collision)
    {
        if (!HasStateAuthority) return;

        var ball = collision.gameObject.GetComponent<BilliardBall>();
        if (ball != null)
        {
            if (!ball.Object.HasStateAuthority)
            {
                ball.Object.RequestStateAuthority();
            }

            ball.LastHitter = Object.InputAuthority;

            Vector3 dir = (ball.transform.position - transform.position).normalized;
            dir.y = 0;

            float power = Mathf.Max(currentVelocity.magnitude, 1.0f);

            ball.Velocity = dir * power * 1.2f;

            currentVelocity = Vector3.zero;
            Debug.Log($"【衝突】ボール ({collision.gameObject.name}) に当たり、弾きました。");
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