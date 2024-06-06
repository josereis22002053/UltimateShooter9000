using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using TMPro;
using UnityEngine.UIElements;
using UnityEditor.Rendering;
using System.Linq;
using Unity.Netcode;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using UnityEngine.SceneManagement;

public class Matchmaking : NetworkBehaviour
{
    [SerializeField] private Transform              _connectedClientsPanel;
    [SerializeField] private Transform              _clientsInQueuePanel;
    [SerializeField] private Transform              _logsPanel;
    [SerializeField] private ConnectedClientInfo    _clientInfoPrefab;
    [SerializeField] private ConnectionInfo         _connectionInfoPrefab;
    [SerializeField] private TextMeshProUGUI        _logEntryPrefab;

    [SerializeField] private string newEntryTest = "NewEntry";
    
    [SerializeField] private List<ConnectedClientInfo> _connectedClients = new List<ConnectedClientInfo>();

    CanvasManager _canvasManager;
    private List<ConnectedClientInfo> _playersInQueue;

    private void Awake()
    {
        //_connectedClients = new List<TextMeshProUGUI>();
        _playersInQueue = new List<ConnectedClientInfo>();
    }

    private void Start()
    {
        _canvasManager = FindObjectOfType<CanvasManager>();
        var networkSetup = FindObjectOfType<NetworkSetup>();
        networkSetup.networkSetupDone += Initialize;
    }

    private void Update()
    {
        //if (Input.GetKeyDown(KeyCode.KeypadDivide)) AddClientToConnectedClients(newEntryTest);

        //if (Input.GetKeyDown(KeyCode.Keypad0)) RemoveClientFromConnectedClients(newEntryTest);
        if (_playersInQueue.Count > 0)
        {
            foreach(var player in _playersInQueue)
            {
                player.TimeSinceLastGapUpdate += Time.deltaTime;
                if (player.TimeSinceLastGapUpdate >= 5.0f)
                {
                    player.EloGapMatching += 50;
                    player.TimeSinceLastGapUpdate = 0.0f;

                    var inQueueEntries = _clientsInQueuePanel.GetComponentsInChildren<TextMeshProUGUI>();

                    var playerEntry = inQueueEntries.FirstOrDefault(e => e.text.Contains(player.UserName));
                    playerEntry.text = $"{player.UserName} | {player.Elo} | {player.EloGapMatching}";
                }
            }

            if (_playersInQueue.Count >= 2)
            {
                foreach(var player in _playersInQueue) CheckForCompatibleOpponent(player);

                if (_playersInQueue.Any(p => p.FoundMatch))
                {
                    foreach(var player in _playersInQueue.ToList())
                    {
                        if (player.FoundMatch) RemovePlayerFromQueue(player.UserName);
                    }
                }
            }
        }
    }

    private void Initialize()
    {
        //_canvasManager.DisplayLoginScreen(!NetworkManager.Singleton.IsServer);

        if (NetworkManager.Singleton.IsServer)
            NetworkManager.Singleton.OnClientDisconnectCallback+= RemoveClientFromConnectedClients;

    }

    public void AddClientToConnectedClients(string userName, string password, int elo, ulong clientId)
    {
        //_connectedClients.Add()

        ConnectedClientInfo newClientInfo = Instantiate(_clientInfoPrefab);
        newClientInfo.UserName = userName;
        newClientInfo.Password = password;
        newClientInfo.Elo = elo;
        newClientInfo.EloGapMatching = 50;
        newClientInfo.FoundMatch = false;
        newClientInfo.ClientID = clientId;
        _connectedClients.Add(newClientInfo);

        Debug.Log($"Added {newClientInfo.UserName} to connected clients");

        TextMeshProUGUI newEntry = Instantiate(_logEntryPrefab, _connectedClientsPanel);
        newEntry.text = newClientInfo.UserName;
        

        AddLogEntry(LogEntryType.ClientConnected, newClientInfo.UserName);
        //LogEntry newEntry = new LogEntry();
    }

