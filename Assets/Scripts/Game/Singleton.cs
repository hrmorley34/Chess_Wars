using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public static class Singleton
{
    public static GameObject GameController { get => GameObject.FindGameObjectWithTag("GameController"); }
    public static Game Game { get => GameController.GetComponent<Game>(); }

    public static GameObject NetworkManagerObject { get => GameObject.FindGameObjectWithTag("NetworkManager"); }
    public static NetworkManager NetworkManager { get => NetworkManagerObject.GetComponent<NetworkManager>(); }

    public static NetworkObject LocalPlayerObject { get => NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject(); }
    public static Player LocalPlayer { get => LocalPlayerObject.GetComponent<Player>(); }

    public static NetworkObject GetPlayerObject(ulong id)
    {
        return NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(id);
    }
    public static NetworkObject GetPlayerObject(ServerRpcParams serverRpcParams)
        => GetPlayerObject(serverRpcParams.Receive.SenderClientId);
    public static Player GetPlayer(ulong id) => GetPlayerObject(id).GetComponent<Player>();
    public static Player GetPlayer(ServerRpcParams serverRpcParams)
        => GetPlayer(serverRpcParams.Receive.SenderClientId);
}
