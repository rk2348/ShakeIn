using UnityEngine;

public class ConstrainYAxis : MonoBehaviour
{
    // 固定したいY軸の値
    public float FixedY = -0.8f;

    void Update()
    {
        // 現在のポジションを取得
        Vector3 currentPosition = transform.position;

        // Y座標だけを書き換えて再代入
        transform.position = new Vector3(currentPosition.x, FixedY, currentPosition.z);
    }
}