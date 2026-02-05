using UnityEngine;
using Fusion;

public class BilliardPocket : NetworkBehaviour
{
    // scorePerBall 変数を削除

    private void OnTriggerEnter(Collider other)
    {
        if (!Object.HasStateAuthority) return;

        var ball = other.GetComponent<BilliardBall>();
        if (ball != null)
        {
            // スコア加算処理を削除し、ボールの消去のみを行う
            Debug.Log("Ball potted.");
            Runner.Despawn(ball.Object);
        }
    }
}