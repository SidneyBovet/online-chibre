using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine.Networking;
using UnityEngine;
using Cards;

public class Player : NetworkBehaviour
{
    [SerializeField] private GameObject cardBackPrefab;

    public int playerId = -1;

    private GameManager gameManager;
    private Card[] cards;
    public List<CardType> hand = new List<CardType>();
    private bool canPlay = false;
    private CardType[] currentTrick;
    private CardColor trump;
    private CardColor? trickColor = null;
    private Card movingCard;
    private Camera cam;

    private Vector3 cardPickupTranslation = new Vector3(0f, 0.01f, 0f);
    private Vector3 cardDropTranslation = new Vector3(0f, 0.03f, 0f);

    [TargetRpc]
    public void TargetSetPlayerId(NetworkConnection target, int id)
    {
        playerId = id;
        Scoreboard.instance.isTeamOne = (id % 2) == 0;
    }

    [ClientRpc]
    public void RpcUpdateScores(int teamOne, int teamTwo)
    {
        Scoreboard.instance.UpdateScores(teamOne, teamTwo);
    }

    [ClientRpc]
    public void RpcUpdateExtraLineOne(string line)
    {
        Scoreboard.instance.UpdateExtraLines(line, null);
    }

    [ClientRpc]
    public void RpcUpdateExtraLineTwo(string line)
    {
        Scoreboard.instance.UpdateExtraLines(null, line);
    }

    [TargetRpc]
    public void TargetReceiveCards(NetworkConnection target, CardType[] dealtCards)
    {
        Debug.Assert(dealtCards.Length == 9);

        // save new hand
        hand.Clear();
        foreach (CardType c in dealtCards)
            hand.Add(c);

        // sort hand
        hand.Sort(new ChibreManager.CardComparer());

        // set cards as in hand
        foreach (Card c in cards)
            if (hand.Contains(c.cardType))
                c.cardState = Card.CardState.InHand;
    }

    [TargetRpc]
    public void TargetSetCanPlay(NetworkConnection target, bool canPlay)
    {
        this.canPlay = canPlay;
    }

    [ClientRpc]
    public void RpcSetTrump(CardColor trump)
    {
        this.trump = trump;
    }

    [ClientRpc]
    public void RpcSetTrickColor(CardColor trickColor)
    {
        this.trickColor = trickColor;
    }

    [ClientRpc]
    public void RpcResetTrickColor()
    {
        this.trickColor = null;
    }

    [ClientRpc]
    public void RpcSetPlayedCards(CardType[] cardsPlayed)
    {
        currentTrick = cardsPlayed;
    }

    [ClientRpc]
    public void RpcClearPlayedCards()
    {
        foreach (Card c in cards)
        {
            if (c.cardState == Card.CardState.Played)
                c.transform.position = Vector3.zero;
        }
    }

    [Command]
    private void CmdOnCardPlayed(CardType cardPlayed, int id)
    {
        gameManager.OnCardPlayed(cardPlayed, id); // record played card
        RpcRevealCard(cardPlayed); // ask instances of me on other clients to reveal the card
    }

    [ClientRpc]
    private void RpcRevealCard(CardType card)
    {
        if (isLocalPlayer) return;

        foreach (Card c in cards)
        {
            if (c.cardType.color == card.color
            && c.cardType.rank == card.rank)
                c.transform.GetChild(0).gameObject.SetActive(false);
        }
    }

    private void Awake()
    {
        gameManager = FindObjectOfType<GameManager>();
        cards = GetComponentsInChildren<Card>();
        Debug.Assert(cards.Length == 36);
        foreach (Card c in cards)
        {
            gameObject.SetActive(false);
            var netTransformChild = gameObject.AddComponent<NetworkTransformChild>();
            netTransformChild.target = c.transform;
            netTransformChild.sendInterval = 0.01f;
            gameObject.SetActive(true);
        }
    }

