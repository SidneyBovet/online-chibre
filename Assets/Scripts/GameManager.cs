using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using UnityEngine.Networking;
using Cards;

public class GameManager : NetworkBehaviour
{
    private enum GameState
    {
        WaitingForPlayers,
        Deal,
        ChooseTrump,
        OngoingTrick,
        MatchEnded,
        Ended
    }

    private const int PLAYER_CONT = 2;

    private Player[] players = new Player[PLAYER_CONT];
    private List<CardType> allCards;
    private int maxScore = 1000;
    private int tricksCount = 0;
    private int cardsDroppedCount = 0;
    private int firstPlayer = 0;
    private int trumpChoser = 0;
    private CardColor trickColor;
    private CardType[] currentTrick = new CardType[PLAYER_CONT]; // player one populates currentTrick[0], etc.
    private List<CardType> cardsTeamOne, cardsTeamTwo;
    private CardColor trump = CardColor.Spades;
    private GameState gameState = GameState.WaitingForPlayers;
    private float timer;

    /// <summary>
    /// playerId is zero-indexed!
    /// </summary>
    public void OnCardPlayed(CardType cardPlayed, int playerId)
    {
        currentTrick[playerId] = cardPlayed;
        if (cardsDroppedCount == 0)
        {
            trickColor = cardPlayed.color;
            UpdatePlayerTrickColor(trickColor);
        }
        cardsDroppedCount++;
        timer = Time.time + 1f;
        UpdatePlayerCardsPlayed();
        if (cardsDroppedCount < players.Length)
            UpdatePlayerAuthorization((firstPlayer + cardsDroppedCount) % players.Length);
        else
            UpdatePlayerAuthorization(-1);
    }

    private void Awake()
    {
        cardsTeamOne = new List<CardType>();
        cardsTeamTwo = new List<CardType>();
        allCards = new List<CardType>();
        foreach (CardColor color in Enum.GetValues(typeof(CardColor)))
        {
            foreach (CardRank rank in Enum.GetValues(typeof(CardRank)))
            {
                CardType type;
                type.rank = rank;
                type.color = color;
                allCards.Add(type);
            }
        }
    }

    private void Update()
    {
        if (!isServer) return;

        switch (gameState)
        {
            case GameState.WaitingForPlayers:
                // wait for all players to connect
                if (NetworkServer.connections.Count >= players.Length)
                {
                    Debug.Log("Game starts");
                    cardsTeamOne.Clear();
                    cardsTeamTwo.Clear();
                    tricksCount = 0;
                    timer = Time.time + 1f;
                    gameState = GameState.Deal;
                }
                break;
            case GameState.Deal:
                // give ourselves one second before dealing
                if (Time.time - timer > 0f)
                {
                    Debug.Log("Dealing cards");
                    UpdateScores();
                    DealCards();
                    UpdatePlayerAuthorization(firstPlayer);
                    gameState = GameState.ChooseTrump;
                }
                break;
            case GameState.ChooseTrump:
                // wait for team to choose trump
                Debug.Log("Chosing trump");
                HandleTrumpChoice();
                break;
            case GameState.OngoingTrick:
                // detect end of trick and proceed
                if (cardsDroppedCount == players.Length
                    && Time.time - timer > 0f)
                {
                    Debug.Log("Trick ended, recording cards played");
                    HandleTrickEnd();
                }
                break;
            case GameState.MatchEnded:
                // compute scores and deal again
                Debug.Log("Match ended");
                HandleMatchEnd();
                break;
            case GameState.Ended:
                UpdateExtraLines("GAME ENDED,", "THANKS FOR PLAYING");
                break;
            default:
                Debug.LogError("Unknown game state: " + gameState);
                break;
        }
    }

    private void HandleTrumpChoice()
    {
        // simply cycle through all color for now
        trump = (CardColor)(((int)trump + 1) % Enum.GetValues(typeof(CardColor)).Length);

        UpdatePlayerTrump(trump);
        UpdateExtraLines("TRUMP IS " + trump.ToString(), "");

        gameState = GameState.OngoingTrick;
    }

