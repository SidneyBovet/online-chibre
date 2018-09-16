using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine.Networking;
using UnityEngine;
using Cards;

public class Player : NetworkBehaviour
{
    [SerializeField] private GameObject cardBackPrefab;

    private GameManager gameManager;
    private int playerId = -1;
    private Card[] cards;
    private HashSet<CardType> hand = new HashSet<CardType>();
    private bool canPlay = false;
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

    [TargetRpc]
    public void TargetReceiveCards(NetworkConnection target, CardType[] dealtCards)
    {
        Debug.Assert(dealtCards.Length == 9);

        // save new hand
        hand.Clear();
        foreach (CardType c in dealtCards)
            hand.Add(c);
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
            cam.transform.RotateAround(Vector3.zero, Vector3.up, transform.rotation.eulerAngles.y);
        }
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        RepositionCards();
        HandleMouseEvents();
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
                if (movingCard != null
                    && movingCard.cardState == Card.CardState.HoveringDropZone
                    && CanDropCard(movingCard.cardType))
                {
                    movingCard.cardState = Card.CardState.Played;
                    movingCard.transform.Translate(movingCard.transform.InverseTransformVector(-cardDropTranslation));
                    hand.Remove(movingCard.cardType);
                    CmdOnCardPlayed(movingCard.cardType, playerId);
                    movingCard = null;
                }
                movingCard = null;
            }
        }
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

    private bool CanDropCard(CardType intendedCard)
    {
        if (trickColor == null) // no color defined yet
        {
            return true;
        }
        else if (intendedCard.color == trickColor) // that's a legal card
        {
            return true;
        }
        else // not allowed if hand contains a card from current trick that's not the Jack of trump
        {
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
        int cardsPlaced = 0;
        foreach (Card c in cards)
        {
            if (c == movingCard)
            {
                cardsPlaced++;
                continue;
            }
            else if (hand.Contains(c.cardType))
            {
                var posX = -(totalWidth / 2f) + cardsPlaced * cardSpacing;
                c.transform.localPosition = new Vector3(posX, 0f, 0f);
                cardsPlaced++;
            }
        }
    }
}
