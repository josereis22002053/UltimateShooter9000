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
    }

    private void Initialize()
    {
        _canvasManager.DisplayLoginScreen(!NetworkManager.Singleton.IsServer);

        if (NetworkManager.Singleton.IsServer)
            NetworkManager.Singleton.OnClientDisconnectCallback+= RemoveClientFromConnectedClients;

    }

    public void AddClientToConnectedClients(string userName, ulong clientId)
    {
        //_connectedClients.Add()

        ConnectedClientInfo newClientInfo = Instantiate(_clientInfoPrefab);
        newClientInfo.UserName = userName;
        newClientInfo.ClientID = clientId;
        _connectedClients.Add(newClientInfo);

        TextMeshProUGUI newEntry = Instantiate(_logEntryPrefab, _connectedClientsPanel);
        newEntry.text = newClientInfo.UserName;
        

        AddLogEntry(LogEntryType.ClientConnected, newClientInfo.UserName);
        //LogEntry newEntry = new LogEntry();
    }

    private void RemoveClientFromConnectedClients(ulong clientId)
    {
        ConnectedClientInfo client = _connectedClients.First(c => c.ClientID == clientId);
        _connectedClients.Remove(client);

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
            default:
                newEntry.text = $"Unknown log type. ({logType})";
                break;
        }
    }

    public void AddPlayerToQueue()
    {
        AddPlayerToQueueServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddPlayerToQueueServerRpc(ServerRpcParams serverRpcParams = default)
    {
        
        var clientId = serverRpcParams.Receive.SenderClientId;

        if (_playersInQueue.Any(p => p.ClientID == clientId)) return;

        Debug.Log($"Received server rpc from client {clientId}");

        

        if (_playersInQueue.Count > 0)
        {
            AddLogEntry(LogEntryType.MatchCreated,
                        _playersInQueue[0].UserName,
                        _connectedClients.First(c => c.ClientID == clientId).UserName);
            
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId,  _playersInQueue[0].ClientID}
                }
            };

            MatchFoundClientRpc(clientRpcParams);
            Run("UltimateShooter9000.exe", "--gameServer 7778");
        }
        else
        {
            var client = _connectedClients.First(c => c.ClientID == clientId);
            _playersInQueue.Add(client);
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
