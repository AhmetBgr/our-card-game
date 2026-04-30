using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MinionRangeHandler : Singleton<MinionRangeHandler>
{
    [Serializable]
    public struct RangeInfo
    {
        public Vector2Int[] indexes;
        public int range;
        public GameObject rangeImageObject;
    }

    public RangeInfo[] ranges;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    public void ShowRange(Vector2Int index, int range)
    {
        Debug.Log("shouldshow range 2");

        foreach (RangeInfo rangeInfo in ranges)
        {
            Debug.Log("range active?: " + (rangeInfo.indexes.Contains(index) && rangeInfo.range == range));
            rangeInfo.rangeImageObject.SetActive(rangeInfo.indexes.Contains(index) && rangeInfo.range == range);
        }
    }

    public void HideRange()
    {
        foreach (RangeInfo rangeInfo in ranges)
        {
            rangeInfo.rangeImageObject.SetActive(false);
        }
    }
}
