using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Non-networked object updated by Player at the server's GameManager's request.
///  Displays current score, as well as two extra lines of text to remind trump color and other things.
/// </summary>
public class Scoreboard : MonoBehaviour
{
    public static Scoreboard instance; // singleton for convenience
    public bool isTeamOne = true; // defines which scores get printed left and right
    [SerializeField]
    private TextMesh scoreUs;
    [SerializeField]
    private TextMesh scoreThem;
    [SerializeField]
    private TextMesh extraLineOne;
    [SerializeField]
    private TextMesh extraLineTwo;

    private void Awake()
    {
        if (instance != null)
            Debug.LogError("There cannot be multiple chibre managers in this scene.");
        else
            instance = this;
    }

    public void UpdateScores(int teamOne, int teamTwo)
    {
        if (isTeamOne)
        {
            scoreUs.text = teamOne.ToString();
            scoreThem.text = teamTwo.ToString();
        }
        else
        {
            scoreUs.text = teamTwo.ToString();
            scoreThem.text = teamOne.ToString();
        }
    }

    public void UpdateExtraLines(string lineOne, string lineTwo)
    {
        if (lineOne != null) extraLineOne.text = lineOne.ToUpper();
        if (lineTwo != null) extraLineTwo.text = lineTwo.ToUpper();
    }
}
