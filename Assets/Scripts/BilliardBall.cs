using UnityEngine;
using Fusion;

public class BilliardBall : NetworkBehaviour
{
    [Networked] public Vector3 Velocity { get; set; }
    [Networked] public PlayerRef LastHitter { get; set; }

    // 【追加】位置情報をネットワークで正確に同期するための変数
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
        var manager = BilliardTableManager.Instance;
        if (manager == null) manager = FindObjectOfType<BilliardTableManager>();
        if (manager != null) manager.RegisterBall(this);

        // 初期化時に現在の位置をネットワーク変数にセット（権限がある場合）
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
            // --- 権限がある場合の処理（物理計算を行う） ---

            // LastHitterの更新
            if (_pendingLastHitter.HasValue)
            {
                LastHitter = _pendingLastHitter.Value;
                _pendingLastHitter = null;
            }

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

            // 【重要】計算結果の位置をネットワーク変数に保存
            NetworkPosition = transform.position;
        }
        else
        {
            // --- 権限がない場合の処理（位置を同期する） ---

            // ネットワーク上の正しい位置に強制的に合わせる
            // これにより、画面ごとの位置ズレ（ワープ現象）を防ぐ
            // ※より滑らかにしたい場合は Vector3.Lerp を使用するが、まずは正確性を重視
            if (Vector3.Distance(transform.position, NetworkPosition) > 0.001f)
            {
                transform.position = NetworkPosition;
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

        // 衝突判定
        if (distance < minDistance)
        {
            // 【重要】相手のボールの権限チェックと取得
            // 相手のボールを動かすには権限が必要。持っていなければ要求する。
            if (!other.Object.HasStateAuthority)
            {
                other.Object.RequestStateAuthority();
                // 権限取得には時間がかかるため、このフレームでの完全な物理反映は保証できないが、
                // 次のフレーム以降で同期されるようにする。
            }

            // LastHitterの伝播
            if (this.LastHitter != PlayerRef.None)
            {
                // 相手の権限を持っていれば書き込む
                if (other.Object.HasStateAuthority)
                {
                    other.LastHitter = this.LastHitter;
                }
            }

            // --- 物理計算 ---
            Vector3 normal = delta.normalized;
            if (distance == 0) normal = Vector3.forward;

            // めり込み解消（位置補正）
            float overlap = minDistance - distance;
            // 自分だけ動かす（相手の権限がない場合、相手の位置を変えるとめり込みが悪化することがあるため）
            // 理想は両方動かすが、権限ベースでは自分が避けるのが安全
            transform.position += normal * overlap;

            // NetworkPositionも更新しておく
            if (Object.HasStateAuthority) NetworkPosition = transform.position;


            // 速度の交換計算
            Vector3 relativeVelocity = this.Velocity - other.Velocity;
            float velocityAlongNormal = Vector3.Dot(relativeVelocity, normal);

            // 互いに近づいている場合のみ計算
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

                // 自分の速度適用
                this.Velocity = v1TangentVec + (normal * v1NormalNew);

                // 相手の速度適用（権限がある場合のみ有効だが、権限リクエスト中なら次回反映される）
                if (other.Object.HasStateAuthority)
                {
                    other.Velocity = v2TangentVec + (normal * v2NormalNew);
                }
                else
                {
                    // 権限がない場合、本来はここでRPCを送るのがベストだが、
                    // 簡易的には「自分が弾かれる」計算は上記で成立しているため、相手の反応は権限取得を待つ
                }
            }
        }
    }
}