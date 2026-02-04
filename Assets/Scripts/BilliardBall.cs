using UnityEngine;
using Fusion;

public class BilliardBall : NetworkBehaviour
{
    [Networked] public Vector3 Velocity { get; set; } // ネットワーク同期される速度

    [Header("物理設定")]
    [SerializeField] public float radius = 0.03f;     // ボールの半径
    [SerializeField] private float friction = 0.985f;  // 摩擦（毎フレームの速度維持率）
    [SerializeField] private float stopThreshold = 0.01f; // 停止とみなす速度
    [SerializeField] private float bounciness = 0.8f;  // クッションの反発係数

    [Header("テーブル範囲")]
    [SerializeField] private float tableWidth = 1.0f;  // X方向の端
    [SerializeField] private float tableLength = 2.0f; // Z方向の端

    // ★追加：Spawnされたタイミング（ネットワーク変数が安全に使える状態）でマネージャーに登録する
    public override void Spawned()
    {
        // マネージャーを探して登録
        var manager = BilliardTableManager.Instance;
        if (manager == null) manager = FindObjectOfType<BilliardTableManager>();

        if (manager != null)
        {
            manager.RegisterBall(this);
        }
    }

    // ★追加：消えるタイミングでマネージャーから除外する
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        var manager = BilliardTableManager.Instance;
        if (manager == null) manager = FindObjectOfType<BilliardTableManager>();

        if (manager != null)
        {
            manager.UnregisterBall(this);
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 速度が一定以上ある場合のみ移動処理を行う
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
            // 速度が極小になったら完全に停止させる
            Velocity = Vector3.zero;
        }
    }

    private void CheckWallCollision()
    {
        Vector3 pos = transform.position;
        Vector3 vel = Velocity;

        // X方向の壁との判定
        if (Mathf.Abs(pos.x) + radius > tableWidth)
        {
            pos.x = Mathf.Sign(pos.x) * (tableWidth - radius);
            vel.x *= -1 * bounciness; // 速度を反転して反発
        }

        // Z方向の壁との判定
        if (Mathf.Abs(pos.z) + radius > tableLength)
        {
            pos.z = Mathf.Sign(pos.z) * (tableLength - radius);
            vel.z *= -1 * bounciness; // 速度を反転して反発
        }

        transform.position = pos;
        Velocity = vel;
    }

    /// <summary>
    /// 他のボールとの衝突を解決します（BilliardTableManagerから呼ばれます）
    /// </summary>
    /// <summary>
    /// 他のボールとの衝突を解決します（BilliardTableManagerから呼ばれます）
    /// </summary>
    public void ResolveBallCollision(BilliardBall other)
    {
        // 念の為の安全策
        if (!Object.IsValid || !other.Object.IsValid) return;

        // 2つのボールの距離を計算
        Vector3 delta = transform.position - other.transform.position;
        float distance = delta.magnitude;
        float minDistance = this.radius + other.radius;

        // 半径の合計より距離が近い＝衝突している
        if (distance < minDistance)
        {
            // ---------------------------------------------------------
            // 1. 重なり防止（めり込みを修正）
            // ---------------------------------------------------------
            Vector3 normal = delta.normalized;
            if (distance == 0) normal = Vector3.forward; // ゼロ除算回避

            float overlap = minDistance - distance;

            // 互いに半分ずつ押し出す
            transform.position += normal * (overlap / 2f);
            other.transform.position -= normal * (overlap / 2f);

            // ---------------------------------------------------------
            // 2. 物理的な衝突計算（ベクトル分解）
            // ---------------------------------------------------------

            // 相対速度を計算
            Vector3 relativeVelocity = this.Velocity - other.Velocity;
            float velocityAlongNormal = Vector3.Dot(relativeVelocity, normal);

            // お互いに近づいている場合のみ衝突処理を行う
            if (velocityAlongNormal < 0)
            {
                // 球同士の反発係数（1.0に近いほど完全弾性衝突＝エネルギーロスなし）
                // 硬い球同士なので通常は 0.9 ～ 0.98 程度
                float ballRestitution = 0.98f;

                // --- 手球 (this) の計算 ---
                // 現在の速度を法線成分と接線成分に分解
                float v1DotNormal = Vector3.Dot(this.Velocity, normal);
                Vector3 v1NormalVec = normal * v1DotNormal; // 法線成分ベクトル
                Vector3 v1TangentVec = this.Velocity - v1NormalVec; // 接線成分ベクトル

                // --- 相手球 (other) の計算 ---
                float v2DotNormal = Vector3.Dot(other.Velocity, normal);
                Vector3 v2NormalVec = normal * v2DotNormal;
                Vector3 v2TangentVec = other.Velocity - v2NormalVec;

                // --- 1次元の完全弾性衝突公式（質量が等しい場合） ---
                // 新しい法線速度 v1' = (v1 * (1-e) + v2 * (1+e)) / 2
                // 新しい法線速度 v2' = (v2 * (1-e) + v1 * (1+e)) / 2

                float v1NormalNew = (v1DotNormal * (1 - ballRestitution) + v2DotNormal * (1 + ballRestitution)) / 2f;
                float v2NormalNew = (v2DotNormal * (1 - ballRestitution) + v1DotNormal * (1 + ballRestitution)) / 2f;

                // --- 速度の合成 ---
                // 法線成分は新しく計算した値を使い、接線成分はそのまま維持する
                this.Velocity = v1TangentVec + (normal * v1NormalNew);
                other.Velocity = v2TangentVec + (normal * v2NormalNew);
            }
        }
    }
}