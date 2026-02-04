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

    [Header("壁の設定")]
    [SerializeField] private string wallTag = "Wall"; // 壁とみなすオブジェクトのタグ

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
            // 1. 壁との衝突予測と解決
            HandleWallCollision();

            // 2. 位置の更新
            transform.position += Velocity * Runner.DeltaTime;

            // 3. 摩擦による減速
            Velocity *= friction;
        }
        else
        {
            // 速度が極小になったら完全に停止させる
            Velocity = Vector3.zero;
        }
    }

    /// <summary>
    /// 進行方向にある壁を検知して衝突処理を行います
    /// </summary>
    private void HandleWallCollision()
    {
        // 今回のフレームで移動する距離
        float moveDistance = Velocity.magnitude * Runner.DeltaTime;

        // 移動量がほぼゼロなら判定しない
        if (moveDistance <= Mathf.Epsilon) return;

        // 進行方向に球（Sphere）を飛ばして壁があるか調べる
        // ※自身のColliderに当たらないよう注意が必要ですが、SphereCastは通常開始位置のColliderを無視します
        if (Physics.SphereCast(transform.position, radius, Velocity.normalized, out RaycastHit hit, moveDistance))
        {
            // 当たったオブジェクトが指定されたタグ（壁）か確認
            if (hit.collider.CompareTag(wallTag))
            {
                // 壁の法線を使って反射ベクトルを計算
                Vector3 reflectDir = Vector3.Reflect(Velocity, hit.normal);

                // 速度を更新（反発係数を適用）
                Velocity = reflectDir * bounciness;

                // 補足:
                // 厳密な物理挙動では「衝突点まで移動→反射→残りの距離を移動」としますが、
                // ここでは簡易的に「速度を反射させる」ことで次のフレームから跳ね返るようにしています。
            }
        }
    }

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
                // 球同士の反発係数
                float ballRestitution = 0.98f;

                // --- 手球 (this) の計算 ---
                float v1DotNormal = Vector3.Dot(this.Velocity, normal);
                Vector3 v1NormalVec = normal * v1DotNormal;
                Vector3 v1TangentVec = this.Velocity - v1NormalVec;

                // --- 相手球 (other) の計算 ---
                float v2DotNormal = Vector3.Dot(other.Velocity, normal);
                Vector3 v2NormalVec = normal * v2DotNormal;
                Vector3 v2TangentVec = other.Velocity - v2NormalVec;

                // --- 1次元の完全弾性衝突公式 ---
                float v1NormalNew = (v1DotNormal * (1 - ballRestitution) + v2DotNormal * (1 + ballRestitution)) / 2f;
                float v2NormalNew = (v2DotNormal * (1 - ballRestitution) + v1DotNormal * (1 + ballRestitution)) / 2f;

                // --- 速度の合成 ---
                this.Velocity = v1TangentVec + (normal * v1NormalNew);
                other.Velocity = v2TangentVec + (normal * v2NormalNew);
            }
        }
    }
}