using UnityEngine;
using Fusion;

public class BilliardPocket : NetworkBehaviour
{
    [SerializeField] private int scorePerBall = 1;

    private void OnTriggerEnter(Collider other)
    {
        if (!Object.HasStateAuthority) return;

        var ball = other.GetComponent<BilliardBall>();
        if (ball != null)
        {
            if (ball.LastHitter != PlayerRef.None)
            {
                var manager = BilliardTableManager.Instance;
                if (manager != null)
                {
                    manager.AddScore(ball.LastHitter, scorePerBall);
                    Debug.Log($"Ball potted by Player {ball.LastHitter.PlayerId}. Added {scorePerBall} points.");
                }
            }
            else
            {
                Debug.Log("Ball potted, but no player touched it recently.");
            }

            Runner.Despawn(ball.Object);
        }
    }
}