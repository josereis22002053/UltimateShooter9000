using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine.Video;
using System.Text.RegularExpressions;

public class CanvasManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI    _blueTeamScore;
    [SerializeField] private TextMeshProUGUI    _greenTeamScore;
    [SerializeField] private GameObject         _gameStartScreen;
    [SerializeField] private GameObject         _endScreen;
    [SerializeField] private GameObject         _errorDisplay;
    [SerializeField] private GameObject         _loginScreen;
    [SerializeField] private GameObject         _loggedInScreen;
    [SerializeField] private TextMeshProUGUI    _gameStartTimer;
    [SerializeField] private TextMeshProUGUI    _result;
    [SerializeField] private TextMeshProUGUI    _error;
    [SerializeField] private TextMeshProUGUI    _playerInfo;

    [SerializeField] private bool _inGameCanvas;

    private void Start()
    {
        if (_inGameCanvas)
        {
            _gameStartScreen.SetActive(false);
            _endScreen.SetActive(false);

            MatchManager matchManager = FindObjectOfType<MatchManager>();
            matchManager.gameStarting += SubscribeToPlayers;
            matchManager.gameStarting += DisplayGameStartScreen;
            matchManager.GameEnded += DisplayResult;
        }
    }

    private void UpdateScoreUI(Team team, int newScore)
    {
        TextMeshProUGUI scoreToUpdate = team == Team.Blue ? _blueTeamScore : _greenTeamScore;

        Debug.Log($"Updating {team}'s score");
        scoreToUpdate.text = newScore.ToString();
    }

    private void SubscribeToPlayers()
    {
        var players = FindObjectsOfType<Player>();
        Debug.Log($"CanvasManager found {players.Length} players when subscribing");
        foreach (var p in players)
        {
            p.PlayerDied += UpdateScoreUI;
        }
    }

    private void DisplayResult(Team winner)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            _result.text = $"{winner} won!";
            _endScreen.SetActive(true);
            return;
        }

        Player[] players = FindObjectsOfType<Player>();
        foreach (Player p in players)
        {
            if (p.IsLocalPlayer)
            {
                if (p.Team == winner)
                {
                    _result.text = "You won!";
                }
                else
                {
                    _result.text = "You lost!";
                }
                _endScreen.SetActive(true);
                break;
            }
        }
    }

    private void DisplayGameStartScreen()
    {
        StartCoroutine(DisplayGameStartScreenCR());
    }

    private IEnumerator DisplayGameStartScreenCR()
    {
        int timer = 3;
        _gameStartTimer.text = $"Game starting in {timer}";
        _gameStartScreen.SetActive(true);
        yield return new WaitForSeconds(1);

        timer--;
        _gameStartTimer.text = $"Game starting in {timer}";
        yield return new WaitForSeconds(1);

        timer--;
        _gameStartTimer.text = $"Game starting in {timer}";
        yield return new WaitForSeconds(1);

        _gameStartScreen.SetActive(false);
    }

    public void DisplayError(MessageType error)
    {
        switch (error)
        {
            case MessageType.UsernameInvalidSize:
                _error.text = "Username must be between 3 and 20 characters long.";
                break;
            case MessageType.UsernameAlreadyExists:
                _error.text = "Username already exists.";
                break;
            case MessageType.UsernameNotExists:
                _error.text = "Username doesn't exist.";
                break;
            case MessageType.PasswordInvalidSize:
                _error.text = "Password must be between 5 and 20 characters long.";
                break;
            case MessageType.PasswordContainsWhitespace:
                _error.text = "Password can't contain white spaces.";
                break;
            case MessageType.PasswordNotCorrect:
                _error.text = "Incorrect password.";
                break;
            case MessageType.CreateAccountSuccessful:
                _error.text = "Account created. Please login.";
                break;
            default:
                _error.text = "Unknown error";
                break;
        }

        _errorDisplay.SetActive(true);
    }

    public void DisplayLoggedInScreen(string userName, int elo)
    {
        UpdatePlayerInfo(userName, elo);
        _loginScreen.SetActive(false);
        _loggedInScreen.SetActive(true);
    }

    private void UpdatePlayerInfo(string userName, int elo)
    {
        string aux = $"Player info\n  Username: {userName}\n  Rating: {elo}";
        _playerInfo.text = aux;
    }
}
