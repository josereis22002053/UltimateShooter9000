using System.Collections.Generic;
using UnityEngine;
using System;
using TMPro;
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
    [SerializeField] private Transform              _connectedClientsPanel;
    [SerializeField] private Transform              _clientsInQueuePanel;
    [SerializeField] private Transform              _logsPanel;
    [SerializeField] private ConnectedClientInfo    _clientInfoPrefab;
    [SerializeField] private ConnectionInfo         _connectionInfoPrefab;
    [SerializeField] private TextMeshProUGUI        _logEntryPrefab;


    private List<ushort>                        _availablePorts;
    private List<ushort>                        _onGoingMatchPorts;
    private List<Socket>                        _connectedMatchServers;
    private List<ConnectedClientInfo>           _connectedClients;
    private List<ConnectedClientInfo>           _playersInQueue;
    private Dictionary<ushort, (ulong, ulong)>  _matchServersStartingUp;
    private Dictionary<Socket, uint>            _socketDict;
    private Socket                              _listenerSocket;
    private CanvasManager                       _canvasManager;
    private int                                 _matchServerPort;
    private uint                                _initialCompatibleEloGap;
    private uint                                _compatibleEloGapUpdateValue;
    private uint                                _eloGapUpdateInterval;

    private void Awake()
    {
        _availablePorts             = new List<ushort>(ApplicationSettings.Instance.Settings.MatchMakingSettings.MatchServerPorts);
        _onGoingMatchPorts          = new List<ushort>();
        _connectedMatchServers      = new List<Socket>();
        _connectedClients           = new List<ConnectedClientInfo>();
        _playersInQueue             = new List<ConnectedClientInfo>();
        _matchServersStartingUp     = new Dictionary<ushort, (ulong, ulong)>();
        _socketDict                 = new Dictionary<Socket, uint>();
        _matchServerPort            = ApplicationSettings.Instance.Settings.MatchMakingSettings.MatchMakingServerPortMatchServers;
        _initialCompatibleEloGap    = ApplicationSettings.Instance.Settings.MatchMakingSettings.InitialCompatibleEloGap;
        _compatibleEloGapUpdateValue= ApplicationSettings.Instance.Settings.MatchMakingSettings.CompatibleEloGapUpdateValue;
        _eloGapUpdateInterval       = ApplicationSettings.Instance.Settings.MatchMakingSettings.EloGapUpdateInterval;
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
                    _socketDict.Add(newClientSocket, 0);
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
                if (player.TimeSinceLastGapUpdate >= _eloGapUpdateInterval)
                {
                    player.EloGapMatching += _compatibleEloGapUpdateValue;
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
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, _matchServerPort);

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
        foreach (Socket socket in _connectedMatchServers.ToList())
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
                    if (incomingMessage == "")
                    {
                        Debug.Log("Receiving nothing");
                        _socketDict[socket]++;
                        if (_socketDict[socket] >= 500)
                        {
                            Debug.Log("Removing socket");
                            socket.Close();
                            _connectedMatchServers.Remove(socket);
                        }
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
            ushort port = (ushort)int.Parse(messageReceived[1]);
            if (_matchServersStartingUp.ContainsKey(port))
            {
                Debug.Log($"MatchServer with port {port} is READY");
                SendPlayersToMatchServer(port);
                _matchServersStartingUp.Remove(port);
            }
            else
            {
                Debug.Log($"Received {port}. This port isn't on waiting for startup dictionary");
            }
        }
        else if (messageReceived[0] == "SHUTDOWN")
        {
            ushort port = (ushort)int.Parse(messageReceived[1]);
            if (ApplicationSettings.Instance.Settings.MatchMakingSettings.MatchServerPorts.Contains(port) && _onGoingMatchPorts.Contains(port))
            {
                Debug.Log($"MatchServer with port {messageReceived[1]} was SHUTDOWN");
                _onGoingMatchPorts.Remove(port);
                _availablePorts.Add(port);
            }
        }
    }

    public void AddClientToConnectedClients(string userName, string password, int elo, ulong clientId)
    {
        ConnectedClientInfo newClientInfo = Instantiate(_clientInfoPrefab);
        newClientInfo.UserName = userName;
        newClientInfo.Password = password;
        newClientInfo.Elo = elo;
        newClientInfo.EloGapMatching = _initialCompatibleEloGap;
        newClientInfo.FoundMatch = false;
        newClientInfo.ClientID = clientId;
        _connectedClients.Add(newClientInfo);

        Debug.Log($"Added {newClientInfo.UserName} to connected clients");

        TextMeshProUGUI newEntry = Instantiate(_logEntryPrefab, _connectedClientsPanel);
        newEntry.text = newClientInfo.UserName;
        

        AddLogEntry(LogEntryType.ClientConnected, newClientInfo.UserName);
    }

    public bool IsClientConnected(string userName)
    {
        return _connectedClients.Any(c => c.UserName == userName);
    }

    private void RemoveClientFromConnectedClients(ulong clientId)
    {
        ConnectedClientInfo client = _connectedClients.FirstOrDefault(c => c.ClientID == clientId);

        if (client == null)
        {
            Debug.Log("Tried removing client that wasn't yet connected/logged on");
            return;
        }
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

        if (_availablePorts.Count == 0)
        {
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            };

            UpdateMatchmakingStatusClientRpc(MatchmakingStatus.NoAvailableServers, clientRpcParams);
            Debug.Log("No available ports");
            return;
        }

        if (_playersInQueue.Any(p => p.ClientID == clientId)) return;

        Debug.Log($"Received server rpc from client {clientId}");

        
        if (_playersInQueue.Count > 0)
        {   
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
        if (_availablePorts.Count == 0)
        {
            var clientRpcParams2 = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId1, clientId2 }
                }
            };

            UpdateMatchmakingStatusClientRpc(MatchmakingStatus.NoAvailableServers, clientRpcParams2);
            Debug.Log("No available ports");
            return;
        }


        Run("Builds\\UltimateShooter9000.exe", $"--gameServer {_availablePorts[0]} {_matchServerPort}");
        //Run("UltimateShooter9000.exe", $"--gameServer {_availablePorts[0]} {_matchServerPort}");

        _matchServersStartingUp.Add(_availablePorts[0], (clientId1, clientId2));
        _onGoingMatchPorts.Add(_availablePorts[0]);
        _availablePorts.RemoveAt(0);

        var clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId1, clientId2 }
            }
        };

        UpdateMatchmakingStatusClientRpc(MatchmakingStatus.WaitingForServer, clientRpcParams);

        AddLogEntry(LogEntryType.MatchCreated,
                        _connectedClients.First(c => c.ClientID == clientId1).UserName,
                        _connectedClients.First(c => c.ClientID == clientId2).UserName);
    }

    private void SendPlayersToMatchServer(ushort port)
    {
        (ulong client1, ulong client2) clientsToSend = _matchServersStartingUp[port];

        var clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientsToSend.client1, clientsToSend.client2 }
            }
        };

        JoinMatchClientRpc(port, clientRpcParams);
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
