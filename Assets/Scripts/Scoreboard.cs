using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class Scoreboard : NetworkBehaviour
{
    public static Scoreboard instance;
    public bool isTeamOne = true;
    [SerializeField]
    private TextMesh scoreTeamOne;
    [SerializeField]
    private TextMesh scoreTeamTwo;
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

    [ClientRpc]
    public void RpcUpdateScores(int teamOne, int teamTwo)
    {
        Debug.LogFormat("Updating scores: {0} - {1}", teamOne, teamTwo);
        
        if (isTeamOne)
        {
            scoreTeamOne.text = teamOne.ToString();
            scoreTeamTwo.text = teamTwo.ToString();
        }
        else
        {
            scoreTeamOne.text = teamTwo.ToString();
            scoreTeamTwo.text = teamOne.ToString();
        }
    }

    [ClientRpc]
    public void RpcUpdateExtraLines(string lineOne, string lineTwo)
    {
        if (lineOne != null) extraLineOne.text = lineOne.ToUpper();
        if (lineTwo != null) extraLineTwo.text = lineTwo.ToUpper();
    }
}
