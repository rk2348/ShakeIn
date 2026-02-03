using Fusion;
using UnityEngine;

public class PlayerSpawner : NetworkBehaviour
{
    [Header("Network Objects")]
    [SerializeField] private Transform networkHead;
    [SerializeField] private Transform networkLeftHand;
    [SerializeField] private Transform networkRightHand;

    // ローカルのOVRCameraRigの各パーツを参照するための変数
    private Transform localHeadAnchor;
    private Transform localLeftHandAnchor;
    private Transform localRightHandAnchor;

    public override void Spawned()
    {
        // 自分が操作するアバター（自分自身）の場合だけ、シーン内のOVRCameraRigを探す
        if (HasInputAuthority)
        {
            // シーン内のOVRCameraRigにある各Anchorを見つける
            // (タグや名前で検索、またはManagerから取得)
            var rig = GameObject.FindObjectOfType<OVRCameraRig>();
            if (rig != null)
            {
                localHeadAnchor = rig.centerEyeAnchor;
                localLeftHandAnchor = rig.leftHandAnchor;
                localRightHandAnchor = rig.rightHandAnchor;
            }

            // 自分のアバターのモデル（頭など）は、自分の視界の邪魔になるので非表示にする処理
            // networkHead.gameObject.SetActive(false); 
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 自分の入力権限（HasInputAuthority）がある場合のみ、リグの動きをネットワーク用オブジェクトにコピーする
        if (HasInputAuthority && localHeadAnchor != null)
        {
            // これらのオブジェクトに NetworkTransform が付いていれば、
            // ここで代入した瞬間に他プレイヤーへ同期が始まります
            networkHead.position = localHeadAnchor.position;
            networkHead.rotation = localHeadAnchor.rotation;

            networkLeftHand.position = localLeftHandAnchor.position;
            networkLeftHand.rotation = localLeftHandAnchor.rotation;

            networkRightHand.position = localRightHandAnchor.position;
            networkRightHand.rotation = localRightHandAnchor.rotation;
        }
    }
}