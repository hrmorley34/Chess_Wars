using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[Flags]
public enum Permissions : byte
{
    None = 0,
    ViewHealth = 1,
    Move = 2,
    FullMove = Move,
    Attack = 4,
    Heal = 8,
    FullAttack = Attack | Heal,
    FullInteract = ViewHealth | FullMove | FullAttack,
};

public class Player : MonoBehaviour
{
    Dictionary<PlayerSide, Permissions> Teams;

    public void SetPermissions(PlayerSide team, Permissions permissions)
    {
        Teams[team] = permissions;
    }

    public void ClearPermissions(PlayerSide team)
    {
        Teams.Remove(team);
    }

    public Permissions GetPermissions(PlayerSide team)
    {
        return Teams.GetValueOrDefault(team, Permissions.None);
    }

    public bool HasPermissions(PlayerSide team, Permissions perms)
    {
        if (perms == Permissions.None) return GetPermissions(team) == Permissions.None;
        return (GetPermissions(team) & perms) == perms;
    }

    public Player()
    {
        Teams = new();
    }
}
