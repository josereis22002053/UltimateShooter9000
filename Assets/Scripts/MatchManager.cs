using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using System;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using System.Linq;
using UnityEngine.SceneManagement;
using System.Net.Sockets;
using System.Net;
using System.Text;

public class MatchManager : NetworkBehaviour
{
    public delegate void GameStarting();
    public delegate void GameStarted();
    
    public event GameStarting   gameStarting;
    public event GameStarted    gameStarted;
    public event Action<Team>   GameEnded;

    [SerializeField] private int _blueTeamKills = 0;
    [SerializeField] private int _greenTeamKills = 0;

    private GameState                       _currentGameState;
    private Dictionary<ulong, PlayerInfo>   _connectedClients;
    private List<PlayerInfo>                _connectedPlayersList;
    private DatabaseManager                 _databaseManager;
    private Socket                          _socket;
    private ushort                          _port;
    private uint                            _requiredKillsToWin;
    private ushort                          _eloUpdateValue;
    private int                             _minEloAllowed;
    private int                             _connectedPlayers;

    private void Awake()
    {
        _currentGameState = GameState.WaitingForPlayers;
        gameStarting += StartGame;
        gameStarted += CreatePlayerDictionary;
        GameEnded += UpdatePlayerDatabase;
        _requiredKillsToWin = ApplicationSettings.Instance.Settings.GameSettings.RequiredKillsToWin;
        _eloUpdateValue = ApplicationSettings.Instance.Settings.GameSettings.EloUpdateValue;
        _minEloAllowed = ApplicationSettings.Instance.Settings.GameSettings.MinEloAllowed;
    }

    private void Start()
    {
        var networkSetup = FindObjectOfType<NetworkSetup>();
        networkSetup.networkSetupDone += ConnectToMatchMakingServer;

        var networkManager = FindObjectOfType<NetworkManager>();
        networkManager.OnClientDisconnectCallback += CheckIfPlayerQuitDuringMatch;
    }

