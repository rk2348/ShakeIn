using UnityEngine;
using Fusion;

public class BilliardBall : NetworkBehaviour
{
    [Networked] public Vector3 Velocity { get; set; } // ネットワーク同期される速度

    [Header("設定")]
    [SerializeField] private float radius = 0.03f;     // ボールの半径
    [SerializeField] private float friction = 0.98f;   // 摩擦（毎フレームの速度維持率）
    [SerializeField] private float stopThreshold = 0.05f; // 停止とみなす速度
    [SerializeField] private float bounciness = 0.8f;  // クッションの反発係数

    public override void FixedUpdateNetwork()
    {
        if (Velocity.magnitude > stopThreshold)
        {
            // 1. 位置の更新
            transform.position += Velocity * Runner.DeltaTime;

            // 2. 摩擦による減速
            Velocity *= friction;

            // 3. 壁（クッション）との衝突判定
            CheckWallCollision();
        }
        else
        {
            Velocity = Vector3.zero;
        }
    }

    private void CheckWallCollision()
    {
        // 簡単な例：テーブルのサイズを X:-1~1, Z:-2~2 と仮定
        Vector3 pos = transform.position;
        Vector3 vel = Velocity;

        // X方向の壁
        if (Mathf.Abs(pos.x) + radius > 1.0f)
        {
            pos.x = Mathf.Sign(pos.x) * (1.0f - radius);
            vel.x *= -1 * bounciness; // X軸の速度を反転
        }

        // Z方向の壁
        if (Mathf.Abs(pos.z) + radius > 2.0f)
        {
            pos.z = Mathf.Sign(pos.z) * (2.0f - radius);
            vel.z *= -1 * bounciness; // Z軸の速度を反転
        }

        transform.position = pos;
        Velocity = vel;
    }

    // 他のボールとの衝突（簡易版）
    // 本来はマネージャー側で一括管理するのが理想的ですが、ロジックの核を紹介します
    public void ResolveBallCollision(BilliardBall other)
    {
        Vector3 delta = transform.position - other.transform.position;
        float distance = delta.magnitude;

        if (distance < radius * 2)
        {
            // 1. 重なり防止（めり込み回避）
            Vector3 normal = delta.normalized;
            float overlap = radius * 2 - distance;
            transform.position += normal * (overlap / 2f);
            other.transform.position -= normal * (overlap / 2f);

            // 2. 速度の交換（弾性衝突）
            // 衝突線方向の速度成分を計算
            float v1 = Vector3.Dot(this.Velocity, normal);
            float v2 = Vector3.Dot(other.Velocity, normal);

            // 質量が同じなら、この成分を入れ替えるだけでビリヤードっぽくなる
            Vector3 velocityChange = normal * (v1 - v2);
            this.Velocity -= velocityChange;
            other.Velocity += velocityChange;
        }
    }
}