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
    public void ResolveBallCollision(BilliardBall other)
    {
        // ★追加：念の為の安全策
        if (!Object.IsValid || !other.Object.IsValid) return;

        // 2つのボールの距離を計算
        Vector3 delta = transform.position - other.transform.position;
        float distance = delta.magnitude;
        float minDistance = this.radius + other.radius;

        // 半径の合計より距離が近い＝衝突している
        if (distance < minDistance)
        {
            // 1. 重なり防止（めり込みを修正）
            Vector3 normal = delta.normalized;
            if (distance == 0) normal = Vector3.forward;

            float overlap = minDistance - distance;

            // 互いに半分ずつ押し出す
            transform.position += normal * (overlap / 2f);
            other.transform.position -= normal * (overlap / 2f);

            // 2. 衝突による速度の入れ替え（完全交換）
            // ベクトル演算による物理計算ではなく、速度そのものを入れ替えます。

            // お互いに近づいている（衝突に向かっている）場合のみ処理
            // (これをチェックしないと、重なっている間に連続で入れ替わり続けておかしくなります)
            Vector3 relativeVelocity = this.Velocity - other.Velocity;
            float velocityAlongNormal = Vector3.Dot(relativeVelocity, normal);

            if (velocityAlongNormal < 0)
            {
                // 速度ベクトルをまるごと交換する
                // これにより、当てた側(this)の速度と向きがそのまま相手(other)に移り、
                // 相手の速度(止まっていれば0)が自分に移ります。
                Vector3 tempVelocity = this.Velocity;
                this.Velocity = other.Velocity;
                other.Velocity = tempVelocity;
            }
        }
    }
}