    private void HandleMatchEnd()
    {
        ChibreManager.instance.OnMatchEnd(trump, cardsTeamOne.ToArray(), cardsTeamTwo.ToArray());
        UpdateScores();
        cardsDroppedCount = 0;
        tricksCount = 0;
        cardsTeamOne.Clear();
        cardsTeamTwo.Clear();
        if (ChibreManager.instance.scoreTeamOne > maxScore
            || ChibreManager.instance.scoreTeamTwo > maxScore)
            gameState = GameState.Ended;
        else
        {
            timer = Time.time + 1f;
            gameState = GameState.Deal;
        }
    }

    private void HandleTrickEnd()
    {
        int winningPlayer = ChibreManager.GetTrickWinner(trump, trickColor, currentTrick);
        if (winningPlayer % 2 == 0)
        {
            Debug.Log("Team one wins this trick.");
            cardsTeamOne.AddRange(currentTrick);
        }
        else
        {
            Debug.Log("Team two wins this trick.");
            cardsTeamTwo.AddRange(currentTrick);
        }

        // set everything for the next trick
        cardsDroppedCount = 0;
        firstPlayer = winningPlayer;
        UpdatePlayerAuthorization(firstPlayer);
        UpdatePlayerTrickColor(null);
        foreach (Player p in players)
            p.RpcClearPlayedCards();
        tricksCount++;

        if (tricksCount == (int)(allCards.Count / (float)PLAYER_CONT))
        {
            trumpChoser = (trumpChoser + 1) % players.Length;
            gameState = GameState.MatchEnded;
        }
    }

    public void AddPlayer(Player player, int playerId)
    {
        players[playerId] = player;
        player.TargetSetPlayerId(player.connectionToClient, playerId);
    }

    private void DealCards()
    {
        allCards.OrderBy(o => UnityEngine.Random.Range(-1f, 1f));

        for (int player = 0; player < players.Length; player++)
        {
            List<CardType> playerCards = new List<CardType>();
            for (int i = 0; i < allCards.Count; i++)
            {
                if (i % players.Length == player)
                    playerCards.Add(allCards[i]);

                if (allCards[i].color == CardColor.Diamonds
                    && allCards[i].rank == CardRank.Seven)
                    trumpChoser = player;
            }

            if (player < players.Length)
            {
                //Debug.Log("Dealing cards to player " + player);
                players[player].TargetReceiveCards(players[player].connectionToClient, playerCards.ToArray());
            }
            else
            {
                // deal cards to bot
            }
        }
    }

    private void UpdatePlayerAuthorization(int playingClient)
    {
        //Debug.Log("Player " + playingClient + " may play now");

        // set authorization for all clients
        for (int i = 0; i < players.Length; i++)
            players[i].TargetSetCanPlay(players[i].connectionToClient, i == playingClient);
    }

    private void UpdatePlayerTrickColor(CardColor? trickColor)
    {
        if (trickColor == null)
            foreach (var player in players)
                player.RpcResetTrickColor();
        else
            foreach (var player in players)
                player.RpcSetTrickColor((CardColor)trickColor);
    }

    private void UpdatePlayerTrump(CardColor trump)
    {
        foreach (var player in players)
            player.RpcSetTrump(trump);
    }

    private void UpdatePlayerCardsPlayed()
    {
        // extract already played cards from list
        var playedCards = new CardType[cardsDroppedCount];
        for (int i = 0; i < cardsDroppedCount; i++)
            playedCards[i] = currentTrick[(firstPlayer + i) % currentTrick.Length];

        // send them
        foreach (var player in players)
            player.RpcSetPlayedCards(playedCards);
    }

    private void UpdateScores()
    {
        foreach (var player in players)
            player.RpcUpdateScores(
                ChibreManager.instance.scoreTeamOne,
                ChibreManager.instance.scoreTeamTwo);
    }

    private void UpdateExtraLines(string lineOne, string lineTwo)
    {
        foreach (var player in players)
        {
            if (lineOne != null)
                player.RpcUpdateExtraLineOne(lineOne);
            if (lineTwo != null)
                player.RpcUpdateExtraLineTwo(lineTwo);
        }
    }
}
