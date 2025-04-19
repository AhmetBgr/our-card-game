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
