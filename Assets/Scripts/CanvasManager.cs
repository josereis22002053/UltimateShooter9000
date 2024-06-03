using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Collections;
using Unity.VisualScripting;

public class CanvasManager : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI _blueTeamScore;
    [SerializeField] private TextMeshProUGUI _greenTeamScore;

    private bool gameStarted = false;

    public void UpdateScoreUI(Team team, int newScore)
    {
        TextMeshProUGUI scoreToUpdate = team == Team.Blue ? _blueTeamScore : _greenTeamScore;

        Debug.Log($"Updating {team}'s score");
        scoreToUpdate.text = newScore.ToString();
    }

    private void Start()
    {
        MatchManager matchManager = FindObjectOfType<MatchManager>();
        matchManager.gameStarted += SubscribeToPlayers;
    }

    // private void OnEnable()
    // {
    //     if (NetworkManager.Singleton.IsServer) return;

    //     var players = FindObjectsOfType<Player>();
    //     foreach (var p in players)
    //     {
    //         if (p.IsLocalPlayer)
    //         {
    //             p.PlayerDied += UpdateScoreUI;
    //             break;
    //         }
    //     }
    // }

    // private void OnDisable()
    // {
    //     if (NetworkManager.Singleton.IsServer) return;

    //     var players = FindObjectsOfType<Player>();
    //     foreach (var p in players)
    //     {
    //         if (p.IsLocalPlayer)
    //         {
    //             p.PlayerDied -= UpdateScoreUI;
    //             break;
    //         }
    //     }
    // }

    // private void Update()
    // {
    //     if (NetworkManager.Singleton.IsServer && !gameStarted)
    //     {
    //         if (NetworkManager.Singleton.ConnectedClients.Count >= 2)
    //         {
    //             gameStarted = true;
    //             // Debug.Log("Subscribing to players on server!");
    //             // gameStarted = true;
    //             // var players = FindObjectsOfType<Player>();
    //             // foreach (var p in players)
    //             // {
    //             //     p.PlayerDied += UpdateScoreUI;
    //             // }
    //             // SubscribeToPlayersClientRpc();
    //             StartCoroutine(SubscribeToPlayers());
    //         }
    //     }   
    // }

    private void SubscribeToPlayers()
    {
        //Debug.Log("Subscribing to players on server!");
        //gameStarted = true;
        var players = FindObjectsOfType<Player>();
        Debug.Log($"CanvasManager found {players.Length} players when subscribing");
        foreach (var p in players)
        {
            p.PlayerDied += UpdateScoreUI;
        }
        //SubscribeToPlayersClientRpc();
    }

    [ClientRpc]
    private void SubscribeToPlayersClientRpc()
    {
        //Debug.Log("Subscribing to players on client!");
        var players = FindObjectsOfType<Player>();
        Debug.Log($"Found {players.Length} players when subscribing");
        foreach (var p in players)
        {
            Debug.Log($"Subscribing to {p.Team} player");
            p.PlayerDied += UpdateScoreUI;
        }
    }
}
