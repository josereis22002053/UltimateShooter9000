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
using System.Net.Sockets;
using System.Net;
using System.Text;

public class Matchmaking : NetworkBehaviour
{
    [SerializeField] private int                    _matchServerCommunicationPort = 1234;
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

    
    [SerializeField] private List<Socket> _connectedMatchServers = new List<Socket>();

    private Socket _listenerSocket;

    private void Awake()
    {
        //_connectedClients = new List<TextMeshProUGUI>();
        _playersInQueue = new List<ConnectedClientInfo>();

        //_connectedMatchServers= new List<Socket>();
    }

    private void Start()
    {
        _canvasManager = FindObjectOfType<CanvasManager>();
        var networkSetup = FindObjectOfType<NetworkSetup>();
        networkSetup.networkSetupDone += Initialize;
    }

    private void Update()
    {
        if (_listenerSocket != null)
        {
            // Wait for a connection to be made - a new socket is created when that happens
            try
            {
                Socket newClientSocket = _listenerSocket.Accept();

                if (newClientSocket != null)
                {
                    Console.WriteLine("New match server connected!");

                    newClientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    _connectedMatchServers.Add(newClientSocket);
                }
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode == SocketError.WouldBlock)
                {
                }
                else
                {
                    Debug.Log($"SocketException: {e}");
                }
            }
        }

        if (_connectedMatchServers.Count > 0)
        {
            ReceiveMatchServerMessages();
        }

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
        {
            NetworkManager.Singleton.OnClientDisconnectCallback+= RemoveClientFromConnectedClients;
            OpenConnection();
        }
    }

    private void OpenConnection()
    {
        try
        {
            // Create listener socket
            // Prepare an endpoint for the socket, at port 80
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, _matchServerCommunicationPort);

            // Create a Socket that will use TCP protocol
            _listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // A Socket must be associated with an endpoint using the Bind method
            _listenerSocket.Bind(localEndPoint);

            // Specify how many requests a Socket can listen before it gives Server busy response.
            // We will listen 10 request at a time
            _listenerSocket.Listen(10);

            _listenerSocket.Blocking = false;

            Debug.Log("Ready to communicate with match servers");
        }
        catch (SocketException e)
        {
            Debug.Log($"SocketException: {e}");
        }
    }

    private void ReceiveMatchServerMessages()
    {
        foreach (Socket socket in _connectedMatchServers)
        {
            if (socket.Connected)
            {
                try
                {
                    // Prepare space for request
                    string incomingMessage = "";
                    byte[] bytes = new byte[1024];
                    while (true)
                    {
                        // Read a max. of 1024 bytes
                        int bytesRec = socket.Receive(bytes);
                        // Convert that to a string
                        incomingMessage += Encoding.UTF32.GetString(bytes, 0, bytesRec);
                        // If we've read less than 1024 bytes, assume we've already received the
                        // whole message
                        if (bytesRec < bytes.Length)
                        {
                            // No more data to receive, just exit
                            break;
                        }
                    }
                    Debug.Log($"Received message: {incomingMessage}");
                    ProcessMessage(incomingMessage);  
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode == SocketError.WouldBlock)
                    {
                    }
                    else
                    {
                        Debug.Log($"SocketException: {e}");
                    }
                }

                
                // else
                // {
                //     try
                //     {
                //         if (client.Poll(1, SelectMode.SelectRead))
                //         {
                //         }
                //     }
                //     catch (SocketException e)
                //     {
                //         Console.WriteLine(e);

                //         // Close the socket if it's not connected anymore
                //         client.Close();
                //         _clientSockets.Remove(client);
                //     }
                // }
            }
            else
            {
                // Close the socket if it's not connected anymore
                socket.Close();
                _connectedMatchServers.Remove(socket);
                Debug.Log("Socket closed");
            }
        }
    }

    private void ProcessMessage(string message)
    {
        string[] messageReceived = message.Split();

        if (messageReceived[0] == "READY")
        {
            Debug.Log($"MatchServer with port {messageReceived[1]} is READY");
            // CHECK IF SERVER EXISTS
            // SEND CLIENTS TO SERVER
        }
        else if (messageReceived[0] == "SHUTDOWN")
        {
            // REMOVE SERVER FROM OCCUPIED SERVERS
            // ADD IT BACK TO AVAILABLE SERVERS
        }
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
        _canvasManager.UpdateMatchmakingStatus(MatchmakingStatus.LookingForOpponent);
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
            foreach (var opponent in _playersInQueue)
            {
                if (Mathf.Abs(playerJoiningQueue.Elo - opponent.Elo) <= opponent.EloGapMatching)
                {
                    Debug.Log($@"Matching {playerJoiningQueue.UserName} with {opponent.UserName}. 
                              Elo gap is {Mathf.Abs(playerJoiningQueue.Elo - opponent.Elo)}");

                    RemovePlayerFromQueue(opponent.UserName);
                    foundOpponent = true;
                    MatchFound(playerJoiningQueue.ClientID, opponent.ClientID);
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

            MatchFound(player.ClientID, opponent.ClientID);
            Debug.Log($@"Matching {player.UserName} with {opponent.UserName}. 
                        Elo gap is {Mathf.Abs(player.Elo - opponent.Elo)}");
        }
    }

    private void MatchFound(ulong clientId1, ulong clientId2)
    {
        var clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId1, clientId2 }
            }
        };

        UpdateMatchmakingStatusClientRpc(MatchmakingStatus.WaitingForServer, clientRpcParams);
    }

    [ClientRpc]
    private void UpdateMatchmakingStatusClientRpc(MatchmakingStatus status, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log("Executing UpdateMatchmakingStatusClientRpc on client");
        _canvasManager.UpdateMatchmakingStatus(status);
    }

    [ClientRpc]
    private void JoinMatchClientRpc(ushort matchServerPort, ClientRpcParams clientRpcParams = default)
    {
        ConnectionInfo clientConnectionInfo = Instantiate(_connectionInfoPrefab);
        clientConnectionInfo.ConnectionPort = matchServerPort;

        DontDestroyOnLoad(clientConnectionInfo.gameObject);

        NetworkManager.Singleton.Shutdown();
        NetworkManager networkManager = FindObjectOfType<NetworkManager>();
        Destroy(networkManager.gameObject);

        SceneManager.LoadScene(2);
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
