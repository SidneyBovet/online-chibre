using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Networking;
using Cards;

public class GameManager : NetworkBehaviour
{
    private enum GameState
    {
        WaitingForPlayers,
        DiscoverPlayers,
        Deal,
        ChooseTrump,
        OngoingTrick,
        ComputeResults
    }

    private List<CardType> allCards;
    private int tricksCount = 0;
    private int cardsDroppedCount = 0;
    private CardColor trickColor;
    private CardType[] currentTrick = new CardType[4]; // player one populates currentTrick[0], etc.
    private List<CardType> cardsTeamOne, cardsTeamTwo;
    private CardColor trump;

    private Player[] players;
    private GameState gameState = GameState.WaitingForPlayers;
    private float t0;

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
        t0 = Time.time;
        UpdatePlayerAuthorization(cardsDroppedCount);
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
                if (NetworkServer.connections.Count > 0)
                {
                    Debug.Log("Game starts");
                    cardsTeamOne.Clear();
                    cardsTeamTwo.Clear();
                    tricksCount = 0;
                    t0 = Time.time;
                    gameState = GameState.DiscoverPlayers;
                }
                break;
            case GameState.DiscoverPlayers:
                if (Time.time - t0 > 1f)
                {
                    Debug.Log("Discovering players");
                    Scoreboard.instance.RpcUpdateScores(
                        ChibreManager.instance.scoreTeamOne,
                        ChibreManager.instance.scoreTeamTwo);
                    DiscoverPlayers();
                    gameState = GameState.Deal;
                }
                break;
            case GameState.Deal:
                // give ourselves one second before discovering players and dealing
                Debug.Log("Dealing cards");
                DealCards();
                UpdatePlayerAuthorization(0);
                gameState = GameState.ChooseTrump;
                break;
            case GameState.ChooseTrump:
                // wait for team to choose trump
                trump = CardColor.Spades;
                UpdatePlayerTrump(trump);
                gameState = GameState.OngoingTrick;
                break;
            case GameState.OngoingTrick:
                // detect end of trick and proceed
                if (cardsDroppedCount == 4
                    && Time.time - t0 > 1f)
                {
                    Debug.Log("Trick ended, recording cards played");
                    int winningPlayer = ChibreManager.instance.OnTrickEnd(trump, trickColor, currentTrick);
                    if (winningPlayer % 2 == 0)
                        cardsTeamOne.AddRange(currentTrick);
                    else
                        cardsTeamTwo.AddRange(currentTrick);

                    cardsDroppedCount = 0;
                    UpdatePlayerTrickColor(null);

                    foreach (Player p in players)
                        p.RpcClearPlayedCards();

                    tricksCount++;

                    if (tricksCount == 9)
                    {
                        Debug.Log("Match ended");
                        gameState = GameState.ComputeResults;
                    }
                }
                break;
            case GameState.ComputeResults:
                // compute scores and deal again (maybe with timer)
                Debug.Log("Computing results");
                ChibreManager.instance.OnMatchEnd(trump, cardsTeamOne.ToArray(), cardsTeamTwo.ToArray());
                Scoreboard.instance.RpcUpdateScores(
                    ChibreManager.instance.scoreTeamOne,
                    ChibreManager.instance.scoreTeamTwo);
                break;
            default:
                Debug.LogError("Unknown game state: " + gameState);
                break;
        }
    }

    private void DiscoverPlayers()
    {
        List<Player> playersFound = new List<Player>();
        int playerId = 0;

        foreach (var player in NetworkServer.objects.Values)
        {
            var playerComponent = player.GetComponent<Player>();
            if (playerComponent != null)
            {
                playersFound.Add(playerComponent);
                playerComponent.TargetSetPlayerId(playerComponent.connectionToClient, playerId);
                playerId++;
            }
        }

        players = playersFound.ToArray();
    }

    private void DealCards()
    {

        for (int player = 0; player < 4; player++)
        {
            List<CardType> playerCards = new List<CardType>();
            for (int i = 0; i < allCards.Count; i++)
            {
                if (i % 4 == player)
                    playerCards.Add(allCards[i]);
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
        // debug:
        playingClient %= players.Length;
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
}
