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

    // カメラリグのルート（移動の基準）
    private Transform localRigRoot;

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            var rig = GameObject.FindObjectOfType<OVRCameraRig>();
            if (rig != null)
            {
                // カメラリグのルートを取得
                localRigRoot = rig.transform;

                localHeadAnchor = rig.centerEyeAnchor;
                localLeftHandAnchor = rig.leftHandAnchor;
                localRightHandAnchor = rig.rightHandAnchor;
            }

            // 自分の頭のメッシュを非表示にする処理
            var renderers = networkHead.GetComponentsInChildren<MeshRenderer>();
            foreach (var r in renderers) r.enabled = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 入力権限がある場合のみ実行
        if (HasInputAuthority && localRigRoot != null)
        {
            // ★追加: アバター本体（このスクリプトがついている親）をカメラリグの位置・回転に合わせる
            // これにより、NetworkTransformが「移動した座標」を他プレイヤーに送信します
            transform.position = localRigRoot.position;
            transform.rotation = localRigRoot.rotation;

            // 各パーツ（頭・手）の同期
            // ※注意: もしnetworkHeadなどがPlayerの子オブジェクトの場合、
            // 親（transform）が動いた分だけ二重に動いてしまう可能性があります。
            // その場合は、localHeadAnchor.position ではなく localHeadAnchor.localPosition を使うなどの調整が必要ですが、
            // まずはこのコードで「移動」が同期されるか確認してください。
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