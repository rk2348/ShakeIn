using UnityEngine;
using Fusion;

public class BilliardBall : NetworkBehaviour
{
    [Header("ボール設定")]
    [Tooltip("手球は0、的球は1?9等の番号を設定してください")]
    public int BallNumber = 0;

    [Networked] public Vector3 Velocity { get; set; }
    [Networked] public PlayerRef LastHitter { get; set; }
    [Networked] private Vector3 NetworkPosition { get; set; }

    [Header("物理設定")]
    private float radius = 0.05f; // 半径を実測値に合わせて調整推奨
    [SerializeField] private float friction = 0.985f;
    [SerializeField] private float stopThreshold = 0.01f;
    [SerializeField] private float bounciness = 0.8f; // 壁反射時の速度維持率
    [SerializeField] private string wallTag = "Wall";

    private PlayerRef? _pendingLastHitter = null;

    // 衝突判定用の定数（プレイヤー側と共有）
    private const float CollisionSpeedRetention = 0.2f;
    private const float PowerMultiplier = 1.2f;

    public override void Spawned()
    {
        var manager = BilliardTableManager.Instance;
        if (manager == null) manager = FindObjectOfType<BilliardTableManager>();
        if (manager != null) manager.RegisterBall(this);

        if (Object.HasStateAuthority)
        {
            NetworkPosition = transform.position;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        var manager = BilliardTableManager.Instance;
        if (manager == null) manager = FindObjectOfType<BilliardTableManager>();
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
            if (Vector3.Distance(transform.position, NetworkPosition) > 0.001f)
            {
                transform.position = NetworkPosition;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Object != null && Object.HasStateAuthority)
        {
            if (other.GetComponent<BilliardPocket>() != null)
            {
                Debug.Log($"【BilliardBall】ボール{BallNumber}がポケットに入りました。Despawnします。");
                Runner.Despawn(Object);
            }
        }
    }

    private void HandleWallCollision()
    {
        float moveDistance = Velocity.magnitude * Runner.DeltaTime;
        if (moveDistance <= Mathf.Epsilon) return;

        float margin = radius + 0.01f;
        Vector3 direction = Velocity.normalized;
        Vector3 origin = transform.position;
        float checkDistance = moveDistance + margin;

        if (Physics.SphereCast(origin, radius, direction, out RaycastHit hit, checkDistance))
        {
            if (hit.collider.CompareTag(wallTag))
            {
                // 反射ベクトル計算（Y軸は固定）
                Vector3 reflectDir = Vector3.Reflect(Velocity, hit.normal);
                reflectDir.y = 0;

                // 速度の適用と減衰
                Velocity = reflectDir * bounciness;

                Debug.Log($"【的球反射】ボール{BallNumber}が壁に衝突。速度維持率: {bounciness}");
            }
        }
    }

    public void ResolveBallCollision(BilliardBall other)
    {
        if (!Object.IsValid || !other.Object.IsValid) return;

        Vector3 delta = other.transform.position - transform.position;
        float distance = delta.magnitude;
        float minDistance = this.radius + other.radius;

        if (distance < minDistance)
        {
            if (!other.Object.HasStateAuthority) other.Object.RequestStateAuthority();
            if (this.LastHitter != PlayerRef.None && other.Object.HasStateAuthority)
            {
                other.LastHitter = this.LastHitter;
            }

            // 位置補正
            Vector3 normal = delta.normalized;
            if (distance == 0) normal = Vector3.forward;
            float overlap = minDistance - distance;

            transform.position -= normal * (overlap * 0.5f);
            other.transform.position += normal * (overlap * 0.5f);

            if (Object.HasStateAuthority) NetworkPosition = transform.position;

            // 速度転送ロジック（プレイヤー衝突側と統一）
            BilliardBall hitter = (this.Velocity.magnitude >= other.Velocity.magnitude) ? this : other;
            BilliardBall target = (hitter == this) ? other : this;

            Vector3 hitDir = (target.transform.position - hitter.transform.position).normalized;
            hitDir.y = 0;

            float power = Mathf.Max(hitter.Velocity.magnitude, 1.0f);
            Vector3 transferredVelocity = hitDir * power * PowerMultiplier;

            target.Velocity = transferredVelocity;
            hitter.Velocity *= CollisionSpeedRetention;

            Debug.Log($"【球間衝突】{hitter.BallNumber}が{target.BallNumber}に衝突。パワー転送: {power}");
        }
    }
}