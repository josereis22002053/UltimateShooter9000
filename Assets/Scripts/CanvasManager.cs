using UnityEngine;
using TMPro;

public class CanvasManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _blueTeamScore;
    [SerializeField] private TextMeshProUGUI _greenTeamScore;

    public void UpdateScoreUI(Team team, string newScore)
    {
        TextMeshProUGUI scoreToUpdate = team == Team.Blue ? _greenTeamScore : _blueTeamScore;

        Team teamToUpdate = team == Team.Blue ? Team.Green : Team.Blue;
        Debug.Log($"Updating {teamToUpdate}'s score");
        scoreToUpdate.text = newScore;
    }
}