    private void ConnectToMatchMakingServer()
    {
        if (IsServer)
        {   
            string[] args = System.Environment.GetCommandLineArgs();
            int matchMakingPort = 0;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--gameServer")
                {
                    _port = ushort.Parse(args[i + 1]);
                    matchMakingPort = int.Parse(args[i + 2]);
                    break;
                }
            }

            try
            {
                // Convert the server hostname/IP to an IP address 
                // For maximum compatibility, I'm using IPv4, so I make sure I'm using
                // a IPv4 interface
                IPHostEntry ipHost = Dns.GetHostEntry(ApplicationSettings.Instance.Settings.MatchMakingSettings.MatchMakingServerIp);
                IPAddress   ipAddress = null;
                for (int i = 0; i < ipHost.AddressList.Length; i++)
                {
                    if (ipHost.AddressList[i].AddressFamily == AddressFamily.InterNetwork)
                    {
                        ipAddress = ipHost.AddressList[i];
                    }
                }
                // Create and connect the socket to the remote endpoint (TCP)
                IPEndPoint remoteEndPoint = new IPEndPoint(ipAddress, matchMakingPort);

                // Create a Socket that will use TCP protocol
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Connect to endpoint
                _socket.Connect(remoteEndPoint);

                Debug.Log("Server is ready to communicate with matchmaking");

                // Convert to bytes
                byte[] msgToSend = Encoding.UTF32.GetBytes($"READY {_port}");

                try
                {
                    // Send message to server
                    _socket.Send(msgToSend);
                }
                catch (SocketException e)
                {
                    Debug.Log($"SocketException: {e}");
                }
            }
            catch (SocketException e)
            {
                Debug.Log($"SocketException: {e}");
            }
        }
    }

    private void OnApplicationQuit()
    {
        Debug.Log("Match server quit");

        // Convert to bytes
        byte[] msgToSend = Encoding.UTF32.GetBytes($"SHUTDOWN {_port}");

        try
        {
            // Send message to server
            _socket.Send(msgToSend);
        }
        catch (SocketException e)
        {
            Debug.Log($"SocketException: {e}");
        }

        _socket.Disconnect(false);
        _socket.Close();
    }

    public void PlayerPrefabInstantiated()
    {
        if (!IsServer) return;
        if (_currentGameState != GameState.WaitingForPlayers) return;

        Debug.Log("Player prefab was instantiated");
        _connectedPlayers++;

        if (_connectedPlayers >= 2)
        {
            _databaseManager = FindObjectOfType<DatabaseManager>();
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

    private void CreatePlayerDictionary()
    {
        if (!IsServer) return;

        var players = FindObjectsOfType<Player>();
        Debug.Log($"Found {players.Length} players when creating dictionary");
        _connectedClients = new Dictionary<ulong, PlayerInfo>();
        
        foreach (Player player in players)
        {
            Debug.Log($"Adding {player.UserName} to dictionary");

            _connectedClients.Add(player.PlayerId, new PlayerInfo(player.UserName,
                                                                  player.Elo,
                                                                  player.Team));
        }

        Debug.Log($"Dictionary has {_connectedClients.Count} after creation");
        UpdatePlayerInfoUI();
    }

    private void UpdatePlayerInfoUI()
    {
        string bluePlayerName = _connectedClients.FirstOrDefault(c => c.Value.Team == Team.Blue).Value.UserName;
        string greenPlayerName = _connectedClients.FirstOrDefault(c => c.Value.Team == Team.Green).Value.UserName;

        FindObjectOfType<CanvasManager>().UpdatePlayerInfoInGameUI(bluePlayerName, greenPlayerName);
        SyncPlayersInfoUIClientRpc(bluePlayerName, greenPlayerName);
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

    private void UpdatePlayerDatabase(Team winner)
    {
        if (!IsServer) return;

        foreach(var player in _connectedClients.Values)
        {
            Debug.Log($"Getting {player.UserName} info from database");
            (string name, int elo, int kills, int deaths) playerStats = _databaseManager.GetPlayerInfo(player.UserName);
            
            int newKills = 0;
            int newDeaths = 0;
            int newElo = 0;

            if (player.Team == Team.Blue)
            {
                newKills = playerStats.kills + _blueTeamKills;
                newDeaths = playerStats.deaths + _greenTeamKills;
            }
            else if (player.Team == Team.Green)
            {
                newKills = playerStats.kills + _greenTeamKills;
                newDeaths = playerStats.deaths + _blueTeamKills;
            }

            if (player.Team == winner)
                newElo = playerStats.elo + _eloUpdateValue;
            else
                newElo = Mathf.Max(_minEloAllowed, playerStats.elo - _eloUpdateValue);

            _databaseManager.UpdatePlayerStats(player.UserName, newElo, newKills, newDeaths);
        }
    }

    private void CheckIfPlayerQuitDuringMatch(ulong clientId)
    {
        if (!IsServer) return;

        if (_currentGameState != GameState.Finished)
        {
            var playerWhoQuit = _connectedClients[clientId];
            Debug.Log($"{playerWhoQuit.UserName} quit during match");
            Team winner = playerWhoQuit.Team == Team.Blue ? Team.Green : Team.Blue;

            Debug.Log($"Game ended. Winner is {winner}");
            _currentGameState = GameState.Finished;
            OnGameEnded(winner);
            EndGameClientRpc(winner);
        }
        
        _connectedPlayers--;
        if (_connectedPlayers <= 0) Application.Quit();
    }


    // Called by "Back to lobby" button on client
    public void BackToLobby()
    {
        StartCoroutine(BackToLobbyCR());
    }

    private IEnumerator BackToLobbyCR()
    {
        var networkManager = FindObjectOfType<NetworkManager>();
        networkManager.Shutdown();

        yield return null;
        Destroy(networkManager.gameObject);

        yield return null;

        var connectionInfoObj = FindObjectOfType<ConnectionInfo>().gameObject;
        Destroy(connectionInfoObj);

        yield return null;

        SceneManager.LoadScene(0);
    }

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

    [ClientRpc]
    private void SyncPlayersInfoUIClientRpc(string blueUserName, string greenUsername)
    {
        FindObjectOfType<CanvasManager>().UpdatePlayerInfoInGameUI(blueUserName, greenUsername);
    }

    public struct PlayerInfo
    {
        public string UserName;
        public int Elo;
        public Team Team;

        public PlayerInfo(string userName, int elo, Team team)
        {
            UserName = userName;
            Elo = elo;
            Team = team;
        }
    }
}

