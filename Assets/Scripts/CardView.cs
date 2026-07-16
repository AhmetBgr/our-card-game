using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Unity.Collections.LowLevel.Unsafe;

public class CardView : MonoBehaviour
{
    [SerializeField] private Image art;
    [SerializeField] private Image frame;

    [SerializeField] private Sprite minionFrame;
    [SerializeField] private Sprite spellFrame;
    [SerializeField] private Sprite upgradedMinionFrame;
    [SerializeField] private Sprite upgradedSpellFrame;

    [SerializeField] private GameObject[] minionTypeIconObjects;

    //[SerializeField] private Image _iconRenderer;
    [SerializeField] private Image cardBack;
    [SerializeField] private Sprite cardBackImage;
    [SerializeField] private Sprite upgradedCardBackImage;


    [SerializeField] private TextMeshProUGUI nametext;
    [SerializeField] private TextMeshProUGUI desctext;
    [SerializeField] private TextMeshProUGUI attacktext;
    [SerializeField] private TextMeshProUGUI healthtext;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private Transform costTransform;

    private Tween gearRotateTween;

    private void Start()
    {
        gearRotateTween?.Kill();
        gearRotateTween = costTransform.DOLocalRotate(Vector3.forward * 360, 10f, RotateMode.FastBeyond360).SetEase(Ease.Linear).SetLoops(-1);
        gearRotateTween.timeScale = 0;
    }



    private void OnDestroy()
    {

    }

    public void UpdateView(CardModal card)
    {
        if (card == null) return;
        UpdateTexts(card);

        art.sprite = card.cardArt;

        costTransform.gameObject.SetActive(card.isPlayerMinion);
        costText.gameObject.SetActive(card.isPlayerMinion);
        cardBack.gameObject.SetActive(!card.isPlayerMinion);
        cardBack.sprite = card.isUpgraded ? upgradedCardBackImage : cardBackImage;

        if (card.isUpgraded)
        {
            frame.sprite = card.attack == 0 && card.health == 0 ? upgradedSpellFrame : upgradedMinionFrame;

        }
        else { 
            frame.sprite = card.attack ==0 && card.health ==0 ? spellFrame : minionFrame;

        }

        minionTypeIconObjects[0].transform.parent.gameObject.SetActive(card.isPlayerMinion);

        attacktext.transform.parent.gameObject.SetActive(card.health > 0 && card.attack > 0 && card.isPlayerMinion);
        healthtext.transform.parent.gameObject.SetActive(card.health > 0 && card.isPlayerMinion);


    }
    /// <summary>
    /// Turn-into-another-card flip: one full 360° spin around Y, with `onHalfway` fired at the halfway
    /// point (180°) so the card's identity is swapped while it is turned away from the viewer and its
    /// face is unreadable, instead of popping in place.
    ///
    /// Two +180° LocalAxisAdd steps rather than one absolute 360° tween: added rotation lands back on
    /// whatever rotation the card started at, so this is safe on a card sitting at a hand-fan angle and
    /// can't fight the play/discard tweens by snapping it upright.
    /// </summary>
    public void PlayTurnIntoAnimation(Action onHalfway, float duration)
    {
        float half = duration * 0.5f;

        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOLocalRotate(new Vector3(0f, 180f, 0f), half, RotateMode.LocalAxisAdd)
            .SetEase(Ease.InSine));
        seq.AppendCallback(() => onHalfway?.Invoke());
        seq.Append(transform.DOLocalRotate(new Vector3(0f, 180f, 0f), half, RotateMode.LocalAxisAdd)
            .SetEase(Ease.OutSine));
    }

    public void UpdateGearSpeed(CardModal card)
    {
        if (gearRotateTween == null) return;

        bool playable = GameManager.Instance.player.availibleMana >= card.cost && GameManager.Instance.currentState != GameState.EndGame;
        // Spin rate scales with cost, but a playable card must always visibly spin — otherwise a
        // zero-cost card freezes (timeScale 0) and looks unaffordable. Floor the speed at 1 so a
        // cost-0 card spins like a cost-1 card.
        gearRotateTween.timeScale = playable ? Mathf.Max(card.cost, 1f) : 0f;
    }
    private void UpdateTexts(CardModal card)
    {
        nametext.text = card.name;
        desctext.text = card.desc;

        attacktext.text = card.attack.ToString();
        healthtext.text = card.health.ToString();
        costText.text = card.cost.ToString();
        int selectedIcon = 0;

        if(card.range == 1)
        {
            selectedIcon = 0;
        }
        else if(card.range == 2)
        {
            selectedIcon = 1;
        }
        else if (card.range == 3)
        {
            selectedIcon = 2;
        }
        for(int i = 0; i < minionTypeIconObjects.Length; i++) 
        {
            minionTypeIconObjects[i].SetActive(selectedIcon == i);
        }
    }

}
