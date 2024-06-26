using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Linq;
using System;

public class LoginManager : NetworkBehaviour
{
    [SerializeField] private TMP_InputField         _userNameField;
    [SerializeField] private TMP_InputField         _passwordField;
    [SerializeField] private ConnectedClientInfo    _clientInfoPrefab;

    private CanvasManager   _canvasManager;
    private DatabaseManager _databaseManager;
    private Matchmaking     _matchmakingManager;

    private void Start()
    {
        _canvasManager = FindObjectOfType<CanvasManager>();

        //if (IsServer)
        _databaseManager = FindObjectOfType<DatabaseManager>();
        _matchmakingManager = FindObjectOfType<Matchmaking>();

        var networkSetup = FindObjectOfType<NetworkSetup>();
        networkSetup.networkSetupDone += Initialize;
    }

    private void Initialize()
    {
        _canvasManager.DisplayLoginScreen(!NetworkManager.Singleton.IsServer);
    }

    public void SignUp()
    {
        string userNameInput = _userNameField.text;
        string passwordInput = _passwordField.text;

        Debug.Log($"Username = {userNameInput} | Password = {passwordInput}");

        CreateAccountServerRpc(userNameInput, passwordInput);
    }

    public void Login()
    {
        string userNameInput = _userNameField.text;
        string passwordInput = _passwordField.text;

        LoginServerRpc(userNameInput, passwordInput);
    }

    private void Login(string userName, string password)
    {
        Debug.Log("Logging in");
        LoginServerRpc(userName, password);
    }

    private bool ValidUsername(string username, ulong clientId)
    {
        var clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        };

        if (username.Length < 3 || username.Length > 20)
        {
            DisplayMessageClientRpc(MessageType.UsernameInvalidSize, clientRpcParams);
            return false;
        }
        else if (_databaseManager.PlayerExists(username))
        {
            DisplayMessageClientRpc(MessageType.UsernameAlreadyExists, clientRpcParams);
            return false;
        }
        else
        {
            return true;
        }
    }

    private bool ValidPassword(string password, ulong clientId)
    {
        var clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        };

        if (password.Length < 5 || password.Length > 20)
        {
            DisplayMessageClientRpc(MessageType.PasswordInvalidSize, clientRpcParams);
            return false;
        }
        else if (password.Any(char.IsWhiteSpace))
        {
            DisplayMessageClientRpc(MessageType.PasswordContainsWhitespace, clientRpcParams);
            return false;
        }
        else
        {
            return true;
        }
    }


    [ServerRpc(RequireOwnership = false)]
    private void CreateAccountServerRpc(string userName, string password, ServerRpcParams serverRpcParams = default)
    {
        var clientId = serverRpcParams.Receive.SenderClientId;

        var clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        };

        if (ValidUsername(userName, clientId) && ValidPassword(password, clientId))
        {
            _databaseManager.AddPlayer(userName, password, 500, 0, 0);
            DisplayMessageClientRpc(MessageType.CreateAccountSuccessful, clientRpcParams);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void LoginServerRpc(string userName, string password, ServerRpcParams serverRpcParams = default)
    {
        Debug.Log("Logging in server rpc");
        var clientId = serverRpcParams.Receive.SenderClientId;

        Debug.Log($"Received server rpc from client {clientId}");

        var clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        };

        if (_databaseManager.PlayerExists(userName))
        {
            if (_databaseManager.CheckPlayerCredentials(userName, password))
            {
                if (_matchmakingManager.IsClientConnected(userName))
                {
                    DisplayMessageClientRpc(MessageType.UserAlreadyConnected, clientRpcParams);
                }
                else
                {
                    // LOGIN ON CLIENT
                    (string name, int elo, int kills, int deaths) playerInfo = _databaseManager.GetPlayerInfo(userName);
                    
                    LoginClientRpc(playerInfo.name, password, playerInfo.elo, playerInfo.kills, playerInfo.deaths, clientRpcParams);

                    // Add to connected clients
                    _matchmakingManager.AddClientToConnectedClients(userName, password, playerInfo.elo, clientId);
                }
            }
            else
            {
                DisplayMessageClientRpc(MessageType.PasswordNotCorrect, clientRpcParams);
            }
            
        }
        else
        {
            DisplayMessageClientRpc(MessageType.UsernameNotExists, clientRpcParams);
        }
            
    }

    [ClientRpc]
    private void DisplayMessageClientRpc(MessageType error, ClientRpcParams clientRpcParams = default)
    {
        _canvasManager.DisplayError(error);
    }

    [ClientRpc]
    private void LoginClientRpc(string userName, string password, int elo, int kills, int deaths, ClientRpcParams clientRpcParams = default)
    {
        _canvasManager.DisplayLoggedInScreen(userName, elo, kills, deaths);
        
        var connectedClientInfo = FindObjectOfType<ConnectedClientInfo>();
        if (connectedClientInfo)
        {
            Debug.Log($"Already had connect client info on client {connectedClientInfo.UserName}");
            connectedClientInfo.Elo = elo;
        }
        else
        {
            Debug.Log($"Didn't have connected client info, creating info on client");
            var clientInfo = Instantiate(_clientInfoPrefab);
            clientInfo.UserName = userName;
            clientInfo.Password = password;
            clientInfo.Elo = elo;

            DontDestroyOnLoad(clientInfo.gameObject);
        }
    }
}
