using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public readonly struct BoardCoords : INetworkSerializeByMemcpy
{
    public BoardCoords(int x, int y)
    {
        X = x;
        Y = y;
    }

    public readonly int X;
    public readonly int Y;
}

public readonly struct ChessmanActionDistance : INetworkSerializeByMemcpy
{
    public ChessmanActionDistance(int max, bool lineOfSight = false)
    {
        Minimum = 1;
        Maximum = max;
        LineOfSight = lineOfSight;
    }

    public ChessmanActionDistance(int min, int max, bool lineOfSight = false)
    {
        Minimum = min;
        Maximum = max;
        LineOfSight = lineOfSight;
    }

    /// <summary>
    /// The inclusive minimum distance
    /// </summary>
    public readonly int Minimum;
    /// <summary>
    /// The inclusive maximum distance
    /// </summary>
    public readonly int Maximum;
    public readonly bool LineOfSight;

    public static readonly ChessmanActionDistance Disabled = new(-1, -1);

    public bool Active() => Minimum >= 0 && Maximum >= 0;
}

public class Chessman : NetworkBehaviour
{
    //References to objects in our Unity Scene
    public GameObject movePlate;
    public GameObject healthBar;

    //Position for this Chesspiece on the Board
    //The correct position will be set later
    private int xBoard { get => posBoard.X; }
    private int yBoard { get => posBoard.Y; }
    private BoardCoords posBoard { get => net_posBoard.Value; set => net_posBoard.Value = value; }
    private NetworkVariable<BoardCoords> net_posBoard = new(new BoardCoords(-1, -1));

    // Variable representing the speed of a unit
    private ChessmanActionDistance move { get => new ChessmanActionDistance(1, speed); }
    private int speed { get => net_speed.Value; set => net_speed.Value = value; }
    private NetworkVariable<int> net_speed = new(0);
    // Variable representing the kill range of a unit
    private ChessmanActionDistance attack { get => net_attack.Value; set => net_attack.Value = value; }
    private NetworkVariable<ChessmanActionDistance> net_attack = new(ChessmanActionDistance.Disabled);
    // Variable representing health
    private int health { get => net_health.Value; set => net_health.Value = value; }
    private NetworkVariable<int> net_health = new();
    private int maxHealth { get => net_maxHealth.Value; set => net_maxHealth.Value = value; }
    private NetworkVariable<int> net_maxHealth = new();
    // Variable representing attack damage
    private int baseDamage { get => net_baseDamage.Value; set => net_baseDamage.Value = value; }
    private NetworkVariable<int> net_baseDamage = new();
    // Variable representing heal distance
    private ChessmanActionDistance heal { get => net_heal.Value; set => net_heal.Value = value; }
    private NetworkVariable<ChessmanActionDistance> net_heal = new(ChessmanActionDistance.Disabled);
    // Variable representing heal cooldown in turns
    private int healCooldown { get => net_healCooldown.Value; set => net_healCooldown.Value = value; }
    private NetworkVariable<int> net_healCooldown = new(0);
    private int maxHealCooldown { get => net_maxHealCooldown.Value; set => net_maxHealCooldown.Value = value; }
    private NetworkVariable<int> net_maxHealCooldown = new(2);

    //Variable for keeping track of the player it belongs to "black" or "white"
    private PlayerSide player { get => (PlayerSide)net_player.Value; set => net_player.Value = (byte)value; }
    private NetworkVariable<byte> net_player = new();

    //References to all the possible Sprites that this Chesspiece could be
    public Sprite black_main_castle, black_cavalry, black_archer, black_mage, black_castle, black_foot_soldier;
    public Sprite white_bottom_main_castle, white_top_main_castle, white_cavalry, white_archer, white_mage, white_castle, white_foot_soldier;