    private void RemoveClientFromConnectedClients(ulong clientId)
    {
        ConnectedClientInfo client = _connectedClients.First(c => c.ClientID == clientId);
        _connectedClients.Remove(client);

        if (_playersInQueue.Contains(client))
        {
            Debug.Log($"Client {client.UserName} was in queue when disconnected. Removing from queue");
            RemovePlayerFromQueue(client.UserName);
        }

        var entries = _connectedClientsPanel.transform.GetComponentsInChildren<TextMeshProUGUI>();
        foreach (var entry in entries)
        {
            if (entry.text == client.UserName)
            {
                Destroy(entry.gameObject);
                break;
            }
        }

        AddLogEntry(LogEntryType.ClientDisconnected, client.UserName);

        Destroy(client.gameObject);
    }

    private void AddLogEntry(LogEntryType logType, string userName1 = null, string userName2 = null)
    {
        TextMeshProUGUI newEntry = Instantiate(_logEntryPrefab, _logsPanel);
        switch (logType)
        {
            case LogEntryType.ClientConnected:
                newEntry.text = $"{userName1} signed in.";
                //AddClientToConnectedClients(userName1);
                break;
            case LogEntryType.ClientDisconnected:
                newEntry.text = $"{userName1} signed out.";
                break;
            case LogEntryType.ClientJoinedQueue:
                newEntry.text = $"{userName1} joined the queue.";
                break;
            case LogEntryType.ClientLeftQueue:
                newEntry.text = $"{userName1} left the queue.";
                break;
            case LogEntryType.MatchCreated:
                newEntry.text = $"Launched match for {userName1} and {userName2}";
                break;
            default:
                newEntry.text = $"Unknown log type. ({logType})";
                break;
        }
    }

    // Called by "Find match button" on client
    public void AddPlayerToQueue()
    {
        AddPlayerToQueueServerRpc();
    }

