using UnityEngine;

public class VRPlayerMovement : MonoBehaviour
{
    [Header("移動速度")]
    [SerializeField] private float speed = 2.0f;

    [Header("参照")]
    [SerializeField] private Transform centerEyeAnchor; // OVRCameraRigのCenterEyeAnchorを指定

    void Update()
    {
        // --- 移動処理 ---
        // 右スティックの入力を取得 (SecondaryThumbstick = 右手)
        Vector2 input = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);

        if (input.magnitude > 0.1f)
        {
            Vector3 forward = centerEyeAnchor.forward;
            Vector3 right = centerEyeAnchor.right;

            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDirection = (forward * input.y + right * input.x);
            transform.position += moveDirection * speed * Time.deltaTime;
        }

        // --- 振動処理 (追加) ---
        // Aボタン（右手のButton.One）が押された瞬間
        if (OVRInput.GetDown(OVRInput.Button.One))
        {
            // 右手コントローラーを振動させる（周波数: 1.0, 強さ: 1.0, 右手）
            OVRInput.SetControllerVibration(1.0f, 1.0f, OVRInput.Controller.RTouch);
        }

        // Aボタンが離された瞬間、振動を止める
        if (OVRInput.GetUp(OVRInput.Button.One))
        {
            // 振動を停止（周波数: 0, 強さ: 0）
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
        }
    }
}