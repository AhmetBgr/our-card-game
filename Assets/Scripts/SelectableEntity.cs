using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.SearchService;
using UnityEngine;

public class SelectableEntity : MonoBehaviour
{
    [SerializeField] private bool iSselectable = false;
    private Color selectableColor;
    private Color hoverColor;

    private bool _isHoverPreview;

    public bool IsSelectable => iSselectable;

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
        if (!iSselectable)
        {
            _isHoverPreview = false;
        }

        UpdateVisuals();

        //PlayBreathAnimation();

        if (col == null) return;  

        col.enabled = value;
    }

    private Sequence breathSequence;
    public void PlayBreathAnimation()
    {
        KillBreathAnimation();
        highlight.DOFade(0f, 1f).SetLoops(-1, LoopType.Yoyo);
        /*breathSequence = DOTween.Sequence();
        //breathSequence.Append(highlight.DOColor(selectableColor, 1f));
        breathSequence.Append(highlight.DOColor(Color.white * 0f, 1f));
        breathSequence.Append(highlight.DOColor(selectableColor, 1f));

        breathSequence.SetLoops(-1);*/
    }
    private void KillBreathAnimation()
    {
        breathSequence.Kill();
    }
    public void SetHoverPreview(bool value)
    {
        _isHoverPreview = value;
        UpdateVisuals();
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
        if (highlight == null) return;

        if (_isHoverPreview)
        {
            hover.enabled = true;
            highlight.enabled = false;

            return;
        }

        if (iSselectable)
        {
            highlight.enabled = true;
            hover.enabled = false;
            return;
        }

        highlight.enabled = false;
    }
}
