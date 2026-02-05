using UnityEngine;
using Fusion;

public class BilliardBall : NetworkBehaviour
{
    // 最後にこのボールに影響を与えたプレイヤーを同期変数として保持
    [Networked] public Vector3 Velocity { get; set; }
    [Networked] public PlayerRef LastHitter { get; set; }

    [Header("物理設定")]
    [SerializeField] public float radius = 0.03f;
    [SerializeField] private float friction = 0.985f;
    [SerializeField] private float stopThreshold = 0.01f;
    [SerializeField] private float bounciness = 0.8f;

    [Header("壁の設定")]
    [SerializeField] private string wallTag = "Wall";

    public override void Spawned()
    {
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
        if (manager != null) manager.UnregisterBall(this);
    }

    public override void FixedUpdateNetwork()
    {
        // 移動処理
        if (Velocity.magnitude > stopThreshold)
        {
            HandleWallCollision();
            transform.position += Velocity * Runner.DeltaTime;
            Velocity *= friction;
        }
        else
        {
            Velocity = Vector3.zero;
        }
    }

    private void HandleWallCollision()
    {
        float moveDistance = Velocity.magnitude * Runner.DeltaTime;
        if (moveDistance <= Mathf.Epsilon) return;

        // 壁との衝突検知
        if (Physics.SphereCast(transform.position, radius, Velocity.normalized, out RaycastHit hit, moveDistance))
        {
            if (hit.collider.CompareTag(wallTag))
            {
                // 反射計算
                Vector3 reflectDir = Vector3.Reflect(Velocity, hit.normal);
                Velocity = reflectDir * bounciness;
            }
        }
    }

    public void ResolveBallCollision(BilliardBall other)
    {
        if (!Object.IsValid || !other.Object.IsValid) return;

        Vector3 delta = transform.position - other.transform.position;
        float distance = delta.magnitude;
        float minDistance = this.radius + other.radius;

        // ボール同士の重なりチェック
        if (distance < minDistance)
        {
            // ▼▼▼ LastHitterの伝播処理 (ここが追加箇所) ▼▼▼
            // 玉突きが発生した際、打った人の情報を伝播させる
            if (this.LastHitter != PlayerRef.None)
            {
                other.LastHitter = this.LastHitter;
            }
            else if (other.LastHitter != PlayerRef.None)
            {
                this.LastHitter = other.LastHitter;
            }
            // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

            Vector3 normal = delta.normalized;
            if (distance == 0) normal = Vector3.forward;

            // 押し出し処理
            float overlap = minDistance - distance;
            transform.position += normal * (overlap / 2f);
            other.transform.position -= normal * (overlap / 2f);

            Vector3 relativeVelocity = this.Velocity - other.Velocity;
            float velocityAlongNormal = Vector3.Dot(relativeVelocity, normal);

            // 近づき合っている場合のみ物理衝突計算を行う
            if (velocityAlongNormal < 0)
            {
                // 物理挙動の計算
                float ballRestitution = 0.98f;

                float v1DotNormal = Vector3.Dot(this.Velocity, normal);
                Vector3 v1NormalVec = normal * v1DotNormal;
                Vector3 v1TangentVec = this.Velocity - v1NormalVec;

                float v2DotNormal = Vector3.Dot(other.Velocity, normal);
                Vector3 v2NormalVec = normal * v2DotNormal;
                Vector3 v2TangentVec = other.Velocity - v2NormalVec;

                float v1NormalNew = (v1DotNormal * (1 - ballRestitution) + v2DotNormal * (1 + ballRestitution)) / 2f;
                float v2NormalNew = (v2DotNormal * (1 - ballRestitution) + v1DotNormal * (1 + ballRestitution)) / 2f;

                this.Velocity = v1TangentVec + (normal * v1NormalNew);
                other.Velocity = v2TangentVec + (normal * v2NormalNew);
            }
        }
    }



}