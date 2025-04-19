using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;
public class ManaBarController : MonoBehaviour
{
    public TextMeshProUGUI[] maxBarTextObjects;
    public Animator animator;
    public Color maxManaColor;
    public Transform gear;

    void Start()
    {
        //animator.speed = -1;
        
        //animator.Play("ManaBarAnimReversed", 0 , 1f);
        Player.OnPlayerManaChanged += PlayCorrectAnimation;
    }

    private void OnDestroy()
    {
        Player.OnPlayerManaChanged -= PlayCorrectAnimation;

    }

    // Update is called once per frame
    public void UpdateMacManaBarText(int value)
    {
        if(value == GameManager.Instance.maxMana | value == GameManager.Instance.player.availibleMana)
        {
            animator.speed = 0f;
            gear.DOKill();
            //animator.StopPlayback();
        }

        if (value >= maxBarTextObjects.Length | value < 0) return;

        for (int i = 0; i < maxBarTextObjects.Length; i++)
        {
            maxBarTextObjects[i].gameObject.SetActive(i == value || i == GameManager.Instance.maxMana);
            maxBarTextObjects[i].color = i == GameManager.Instance.maxMana ?  Color.white : maxManaColor;
        }
    }

    private void PlayCorrectAnimation(int newValue, int oldValue)
    {
        //Debug.Log("old: " + oldValue + ", new: " +  newValue);   

        if (newValue > oldValue)
        {
            animator.speed = 1f;
            animator.Play("ManaBarAnim", 0 , (float)oldValue / 10); //, 0, animator.GetCurrentAnimatorClipInfo(0)[0].clip
            //Debug.Log("here" + (float)oldValue / 10);
            gear.DOKill();
            gear.DORotate(Vector3.forward*360, 0.1f, RotateMode.FastBeyond360).SetLoops(-1);

        }
        else if (newValue < oldValue) {
            //Debug.Log("here2: " + (float) newValue / 10);
            animator.speed = 1f;
            animator.Play("ManaBarAnimReversed", 0,  (float)(10-oldValue+0.5f) / 10);
            gear.DOKill();

            gear.DORotate(Vector3.forward * -360, 0.1f, RotateMode.FastBeyond360).SetLoops(-1);


        }
    }
}
