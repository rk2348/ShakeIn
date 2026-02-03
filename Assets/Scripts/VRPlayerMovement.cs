using UnityEngine;

public class VRPlayerMovement : MonoBehaviour
{
    [Header("移動させるオブジェクト")]
    [SerializeField] private Transform targetObject;

    [Header("移動速度")]
    [SerializeField] private float speed = 2.0f;

    [Header("視点の参照 (CenterEyeAnchor)")]
    [SerializeField] private Transform centerEyeAnchor;

    void Update()
    {
        if (targetObject == null || centerEyeAnchor == null) return;

        // 右スティックの入力を取得
        Vector2 input = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);

        // スティックが倒されている場合のみ移動
        if (input.magnitude > 0.1f)
        {
            // カメラの向きに基づいた移動方向の計算
            Vector3 forward = centerEyeAnchor.forward;
            Vector3 right = centerEyeAnchor.right;

            // Y成分を無効化して水平移動に限定
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            // 移動ベクトルの合成
            Vector3 moveDirection = (forward * input.y + right * input.x);

            // 対象オブジェクトの座標を更新
            targetObject.position += moveDirection * speed * Time.deltaTime;
        }

        // --- 視覚的なフィードバック（振動） ---
        if (OVRInput.GetDown(OVRInput.Button.One)) // Aボタン
        {
            OVRInput.SetControllerVibration(1.0f, 0.5f, OVRInput.Controller.RTouch);
        }
        if (OVRInput.GetUp(OVRInput.Button.One))
        {
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
        }
    }
}