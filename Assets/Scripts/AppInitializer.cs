using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AppInitializer : MonoBehaviour
{
    [SerializeField] private NetworkRunner runnerPrefab;

    [Header("UI Panels")]
    [SerializeField] private GameObject selectionPanel;  // 最初（運営/ファン）
    [SerializeField] private GameObject staffTypePanel; // 運営の中身（管理/アイドル）
    [SerializeField] private GameObject passwordPanel;   // パスワード入力

    [Header("UI Elements")]
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Passwords")]
    [SerializeField] private string staffPassword = "staff"; // 管理・アイドル共通

    private PlayerRole _pendingRole = PlayerRole.None;

    void Start()
    {
        // 全パネルを一旦非表示にしてから初期パネルを表示
        selectionPanel.SetActive(true);
        staffTypePanel.SetActive(false);
        passwordPanel.SetActive(false);
        statusText.text = "役割を選択してください";
    }

    // --- メインメニューの処理 ---

    public void OnSelectStaffGroup()
    {
        selectionPanel.SetActive(false);
        staffTypePanel.SetActive(true);
        statusText.text = "種別を選択してください";
    }

    public void OnSelectGuest()
    {
        _pendingRole = PlayerRole.Guest;
        // ファンはパスワード不要なので直接ゲーム開始
        StartNetworkGame();
    }

    // --- 運営サブメニューの処理 ---

    public void OnSelectAdmin()
    {
        _pendingRole = PlayerRole.Admin;
        ShowPasswordInput();
    }

    public void OnSelectIdol()
    {
        _pendingRole = PlayerRole.Idol;
        ShowPasswordInput();
    }

    private void ShowPasswordInput()
    {
        staffTypePanel.SetActive(false);
        passwordPanel.SetActive(true);
        statusText.text = $"{_pendingRole} のパスワードを入力してください";
    }

    // --- パスワード・接続処理 ---

    public void OnConfirmPassword()
    {
        string input = passwordInputField.text;

        // 管理またはアイドルの時にパスワードをチェック
        if (input == staffPassword)
        {
            RoleIdentifier.SetRole(_pendingRole);
            StartNetworkGame();
        }
        else
        {
            statusText.text = "パスワードが正しくありません";
            passwordInputField.text = "";
        }
    }

    async void StartNetworkGame()
    {
        selectionPanel.SetActive(false);
        staffTypePanel.SetActive(false);
        passwordPanel.SetActive(false);
        statusText.text = "接続中...";

        // 役割を確定（ファンはここでセット）
        RoleIdentifier.SetRole(_pendingRole);

        var runner = Instantiate(runnerPrefab);

        // 管理またはアイドルならHost、ファンならClient
        GameMode mode = (_pendingRole == PlayerRole.Admin || _pendingRole == PlayerRole.Idol)
                        ? GameMode.Host : GameMode.Client;

        var sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

        await runner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = "HandshakeRoom",
            SceneManager = sceneManager
        });

        Debug.Log($"Role confirmed: {_pendingRole} / GameMode: {mode}");
    }
}