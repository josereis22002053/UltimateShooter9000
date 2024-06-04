using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using System;
using System.Runtime.CompilerServices;

public class MatchManager : NetworkBehaviour
{
    private int connectedPlayers;

    public delegate void GameStarted();
    public event GameStarted gameStarted;
    public event Action<Team> GameEnded;

    public GameState CurrentGameSate => _currentGameState;

    [SerializeField] private int _blueTeamKills = 0;
    [SerializeField] private int _greenTeamKills = 0;
    [SerializeField] private int _requiredKillsToWin = 5;

    private GameState _currentGameState;


    private void Awake()
    {
        _currentGameState = GameState.Starting;
        gameStarted += StartGame;
    }

    public void PlayerPrefabInstantiated()
    {
        if (!IsServer) return;
        if (_currentGameState != GameState.Starting) return;

        Debug.Log("Player prefab was instantiated");
        connectedPlayers++;

        if (connectedPlayers >= 2)
        {
            OnGameStarted();
            StarGameClientRpc();
        }
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
        _currentGameState = GameState.InProgress;
        SubscribeToPlayers();
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
            OnGameEnded(team);
            EndGameClientRpc(team);
        }
    }

    [ClientRpc]
    private void StarGameClientRpc()
    {
        OnGameStarted();
    }

    [ClientRpc]
    private void EndGameClientRpc(Team winner)
    {
        OnGameEnded(winner);
    }
}