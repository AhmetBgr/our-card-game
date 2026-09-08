using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The "keep this resolution?" question that follows a resolution change, with a countdown that reverts
/// on its own.
///
/// A resolution the display cannot show, or one that puts the window off the edge of the desktop, is a
/// change the player cannot undo -- the button to undo it is the thing they can no longer see. So the
/// change is applied FIRST, and this asks whether it worked; silence is taken as no, because a player
/// who can see the question answers it.
///
/// Owned by <see cref="SettingsPanelController"/>, which decides what keeping and reverting mean. This
/// only counts, and reports which way it ended.
/// </summary>
public class ResolutionConfirmDialog : MonoBehaviour
{
    [Tooltip("The part that is shown and hidden. Empty = this object.")]
    [SerializeField] private GameObject root;

    [SerializeField] private TMP_Text bodyLabel;

    [SerializeField] private Button keepButton;
    [SerializeField] private Button revertButton;

    [Tooltip("Seconds the player has to say yes before the change is undone for them.")]
    [Min(1f)]
    [SerializeField] private float countdownSeconds = 15f;

    /// <summary>Whether the question is on screen.</summary>
    public bool IsShowing => Body != null && Body.activeSelf;

    private GameObject Body => root != null ? root : gameObject;

    private Action _onKeep;
    private Action _onRevert;

    private string _nowLine;
    private string _revertLine;

    private float _deadline;
    private int _shownSeconds = -1;

    private void Awake()
    {
        if (keepButton != null) keepButton.onClick.AddListener(Keep);
        if (revertButton != null) revertButton.onClick.AddListener(Revert);

        // Authored on so it can be laid out; never up at kickoff.
        if (root != null) root.SetActive(false);
    }

    /// <summary>
    /// Puts the question up and starts the clock. <paramref name="nowLine"/> says what the screen is
    /// now ("Now fullscreen"), <paramref name="revertLine"/> names what it goes back to ("windowed").
    /// <paramref name="onKeep"/> runs if the player says yes; <paramref name="onRevert"/> if they say
    /// no, or say nothing for long enough.
    /// </summary>
    public void Show(string nowLine, string revertLine, Action onKeep, Action onRevert)
    {
        _nowLine = nowLine;
        _revertLine = revertLine;
        _onKeep = onKeep;
        _onRevert = onRevert;

        // Unscaled: the pause menu stops game time, and a countdown that stops with it never ends.
        _deadline = Time.unscaledTime + countdownSeconds;
        _shownSeconds = -1;

        if (Body != null) Body.SetActive(true);

        RefreshBody();
    }

    /// <summary>Takes the question down without answering it. For the panel closing underneath it.</summary>
    public void Hide()
    {
        _onKeep = null;
        _onRevert = null;

        if (Body != null) Body.SetActive(false);
    }

    private void Update()
    {
        if (!IsShowing) return;

        if (Time.unscaledTime >= _deadline)
        {
            Revert();
            return;
        }

        RefreshBody();
    }

    private void Keep()
    {
        Action callback = _onKeep;
        Hide();
        callback?.Invoke();
    }

    private void Revert()
    {
        Action callback = _onRevert;
        Hide();
        callback?.Invoke();
    }

    /// <summary>Rewrites the caption only when the whole second changes, not every frame.</summary>
    private void RefreshBody()
    {
        if (bodyLabel == null) return;

        int seconds = Mathf.Max(0, Mathf.CeilToInt(_deadline - Time.unscaledTime));
        if (seconds == _shownSeconds) return;

        _shownSeconds = seconds;
        bodyLabel.text = $"{_nowLine}\nReverting to {_revertLine} in {seconds} s";
    }
}
