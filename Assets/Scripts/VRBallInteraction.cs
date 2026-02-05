using UnityEngine;
using Fusion;

public class VRBallInteraction : NetworkBehaviour
{
    [SerializeField] private BilliardBall cueBall;
    [SerializeField] private float hitMultiplier = 1.5f;
    [SerializeField] private float minHitVelocity = 0.2f;
    [SerializeField] private Transform rightHandAnchor;

    [SerializeField] private float autoShootPower = 3.0f; // 発射パワー
    [SerializeField] private float requiredHoldTime = 1.0f; // 発射までの溜め時間

    //内部変数
    private Vector3 lastHandPosition;
    private Vector3 handVelocity;

    //溜め処理用
    private float holdTimer = 0f;
    private bool hasFired = false; //押しっぱなしでの連射防止用フラグ

    public override void FixedUpdateNetwork()
    {
        if (cueBall == null || rightHandAnchor == null) return;
        if (cueBall.Object == null || !cueBall.Object.IsValid) return;

        //左手スティックの溜め撃ち処理
        HandleStickChargeShot();

        if (!HasStateAuthority) return;

        //右手衝突処理
        handVelocity = (rightHandAnchor.position - lastHandPosition) / Runner.DeltaTime;
        lastHandPosition = rightHandAnchor.position;

        float distance = Vector3.Distance(rightHandAnchor.position, cueBall.transform.position);

        if (distance < 0.05f)
        {
            if (handVelocity.magnitude > minHitVelocity)
            {
                //右手ではじいたPlayerを記録
                cueBall.LastHitter = Object.InputAuthority;

                Vector3 newVelocity = handVelocity * hitMultiplier;
                newVelocity.y = 0;
                cueBall.Velocity = newVelocity;
                cueBall.transform.position += newVelocity.normalized * 0.01f;
            }
        }
    }

    private void HandleStickChargeShot()
    {
        Vector2 inputStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

        if (inputStick.magnitude > 0.2f)
        {
            if (!cueBall.Object.HasStateAuthority)
            {
                cueBall.Object.RequestStateAuthority();
                return;
            }

            if (!hasFired)
            {
                holdTimer += Runner.DeltaTime;

                if (holdTimer >= requiredHoldTime)
                {
                    FireBall(inputStick);

                    hasFired = true;

                    holdTimer = 0f;
                }
            }
        }
        else
        {
            holdTimer = 0f;
            hasFired = false;
        }
    }

    private void FireBall(Vector2 direction)
    {
        Debug.Log("【発射】溜め撃ち実行！");

        //最後に触れたPlayerを記録
        cueBall.LastHitter = Object.InputAuthority;

        Vector3 shootVelocity = new Vector3(direction.x, 0, direction.y).normalized * autoShootPower;

        cueBall.Velocity = shootVelocity;
    }
}