using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class CardHandLayout : MonoBehaviour
{
    public List<Transform> cards = new List<Transform>();

    public float radius = 5f;
    public float maxAngle = 30f;
    public float animationSpeed = 10f;
    public float minAngleStep = 200f;

    public Transform deckPosition;
    public Transform cardmovePosition;
    public Transform cardplaceholder;
    public RectTransform peekPosition;

    public float cardinitialScale;

    private Transform _peekCard;
    private int _peekIndex = -1;

    // The hand slot the currently-peeking card came from, so a cancelled drag can
    // restore it to its original position.
    public int PeekIndex => _peekIndex;

    void Update()
    {
        UpdateCardPositions();
    }

    // Adds a freshly-drawn card with the flip animation. Inserted into the visual list only
    // after the flip completes (player cards) or immediately (opponent cards).
    public void AddCard(Transform card, Transform startPos = null)
    {
        card.SetParent(transform);
        card.localScale = Vector3.one * cardinitialScale;

        CardController cardCont = card.GetComponent<CardController>();
        cardCont.handLayout = this;
        bool isPlayerCard = cardCont.modal.isPlayerMinion;

        card.position = startPos == null ? deckPosition.position : startPos.position;

        if (isPlayerCard)
        {
            cardCont.canPeek = false;
            card.DOMove(cardmovePosition.position, 0.25f);
            card.DOScale(1.5f, 0.25f);

            cardCont.modal.isPlayerMinion = false;
            card.rotation = Quaternion.Euler(0f, 180f, 0f);
            card.DORotate(Vector3.up * 90, 0.15f).OnComplete(() =>
            {
                cardCont.modal.isPlayerMinion = isPlayerCard;
                cardCont.view.UpdateView(cardCont.modal);
                card.DORotate(Vector3.up * 0, 0.15f).OnComplete(() =>
                {
                    DOVirtual.DelayedCall(0.5f, () =>
                    {
                        cardCont.canPeek = true;
                        cards.Insert(0, card);
                    });
                });
            });
        }
        else
        {
            cards.Insert(0, card);
        }
    }

    // Inserts a card (or the placeholder) at a specific index without animation.
    public void InsertCardAt(Transform card, int index)
    {
        card.SetParent(transform);
        card.SetSiblingIndex(Mathf.Clamp(transform.childCount - index - 1, 0, transform.childCount - 1));
        cards.Insert(index < 0 ? 0 : index, card);

        CardController cardCont = card.GetComponent<CardController>();
        if (cardCont != null) cardCont.handLayout = this;
    }

    public int RemoveCard(Transform card)
    {
        int index = cards.IndexOf(card);
        cards.Remove(card);
        return index;
    }

    public bool BeginPeek(CardController card)
    {
        if (_peekCard != null) return false;

        int index = RemoveCard(card.transform);
        if (index < 0) return false;

        InsertCardAt(cardplaceholder, index);
        _peekCard = card.transform;
        _peekIndex = index;
        return true;
    }

    public void EndPeek()
    {
        if (_peekCard == null) return;

        RemoveCard(cardplaceholder);
        cardplaceholder.SetParent(transform.parent);
        InsertCardAt(_peekCard, _peekIndex);

        _peekCard = null;
        _peekIndex = -1;
    }

    // Removes the placeholder without re-inserting the peeking card — used when the
    // card is being taken over by another system (drag, card play) and should stay
    // out of the layout's `cards` list.
    public void CancelPeek()
    {
        if (_peekCard == null) return;

        RemoveCard(cardplaceholder);
        cardplaceholder.SetParent(transform.parent);

        _peekCard = null;
        _peekIndex = -1;
    }

    public bool IsPeeking(CardController card)
    {
        return _peekCard == card.transform;
    }

    void UpdateCardPositions()
    {
        for (int i = cards.Count - 1; i >= 0; i--)
        {
            if (cards[i] == null) cards.RemoveAt(i);
        }

        int count = cards.Count;
        if (count == 0) return;

        float angleStep;
        float startAngle;

        if (count == 1)
        {
            angleStep = 0f;
            startAngle = 0f;
        }
        else
        {
            angleStep = Mathf.Min(maxAngle / (count - 1), minAngleStep);
            float totalAngle = angleStep * (count - 1);
            startAngle = -totalAngle / 2f;
        }

        for (int i = 0; i < count; i++)
        {
            float angleDeg = startAngle + angleStep * i;
            float angleRad = angleDeg * Mathf.Deg2Rad;

            float x = Mathf.Sin(angleRad) * radius;
            float y = -Mathf.Cos(angleRad) * radius + radius;

            Vector3 targetPos = new Vector3(x, y, 0);
            Quaternion targetRot = Quaternion.Euler(0, 0, angleDeg);

            Transform card = cards[i];
            card.localPosition = Vector3.Lerp(card.localPosition, targetPos, Time.deltaTime * animationSpeed);
            card.localRotation = Quaternion.Lerp(card.localRotation, targetRot, Time.deltaTime * animationSpeed);
            card.localScale = Vector3.Lerp(card.localScale, Vector3.one, Time.deltaTime * animationSpeed);
        }
    }
}
