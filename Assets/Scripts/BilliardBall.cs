using UnityEngine;
using Fusion;

public class BilliardBall : NetworkBehaviour
{
    [Header("ボール設定")]
    [Tooltip("手球は0、的球は1?9等の番号")]
    public int BallNumber = 0;

    [Networked] public Vector3 Velocity { get; set; }
    [Networked] public PlayerRef LastHitter { get; set; }
    [Networked] private Vector3 NetworkPosition { get; set; }

    [Header("物理設定")]
    [SerializeField] public float radius = 0.03f;
    [SerializeField] private float friction = 0.985f;
    [SerializeField] private float stopThreshold = 0.01f;
    [SerializeField] private float bounciness = 0.8f;
    [SerializeField] private string wallTag = "Wall";

    private PlayerRef? _pendingLastHitter = null;

    public override void Spawned()
    {
        // シーンロード直後はManagerが見つからない場合があるため、遅延対応できるようにする
        RegisterToManager();

        if (Object.HasStateAuthority)
        {
            NetworkPosition = transform.position;
        }
    }

    private void RegisterToManager()
    {
        var manager = BilliardTableManager.Instance;
        // シーン内にManagerがあるか検索
        if (manager == null) manager = FindObjectOfType<BilliardTableManager>();

        if (manager != null)
        {
            manager.RegisterBall(this);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        var manager = BilliardTableManager.Instance;
        if (manager != null) manager.UnregisterBall(this);
    }

    public void OnHit(Vector3 velocity, PlayerRef hitter)
    {
        if (!Object.HasStateAuthority)
        {
            Object.RequestStateAuthority();
        }
        _pendingLastHitter = hitter;
        this.Velocity = velocity;
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority)
        {
            if (_pendingLastHitter.HasValue)
            {
                LastHitter = _pendingLastHitter.Value;
                _pendingLastHitter = null;
            }

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

            NetworkPosition = transform.position;
        }
        else
        {
            // 位置同期の補間
            if (Vector3.Distance(transform.position, NetworkPosition) > 0.001f)
            {
                transform.position = Vector3.Lerp(transform.position, NetworkPosition, Runner.DeltaTime * 10f);
            }
        }
    }

    private void HandleWallCollision()
    {
        float moveDistance = Velocity.magnitude * Runner.DeltaTime;
        if (moveDistance <= Mathf.Epsilon) return;

        float margin = radius + 0.05f;
        Vector3 direction = Velocity.normalized;
        Vector3 origin = transform.position;

        // Raycastの開始位置を少し後ろにずらす（めり込み対策）
        if (Physics.SphereCast(origin - (direction * 0.01f), radius, direction, out RaycastHit hit, moveDistance + margin))
        {
            if (hit.collider.CompareTag(wallTag))
            {
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

        if (distance < minDistance)
        {
            if (!other.Object.HasStateAuthority) other.Object.RequestStateAuthority();

            if (this.LastHitter != PlayerRef.None && other.Object.HasStateAuthority)
            {
                other.LastHitter = this.LastHitter;
            }

            Vector3 normal = delta.normalized;
            if (distance == 0) normal = Vector3.forward;

            // めり込み解消
            float overlap = minDistance - distance;
            transform.position += normal * (overlap * 0.5f);
            other.transform.position -= normal * (overlap * 0.5f);

            if (Object.HasStateAuthority) NetworkPosition = transform.position;
            // other側のNetworkPosition更新は相手のFixedUpdateNetworkで行われるか、ここで行うなら権限チェックが必要

            // 速度計算（簡易版）
            Vector3 relativeVelocity = this.Velocity - other.Velocity;
            float velocityAlongNormal = Vector3.Dot(relativeVelocity, normal);

            if (velocityAlongNormal < 0)
            {
                float restitution = 0.9f;
                float j = -(1 + restitution) * velocityAlongNormal;
                j /= 2; // 質量が同じと仮定

                Vector3 impulse = j * normal;
                this.Velocity += impulse;
                if (other.Object.HasStateAuthority) other.Velocity -= impulse;
            }
        }
    }
}