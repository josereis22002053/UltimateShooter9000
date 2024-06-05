using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Linq;
using System;

public class LoginManager : NetworkBehaviour
{
    [SerializeField] private TMP_InputField _userNameField;
    [SerializeField] private TMP_InputField _passwordField;

    private CanvasManager _canvasManager;
    private DatabaseManager _databaseManager;
    private Matchmaking _matchmakingManager;

    private void Start()
    {
        _canvasManager = FindObjectOfType<CanvasManager>();

        //if (IsServer)
        _databaseManager = FindObjectOfType<DatabaseManager>();
        _matchmakingManager = FindObjectOfType<Matchmaking>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5)) Login();
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
            _databaseManager.AddPlayer(userName, password, 500);
            DisplayMessageClientRpc(MessageType.CreateAccountSuccessful, clientRpcParams);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void LoginServerRpc(string userName, string password, ServerRpcParams serverRpcParams = default)
    {
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
                // LOGIN ON CLIENT
                (string name, int elo) playerInfo = _databaseManager.GetPlayerInfo(userName);
                LoginClientRpc(playerInfo.name, playerInfo.elo, clientRpcParams);

                // Add to connected clients
                _matchmakingManager.AddClientToConnectedClients(userName, clientId);
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
    private void LoginClientRpc(string userName, int elo, ClientRpcParams clientRpcParams = default)
    {
        _canvasManager.DisplayLoggedInScreen(userName, elo);
    }
}
