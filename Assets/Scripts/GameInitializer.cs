using UnityEngine;
using Photon.Pun;

public class GameInitializer : MonoBehaviour
{
    void Start()
    {
        // 1. ネットワーク上にPlayerObjectを生成
        // 自分の位置が重ならないようにランダムな場所に生成
        Vector3 spawnPos = new Vector3(Random.Range(-2f, 2f), 0.5f, Random.Range(-2f, 2f));
        PhotonNetwork.Instantiate("PlayerObject", spawnPos, Quaternion.identity);
    }
}