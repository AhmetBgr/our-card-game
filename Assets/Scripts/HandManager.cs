using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandManager : Singleton<HandManager>
{
    public Transform hand;
    public Transform topview;


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void AddToTopView(Transform card)
    {
        card.transform.SetParent(transform);
    }
    public void AddToHand(Transform card, Transform slot)
    {
        card.transform.SetParent(slot);
    }
    public Transform gethandslot()
    {
        foreach (Transform item in topview)
        {
            if (item.childCount == 0)
                return item;
        }

        return null;
    }
    public Transform gettopviewslot()
    {
        foreach (Transform item in topview)
        {
            if (item.childCount == 0)
                return item;
        }

        return null;
    }
}
