using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
public class ManaBarController : MonoBehaviour
{
    public TextMeshProUGUI[] maxBarTextObjects;
    public Animator animator;
    public Color maxManaColor;
    // Start is called before the first frame update
    void Start()
    {
        //animator.speed = -1;
        
        //animator.Play("ManaBarAnimReversed", 0 , 1f);
        GameManager.OnPlayerManaChanged += PlayCorrectAnimation;
    }

    private void OnDestroy()
    {
        GameManager.OnPlayerManaChanged -= PlayCorrectAnimation;

    }

    // Update is called once per frame
    public void UpdateMacManaBarText(int value)
    {
        if(value == GameManager.Instance.maxMana | value == GameManager.Instance.curPlayerMana)
        {
            animator.speed = 0f;
            //animator.StopPlayback();
        }

        if (value >= maxBarTextObjects.Length | value < 0) return;

        for (int i = 0; i < maxBarTextObjects.Length; i++)
        {
            maxBarTextObjects[i].gameObject.SetActive(i == value || i == GameManager.Instance.maxMana);
            maxBarTextObjects[i].color = i == GameManager.Instance.maxMana ? maxManaColor : Color.white;
        }
    }

    private void PlayCorrectAnimation(int newValue, int oldValue)
    {
        Debug.Log("old: " + oldValue + ", new: " +  newValue);   

        if (newValue > oldValue)
        {
            animator.speed = 1f;
            animator.Play("ManaBarAnim", 0 , (float)oldValue / 10); //, 0, animator.GetCurrentAnimatorClipInfo(0)[0].clip
            Debug.Log("here" + (float)oldValue / 10);

        }
        else if (newValue < oldValue) {
            Debug.Log("here2: " + (float) newValue / 10);
            animator.speed = 1f;
            animator.Play("ManaBarAnimReversed", 0,  (float)(10-oldValue+0.5f) / 10);

        }
    }
}
