using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Collections;
using Unity.VisualScripting;

public class CanvasManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _blueTeamScore;
    [SerializeField] private TextMeshProUGUI _greenTeamScore;

    public void UpdateScoreUI(Team team, int newScore)
    {
        TextMeshProUGUI scoreToUpdate = team == Team.Blue ? _blueTeamScore : _greenTeamScore;

        Debug.Log($"Updating {team}'s score");
        scoreToUpdate.text = newScore.ToString();
    }

    private void Start()
    {
        MatchManager matchManager = FindObjectOfType<MatchManager>();
        matchManager.gameStarted += SubscribeToPlayers;
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
}
