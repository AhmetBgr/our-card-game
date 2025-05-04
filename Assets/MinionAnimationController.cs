using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class MinionAnimationController : MonoBehaviour
{
    [Serializable]
    public struct SlashInfo
    {
        public Vector3 dir; // not normalized
        public Vector3 pos;
        public Vector3 rotation;
    }

    public Animator animator;

    public Transform arrowAnimationObj;
    public Transform arrowImageObj;

    public Transform slashAnimationObj;


    public SlashInfo[] slashInfo;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    public IEnumerator PlayArrowAnimation(Vector3 dir, Vector3 targetPos, float delay = 0f, Action onComplete = null)
    {
        yield return new WaitForSeconds(delay);

        //arrowAnimationObj.position = pos;
        arrowImageObj.gameObject.SetActive(true);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        arrowImageObj.rotation = Quaternion.Euler(0, 0, angle-45);
        arrowImageObj.localPosition = Vector3.zero;

        arrowImageObj.DOMove(targetPos, 0.5f).OnComplete(() =>
        {
            arrowImageObj.gameObject.SetActive(false);

            onComplete?.Invoke();

        });
    }

    public void PlayArrowHitAnimation()
    {
        animator.Play("Arrow");
    }

    public IEnumerator PlaySlashAnimation(Vector3 dir, float delay = 0)
    {
        yield return new WaitForSeconds(delay);


        foreach (var slash in slashInfo)
        {
            if(slash.dir.normalized == dir.normalized)
            {
                slashAnimationObj.position = slash.pos + transform.position;
                slashAnimationObj.rotation = Quaternion.Euler(slash.rotation);
                animator.Play("Slash");
                break;
            }
        }
    }
}