    public void Activate()
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogError($"Call to {nameof(Activate)} from non-server");
            return;
        }

        string[] nameParts = this.name.Split('_', 2);
        string nameSide = nameParts[0], nameType = nameParts[1];

        //Get the game controller
        GameObject controller = Singleton.GameController;

        net_posBoard.OnValueChanged = UpdateCoords;

        switch (nameSide)
        {
            case "black": player = PlayerSide.black; break;
            case "white": player = PlayerSide.white; break;
            default: Debug.LogError($"Unknown nameSide: {nameSide}"); break;
        }

        attack = new(1);
        switch (nameType)
        {
            case "castle":
                speed = 1;
                maxHealth = health = 100;
                baseDamage = 10;
                break;
            case "foot_soldier":
                speed = 2;
                maxHealth = health = 60;
                baseDamage = 10;
                break;
            case "archer":
                speed = 2;
                maxHealth = health = 30;
                baseDamage = 30;
                attack = new(1, 4);
                break;
            case "mage":
                speed = 2;
                maxHealth = health = 30;
                baseDamage = 20;
                attack = new(1, true);
                heal = new(1, true);
                maxHealCooldown = 2;
                break;
            case "cavalry":
                speed = 3;
                maxHealth = health = 60;
                baseDamage = 20;
                break;
            case "top_main_castle":
            case "bottom_main_castle":
            case "main_castle":
            // TODO
            default:
                Debug.LogError($"Unknown nameType: {nameType}");
                break;
        }

        Activate_ClientRPC();
    }

    [ClientRpc]
    public void Activate_ClientRPC()
    {
        //Choose correct sprite based on piece's name
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        switch (this.name)
        {
            case "black_main_castle": spriteRenderer.sprite = black_main_castle; break;
            case "black_cavalry": spriteRenderer.sprite = black_cavalry; break;
            case "black_archer": spriteRenderer.sprite = black_archer; break;
            case "black_mage": spriteRenderer.sprite = black_mage; break;
            case "black_castle": spriteRenderer.sprite = black_castle; break;
            case "black_foot_soldier": spriteRenderer.sprite = black_foot_soldier; break;
            case "white_bottom_main_castle": spriteRenderer.sprite = white_bottom_main_castle; break;
            case "white_top_main_castle": spriteRenderer.sprite = white_top_main_castle; break;
            case "white_cavalry": spriteRenderer.sprite = white_cavalry; break;
            case "white_archer": spriteRenderer.sprite = white_archer; break;
            case "white_mage": spriteRenderer.sprite = white_mage; break;
            case "white_castle": spriteRenderer.sprite = white_castle; break;
            case "white_foot_soldier": spriteRenderer.sprite = white_foot_soldier; break;
            default: Debug.LogError($"Unknown name: {this.name}"); break;
        }

        //Take the instantiated location and adjust transform
        SetPositionFromCoords();
    }

    public void UpdateCoords(BoardCoords oldValue, BoardCoords newValue)
    {
        if (!NetworkManager.Singleton.IsClient)
        {
            Debug.LogError($"Call to {nameof(UpdateCoords)} from non-client");
            return;
        }

        //Set the Chesspiece's original location to be empty
        Singleton.Game.SetPositionEmpty(oldValue.X, oldValue.Y);

        SetPositionFromCoords();

        //Update the matrix
        Singleton.Game.SetPosition(gameObject);
    }

    public void SetCoords(int x, int y)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogError($"Call to {nameof(SetCoords)} from non-server");
            return;
        }

        posBoard = new(x, y);
    }

    public void SetPositionFromCoords()
    {
        if (!NetworkManager.Singleton.IsClient)
        {
            Debug.LogError($"Call to {nameof(SetPositionFromCoords)} from non-client");
            return;
        }

        //Get the board value in order to convert to xy coords
        float x = xBoard;
        float y = yBoard;

        //Adjust by variable offset
        x *= BOX_WIDTH;
        y *= BOX_HEIGHT;

        //Add constants (pos 0,0)
        x += BOX_OFFSET_X;
        y += BOX_OFFSET_Y;

        //Set actual unity values
        this.transform.position = new Vector3(x, y, -1.0f);
    }

    public int GetXBoard() => xBoard;
    public int GetYBoard() => yBoard;

    public int GetHealth()
    {
        return health;
    }

    public int GetMaxHealth()
    {
        return maxHealth;
    }

    const float DamageVariableMin = 0.75f;
    const float DamageVariableMax = 1.25f;

    public int GetDamage()
    {
        return (int)(baseDamage * UnityEngine.Random.Range(DamageVariableMin, DamageVariableMax));
    }

    public void DealDamage(int dealtDamage)
    {
        health = health - dealtDamage;
        if (health < 0) health = 0;
    }

    public void HealHealth()
    {
        health = maxHealth;
    }

    public void HealHealth(int healing)
    {
        health = health + healing;
        if (health > maxHealth) health = maxHealth;
    }

    public void ReduceHealCooldown()
    {
        if (healCooldown > 0) healCooldown--;
    }

    public void SetHealCooldown()
    {
        healCooldown = maxHealCooldown;
    }

    public bool CheckHealCooldown()
    {
        return healCooldown > 0;
    }

    private void OnMouseUp()
    {
        if (!NetworkManager.Singleton.IsClient)
        {
            Debug.LogError($"Call to {nameof(OnMouseUp)} from non-client");
            return;
        }

        if (Singleton.Game.IsGameOver()) return;

        bool HasPermission(Permissions perm) => Singleton.LocalPlayer.HasPermissions(player, perm);

        //Remove all moveplates relating to previously selected piece
        Game.ClearTurnElements();

        if (Singleton.Game.GetCurrentPlayer() == player)
        {
            TurnType turnType = Singleton.Game.GetCurrentTurnType();

            if (turnType == TurnType.move && HasPermission(Permissions.Move))
            {
                InitiateMovePlates(PointMovePlate, move);
            }
            else if (turnType == TurnType.attack)
            {
                if (attack.Active() && HasPermission(Permissions.Attack))
                {
                    InitiateMovePlates(PointAttackPlate, attack);
                }

                if (heal.Active() && HasPermission(Permissions.Heal))
                {
                    InitiateMovePlates(PointHealPlate, heal);
                }
            }
        }

        if (HasPermission(Permissions.ViewHealth))
        {
            // Show health bar
            InitiateHealthBar();
        }
    }

    public void InitiateMovePlates(PointAnyPlate plateType, ChessmanActionDistance distance)
    {
        if (!distance.Active()) return;

        // TODO: distance.LineOfSight
        int maxRange = distance.Maximum, minRange = distance.Minimum;
        for (int x = xBoard - maxRange; x <= xBoard + maxRange; x++)
        {
            for (int y = yBoard - maxRange; y <= yBoard + maxRange; y++)
            {
                if ((xBoard - minRange < x) && (x < xBoard + minRange)
                    && (yBoard - minRange < y) && (y < yBoard + minRange))
                {
                    continue;  // skip checking below min distance
                }
                plateType(x, y);
            }
        }
    }

    public bool CanMovePlate(ChessmanActionDistance distance, int x, int y)
    {
        if (!distance.Active()) return false;

        // TODO: distance.LineOfSight
        int maxRange = distance.Maximum, minRange = distance.Minimum;
        return
            xBoard - maxRange <= x && x <= xBoard + maxRange
            && yBoard - maxRange <= y && y <= yBoard + maxRange
            && !((xBoard - minRange < x) && (x < xBoard + minRange)
                && (yBoard - minRange < y) && (y < yBoard + minRange));
    }

    public delegate void PointAnyPlate(int x, int y);

    public void PointMovePlate(int x, int y)
    {
        Game game = Singleton.Game;
        if (game.PositionOnBoard(x, y))
        {
            GameObject cp = game.GetPosition(x, y);

            // Debug.Log($"{x}, {y}, {cp}");

            if (cp == null)
            {
                MovePlateSpawn(x, y, MovePlateType.move);
            }
        }
    }

    public void PointAttackPlate(int x, int y)
    {
        Game game = Singleton.Game;
        if (game.PositionOnBoard(x, y))
        {
            if (game.GetPosition(x, y) != null && game.GetPosition(x, y).GetComponent<Chessman>().player != player)
            {
                MovePlateSpawn(x, y, MovePlateType.attack);
            }
        }
    }

    public void PointHealPlate(int x, int y)
    {
        if (CheckHealCooldown()) return;

        Game game = Singleton.Game;
        if (game.PositionOnBoard(x, y))
        {
            if (game.GetPosition(x, y) == null) return;
            Chessman other = game.GetPosition(x, y).GetComponent<Chessman>();
            // If piece is on our side and has below full health
            if (other.player == player && other.GetHealth() < other.GetMaxHealth())
            {
                MovePlateSpawn(x, y, MovePlateType.heal);
            }
        }
    }

    const int BOX_COUNT_WIDTH = 16;
    const int BOX_COUNT_HEIGHT = 8;
    const float BOX_WIDTH = 0.88f;
    const float BOX_HEIGHT = 0.88f;
    const float BOX_OFFSET_X = -BOX_WIDTH * (BOX_COUNT_WIDTH - 1f) / 2;
    const float BOX_OFFSET_Y = -BOX_HEIGHT * (BOX_COUNT_HEIGHT - 1f) / 2;

    public void MovePlateSpawn(int matrixX, int matrixY, MovePlateType type = MovePlateType.move)
    {
        //Get the board value in order to convert to xy coords
        float x = matrixX;
        float y = matrixY;

        //Adjust by variable offset
        x *= BOX_WIDTH;
        y *= BOX_HEIGHT;

        //Add constants (pos 0,0)
        x += BOX_OFFSET_X;
        y += BOX_OFFSET_Y;

        //Set actual unity values
        GameObject mp = Instantiate(movePlate, new Vector3(x, y, -3.0f), Quaternion.identity);

        MovePlate mpScript = mp.GetComponent<MovePlate>();
        mpScript.type = type;
        mpScript.SetReference(gameObject);
        mpScript.SetCoords(matrixX, matrixY);
    }

    [ServerRpc]
    public void ProcessMovePlate_ServerRPC(MovePlateType type, int destX, int destY, ServerRpcParams serverRpcParams = default)
    {
        var currentPlayer = Singleton.GetPlayer(serverRpcParams);
        bool HasPermission(Permissions perm) => currentPlayer.HasPermissions(Singleton.Game.GetCurrentPlayer(), perm);

        if (Singleton.Game.GetCurrentPlayer() != player)
        {
            Debug.LogError($"Attempted to use wrong player's piece!");
            return;
        }

        TurnType turnType = Singleton.Game.GetCurrentTurnType();

        if (type == MovePlateType.move)
        {
            if (turnType != TurnType.move)
            {
                Debug.LogError($"Attempt to move in wrong turn!");
                return;
            }
            if (!HasPermission(Permissions.Move))
            {
                Debug.LogError($"Attempt to move without permissions!");
                return;
            }
            if (!move.Active())
            {
                Debug.LogError($"Attempt to move disallowed from {GetXBoard()},{GetYBoard()}!");
                return;
            }
            if (!CanMovePlate(move, destX, destY))
            {
                Debug.LogError($"Attempt to move {GetXBoard()},{GetYBoard()} into unreachable space {destX},{destY}!");
                return;
            }
            if (Singleton.Game.GetPosition(destX, destY) != null)
            {
                Debug.LogError($"Attempt to move {GetXBoard()},{GetYBoard()} into non-empty space {destX},{destY}!");
                return;
            }

            //Move reference chess piece to this position
            SetCoords(destX, destY);
        }
        else if (type == MovePlateType.attack)
        {
            if (turnType != TurnType.attack)
            {
                Debug.LogError($"Attempt to attack in wrong turn!");
                return;
            }
            if (!HasPermission(Permissions.Attack))
            {
                Debug.LogError($"Attempt to attack without permissions!");
                return;
            }
            if (!attack.Active())
            {
                Debug.LogError($"Attempt to attack disallowed from {GetXBoard()},{GetYBoard()}!");
                return;
            }
            if (!CanMovePlate(attack, destX, destY))
            {
                Debug.LogError($"Attempt to attack from {GetXBoard()},{GetYBoard()} to unreachable space {destX},{destY}!");
                return;
            }

            GameObject cp = Singleton.Game.GetPosition(destX, destY);
            if (cp == null)
            {
                Debug.LogError($"Attempt to attack from {GetXBoard()},{GetYBoard()} to empty space {destX},{destY}!");
                return;
            }

            Chessman cm = cp.GetComponent<Chessman>();

            cm.DealDamage(GetDamage());
            if (cm.GetHealth() <= 0) Destroy(cp);
        }
        else if (type == MovePlateType.heal)
        {
            if (turnType != TurnType.attack)
            {
                Debug.LogError($"Attempt to heal in wrong turn!");
                return;
            }
            if (!HasPermission(Permissions.Heal))
            {
                Debug.LogError($"Attempt to heal without permissions!");
                return;
            }
            if (!heal.Active())
            {
                Debug.LogError($"Attempt to heal disallowed from {GetXBoard()},{GetYBoard()}!");
                return;
            }
            if (!CanMovePlate(heal, destX, destY))
            {
                Debug.LogError($"Attempt to heal from {GetXBoard()},{GetYBoard()} to unreachable space {destX},{destY}!");
                return;
            }

            GameObject cp = Singleton.Game.GetPosition(destX, destY);
            if (cp == null)
            {
                Debug.LogError($"Attempt to heal from {GetXBoard()},{GetYBoard()} to empty space {destX},{destY}!");
                return;
            }

            Chessman cm = cp.GetComponent<Chessman>();

            cm.HealHealth();
            SetHealCooldown();
        }
        else return;

        //Switch Current Player
        Singleton.Game.NextTurn();

        //Destroy the move plates
        Singleton.Game.ClearTurnElements_ClientRPC();
    }

    public void InitiateHealthBar()
    {
        //Get the board value in order to convert to xy coords
        float x = xBoard;
        float y = yBoard;

        //Adjust by variable offset
        x *= BOX_WIDTH;
        y *= BOX_HEIGHT;

        //Add constants (pos 0,0)
        x += BOX_OFFSET_X;
        y += BOX_OFFSET_Y;

        y += -BOX_HEIGHT / 2 + 0.16f;

        //Set actual unity values
        GameObject hb = Instantiate(healthBar, new Vector3(x, y, -3.0f), Quaternion.identity);

        HealthBar hbScript = hb.GetComponent<HealthBar>();
        hbScript.SetReference(gameObject);
        hbScript.SetCoords(xBoard, yBoard);
        hbScript.SetMaxHealth(maxHealth);
        hbScript.SetHealth(health);
    }
}
