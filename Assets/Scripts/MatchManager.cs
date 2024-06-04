using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using System;
using System.Runtime.CompilerServices;

public class MatchManager : NetworkBehaviour
{
    private int connectedPlayers;

    public delegate void GameStarting();
    public delegate void GameStarted();
    
    public event GameStarting   gameStarting;
    public event GameStarted    gameStarted;
    public event Action<Team>   GameEnded;

    //public static GameState CurrentGameSate = GameState.WaitingForPlayers;

    [SerializeField] private int _blueTeamKills = 0;
    [SerializeField] private int _greenTeamKills = 0;
    [SerializeField] private int _requiredKillsToWin = 5;

    private GameState _currentGameState;


    private void Awake()
    {
        _currentGameState = GameState.WaitingForPlayers;
        gameStarting += StartGame;
    }

    public void PlayerPrefabInstantiated()
    {
        if (!IsServer) return;
        if (_currentGameState != GameState.WaitingForPlayers) return;

        Debug.Log("Player prefab was instantiated");
        connectedPlayers++;

        if (connectedPlayers >= 2)
        {
            OnGameStarting();
            StartingGameClientRpc();
        }
    }

    private void OnGameStarting()
    {
        gameStarting?.Invoke();
    }

    private void OnGameStarted()
    {
        gameStarted?.Invoke();
    }

    private void OnGameEnded(Team winner)
    {
        GameEnded?.Invoke(winner);
    }

    private void StartGame()
    {
        StartCoroutine(StartGameCR());
    }

    private IEnumerator StartGameCR()
    {
        int timer = 3;
        _currentGameState = GameState.Starting;
        SubscribeToPlayers();

        Debug.Log($"Game starting in {timer}");
        yield return new WaitForSeconds(1);

        timer--;
        Debug.Log($"Game starting in {timer}");
        yield return new WaitForSeconds(1);

        timer--;
        Debug.Log($"Game starting in {timer}");
        yield return new WaitForSeconds(1);

        Debug.Log("Game started!");
        OnGameStarted();
        StartGameClientRpc();
    }

    private void SubscribeToPlayers()
    {
        var players = FindObjectsOfType<Player>();
        Debug.Log($"MatchManager found {players.Length} players when subscribing");
        foreach (var p in players)
        {
            p.PlayerDied += UpdatePlayerKills;
        }
    }

    private void UpdatePlayerKills(Team team, int kills)
    {
        if (!IsServer) return;
        
        if (team == Team.Blue)
            _blueTeamKills++;
        else if (team == Team.Green)
            _greenTeamKills++;
        
        if (kills >= _requiredKillsToWin)
        {
            Debug.Log($"Game ended. Winner is {team}");
            _currentGameState = GameState.Finished;
            OnGameEnded(team);
            EndGameClientRpc(team);
        }
    }

    // private void UpdateCurrentGameState(GameState newGameState)
    // {
    //     CurrentGameSate = newGameState;
    //     UpdateCurrentGameStateClientRpc(newGameState);
    // }

    [ClientRpc]
    private void StartingGameClientRpc()
    {
        OnGameStarting();
    }

    [ClientRpc]
    private void StartGameClientRpc()
    {
        OnGameStarted();
    }

    [ClientRpc]
    private void EndGameClientRpc(Team winner)
    {
        OnGameEnded(winner);
    }

    //[ClientRpc]
    // private void UpdateCurrentGameStateClientRpc(GameState newGameState)
    // {
    //     CurrentGameSate = newGameState;
    // }
}
