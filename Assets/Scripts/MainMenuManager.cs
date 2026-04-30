using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    public Button playButton;
    // Start is called before the first frame update
    void Start()
    {
        playButton.onClick.AddListener(() => { SceneTransitionManager.Instance.TransitionToScene("Game"); });
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
