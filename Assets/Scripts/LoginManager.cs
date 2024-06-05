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

    private void Start()
    {
        _canvasManager = FindObjectOfType<CanvasManager>();

        //if (IsServer)
        _databaseManager = FindObjectOfType<DatabaseManager>();
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

        if (ValidUsername(userNameInput) && ValidPassword(passwordInput))
        {
            CreateAccountServerRpc(userNameInput, passwordInput);
        }
    }

    public void Login()
    {
        string userNameInput = _userNameField.text;
        string passwordInput = _passwordField.text;

        if (ValidUsername(userNameInput) && ValidPassword(passwordInput))
        {
            LoginServerRpc(userNameInput, passwordInput);
        }
    }

    private bool ValidUsername(string username)
    {
        if (username.Length < 3 || username.Length > 20)
        {
            _canvasManager.DisplayError(MessageType.UsernameInvalidSize);
            return false;
        }
        else
        {
            ValidUsernameServerRpc(username);
            return true;
        }
    }

    private bool ValidPassword(string password)
    {
        if (password.Length < 5 || password.Length > 20)
        {
            _canvasManager.DisplayError(MessageType.PasswordInvalidSize);
            return false;
        }
        else if (password.Any(char.IsWhiteSpace))
        {
            _canvasManager.DisplayError(MessageType.PasswordContainsWhitespace);
            return false;
        }
        else
        {
            return true;
        }
    }

    

    [ServerRpc(RequireOwnership = false)]
    private void ValidUsernameServerRpc(string userName, ServerRpcParams serverRpcParams = default)
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
            DisplayMessageClientRpc(MessageType.UsernameAlreadyExists, clientRpcParams);
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

        if (!_databaseManager.PlayerExists(userName))
        {
            _databaseManager.AddPlayer(userName, password, 100);
            DisplayMessageClientRpc(MessageType.CreateAccountSuccessful, clientRpcParams);
        }
        else
        {
            DisplayMessageClientRpc(MessageType.UsernameAlreadyExists, clientRpcParams);
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
        _canvasManager.DisplayPlayerInfo(userName, elo);
    }
}
