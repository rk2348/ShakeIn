using UnityEngine;
using Fusion;

public class VRPlayerMovement : NetworkBehaviour
{
    [Header("左手: ビリヤード移動設定")]
    [SerializeField] private float launchPower = 10.0f; //打ち出しの強さ
    [SerializeField] private float friction = 0.98f;    //摩擦
    [SerializeField] private float stopThreshold = 0.01f; //停止判定速度

    [Header("右手: 通常移動設定")]
    [SerializeField] private float normalMoveSpeed = 2.0f; //右手スティックでの移動速度

    //移動させる対象
    private Transform cameraRigRoot;
    //進行方向の基準
    private Transform centerEyeAnchor;

    //慣性速度ベクトル
    private Vector3 currentVelocity;

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            var rig = FindObjectOfType<OVRCameraRig>();
            if (rig != null)
            {
                cameraRigRoot = rig.transform;
                centerEyeAnchor = rig.centerEyeAnchor;
                Debug.Log("【成功】OVRCameraRig を発見しました。移動制御を開始します。");
            }
            else
            {
                Debug.LogError("【エラー】シーン内に OVRCameraRig が見つかりません");
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || cameraRigRoot == null || centerEyeAnchor == null)
        {
            return;
        }

        //慣性移動の処理
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

        Vector2 leftInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

        if (leftInput.magnitude > 0.1f && OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            Vector3 launchDir = (forward * leftInput.y + right * leftInput.x).normalized;

            currentVelocity = launchDir * launchPower;

            Debug.Log($"【移動】Aボタンショット実行！ 方向:{launchDir} 速度:{launchPower}");
        }

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

            // 【追加】ぶつかったプレイヤーを記録 (InputAuthority = このPCの操作プレイヤー)
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