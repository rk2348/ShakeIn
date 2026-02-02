using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject matchingUI; // 「マッチング中...」のテキストなど
    [SerializeField] private GameObject joinButton; // 「入室」ボタン

    void Start()
    {
        // 1. 起動時にサーバー接続
        PhotonNetwork.ConnectUsingSettings();
        matchingUI.SetActive(false);
        joinButton.SetActive(false); // 接続するまでボタンは隠しておく
    }

    public override void OnConnectedToMaster()
    {
        // 2. サーバー接続完了したらボタンを表示
        joinButton.SetActive(true);
    }

    // ボタンのOn Clickから呼び出す
    public void OnClickJoin()
    {
        // 3. 全員同じルーム名（"MainRoom"）で入室
        PhotonNetwork.JoinOrCreateRoom("MainRoom", new RoomOptions { MaxPlayers = 10 }, TypedLobby.Default);

        // UIの切り替え
        joinButton.SetActive(false);
        matchingUI.SetActive(true);
    }

    public override void OnJoinedRoom()
    {
        // 4. 入室成功したらゲームシーンへ移動
        // Build SettingsにGameSceneを追加しておく必要があります
        PhotonNetwork.LoadLevel("GameScene");
    }
}