using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
public class Debugger : MonoBehaviour
{
    public static Debugger Instance { get; private set; }

    private void Awake()
    {
        // Bir örnek varsa ve ben değilse, yoket.

        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            //SceneManager.UnloadSceneAsync(SceneManager.GetActiveScene().buildIndex);
            //SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

            StartCoroutine(UnloadAsyncScene(SceneManager.GetActiveScene().buildIndex));

            //StartCoroutine(LoadAsyncScene(SceneManager.GetActiveScene().buildIndex));
        }

        if (Input.GetKeyDown(KeyCode.M))
        {
            SceneManager.LoadScene("CreateCustomGame");
        }

        if (Input.GetKeyDown(KeyCode.G))
        {
            SceneManager.LoadScene("Game");
        }

        UpdateOpponentHandPeek();
    }

    // Cards currently flipped face up by the peek. Tracked rather than re-derived from the opponent's
    // hand when the peek is switched off, because a card can leave the hand while the peek is on — the
    // AI plays one, or a discard takes it — and reading the hand again would miss it and leave it
    // revealed for good.
    private readonly List<CardController> peekedCards = new List<CardController>();

    // Latched by Alt, and deliberately not reset on scene load: DontDestroyOnLoad keeps this Debugger
    // alive across the R/G reloads above, so leaving it on means the next match opens already revealed.
    private bool peekingOpponentHand;

    /// <summary>
    /// Alt toggles a look at the opponent's hand. Debug aid only: nothing about the cards changes, just
    /// whether their faces are drawn, so the AI plays exactly the same either way.
    /// </summary>
    private void UpdateOpponentHandPeek()
    {
        if (Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt))
        {
            peekingOpponentHand = !peekingOpponentHand;
            Debug.Log($"[Debug] Opponent hand peek {(peekingOpponentHand ? "ON" : "OFF")}");
        }

        // Re-checked every frame rather than only on the toggle press, so cards drawn while the peek is
        // already on get revealed too.
        if (peekingOpponentHand)
        {
            Agent opponent = GameManager.Instance != null ? GameManager.Instance.opponent : null;
            if (opponent != null)
            {
                foreach (var card in opponent.hand)
                {
                    if (card == null || card.view == null || card.modal == null) continue;
                    if (card.view.debugRevealFaceUp) continue;

                    card.view.debugRevealFaceUp = true;
                    card.view.UpdateView(card.modal);
                    peekedCards.Add(card);
                }
            }
            return;
        }

        if (peekedCards.Count == 0) return;

        foreach (var card in peekedCards)
        {
            // Destroyed while revealed (played, discarded, scene reloaded): nothing left to restore.
            if (card == null || card.view == null || card.modal == null) continue;

            card.view.debugRevealFaceUp = false;
            card.view.UpdateView(card.modal);
        }

        peekedCards.Clear();
    }
    IEnumerator LoadAsyncScene(int index)
    {
        // The Application loads the Scene in the background as the current Scene runs.
        // This is particularly good for creating loading screens.
        // You could also load the Scene by using sceneBuildIndex. In this case Scene2 has
        // a sceneBuildIndex of 1 as shown in Build Settings.

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(index);

        // Wait until the asynchronous scene fully loads
        while (!asyncLoad.isDone)
        {
            yield return null;
        }
    }
    
    IEnumerator UnloadAsyncScene(int index)
    {
        AsyncOperation asyncLoad = SceneManager.UnloadSceneAsync(index);
        while (asyncLoad != null && !asyncLoad.isDone)
        {

            yield return null;
        }

        StartCoroutine(LoadAsyncScene(index));

    }
}
