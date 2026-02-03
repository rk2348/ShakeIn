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
            var rig = GameObject.FindObjectOfType<OVRCameraRig>();
            if (rig != null)
            {
                localHeadAnchor = rig.centerEyeAnchor;
                localLeftHandAnchor = rig.leftHandAnchor;
                localRightHandAnchor = rig.rightHandAnchor;
            }

            // 【追加】自分の頭（球）の見た目だけを非表示にする処理
            // networkHeadそのものをSetActive(false)にすると、位置の同期（NetworkTransform）も止まってしまうため、
            // その子供にあるMeshRendererコンポーネントだけを無効にします。
            var renderers = networkHead.GetComponentsInChildren<MeshRenderer>();
            foreach (var r in renderers)
            {
                r.enabled = false;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 自分の入力権限（HasInputAuthority）がある場合のみ、リグの動きをネットワーク用オブジェクトにコピーする
        if (HasInputAuthority && localHeadAnchor != null)
        {
            // これらのオブジェクトに NetworkTransform が付いていれば、他プレイヤーへ同期が始まります
            networkHead.position = localHeadAnchor.position;
            networkHead.rotation = localHeadAnchor.rotation;

            networkLeftHand.position = localLeftHandAnchor.position;
            networkLeftHand.rotation = localLeftHandAnchor.rotation;

            networkRightHand.position = localRightHandAnchor.position;
            networkRightHand.rotation = localRightHandAnchor.rotation;
        }
    }
}