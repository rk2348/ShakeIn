using UnityEngine;

public static class RoleIdentifier
{
    private static PlayerRole _selectedRole = PlayerRole.None;

    // UI‚©‚çİ’è‚·‚é‚½‚ß‚Ìƒƒ\ƒbƒh
    public static void SetRole(PlayerRole role)
    {
        _selectedRole = role;
        Debug.Log($"Role set to: {role}");
    }

    public static PlayerRole GetRole()
    {
        return _selectedRole;
    }
}