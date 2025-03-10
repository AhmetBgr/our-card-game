using System; 
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    public GameObject minionprefab;

    public IEnumerator curaction;
    private Queue<IEnumerator> actionQueue = new Queue<IEnumerator>();

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void TestAc2(CardTEst card)
    {

    }

    public void startcor(IEnumerator cor)
    {
        curaction = cor;
        
        StartCoroutine(cor);
    }

    public IEnumerator startaction( Action oncomplete= null)
    {
        while (curaction != null)
        {
            yield return null;
        }
        oncomplete?.Invoke();
        yield break;
    }
    public void Addtoactions(IEnumerator action)
    {

        actionQueue.Enqueue(action);
    }
    public void removefromactions(IEnumerator action)
    {

        actionQueue.Enqueue(action);
    }

    public void playcard(CardController card)
    {
        actionQueue.Clear();
        ActionHolder.selectedcell = null;
        ActionHolder.selectedMinion = null;

        card.card.OnPlay.Invoke();
        StartCoroutine(ExecuteActions(card));
    }
    IEnumerator ExecuteActions(CardController card)
    {
        while (actionQueue.Count > 0)
        {
            IEnumerator action = actionQueue.Dequeue();
            yield return StartCoroutine(action);
        }

        Destroy(card.gameObject);

        Debug.Log("All actions completed.");
    }
    public void SummonMinion(CardTEst card, Vector3 pos)
    {
        MinionController minion = Instantiate(minionprefab, pos, Quaternion.identity).GetComponent<MinionController>();
        minion.card = card;
    }
}
