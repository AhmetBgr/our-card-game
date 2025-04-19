using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using Unity.VisualScripting.Antlr3.Runtime;

public class CardHandLayout : MonoBehaviour
{
    public List<Transform> cards = new List<Transform>();

    public float radius = 5f;             // Arc radius
    public float maxAngle = 30f;          // Total fan angle (in degrees)
    public float animationSpeed = 10f;
    public float minAngleStep = 200f;

    public Transform deckPosition;
    public Transform cardmovePosition;
    public Transform cardplaceholder;


    public float cardinitialScale;
    void Update()
    {
        UpdateCardPositions();
    }

    public void AddCard(Transform card)
    {
        card.SetParent(transform);
        card.localScale = Vector3.one * cardinitialScale;

        CardController cardCont = card.GetComponent<CardController>();
        bool isplayerCard = cardCont.modal.isPlayerMinion;
        
        if (isplayerCard )
        {
            cardCont.canPeek = false;
            card.DOMove(cardmovePosition.position, 0.25f);
            card.DOScale(1.5f, 0.25f);

            cardCont.modal.isPlayerMinion = false;
            card.rotation = Quaternion.Euler(0f, 180f, 0f);
            card.DORotate(Vector3.up * 90, 0.15f).OnComplete(()=> 
            {

                cardCont.modal.isPlayerMinion = isplayerCard;
                cardCont.view.UpdateView(cardCont.modal);
                card.DORotate(Vector3.up * 0, 0.15f).OnComplete(() =>
                {
                    //card.DOScale(1f, 0.25f);

                    DOVirtual.DelayedCall(0.5f, () => 
                    {
                        cardCont.canPeek = true;

                        cards.Insert(0, card);
                    });
                     // Insert at the front

                });
            });
        }
        else
        {
            cards.Insert(0, card); // Insert at the front
            //card.DOScale(1f, 0.25f);

        }

        card.position = deckPosition.position;
    }

    public void AddCard(Transform card, int index)
    {
        card.SetParent(transform);
        card.SetSiblingIndex(Mathf.Clamp(transform.childCount -  index - 1, 0, transform.childCount - 1));
        //card.localScale = Vector3.one * cardinitialScale;

        //CardController cardCont = card.GetComponent<CardController>();
        //bool isplayerCard = cardCont.modal.isPlayerMinion;
        cards.Insert(index < 0 ? 0 : index, card); // Insert at the front

        //card.position = deckPosition.position;
    }

    public int RemoveCard(Transform card, bool destroy = true)
    {

        int index = cards.IndexOf(card);    
        cards.Remove(card);

        if (destroy)
        {
            Destroy(card.gameObject);
            return index;
        }
        return index;
    }

    void UpdateCardPositions()
    {
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
            // Dynamic angleStep with min spacing
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
            Vector3 targetScale = Vector3.one;

            Transform card = cards[i];
            card.localPosition = Vector3.Lerp(card.localPosition, targetPos, Time.deltaTime * animationSpeed);
            card.localRotation = Quaternion.Lerp(card.localRotation, targetRot, Time.deltaTime * animationSpeed);
            card.localScale = Vector3.Lerp(card.localScale, targetScale, Time.deltaTime * animationSpeed);
        }
    }

}

/*using UnityEngine;
using System.Collections.Generic;

public class CardHandLayout : MonoBehaviour
{
    public List<Transform> cards = new List<Transform>();
    public float radius = 2.0f;
    public float angleRange = 90f;
    public float spacing = 1.5f;
    public float cardYOffset = 0.2f;

    public float animationSpeed = 10f;

    void Update()
    {
        UpdateCardPositions();
    }

    public void AddCard(Transform card)
    {
        cards.Add(card);
        card.SetParent(transform);
    }

    public void RemoveCard(Transform card)
    {
        cards.Remove(card);
        Destroy(card.gameObject);

    }

    void UpdateCardPositions()
    {
        int count = cards.Count;
        float angleStep = (count > 1) ? angleRange / (count - 1) : 0;
        float startAngle = -angleRange / 2;

        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + angleStep * i;
            float rad = angle * Mathf.Deg2Rad;

            Vector3 targetPos = new Vector3(
                Mathf.Sin(rad) * radius,
                -Mathf.Cos(rad) * radius,
                0
            ) + transform.position;

            Quaternion targetRot = Quaternion.Euler(0, 0, angle);

            // Smooth transition
            cards[i].localPosition = Vector3.Lerp(cards[i].localPosition, targetPos, Time.deltaTime * animationSpeed);
            cards[i].localRotation = Quaternion.Lerp(cards[i].localRotation, targetRot, Time.deltaTime * animationSpeed);
        }
    }
}*/
