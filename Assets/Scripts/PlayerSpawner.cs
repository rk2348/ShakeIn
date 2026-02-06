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
            var rig = GameObject.FindObjectOfType<OVRCameraRig>();
            if (rig != null)
            {
                localRigRoot = rig.transform;

                localHeadAnchor = rig.centerEyeAnchor;
                localLeftHandAnchor = rig.leftHandAnchor;
                localRightHandAnchor = rig.rightHandAnchor;
            }

            // --- 修正箇所：自分自身のすべてのレンダラーを非表示にする ---
            // networkHeadだけでなく、自身の子要素にあるすべてのMeshRendererやSkinnedMeshRendererを取得
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                r.enabled = false;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (HasInputAuthority && localRigRoot != null)
        {
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