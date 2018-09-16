using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cards;

public class ChibreManager : MonoBehaviour
{
    /*
	Language (French -> English):

	plie -> trick
	match -> match
	partie -> game
	atout -> trump
	*/

    public static ChibreManager instance = null;

    public int scoreTeamOne = 0;
    public int scoreTeamTwo = 0;

    private Dictionary<CardRank, int> nonTrumpScores = new Dictionary<CardRank, int>
        {
            { CardRank.Ace, 11 },
            { CardRank.King, 4 },
            { CardRank.Queen, 3 },
            { CardRank.Jack, 2 },
            { CardRank.Ten, 10 },
            { CardRank.Nine, 0 },
            { CardRank.Eight, 0 },
            { CardRank.Seven, 0 },
            { CardRank.Six, 0 },
        };
    private Dictionary<CardRank, int> trumpScores = new Dictionary<CardRank, int>
        {
            { CardRank.Ace, 11 },
            { CardRank.King, 4 },
            { CardRank.Queen, 3 },
            { CardRank.Jack, 20 },
            { CardRank.Ten, 10 },
            { CardRank.Nine, 14 },
            { CardRank.Eight, 0 },
            { CardRank.Seven, 0 },
            { CardRank.Six, 0 },
        };

    private void Awake()
    {
        if (instance != null)
            Debug.LogError("There cannot be multiple chibre managers in this scene.");
        else
            instance = this;
    }

    /// <summary>
    /// Return true if lhs beats rhs when both are trump color
    /// </summary>
    public static bool CompareTrumpCards(CardRank lhs, CardRank rhs)
    {
        Debug.Assert(lhs != rhs);

        if (lhs == CardRank.Jack)
            return true;
        else if (rhs == CardRank.Jack)
            return false;
        else if (lhs == CardRank.Nine)
            return true;
        else if (rhs == CardRank.Nine)
            return false;
        else
            return lhs > rhs;
    }

    /// <summary>
    /// Computes which player won the trick
    /// </summary>
    /// <param name="trickCards">The card of this trick, played in order (player one played card [0], etc.</param>
    /// <returns></returns>
    public static int GetTrickWinner(CardColor trump, CardColor trickColor, CardType[] trickCards)
    {
        Debug.Assert(trickCards.Length == 4);

        // find best card
        CardType bestCard = trickCards[0];
        int bestPlayer = 0;
        for (int i = 1; i < trickCards.Length; i++)
        {
            var card = trickCards[i];
            if (card.color == trump)
            {
                if (bestCard.color != trump
                    || CompareTrumpCards(card.rank, bestCard.rank))
                {
                    bestCard = card;
                    bestPlayer = i;
                }
            }
            else if (card.color == trickColor)
            {
                if (bestCard.color != trickColor
                    || card.rank > bestCard.rank)
                {
                    bestCard = card;
                    bestPlayer = i;
                }
            }
        }

        return bestPlayer;
    }

    public void OnMatchEnd(CardColor trump, CardType[] cardsTeamOne, CardType[] cardsTeamTwo)
    {
        scoreTeamOne += ComputeScore(trump, cardsTeamOne);
        scoreTeamTwo += ComputeScore(trump, cardsTeamTwo);

        Debug.Log("Team one total score: " + scoreTeamOne);
        Debug.Log("Team two total score: " + scoreTeamTwo);
    }

    public void OnMeld(int team, int meldScore)
    {
        Debug.LogFormat("Team {0} scores {1} with a meld.", team, meldScore);
    }

    private int ComputeScore(CardColor trump, CardType[] cards)
    {
        int score = 0;

        foreach (var card in cards)
        {
            if (card.color == trump)
                score += trumpScores[card.rank];
            else
                score += nonTrumpScores[card.rank];
        }

        return score;
    }
}
