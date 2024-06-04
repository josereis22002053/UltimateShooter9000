using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Collections;
using Unity.VisualScripting;

public class CanvasManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI    _blueTeamScore;
    [SerializeField] private TextMeshProUGUI    _greenTeamScore;
    [SerializeField] private GameObject         _endScreen;
    [SerializeField] private TextMeshProUGUI    _result;

    public void UpdateScoreUI(Team team, int newScore)
    {
        TextMeshProUGUI scoreToUpdate = team == Team.Blue ? _blueTeamScore : _greenTeamScore;

        Debug.Log($"Updating {team}'s score");
        scoreToUpdate.text = newScore.ToString();
    }

    private void Start()
    {
        _endScreen.SetActive(false);

        MatchManager matchManager = FindObjectOfType<MatchManager>();
        matchManager.gameStarted += SubscribeToPlayers;
        matchManager.GameEnded += DisplayResult;
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
}
