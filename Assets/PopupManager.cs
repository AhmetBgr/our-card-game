using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class PopupManager : Singleton<PopupManager>
{
    public Transform victoryPopup;
    public Transform defeatPopup;

    private Transform curPopup = null;

    void Start()
    {
        
    }


    public void OpenPopup(Transform popup, float delay = 0f)
    {
        CloseCurPopup();
        curPopup = popup;

        curPopup.gameObject.SetActive(true);
        curPopup.localScale = Vector3.zero;

        curPopup.DOScale(1f, 0.5f).SetDelay(delay).OnComplete(() =>
        {

        });
    }

    public void CloseCurPopup()
    {
        if (curPopup == null) return;

        curPopup.DOScale(0f, 0.5f).OnComplete(() =>
        {
            curPopup.gameObject.SetActive(false);
            curPopup = null;
        });
    }
}
