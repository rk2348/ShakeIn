using UnityEngine;
using Fusion;

public class BilliardBall : NetworkBehaviour
{
    [Networked] public Vector3 Velocity { get; set; } //ネットワーク同期される速度
    [Networked] public PlayerRef LastHitter { get; set; } //【追加】最後に触れたプレイヤー

    [Header("物理設定")]
    [SerializeField] public float radius = 0.03f;     //ボールの半径
    [SerializeField] private float friction = 0.985f;  //摩擦
    [SerializeField] private float stopThreshold = 0.01f; //停止とみなす速度
    [SerializeField] private float bounciness = 0.8f;  //クッションの反発係数

    [Header("壁の設定")]
    [SerializeField] private string wallTag = "Wall"; //壁とみなすオブジェクトのタグ

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

        if (manager != null)
        {
            manager.UnregisterBall(this);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Velocity.magnitude > stopThreshold)
        {
            HandleWallCollision();

            transform.position += Velocity * Runner.DeltaTime;

            Velocity *= friction;
        }
        else
        {
            // 速度が極小になったら完全停止
            Velocity = Vector3.zero;
        }
    }

    private void HandleWallCollision()
    {
        float moveDistance = Velocity.magnitude * Runner.DeltaTime;

        if (moveDistance <= Mathf.Epsilon) return;

        if (Physics.SphereCast(transform.position, radius, Velocity.normalized, out RaycastHit hit, moveDistance))
        {
            if (hit.collider.CompareTag(wallTag))
            {
                //壁の法線を使って反射ベクトル計算
                Vector3 reflectDir = Vector3.Reflect(Velocity, hit.normal);

                //反発係数を適用して速度を毎フレーム更新
                Velocity = reflectDir * bounciness;
            }
        }
    }

    public void ResolveBallCollision(BilliardBall other)
    {
        //念の為
        if (!Object.IsValid || !other.Object.IsValid) return;

        Vector3 delta = transform.position - other.transform.position;
        float distance = delta.magnitude;
        float minDistance = this.radius + other.radius;

        if (distance < minDistance)
        {
            //めり込み防止
            Vector3 normal = delta.normalized;
            if (distance == 0) normal = Vector3.forward;

            float overlap = minDistance - distance;

            transform.position += normal * (overlap / 2f);
            other.transform.position -= normal * (overlap / 2f);

            Vector3 relativeVelocity = this.Velocity - other.Velocity;
            float velocityAlongNormal = Vector3.Dot(relativeVelocity, normal);

            if (velocityAlongNormal < 0)
            {
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