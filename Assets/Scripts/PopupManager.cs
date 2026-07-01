using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PopupManager : Singleton<PopupManager>
{
    public Transform victoryPopup;
    public Transform defeatPopup;

    public Button victoryReplayButton;
    public Button victoryExitButton;
    public Button defeatReplayButton;
    public Button defeatExitButton;

    private Transform curPopup = null;

    void Start()
    {
        if (victoryReplayButton != null) victoryReplayButton.onClick.AddListener(Replay);
        if (victoryExitButton != null) victoryExitButton.onClick.AddListener(ExitToMenu);
        if (defeatReplayButton != null) defeatReplayButton.onClick.AddListener(Replay);
        if (defeatExitButton != null) defeatExitButton.onClick.AddListener(ExitToMenu);
    }

    public void Replay()
    {
        SceneTransitionManager.Instance.TransitionToScene("Game");
    }

    public void ExitToMenu()
    {
        SceneTransitionManager.Instance.TransitionToScene("Menu");
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
