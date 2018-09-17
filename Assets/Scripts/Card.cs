using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cards
{
    public enum CardColor
    {
        // Note: upon swapping these the player prefab must be changed, as enums are stored as int in prefabs.
        Diamonds,
        Clubs,
        Hearts,
        Spades
    }

    public enum CardRank
    {
        Six,
        Seven,
        Eight,
        Nine,
        Ten,
        Jack,
        Queen,
        King,
        Ace
    }

    public struct CardType
    {
        public CardColor color;
        public CardRank rank;
    }

    public class Card : MonoBehaviour
    {
        public enum CardState
        {
            Hidden,
            InHand,
            HoveringDropZone,
            Played,
            Collected
        }

        public CardType cardType;
        public CardState cardState = CardState.Hidden;

        [SerializeField] private CardColor cardColor;
        [SerializeField] private CardRank cardRank;

        private void Awake()
        {
            cardType.color = cardColor;
            cardType.rank = cardRank;
        }
        private void OnTriggerEnter(Collider other)
        {
            if (other.name == "DropZone")
                cardState = CardState.HoveringDropZone;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.name == "DropZone")
                cardState = CardState.InHand;
        }
    }
}
