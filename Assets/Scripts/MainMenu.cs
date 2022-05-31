using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class MainMenu : MonoBehaviour
{
    public string PlayScene;
    public string MultiplayerLobbyScene;

    public void PlaySingleplayer()
    {
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", 7777, "127.0.0.1");
        NetworkManager.Singleton.StartHost();
        Singleton.LocalPlayer.SetPermissions(PlayerSide.white, Permissions.FullInteract);
        Singleton.LocalPlayer.SetPermissions(PlayerSide.black, Permissions.FullInteract);
        NetworkManager.Singleton.SceneManager.LoadScene(PlayScene, LoadSceneMode.Single);
    }

    public void PlayMultiplayer()
    {
        SceneManager.LoadScene(MultiplayerLobbyScene);
    }

    public void ShowOptions()
    {
        // TODO: Add options?
    }

    public void Quit()
    {
        Application.Quit();
    }
}
