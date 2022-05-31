using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SkipButtonBehaviour : MonoBehaviour
{
    public Button skipButton;

    void Start()
    {
        Button btn = skipButton.GetComponent<Button>();
        btn.onClick.AddListener(OnButtonPress);
    }

    public void OnButtonPress()
    {
        Singleton.Game.SkipCurrentTurn_ServerRPC();
    }
}
