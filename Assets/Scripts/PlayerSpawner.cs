using Fusion;
using UnityEngine;

// プレイヤーPrefabにアタッチし、実際のOVRCameraRigの動きをネットワークオブジェクトに反映させるクラス
public class VRRigSync : NetworkBehaviour
{
    [Header("Network Objects Parts")]
    [SerializeField] private Transform networkHead;
    [SerializeField] private Transform networkLeftHand;
    [SerializeField] private Transform networkRightHand;

    // ローカルシーン上のVRリグへの参照
    private Transform localHeadAnchor;
    private Transform localLeftHandAnchor;
    private Transform localRightHandAnchor;
    private Transform localRigRoot;

    public override void Spawned()
    {
        // 自分のキャラクターの場合のみ、ローカルのVR機器を探してリンクする
        if (HasInputAuthority)
        {
            FindAndAssignLocalRig();

            // 自分の視界に自分の頭モデルが映らないようにレンダラーを消す
            if (networkHead != null)
            {
                var renderers = networkHead.GetComponentsInChildren<MeshRenderer>();
                foreach (var r in renderers) r.enabled = false;
            }
        }
    }

    private void FindAndAssignLocalRig()
    {
        var rig = FindObjectOfType<OVRCameraRig>();
        if (rig != null)
        {
            localRigRoot = rig.transform;
            localHeadAnchor = rig.centerEyeAnchor;
            localLeftHandAnchor = rig.leftHandAnchor;
            localRightHandAnchor = rig.rightHandAnchor;
            Debug.Log("【VRRigSync】OVRCameraRigとリンクしました。");
        }
        else
        {
            Debug.LogWarning("【VRRigSync】シーン内にOVRCameraRigが見つかりません！");
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 自分が操作権限を持つオブジェクトのみ位置を更新
        if (HasInputAuthority)
        {
            // リグがまだ見つかっていない、あるいはシーンロード等で外れた場合は再検索
            if (localRigRoot == null)
            {
                FindAndAssignLocalRig();
                if (localRigRoot == null) return;
            }

            // 本体の位置同期
            transform.position = localRigRoot.position;
            transform.rotation = localRigRoot.rotation;

            // 各パーツの同期
            if (localHeadAnchor != null && networkHead != null)
            {
                networkHead.position = localHeadAnchor.position;
                networkHead.rotation = localHeadAnchor.rotation;
            }

            if (localLeftHandAnchor != null && networkLeftHand != null)
            {
                networkLeftHand.position = localLeftHandAnchor.position;
                networkLeftHand.rotation = localLeftHandAnchor.rotation;
            }

            if (localRightHandAnchor != null && networkRightHand != null)
            {
                networkRightHand.position = localRightHandAnchor.position;
                networkRightHand.rotation = localRightHandAnchor.rotation;
            }
        }
    }
}