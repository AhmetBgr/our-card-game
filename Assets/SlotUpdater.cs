using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlotUpdater : MonoBehaviour
{
    public List<Transform> slots = new List<Transform>();


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        foreach (var item in slots)
        {
            item.gameObject.SetActive(item.childCount > 0);

        }
    }
}
