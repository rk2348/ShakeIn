using UnityEngine;

public class VRPlayerMovement : MonoBehaviour
{
    [Header("移動速度")]
    [SerializeField] private float speed = 2.0f;

    [Header("参照")]
    [SerializeField] private Transform centerEyeAnchor; // OVRCameraRigのCenterEyeAnchorを指定

    void Update()
    {
        // 左スティックの入力を取得 (PrimaryThumbstick = 通常は左手)
        Vector2 input = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

        if (input.magnitude > 0.1f)
        {
            // 頭（CenterEyeAnchor）の向きに合わせて移動方向を計算
            Vector3 forward = centerEyeAnchor.forward;
            Vector3 right = centerEyeAnchor.right;

            // 上下移動（y軸）を無視して水平移動に限定する
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            // 入力に基づいた移動ベクトルを算出
            Vector3 moveDirection = (forward * input.y + right * input.x);

            // OVRCameraRig（このスクリプトがアタッチされているオブジェクト）を移動
            transform.position += moveDirection * speed * Time.deltaTime;
        }
    }
}