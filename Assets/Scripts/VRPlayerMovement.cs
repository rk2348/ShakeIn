using UnityEngine;

public class VRPlayerMovement : MonoBehaviour
{
    [Header("移動速度")]
    [SerializeField] private float speed = 2.0f;

    [Header("参照")]
    [SerializeField] private Transform centerEyeAnchor; // OVRCameraRigのCenterEyeAnchorを指定

    void Update()
    {
        // 【変更点】右スティックの入力を取得 (SecondaryThumbstick = 右手)
        Vector2 input = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);

        if (input.magnitude > 0.1f)
        {
            // 頭（CenterEyeAnchor）の向きに合わせて移動方向を計算
            Vector3 forward = centerEyeAnchor.forward;
            Vector3 right = centerEyeAnchor.right;

            // 水平移動に限定（y軸を無視）
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            // 右スティックの入力に基づいた移動方向
            Vector3 moveDirection = (forward * input.y + right * input.x);

            // OVRCameraRigの座標を更新
            transform.position += moveDirection * speed * Time.deltaTime;
        }
    }
}