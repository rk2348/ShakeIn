using Fusion;
using UnityEngine;

public class PlayerSpawner : NetworkBehaviour
{
    [Header("Network Objects")]
    [SerializeField] private Transform networkHead;
    [SerializeField] private Transform networkLeftHand;
    [SerializeField] private Transform networkRightHand;

    private Transform localHeadAnchor;
    private Transform localLeftHandAnchor;
    private Transform localRightHandAnchor;

    private Transform localRigRoot;

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            // 初期化試行（見つからなくても後で再試行する）
            FindAndAssignLocalRig();

            var renderers = networkHead.GetComponentsInChildren<MeshRenderer>();
            foreach (var r in renderers) r.enabled = false;
        }
    }

    // 【追加】リグ検索用メソッド
    private void FindAndAssignLocalRig()
    {
        var rig = GameObject.FindObjectOfType<OVRCameraRig>();
        if (rig != null)
        {
            localRigRoot = rig.transform;

            localHeadAnchor = rig.centerEyeAnchor;
            localLeftHandAnchor = rig.leftHandAnchor;
            localRightHandAnchor = rig.rightHandAnchor;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (HasInputAuthority)
        {
            // 【修正】まだリグが見つかっていないなら再検索
            if (localRigRoot == null)
            {
                FindAndAssignLocalRig();
                // まだなければ中断
                if (localRigRoot == null) return;
            }

            // --- 位置同期処理 ---
            transform.position = localRigRoot.position;
            transform.rotation = localRigRoot.rotation;

            if (localHeadAnchor != null)
            {
                networkHead.position = localHeadAnchor.position;
                networkHead.rotation = localHeadAnchor.rotation;
            }

            if (localLeftHandAnchor != null)
            {
                networkLeftHand.position = localLeftHandAnchor.position;
                networkLeftHand.rotation = localLeftHandAnchor.rotation;
            }

            if (localRightHandAnchor != null)
            {
                networkRightHand.position = localRightHandAnchor.position;
                networkRightHand.rotation = localRightHandAnchor.rotation;
            }
        }
    }
}