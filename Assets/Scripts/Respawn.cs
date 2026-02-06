using UnityEngine;
using UnityEngine.InputSystem;

public class RespawnPoint : MonoBehaviour
{
    public float deadLineY = -2.5f;
    public Transform respawnPosition;
    public int maxRespawn = 2;

    int respawnCount = 0;

        // 【追加】Holeタグがついたオブジェクト（穴）に触れたらリスポーン
        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Hole"))
            {
                Debug.Log("Holeに落下しました。リスポーンします。");
                HandlRespawn();
            }
        }

        void HandlRespawn()
        {
            respawnCount++;

            if (respawnCount > maxRespawn)
            {
                gameObject.SetActive(false);
                return;
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Unity 6以降のプロパティ (旧バージョンの場合は rb.velocity を使用)
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (respawnPosition != null)
            {
                transform.position = respawnPosition.position;
            }
        }
}