    private void Start()
    {
        if (!isLocalPlayer)
        {
            foreach (Card c in cards)
            {
                var position = c.transform.position + new Vector3(0f, 0.0001f, 0f);
                var rotation = c.transform.rotation * Quaternion.AngleAxis(180f, Vector3.forward);
                GameObject.Instantiate(cardBackPrefab, position, rotation, c.transform);
            }
        }
        else
        {
            // has local authority, move cards around and position camera
            cam = Camera.main;
            foreach (Card c in cards)
                c.transform.position = Vector3.zero;
            StartCoroutine(MoveCamera(transform.rotation.eulerAngles.y, 0.5f));
            Scoreboard.instance.transform.RotateAround(Vector3.zero, Vector3.up, transform.rotation.eulerAngles.y);
        }
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        RepositionCards();
        HandleMouseEvents();
    }

    private IEnumerator MoveCamera(float angle, float seconds)
    {
        angle = Mathf.Abs(angle) < Mathf.Abs(angle - 360f) ? angle : angle - 360f;
        int steps = 100;
        for (int i = 0; i < steps; i++)
        {
            cam.transform.RotateAround(Vector3.zero, Vector3.up, angle / steps);
            yield return new WaitForSeconds(seconds / steps);
        }
    }

    private void HandleMouseEvents()
    {
        if (canPlay)
        {
            if (Input.GetMouseButton(0))
            {
                MoveCards(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                TryDropCard();
            }
        }
    }

    private void TryDropCard()
    {
        if (movingCard != null
            && movingCard.cardState == Card.CardState.HoveringDropZone
            && CanPlayCard(movingCard.cardType))
        {
            movingCard.cardState = Card.CardState.Played;
            movingCard.transform.Translate(movingCard.transform.InverseTransformVector(-cardDropTranslation));
            hand.Remove(movingCard.cardType);
            CmdOnCardPlayed(movingCard.cardType, playerId);
            canPlay = false;
            movingCard = null;
        }
        movingCard = null;
    }

    private void MoveCards(Vector2 mousePos)
    {
        var point = cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, cam.nearClipPlane));
        var origin = cam.transform.position;
        var direction = point - origin;

        var cardLayer = LayerMask.GetMask("Card");
        RaycastHit hit;
        if (Physics.Raycast(origin, direction, out hit, 3f, cardLayer))
        {
            foreach (Card c in cards)
            {
                if (c.gameObject == hit.collider.gameObject)
                {
                    // pick up card if allowed
                    if (movingCard != c
                        && c.cardState == Card.CardState.InHand)
                    {
                        movingCard = c;
                        hit.point += cardPickupTranslation;
                    }

                    // recenter moving card on hit point
                    if (movingCard != null)
                        movingCard.transform.position = hit.point;

                    break;
                }
            }
        }
    }

    public bool CanPlayCard(CardType intendedCard)
    {
        if (trickColor == null) // no color defined yet
        {
            return true;
        }
        else if (intendedCard.color == trickColor) // that's a legal card
        {
            return true;
        }
        else if (intendedCard.color == trump) // check if some trump were already played, in which case the card must be stronger
        {
            foreach (var card in currentTrick)
            {
                if (card.color == trump
                    && ChibreManager.CompareTrumpCards(card.rank, intendedCard.rank))
                    return false;
            }
            return true;
        }
        else // not allowed if hand contains a card from current trick that's not the Jack of trump
        {
            // TODO: this is a potential security hole, as we trust the client is giving us the right hand
            foreach (var card in hand)
            {
                if (card.color == trickColor
                    && !(card.color == trump
                        && card.rank == CardRank.Jack))
                {
                    return false;
                }
            }
            return true;
        }
    }

    private void RepositionCards()
    {
        // place cards in front of player
        float cardSpacing = 0.1f;
        float totalWidth = (hand.Count - 1) * cardSpacing;
        foreach (Card c in cards)
        {
            if (hand.Contains(c.cardType)
                && c != movingCard)
            {
                var posX = -(totalWidth / 2f) + hand.IndexOf(c.cardType) * cardSpacing;
                c.transform.localPosition = new Vector3(posX, 0f, 0f);
            }
        }
    }
}