    private void RemovePlayerFromQueue(string userName)
    {
        var inQueueEntries = _clientsInQueuePanel.GetComponentsInChildren<TextMeshProUGUI>();
        var playerEntry = inQueueEntries.FirstOrDefault(e => e.text.Contains(userName));
        Destroy(playerEntry.gameObject);

        var playerToRemove = _playersInQueue.FirstOrDefault(p => p.UserName == userName);
        _playersInQueue.Remove(playerToRemove);

        AddLogEntry(LogEntryType.ClientLeftQueue, userName);
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddPlayerToQueueServerRpc(ServerRpcParams serverRpcParams = default)
    {
        
        var clientId = serverRpcParams.Receive.SenderClientId;

        if (_playersInQueue.Any(p => p.ClientID == clientId)) return;

        Debug.Log($"Received server rpc from client {clientId}");

        

        if (_playersInQueue.Count > 0)
        {
    // //         AddLogEntry(LogEntryType.MatchCreated,
    // //                     _playersInQueue[0].UserName,
    // //                     _connectedClients.First(c => c.ClientID == clientId).UserName);
            
    // //         var clientRpcParams = new ClientRpcParams
    // //         {
    // //             Send = new ClientRpcSendParams
    // //             {
    // //                 TargetClientIds = new ulong[] { clientId,  _playersInQueue[0].ClientID}
    // //             }
    // //         };


    // //         RemovePlayerFromQueue(_playersInQueue[0].UserName);

    // //         MatchFoundClientRpc(clientRpcParams);


    // //         // Launch server
    // //         Run("Builds\\UltimateShooter9000.exe", "--gameServer 7778");
    // //         //Run("UltimateShooter9000.exe", "--gameServer 7778");
            
            bool foundOpponent = false;

            var playerJoiningQueue = _connectedClients.FirstOrDefault(c => c.ClientID == clientId);
            foreach (var player in _playersInQueue)
            {
                if (Mathf.Abs(playerJoiningQueue.Elo - player.Elo) <= player.EloGapMatching)
                {
                    Debug.Log($@"Matching {playerJoiningQueue.UserName} with {player.UserName}. 
                              Elo gap is {Mathf.Abs(playerJoiningQueue.Elo - player.Elo)}");

                    RemovePlayerFromQueue(player.UserName);
                    foundOpponent = true;
                    break;
                }
            }
            
            if (!foundOpponent)
            {
                Debug.Log("Couldn't find opponent with ideal elo gap");

                var client = _connectedClients.First(c => c.ClientID == clientId);
                client.TimeSinceLastGapUpdate = 0.0f;
                _playersInQueue.Add(client);

                // Add to Clients in queue panel
                TextMeshProUGUI newEntry = Instantiate(_logEntryPrefab, _clientsInQueuePanel);
                newEntry.text = $"{client.UserName} | {client.Elo} | {client.EloGapMatching}";

                // Add new entry to logs
                AddLogEntry(LogEntryType.ClientJoinedQueue, client.UserName);
            }
        }
        else
        {
            Debug.Log("No players were in queue");

            var client = _connectedClients.First(c => c.ClientID == clientId);
            client.TimeSinceLastGapUpdate = 0.0f;
            _playersInQueue.Add(client);

            // Add to Clients in queue panel
            TextMeshProUGUI newEntry = Instantiate(_logEntryPrefab, _clientsInQueuePanel);
            newEntry.text = $"{client.UserName} | {client.Elo} | {client.EloGapMatching}";

            // Add new entry to logs
            AddLogEntry(LogEntryType.ClientJoinedQueue, client.UserName);
        }
    }

    private void CheckForCompatibleOpponent(ConnectedClientInfo player)
    {
        // foreach (var player in _playersInQueue)
        // {
        //     if (_playersInQueue.Any(p => MathF.Abs(player.Elo - p.Elo) <= player.EloGapMatching))
        //     {
        //         var opponent = _playersInQueue.First(p => MathF.Abs(player.Elo - p.Elo) <= player.EloGapMatching);
        //         RemovePlayerFromQueue(player.UserName);
        //         RemovePlayerFromQueue(opponent.UserName);
        //     }
        // }

        var availableOpponents = _playersInQueue.Where(p => !p.FoundMatch && p != player);
        
        if (availableOpponents.Any(o => MathF.Abs(player.Elo - o.Elo) <= player.EloGapMatching))
        {
            var opponent = availableOpponents.First(p => MathF.Abs(player.Elo - p.Elo) <= player.EloGapMatching);

            player.FoundMatch = true;
            opponent.FoundMatch = true;

            Debug.Log($@"Matching {player.UserName} with {opponent.UserName}. 
                        Elo gap is {Mathf.Abs(player.Elo - opponent.Elo)}");
        }
    }

    [ClientRpc]
    private void MatchFoundClientRpc(ClientRpcParams clientRpcParams = default)
    {
        Debug.Log("Match found!");

        ConnectionInfo connectionInfo = Instantiate(_connectionInfoPrefab);
        connectionInfo.ConnectionPort = 7778;

        DontDestroyOnLoad(connectionInfo.gameObject);

        
        NetworkManager.Singleton.Shutdown();
        NetworkManager networkManager = FindObjectOfType<NetworkManager>();
        Destroy(networkManager.gameObject);

        SceneManager.LoadScene(1);
    }

    private static void Run(string path, string args)
    {
        // Start a new process
        Process process = new Process();
        // Configure the process using the StartInfo properties
        process.StartInfo.FileName = path;
        process.StartInfo.Arguments = args;
        process.StartInfo.WindowStyle = ProcessWindowStyle.Normal; // Choose the window style: Hidden, Minimized, Maximized, Normal
        process.StartInfo.RedirectStandardOutput = false; // Set to true to redirect the output (so you can read it in Unity)
        process.StartInfo.UseShellExecute = true; // Set to false if you want to redirect the output
        // Run the process
        process.Start();
    }
}
