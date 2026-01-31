using UnityEngine;

public static class RoleIdentifier
{
    // 運営側の Quest の識別子（一度実行して Console に出た値をここに貼り直してください）
    private const string AdminDeviceID = "446a0c06c00c623ed1af319fde697a8d83775ed1";

    public static PlayerRole GetRole()
    {
        // Meta 独自の OVRPlugin ではなく、Unity 標準の方法で ID を取得します
        string deviceID = SystemInfo.deviceUniqueIdentifier;

        // 今動かしているデバイスの ID を Console ウィンドウに表示します
        Debug.Log($"Current Device ID: {deviceID}");

        if (deviceID == AdminDeviceID)
        {
            return PlayerRole.Staff;
        }
        else
        {
            return PlayerRole.Guest;
        }
    }
}