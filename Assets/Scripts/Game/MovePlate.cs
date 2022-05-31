using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public enum MovePlateType
{
    move,
    attack,
    heal
}

public class MovePlate : MonoBehaviour
{
    //The Chesspiece that was tapped to create this MovePlate
    GameObject reference = null;

    //Location on the board
    int matrixX;
    int matrixY;

    public MovePlateType type = MovePlateType.move;

    public void Start()
    {
        switch (type)
        {
            case MovePlateType.move:
                // Set to black
                gameObject.GetComponent<SpriteRenderer>().color = new Color(0.0f, 0.0f, 0.0f, 1.0f);
                break;
            case MovePlateType.attack:
                // Set to red
                gameObject.GetComponent<SpriteRenderer>().color = new Color(1.0f, 0.0f, 0.0f, 1.0f);
                break;
            case MovePlateType.heal:
                // Set to purple
                gameObject.GetComponent<SpriteRenderer>().color = new Color(0.7f, 0.2f, 1.0f, 1.0f);
                break;
        }
    }

    public void OnMouseUp()
    {
        GameObject controller = Singleton.GameController;
        Chessman me = reference.GetComponent<Chessman>();

        me.ProcessMovePlate_ServerRPC(type, matrixX, matrixY);

        //Destroy the move plates including self
        Game.ClearTurnElements();
    }

    public void SetCoords(int x, int y)
    {
        matrixX = x;
        matrixY = y;
    }

    public void SetReference(GameObject obj)
    {
        reference = obj;
    }

    public GameObject GetReference()
    {
        return reference;
    }

    static public void DestroyMovePlates()
    {
        //Destroy old MovePlates
        GameObject[] movePlates = GameObject.FindGameObjectsWithTag("MovePlate");
        for (int i = 0; i < movePlates.Length; i++)
        {
            Destroy(movePlates[i]); //Be careful with this function "Destroy" it is asynchronous
        }
    }
}
