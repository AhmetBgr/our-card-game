using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelectableEntity : MonoBehaviour
{
    [SerializeField] private bool iSselectable = false;
    private Color selectableColor;
    private Color hoverColor;

    private bool _isHoverPreview;

    public bool IsSelectable => iSselectable;

    /// <summary>
    /// Raised whenever the normal selection highlight is toggled, carrying its new on/off state. Lets a
    /// coupled indicator that shares the entity's face — e.g. a minion's attack (sword) highlight — hide
    /// while the normal highlight is showing and return when it clears, so the two never light at once.
    /// </summary>
    public event Action<bool> SelectableChanged;

    public SpriteRenderer highlight;
    public SpriteRenderer hover;

    public Collider2D col;


    void Start()
    {
        GameManager.OnTurnEnd += ResetSelectable;
        PlayBreathAnimation();
    }
    private void OnEnable()
    {
        GameManager.Instance.selectables.Add(this);

    }
    private void OnDisable()
    {
        if(GameManager.Instance == null) return;

        GameManager.Instance.selectables.Remove(this);
    }
    private void OnDestroy()
    {
        GameManager.OnTurnEnd -= ResetSelectable;
        KillBreathAnimation();
    }

    private void ResetSelectable(GameState state)
    {
        SetSelectable(false);   
    }
    public void SetSelectable(SelectableParameters parameters)
    {

    }

    public void SetSelectable(bool value)
    {
        iSselectable = value;
        /*if (!iSselectable)
        {
            _isHoverPreview = false;
        }*/

        UpdateVisuals();

        //PlayBreathAnimation();

        if (col != null)
            col.enabled = value;

        SelectableChanged?.Invoke(iSselectable);
    }

    private Sequence breathSequence;
    public void PlayBreathAnimation()
    {
        KillBreathAnimation();
        //highlight.DOFade(0f, 1f).SetLoops(-1, LoopType.Yoyo);
        breathSequence = DOTween.Sequence();
        breathSequence.AppendInterval(0.5f);
        //breathSequence.Append(highlight.DOColor(selectableColor, 1f));
        breathSequence.Append(highlight.DOColor(Color.clear, 0.5f));
        //breathSequence.Append(highlight.DOColor(selectableColor, 1f));
        breathSequence.SetLoops(-1, LoopType.Yoyo);
    }
    private void KillBreathAnimation()
    {
        if (breathSequence != null && breathSequence.IsActive())
            breathSequence.Kill();
    }
    public void SetHoverPreview(bool value)
    {
        _isHoverPreview = value;
        UpdateVisuals();
    }

    // Hide the selectable highlight while keeping the entity clickable (collider stays enabled).
    // Used for the attacker during its own attack-target selection: it must not look like a target,
    // but must stay clickable so the player can click it again to cancel the attack.
    public void SetClickableWithoutHighlight()
    {
        iSselectable = false;
        UpdateVisuals();

        if (col != null)
            col.enabled = true;

        SelectableChanged?.Invoke(iSselectable);
    }

    /*public void ClearVisuals()
    {
        iSselectable = false;
        _isHoverPreview = false;
        UpdateVisuals();
        KillBreathAnimation();

        if (col != null)
        {
            col.enabled = false;
        }
    }
    */
    private void UpdateVisuals()
    {
        //if (highlight == null) return;
        hover.enabled = _isHoverPreview;
        highlight.enabled = iSselectable;
    }
}
