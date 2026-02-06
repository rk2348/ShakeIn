using UnityEngine;
using Fusion;

public class BilliardBall : NetworkBehaviour
{
    [Networked] public Vector3 Velocity { get; set; }
    [Networked] public PlayerRef LastHitter { get; set; }

    [Networked] private Vector3 NetworkPosition { get; set; }

    [SerializeField] public float radius = 0.03f;
    [SerializeField] private float friction = 0.985f;
    [SerializeField] private float stopThreshold = 0.01f;
    [SerializeField] private float bounciness = 0.8f;
    [SerializeField] private string wallTag = "Wall";

    private PlayerRef? _pendingLastHitter = null;

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
                Runner.Despawn(Object);
            }
        }
    }

    private void HandleWallCollision()
    {
        float moveDistance = Velocity.magnitude * Runner.DeltaTime;
        if (moveDistance <= Mathf.Epsilon) return;

        float margin = radius + 0.05f;
        Vector3 direction = Velocity.normalized;
        Vector3 origin = transform.position - (direction * margin);
        float checkDistance = moveDistance + margin;

        if (Physics.SphereCast(origin, radius, direction, out RaycastHit hit, checkDistance))
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
            if (!other.Object.HasStateAuthority)
            {
                other.Object.RequestStateAuthority();
            }

            if (this.LastHitter != PlayerRef.None)
            {
                if (other.Object.HasStateAuthority)
                {
                    other.LastHitter = this.LastHitter;
                }
            }

            Vector3 normal = delta.normalized;
            if (distance == 0) normal = Vector3.forward;

            float overlap = minDistance - distance;
            transform.position += normal * overlap;

            if (Object.HasStateAuthority) NetworkPosition = transform.position;

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

                if (other.Object.HasStateAuthority)
                {
                    other.Velocity = v2TangentVec + (normal * v2NormalNew);
                }
            }
        }
    }
}